"""Offline score -> aligned PCM/Ogg stems, mixes, MIDI, listening page and report.

Uses a local FluidSynth DLL for sample-accurate scheduled instrument rendering.
Only the chip voices are synthesized directly; guitars, bass, organ and drums
come from GeneralUser GS. Nothing here runs inside the game.
"""
from __future__ import annotations

import argparse
import ctypes as ct
import hashlib
import json
import math
import os
from pathlib import Path
import shutil
import subprocess
import sys
import zipfile

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[1]
WORK = ROOT / "Assets/Work/music-lab"
DEPS = WORK / "deps"
sys.path.insert(0,str(DEPS / "python"))
import numpy as np
import soundfile as sf
from scipy.signal import butter, sosfilt, resample_poly
import mido
import imageio_ffmpeg
from compose import make_score


class Fluid:
    def __init__(self, sample_rate):
        dll_path = next((DEPS / "fluidsynth").rglob("libfluidsynth-3.dll"))
        self.dll_dir = os.add_dll_directory(str(dll_path.parent))
        self.lib = ct.CDLL(str(dll_path))
        definitions = {
            "new_fluid_settings": (ct.c_void_p, []),
            "fluid_settings_setnum": (ct.c_int,[ct.c_void_p,ct.c_char_p,ct.c_double]),
            "fluid_settings_setint": (ct.c_int,[ct.c_void_p,ct.c_char_p,ct.c_int]),
            "new_fluid_synth": (ct.c_void_p,[ct.c_void_p]),
            "fluid_synth_sfload": (ct.c_int,[ct.c_void_p,ct.c_char_p,ct.c_int]),
            "fluid_synth_program_select": (ct.c_int,[ct.c_void_p,ct.c_int,ct.c_int,ct.c_int,ct.c_int]),
            "fluid_synth_noteon": (ct.c_int,[ct.c_void_p,ct.c_int,ct.c_int,ct.c_int]),
            "fluid_synth_noteoff": (ct.c_int,[ct.c_void_p,ct.c_int,ct.c_int]),
            "fluid_synth_cc": (ct.c_int,[ct.c_void_p,ct.c_int,ct.c_int,ct.c_int]),
            "fluid_synth_write_float": (ct.c_int,[ct.c_void_p,ct.c_int,ct.c_void_p,ct.c_int,ct.c_int,ct.c_void_p,ct.c_int,ct.c_int]),
            "delete_fluid_synth": (None,[ct.c_void_p]),
            "delete_fluid_settings": (None,[ct.c_void_p]),
        }
        for name,(ret,args) in definitions.items():
            fn=getattr(self.lib,name); fn.restype=ret; fn.argtypes=args
        self.settings = self.lib.new_fluid_settings()
        for key,value in [(b"synth.sample-rate",sample_rate),(b"synth.gain",0.65)]:
            self.lib.fluid_settings_setnum(self.settings,key,value)
        for key,value in [(b"synth.reverb.active",0),(b"synth.chorus.active",0),(b"synth.polyphony",128),(b"synth.cpu-cores",1)]:
            self.lib.fluid_settings_setint(self.settings,key,value)
        self.synth=self.lib.new_fluid_synth(self.settings)
        self.font=self.lib.fluid_synth_sfload(self.synth,str(DEPS/"GeneralUser-GS.sf2").encode(),1)
        if self.font < 0:
            raise RuntimeError("SoundFont did not load")

    def render(self, track, frames, samples_per_beat):
        ch=9 if track["bank"] == 128 else 0
        result=self.lib.fluid_synth_program_select(self.synth,ch,self.font,track["bank"],track["program"])
        if result != 0:
            raise RuntimeError(f"Missing SoundFont preset: {track}")
        self.lib.fluid_synth_cc(self.synth,ch,7,100)
        self.lib.fluid_synth_cc(self.synth,ch,10,64)
        # Two passes warm up samples and releases. Capture the second complete
        # cycle, so the end of the phrase already rings into its beginning.
        events=[]
        for cycle in range(2):
            for n in track["notes"]:
                start=round(n["beat"]*samples_per_beat)+cycle*frames
                end=round((n["beat"]+n["duration"])*samples_per_beat)+cycle*frames
                events.extend([(start,1,n["pitch"],n["velocity"]),(end,0,n["pitch"],0)])
        events.sort()
        audio=np.zeros((frames*2,2),np.float32)
        cursor=0
        for when,on,key,velocity in events+[(frames*2,0,0,0)]:
            when=min(when,frames*2)
            if when > cursor:
                view=audio[cursor:when]
                addr=ct.c_void_p(view.ctypes.data)
                self.lib.fluid_synth_write_float(self.synth,when-cursor,addr,0,2,addr,1,2)
                cursor=when
            if on:
                self.lib.fluid_synth_noteon(self.synth,ch,key,velocity)
            else:
                self.lib.fluid_synth_noteoff(self.synth,ch,key)
        return audio[frames:].copy()

    def close(self):
        self.lib.delete_fluid_synth(self.synth)
        self.lib.delete_fluid_settings(self.settings)
        self.dll_dir.close()


def pulse(track, frames, spb, rate):
    audio=np.zeros((frames,2),np.float32)
    for n in track["notes"]:
        duration=n["duration"]*spb/rate
        count=round((duration+.06)*rate)
        t=np.arange(count)/rate
        frequency=440*2**((n["pitch"]-69)/12)
        # Harmonic-limited pulse: preserves the chip identity without folding
        # ultrasonic harmonics into audible aliasing.
        phase=2*np.pi*frequency*t + .035*np.sin(2*np.pi*5.2*t)*np.minimum(t/.25,1)
        wave=np.zeros(count)
        duty=track["duty"]
        for harmonic in range(1,min(40,int(rate*.46/frequency))+1):
            amplitude=2*np.sin(np.pi*harmonic*duty)/(np.pi*harmonic)
            wave += amplitude*np.cos(harmonic*phase-np.pi*harmonic*duty)
        attack=np.minimum(t/.006,1)
        release=np.clip((duration+.06-t)/.06,0,1)
        envelope=attack*release*(.68+.32*np.exp(-t/.055))
        mono=(wave*envelope*(n["velocity"]/127)**1.3).astype(np.float32)
        index=(round(n["beat"]*spb)+np.arange(count))%frames
        audio[index,0]+=mono
        audio[index,1]+=mono
    return audio


def filter_audio(audio, rate, cutoff, kind):
    # Warm the IIR using a full repetition; retain only the second loop.
    sos=butter(2,cutoff,kind,fs=rate,output="sos")
    doubled=np.concatenate([audio,audio])
    return sosfilt(sos,doubled,axis=0)[len(audio):].astype(np.float32)


def pan(audio, position):
    # Narrow sampled stereo before panning; preserve some drum-kit width.
    mono=audio.mean(axis=1)
    side=(audio[:,0]-audio[:,1])*.20
    left=math.sqrt((1-position)/2)*1.4142
    right=math.sqrt((1+position)/2)*1.4142
    return np.column_stack([(mono+side)*left,(mono-side)*right]).astype(np.float32)


def room(audio, rate, amount):
    result=audio.copy()
    # Circular taps keep the exact loop length and carry ambience over its seam.
    for seconds,gain in [(.037,.43),(.061,.32),(.097,.24),(.149,.17),(.211,.11)]:
        delayed=np.roll(audio,round(seconds*rate),axis=0)
        result+=delayed[:,::-1]*(amount*gain)
    return result


def shape(name, audio, track, rate, spb):
    audio=filter_audio(audio,rate,35 if name=="bass" else 100 if "guitar" in name else 55,"highpass")
    if track.get("processing")=="orchestral":
        # A shared hall places the sampled sections in the same acoustic space.
        audio=filter_audio(audio,rate,8500,"lowpass")
        audio=room(audio,rate,.23 if track["bank"]==128 else .42)
        dry=audio.copy()
        for seconds,gain in [(.283,.12),(.419,.08),(.613,.045)]:
            audio+=np.roll(dry,round(seconds*rate),axis=0)[:,::-1]*gain
        return pan(audio,track["pan"])*track["level"]
    if "guitar" in name or name=="solo":
        audio=filter_audio(audio,rate,5200,"lowpass")
        # SoundFont guitar already contains amp character. Gentle saturation
        # only controls peaks; it is not a replacement for an instrument.
        audio=np.tanh(audio*1.6)/1.6
        audio=room(audio,rate,.12)
        if track.get("processing")=="heavier":
            # More midrange body and a controlled amp edge. Oversample the
            # nonlinear stage so added harmonics do not alias into the mix.
            thick=filter_audio(audio,rate,1400,"lowpass")-filter_audio(audio,rate,180,"lowpass")
            audio=audio+thick*.24
            high=resample_poly(audio,2,1,axis=0)
            audio=resample_poly(np.tanh(high*3.2)/3.2,1,2,axis=0)[:len(audio)].astype(np.float32)
    elif name in ["clean","organ"]:
        audio=room(audio,rate,.26 if name=="clean" else .12)
    elif name in ["chip","answer"]:
        audio=filter_audio(audio,rate,6500,"lowpass")
        for beats,gain in [(.75,.15),(1.5,.055)]:
            audio+=np.roll(audio,round(spb*beats),axis=0)[:,::-1]*gain
        audio=room(audio,rate,.12)
    elif name in ["kit","power_kit"]:
        audio=room(audio,rate,.13)
        if track.get("processing")=="heavier":
            # Parallel soft compression increases the body behind the backbeat
            # while keeping the uncompressed attack in the blend.
            audio=.70*audio+.30*np.tanh(audio*3)/3
    return pan(audio,track["pan"])*track["level"]


def write_midi(score, target):
    midi=mido.MidiFile(ticks_per_beat=960)
    conductor=mido.MidiTrack(); midi.tracks.append(conductor)
    conductor.extend([mido.MetaMessage("track_name",name=score["title"]),
                      mido.MetaMessage("set_tempo",tempo=mido.bpm2tempo(score["bpm"])),
                      mido.MetaMessage("time_signature",numerator=4,denominator=4)])
    next_channel=0
    for name,track in score["tracks"].items():
        channel=9 if track.get("bank")==128 else next_channel
        if track.get("bank")!=128:
            next_channel+=1
            if next_channel==9: next_channel+=1
        out=mido.MidiTrack(); midi.tracks.append(out)
        out.append(mido.MetaMessage("track_name",name=f'{track["stem"]}: {name}'))
        out.append(mido.Message("program_change",channel=channel,program=track.get("program",80)))
        events=[]
        for n in track["notes"]:
            events.extend([(round(n["beat"]*960),1,n),(round((n["beat"]+n["duration"])*960),0,n)])
        previous=0
        for tick,on,n in sorted(events,key=lambda e:(e[0],e[1])):
            out.append(mido.Message("note_on" if on else "note_off",channel=channel,
                       note=n["pitch"],velocity=n["velocity"] if on else 0,time=tick-previous))
            previous=tick
    midi.save(target)


def metrics(audio, rate):
    peak=float(np.max(np.abs(audio)))
    true_peak=float(np.max(np.abs(resample_poly(audio,4,1,axis=0))))
    return dict(frames=len(audio),sample_rate=rate,peak_dbfs=round(20*np.log10(max(peak,1e-12)),2),
                true_peak_dbfs=round(20*np.log10(max(true_peak,1e-12)),2),
                rms_dbfs=round(20*np.log10(max(float(np.sqrt(np.mean(audio**2))),1e-12)),2),
                dc=float(np.mean(audio)),seam_delta=float(np.max(np.abs(audio[0]-audio[-1]))),
                finite=bool(np.isfinite(audio).all()))


def main():
    parser=argparse.ArgumentParser()
    parser.add_argument("--score",type=Path,help="Use an edited score.json instead of recomposing")
    parser.add_argument("--out",type=Path,default=WORK/"copper-circuit")
    parser.add_argument("--reference",type=Path,help="Reuse accepted stems exactly")
    parser.add_argument("--preserve-stems",nargs="+",default=["world","theme"])
    parser.add_argument("--notes",type=Path,default=HERE/"README.md")
    args=parser.parse_args()
    output=args.out.resolve(); output.mkdir(parents=True,exist_ok=True)
    for folder in ["stems","mixes","game","source","licenses"]:
        (output/folder).mkdir(exist_ok=True)
    score=json.loads(args.score.read_text()) if args.score else make_score()
    asset_id=score.get("asset_id","copper-circuit")
    rate=score["sample_rate"]; spb=rate*60/score["bpm"]
    frames=round(spb*score["bars"]*score["beats_per_bar"])
    stems={name:np.zeros((frames,2),np.float32) for name in score["stems"]}
    preserved={}
    reference_gain=None
    if args.reference:
        original=json.loads((args.reference/"source/score.json").read_text())
        reference_gain=json.loads((args.reference/"report.json").read_text())["common_gain"]
        for name,track in score["tracks"].items():
            if track["stem"] in args.preserve_stems:
                assert track==original["tracks"][name],f"Accepted track changed: {name}"
        for name in args.preserve_stems:
            preserved[name],sr=sf.read(args.reference/"stems"/f"{name}.wav",dtype="float32",always_2d=True)
            assert sr==rate and preserved[name].shape==(frames,2)
            stems[name]=preserved[name]/reference_gain
    track_report={}
    for name,track in score["tracks"].items():
        if track["stem"] in preserved:
            track_report[name]={"reused_from":str(args.reference)}
            continue
        print(f'Rendering {name}: {len(track["notes"])} notes',flush=True)
        if track["engine"]=="sf2":
            synth=Fluid(rate)
            try: audio=synth.render(track,frames,spb)
            finally: synth.close()
        else:
            audio=pulse(track,frames,spb,rate)
        audio=shape(name,audio,track,rate,spb)
        track_report[name]=dict(peak=float(np.max(np.abs(audio))),rms=float(np.sqrt(np.mean(audio**2))))
        stems[track["stem"]]+=audio

    # One common gain for all stems. Independent peak normalization would destroy
    # the intended mix. Bound EVERY combination of 0..1 stem gains with headroom.
    bound=np.zeros((frames,2),np.float32)
    for a in stems.values(): bound+=np.abs(a)
    scale=min(3.0,10**(-3.5/20)/float(bound.max()))
    if reference_gain is not None:
        scale=reference_gain
        assert float(bound.max())*scale < .92,"Revision needs less band gain to preserve headroom"
    for name in stems: stems[name]*=scale
    for name,audio in preserved.items():stems[name]=audio
    ffmpeg=imageio_ffmpeg.get_ffmpeg_exe()
    report=dict(title=score["title"],bpm=score["bpm"],frames=frames,duration_seconds=frames/rate,
                common_gain=scale,tracks=track_report,stems={},mixes={})
    for name,audio in stems.items():
        wav=output/"stems"/f"{name}.wav"
        sf.write(wav,audio,rate,subtype="PCM_16")
        ogg=output/"game"/f"{name}.ogg"
        subprocess.run([ffmpeg,"-v","error","-y","-i",str(wav),"-c:a","libvorbis","-q:a","5",
                        "-metadata",f"LOOPSTART=0","-metadata",f"LOOPEND={frames}",str(ogg)],check=True)
        if name in preserved:
            shutil.copy2(args.reference/"stems"/wav.name,wav)
            shutil.copy2(args.reference/"game"/ogg.name,ogg)
        report["stems"][name]=metrics(audio,rate)
        report["stems"][name]["ogg_bytes"]=ogg.stat().st_size
        decoded,decoded_rate=sf.read(ogg,dtype="float32",always_2d=True)
        report["stems"][name]["decoded_frames"]=len(decoded)
        report["stems"][name]["decoded_sample_rate"]=decoded_rate
        assert len(decoded)==frames and decoded_rate==rate,"Codec changed stem length"
        assert report["stems"][name]["finite"]

    for mood,gains in score["moods"].items():
        if (args.reference and original["moods"].get(mood)==gains
                and all(name in preserved or gain==0 for name,gain in zip(score["stems"],gains))):
            # Retain the accepted preview as well as its stems. Remixing PCM16
            # stems would otherwise introduce a second round of quantization.
            for extension in ("wav","mp3"):
                shutil.copy2(args.reference/"mixes"/f"{mood}.{extension}",output/"mixes"/f"{mood}.{extension}")
            reference_report=json.loads((args.reference/"report.json").read_text())
            report["mixes"][mood]=reference_report["mixes"][mood]
            continue
        mixed=sum(stems[name]*gain for name,gain in zip(score["stems"],gains))
        report["mixes"][mood]=metrics(mixed,rate)
        sf.write(output/"mixes"/f"{mood}.wav",mixed,rate,subtype="PCM_16")
        subprocess.run([ffmpeg,"-v","error","-y","-i",str(output/"mixes"/f"{mood}.wav"),
                        "-codec:a","libmp3lame","-b:a","192k",str(output/"mixes"/f"{mood}.mp3")],check=True)

    # One-loop guided demonstration: 8 bars explore, 8 drive, 8 combat, 8 explore.
    # This is a preview, not a separately shipped runtime asset.
    demo=np.zeros((frames,2),np.float32)
    demo_states=["explore","drive","combat","explore"]
    for i,name in enumerate(score["stems"]):
        env=np.full(frames,score["moods"]["explore"][i],np.float32)
        for section in range(1,4):
            start=round(section*8*4*spb); fade=round(4*spb)
            prev=score["moods"][demo_states[section-1]][i]
            new=score["moods"][demo_states[section]][i]
            env[start:start+fade]=np.linspace(prev,new,fade)
            env[start+fade:]=new
        demo+=stems[name]*env[:,None]
    # Fade only the standalone demo's outside edges. Runtime stems stay cyclic.
    fade=round(.02*rate); demo[:fade]*=np.linspace(0,1,fade)[:,None]
    fade=round(.8*rate); demo[-fade:]*=np.linspace(1,0,fade)[:,None]
    sf.write(output/"mixes"/"guided-demo.wav",demo,rate,subtype="PCM_16")
    subprocess.run([ffmpeg,"-v","error","-y","-i",str(output/"mixes"/"guided-demo.wav"),
                    "-codec:a","libmp3lame","-b:a","192k",str(output/"listen-first.mp3")],check=True)
    write_midi(score,output/"source"/f"{asset_id}.mid")
    (output/"source"/"score.json").write_text(json.dumps(score,indent=2)+"\n")
    license_source=DEPS/"GeneralUser-GS-LICENSE.txt"
    shutil.copy2(license_source,output/"licenses"/license_source.name)
    provenance=dict(soundfont="GeneralUser GS 2.0.3 — S. Christian Collins",
                    source="https://github.com/mrbumpy409/GeneralUser-GS",
                    sha256=hashlib.sha256((DEPS/"GeneralUser-GS.sf2").read_bytes()).hexdigest(),
                    composition="Original score created for App2d; no third-party melody input.",
                    synthesis="FluidSynth 2.6.1; offline only."+(" Plus band-limited pulse synthesis." if any(t["engine"]=="pulse" for t in score["tracks"].values()) else ""))
    (output/"licenses"/"provenance.json").write_text(json.dumps(provenance,indent=2)+"\n")
    manifest=dict(title=score["title"],revision=score.get("revision","01 — Original"),bpm=score["bpm"],beats_per_bar=4,bars=score["bars"],
                  key=score.get("key"),asset_id=asset_id,presentation=score.get("presentation",{}),
                  sample_rate=rate,channels=2,loop_start_frame=0,loop_end_frame=frames,
                  stems=[dict(id=name,file=f"{name}.ogg") for name in score["stems"]],
                  moods=score["moods"],transition=dict(quantize_beats=4,fade_beats=4),
                  playback="All stems share a sample cursor. Never independently restart a muted stem.")
    (output/"game"/"manifest.json").write_text(json.dumps(manifest,indent=2)+"\n")
    report["game_audio_bytes"]=sum(x["ogg_bytes"] for x in report["stems"].values())
    report["decoded_float32_bytes"]=frames*2*4*len(stems)
    (output/"report.json").write_text(json.dumps(report,indent=2)+"\n")
    page=(HERE/"player.html").read_text(encoding="utf-8")
    page=page.replace("__MANIFEST__",json.dumps(manifest)).replace("__SCORE_SECTIONS__",json.dumps(score["sections"]))
    page=page.replace('href="copper-circuit-game.zip"',f'href="{asset_id}-game.zip"').replace('href="source/copper-circuit.mid"',f'href="source/{asset_id}.mid"')
    (output/"index.html").write_text(page,encoding="utf-8")
    shutil.copy2(args.notes,output/"README.md")
    with zipfile.ZipFile(output/f"{asset_id}-game.zip","w",zipfile.ZIP_DEFLATED) as archive:
        for folder in ["game","licenses"]:
            for path in (output/folder).iterdir(): archive.write(path,str(path.relative_to(output)))
        archive.write(args.notes,"README.md")
    print(json.dumps({k:report[k] for k in ["duration_seconds","game_audio_bytes","decoded_float32_bytes"]},indent=2))
    print(f"Listen: {output/'index.html'}",flush=True)


if __name__=="__main__": main()
