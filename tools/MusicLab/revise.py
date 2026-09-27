"""Revision 02: preserve the accepted tune; give the band more metal/rock weight."""
from copy import deepcopy
import json
from pathlib import Path

ROOT=Path(__file__).resolve().parents[2]
BASE=ROOT/"Assets/Work/music-lab/copper-circuit"


def heavier_score():
    score=deepcopy(json.loads((BASE/"source/score.json").read_text()))
    score["revision"]="02 — More metal / classic rock"
    score["revision_notes"]=["Main melody, harmony, tempo and form retained",
        "World and theme audio reused byte-for-byte from revision 01",
        "Lower, tighter guitar figures with occasional galloping pickups",
        "Stronger drum backbeat and more sustained combat guitar"]
    tracks=score["tracks"]
    roots={"Em":40,"C":48,"G":43,"D":50,"Am":45,"B":47,"Dsus":50}
    def add(track,beat,pitch,duration,velocity):
        tracks[track]["notes"].append(dict(beat=round(beat,6),pitch=pitch,
                                          duration=duration,velocity=velocity))
    for name,level in [("guitar_l",.76),("guitar_r",.60)]:
        tracks[name]["level"]=level
        tracks[name]["program"]=30
        tracks[name]["processing"]="heavier"
        tracks[name]["notes"]=[]
    # Root/fifth dyads give space in the low register. Short lower chugs alternate
    # with ringing accents; sixteenth pickups occur selectively, not constantly.
    for bar,chord in enumerate(score["progression"]):
        root=roots[chord];start=bar*4;bridge=16<=bar<24
        strokes=[(0,.42,109,True),(.5,.19,91,False),(1,.20,101,False),
                 (1.5,.34,103,True),(2,.41,111,True),(2.5,.18,91,False),
                 (3,.22,102,False),(3.5,.25,98,True)]
        if bar%4==3:
            strokes[-1:]=[(3.5,.16,100,False),(3.75,.15,92,False)]
        if bridge:strokes=[(0,1.15,96,True),(2,.35,101,True),(3,.20,88,False),(3.5,.25,94,False)]
        for off,length,vel,chord_hit in strokes:
            for k,interval in enumerate([0,7] if chord_hit else [0]):
                add("guitar_l",start+off+k*.005,root+interval,length,vel-k*8)
                add("guitar_r",start+off+.010+k*.006,root+interval,length*.93,vel-k*7-4)
    tracks["kit"]["processing"]="heavier"
    for n in tracks["kit"]["notes"]:
        if n["pitch"]==38:n["velocity"]=min(120,n["velocity"]+9)
        if n["pitch"]==36:n["velocity"]=min(120,n["velocity"]+5)
    for bar in range(score["bars"]):
        if bar%4==3 and not 16<=bar<24:
            add("kit",bar*4+3.75,36,.1,88)
    tracks["solo"]["level"]=.76
    tracks["solo"]["processing"]="heavier"
    for n in tracks["solo"]["notes"]:
        n["velocity"]=min(112,n["velocity"]+8)
        n["duration"]*=1.12
    # Keep both mood gain vectors and the accepted foundation unchanged.
    return score


if __name__=="__main__":
    out=BASE/"revisions/heavier-v2"
    out.mkdir(parents=True,exist_ok=True)
    path=out/"input-score.json"
    path.write_text(json.dumps(heavier_score(),indent=2)+"\n")
    print(path)
