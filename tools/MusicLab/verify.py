"""Check the actual delivery files and all legal gain combinations, without listening."""
from pathlib import Path
import itertools
import argparse
import json
import sys
import zipfile

HERE=Path(__file__).resolve().parent
ROOT=HERE.parents[1]
WORK=ROOT/"Assets/Work/music-lab"
sys.path.insert(0,str(WORK/"deps/python"))
import numpy as np
import soundfile as sf
import mido

parser=argparse.ArgumentParser()
parser.add_argument("--out",type=Path,default=WORK/"copper-circuit")
out=parser.parse_args().out
manifest=json.loads((out/"game/manifest.json").read_text())
expected=manifest["loop_end_frame"]
arrays=[];checks=[]
for stem in manifest["stems"]:
    wav,rate=sf.read(out/"stems"/(stem["id"]+".wav"),dtype="float32",always_2d=True)
    ogg,decoded_rate=sf.read(out/"game"/stem["file"],dtype="float32",always_2d=True)
    assert wav.shape==ogg.shape==(expected,2)
    assert rate==decoded_rate==manifest["sample_rate"]
    assert np.isfinite(ogg).all() and np.any(ogg)
    # A large seam jump relative to normal within-track adjacent samples would
    # flag a potentially audible splice. This does not prove perceptual quality.
    delta=np.abs(np.diff(ogg,axis=0))
    seam=float(np.max(np.abs(ogg[0]-ogg[-1])))
    limit=float(np.quantile(delta,.9999))
    assert seam <= max(.005,limit*2),f"Suspicious loop splice: {stem['id']}"
    checks.append(dict(stem=stem["id"],frames=len(ogg),seam_step=seam,
                       normal_step_p9999=limit,passed=True))
    arrays.append(ogg)

maximum_peak=0
for mask in itertools.product([0,1],repeat=len(arrays)):
    mixed=sum(a*g for a,g in zip(arrays,mask))
    peak=float(np.max(np.abs(mixed)))
    assert peak < .98,f"Clipping risk after lossy encoding: {mask}"
    maximum_peak=max(maximum_peak,peak)
asset_id=manifest.get("asset_id","copper-circuit")
score=json.loads((out/"source/score.json").read_text())
assert len(mido.MidiFile(out/"source"/f"{asset_id}.mid").tracks)==len(score["tracks"])+1
with zipfile.ZipFile(out/f"{asset_id}-game.zip") as archive:
    assert archive.testzip() is None
    assert all(not n.endswith((".sf2",".dll",".exe")) for n in archive.namelist())
result=dict(passed=True,stems=checks,all_16_gain_corners_peak_dbfs=20*np.log10(maximum_peak),
            scope="File integrity, frame alignment, loop splice outliers, gain headroom and archive contents. No listening judgment.")
(out/"verification.json").write_text(json.dumps(result,indent=2)+"\n")
print(json.dumps(result,indent=2))
