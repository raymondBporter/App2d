"""Revision 03: lead guitar and drum breaks, with the accepted Drive intact."""
from copy import deepcopy
import json
from pathlib import Path

BASE=Path(__file__).resolve().parents[2]/"Assets/Work/music-lab/copper-circuit"


def overdrive_score():
    score=deepcopy(json.loads((BASE/"revisions/heavier-v2/source/score.json").read_text()))
    score["revision"]="03 — Lead guitar & drum fireworks"
    score["revision_notes"]=[
        "World, Theme and Rock reused byte-for-byte from revision 02",
        "Same tempo, melody, form and mood gains; only Overdrive changes",
        "More forward lead with short answering runs and sustained phrase openings",
        "Alternating tom/snare breaks, kick pickups and phrase-ending drum runs"]
    solo=score["tracks"]["solo"]
    solo["level"]=1.04
    solo["pan"]=-.10
    # Keep the melodic anchors; replace each second bar's ending with a brief
    # chord-aware lead flourish. Every fourth bar leaves room for the drummer.
    runs={"Em":[76,79,81,83,86,83],"C":[76,79,81,84,83,79],
          "G":[74,79,81,83,86,83],"D":[74,78,81,86,81,78],
          "Am":[76,79,81,84,83,81],"B":[75,78,81,83,87,83],
          "Dsus":[74,79,81,86,81,79]}
    def add(track,beat,pitch,duration,velocity):
        track["notes"].append(dict(beat=round(beat,6),pitch=pitch,
                                  duration=round(duration,6),velocity=velocity))
    for bar,chord in enumerate(score["progression"]):
        start=bar*4
        if bar%4==1:
            cut=start+2.5
            solo["notes"]=[n for n in solo["notes"] if not cut<=n["beat"]<start+4]
            for n in solo["notes"]:
                if n["beat"]<cut<n["beat"]+n["duration"]:
                    n["duration"]=cut-n["beat"]-.01
            for j,key in enumerate(runs[chord]):
                add(solo,start+2.5+j*.25,key,.21,102 if j%2==0 else 91)
        if bar%4==3:
            # Ring a chord tone while the kit takes its turn in the foreground.
            solo["notes"]=[n for n in solo["notes"] if not start+2<=n["beat"]<start+4]
            add(solo,start+2,runs[chord][0],1.75,106)

    kit=score["tracks"]["power_kit"]
    kit["level"]=.82
    kit["processing"]="heavier"
    kit["notes"]=[]
    for bar in range(score["bars"]):
        start=bar*4
        # The existing Rock stem supplies the backbeat. These are independent
        # embellishments, with no doubled snare or kick on its existing hits.
        for off in [.5,1.5,2.5,3.5]:
            add(kit,start+off,54,.08,56)
        if bar%4==0:
            add(kit,start,57,.55,87)
            for off,key,vel in [(1.75,36,89),(2.5,48,87),(3.25,45,91),(3.75,43,100)]:
                add(kit,start+off,key,.14,vel)
        elif bar%4==1:
            # Guitar answers here: early drum flourish then leave it room.
            for j,key in enumerate([38,50,47,45]):
                add(kit,start+.125+j*.25,key,.10,[66,91,87,99][j])
            add(kit,start+2.25,36,.10,93)
        elif bar%4==2:
            for off in [.75,1.75,3.75]:
                add(kit,start+off,36,.10,95)
            for j,key in enumerate([50,47,38,45,43,41]):
                add(kit,start+2.5+j*.25+.012,key,.13,[94,80,69,98,91,106][j])
        else:
            # A two-beat drum break, extended at eight-bar phrase endings.
            begin=1 if bar%8==7 else 2
            keys=[38,50,48,38,47,45,38,43,47,45,43,41]
            count=round((4-begin)*4)
            for j,key in enumerate(keys[-count:]):
                add(kit,start+begin+j*.25+.018,key,.12,
                    min(116,83+(j%4)*6+(7 if j==count-1 else 0)))
            for off in [1.75,2.5,3.25]:
                add(kit,start+off,36,.10,93)
    for track in (solo,kit):
        track["notes"].sort(key=lambda n:(n["beat"],n["pitch"]))
    return score


if __name__=="__main__":
    out=BASE/"revisions/overdrive-v3"
    out.mkdir(parents=True,exist_ok=True)
    path=out/"input-score.json"
    path.write_text(json.dumps(overdrive_score(),indent=2)+"\n")
    print(path)
