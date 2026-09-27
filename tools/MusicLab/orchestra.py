"""Crown of Embers: an original orchestral adaptive cue, 120 BPM / D minor."""
import json
from pathlib import Path
from compose import pitch

BASE=Path(__file__).resolve().parents[2]/"Assets/Work/music-lab/copper-circuit"


def make_orchestra():
    tracks={}
    def instrument(name,stem,program,level,pan=0,bank=0):
        tracks[name]=dict(stem=stem,engine="sf2",program=program,bank=bank,
                          level=level,pan=pan,processing="orchestral",notes=[])
    instrument("low_strings","world",48,.68,-.12)
    instrument("harp","world",46,.52,.30)
    instrument("string_air","world",49,.37,-.25)
    instrument("horns","theme",60,.78,-.18)
    instrument("violins","theme",48,.43,.24)
    instrument("short_strings","motion",48,.66,-.38)
    instrument("low_pulse","motion",42,.56,.18)
    instrument("brass_crown","battle",61,.53,.22)
    instrument("timpani","battle",47,.94,-.08)
    instrument("orchestra_drums","battle",0,.85,0,128)
    def note(name,beat,key,duration,velocity=85):
        if key=="-":return
        tracks[name]["notes"].append(dict(beat=round(beat,6),pitch=pitch(key),
            duration=round(duration,6),velocity=velocity))
    chords={"Dm":(38,[50,57,62,65]),"Bb":(34,[53,58,62,65]),
            "F":(41,[53,60,65,69]),"C":(36,[52,55,60,64]),
            "Gm":(43,[55,58,62,67]),"A":(33,[52,57,61,64]),
            "Csus":(36,[53,55,60,65])}
    progression=["Dm","Bb","F","C","Dm","Bb","Gm","A",
                 "F","C","Dm","Bb","Gm","Dm","Bb","A",
                 "Gm","Bb","Dm","Csus","Gm","Bb","A","A",
                 "Dm","Bb","F","C","Gm","Bb","A","A"]
    # Broad horn phrases: a rising call, a major lift, a restrained middle,
    # then the opening call returns higher. Each explicit bar lasts four beats.
    phrases=[
      [("D4",1),("A4",1),("F4",.5),("E4",.5),("D4",1)],
      [("F4",1.5),("D4",.5),("Bb3",1.5),("-",.5)],
      [("C4",.5),("F4",.5),("A4",2),("G4",1)],
      [("E4",1.5),("D4",.5),("C4",1),("-",1)],
      [("D4",.5),("F4",.5),("A4",1),("D5",1.5),("C5",.5)],
      [("Bb4",1),("A4",.5),("F4",.5),("D4",1.5),("-",.5)],
      [("G4",1.5),("A4",.5),("Bb4",1),("G4",1)],
      [("E4",1),("C#4",1),("A3",1.5),("-",.5)],
      [("A4",1.5),("C5",.5),("F5",1.5),("E5",.5)],
      [("D5",.5),("C5",1.5),("G4",1.5),("-",.5)],
      [("A4",.5),("D5",1.5),("F5",1),("E5",1)],
      [("D5",2),("Bb4",1.5),("-",.5)],
      [("Bb4",1),("D5",1),("G5",1),("F5",.5),("D5",.5)],
      [("E5",.5),("F5",1.5),("D5",1),("A4",1)],
      [("Bb4",1.5),("A4",.5),("F4",1),("D4",1)],
      [("E4",1),("A4",1),("C#5",1.5),("-",.5)],
      [("D4",2),("Bb3",1),("-",1)],
      [("F4",1.5),("D4",.5),("Bb3",1),("-",1)],
      [("A3",1),("D4",2),("-",1)],
      [("G4",1.5),("F4",.5),("C4",1),("-",1)],
      [("D4",1),("G4",1.5),("Bb4",.5),("A4",1)],
      [("F4",1),("Bb4",1),("D5",1),("C5",1)],
      [("C#5",1.5),("B4",.5),("A4",1),("E4",1)],
      [("A4",1),("C#5",1),("E5",1),("-",1)],
    ]
    phrases += [[(pitch(n)+12 if n!="-" else n,d) for n,d in phrases[i]] for i in [0,1,2,3]]
    phrases += [phrases[12],phrases[14],phrases[15],
                [("A4",1),("E4",.5),("C#4",.5),("A3",1),("-",1)]]
    for bar,chord in enumerate(progression):
        start=bar*4;root,voices=chords[chord];bridge=16<=bar<20
        # Longer bow strokes and harp make the low-intensity arrangement work
        # on its own, with no military percussion leaking into exploration.
        for off,length,vel in [(0,1.92,75),(2,1.92,68)]:
            note("low_strings",start+off,root,length,vel)
            note("low_strings",start+off,root+12,length,vel-12)
        for key in voices[1:]:note("string_air",start+.02,key,3.92,54 if bridge else 64)
        for j,index in enumerate([0,1,2,3,2,1,3,2]):
            note("harp",start+j*.5+.012,voices[index]+12,.7,[75,55,62,57][j%4])
        cursor=0
        for j,(key,length) in enumerate(phrases[bar]):
            assert sum(d for _,d in phrases[bar])==4
            if key!="-":
                value=pitch(key)
                note("horns",start+cursor,value,length*.94,82 if bridge else 100-j%3*5)
                note("violins",start+cursor+.015,value+12,length*.96,67 if bridge else 78)
                # Brass accents support structural notes, not every melody note.
                if cursor in (0,2) or length>=1.5:
                    note("brass_crown",start+cursor+.01,value,length*.80,91)
            cursor+=length
        # A repeating sixteenth/eighth figure supplies urgency at fixed tempo.
        offsets=[0,.5,.75,1,1.5,2,2.5,2.75,3,3.5]
        pattern=[0,1,2,1,3,0,2,1,3,2]
        if bridge:offsets=[0,1,2,3];pattern=[0,2,1,3]
        for j,(off,index) in enumerate(zip(offsets,pattern)):
            note("short_strings",start+off,voices[index]+12,.19,96 if off in (0,2) else 75+j%3*4)
        for j,off in enumerate([0,.75,1.5,2,2.75,3.5]):
            note("low_pulse",start+off,root+12+(7 if j in (2,5) else 0),.28,96 if j in (0,3) else 77)
        # Timpani is pitched to the harmony. Percussion adds big downbeats,
        # marching ghost notes, and accelerating rolls into eight-bar phrases.
        timp=root if root>=36 else root+12
        for off,vel in [(0,112),(1.5,72),(2,96),(3.5,79)]:
            note("timpani",start+off,timp,.35,vel)
        for off in [0,2]:note("orchestra_drums",start+off,36,.20,111 if off==0 else 94)
        for off in [1,3]:note("orchestra_drums",start+off,38,.12,87)
        for off in [.75,1.75,2.75,3.75]:note("orchestra_drums",start+off,38,.07,40)
        if bar%4==0:note("orchestra_drums",start,49,.8,88)
        if bar%4==3:
            for j in range(8):
                note("orchestra_drums",start+3+j*.125,38 if j<4 else [48,47,45,43][j-4],.08,54+j*8)
            for j in range(4):note("timpani",start+3+j*.25,timp,.13,68+j*10)
    return dict(title="Crown of Embers",asset_id="crown-of-embers",revision="01 — Epic orchestra",bpm=120,
        bars=32,beats_per_bar=4,sample_rate=44100,seed=260926,key="D minor",
        progression=progression,sections=[dict(bar=1,name="The distant citadel"),
            dict(bar=9,name="Banners in the wind"),dict(bar=17,name="Before the charge"),
            dict(bar=25,name="Crown of embers")],stems=["world","theme","motion","battle"],
        moods={"explore":[1,.64,0,0],"drive":[.92,.90,.70,0],"combat":[.86,1,1,.90]},
        presentation=dict(stem_names=["Landscape","Hero theme","String motion","Battle"],
            mood_names=dict(explore="Explore",drive="Advance",combat="Epic")),tracks=tracks)


if __name__=="__main__":
    out=BASE/"revisions/epic-orchestra";out.mkdir(parents=True,exist_ok=True)
    path=out/"input-score.json"
    path.write_text(json.dumps(make_orchestra(),indent=2)+"\n")
    print(path)
