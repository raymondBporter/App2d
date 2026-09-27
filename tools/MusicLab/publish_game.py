"""Promote the selected Music Lab renders to durable game assets; no synthesis required at runtime."""
from pathlib import Path
import json
import shutil
import sys

ROOT=Path(__file__).resolve().parents[2]
WORK=ROOT/'Assets/Work/music-lab/copper-circuit/revisions'
STATIC=ROOT/'Assets/Static/audio/music'
for cue,revision in [('copper-circuit','overdrive-v3'),('crown-of-embers','epic-orchestra')]:
    source=WORK/revision
    target=STATIC/cue
    manifest=json.loads((source/'game/manifest.json').read_text())
    target.mkdir(parents=True,exist_ok=True)
    shutil.copy2(source/'game/manifest.json',target/'manifest.json')
    for stem in manifest['stems']:
        shutil.copy2(source/'game'/stem['file'],target/stem['file'])
    shutil.copytree(source/'licenses',target/'licenses',dirs_exist_ok=True)
    print(f'{cue}: {sum(p.stat().st_size for p in target.glob("*.ogg")):,} bytes')

# Static is the durable input for the normal art pipeline. Refresh only these
# new files in the existing runtime tree; a full pipeline rebuild also copies them.
shutil.copytree(STATIC,ROOT/'Assets/Runtime/audio/music',dirs_exist_ok=True)
shutil.copy2(ROOT/'Assets/Static/levels/cavern/zones.json',ROOT/'Assets/Runtime/levels/cavern/zones.json')
sys.path.insert(0,str(ROOT/'tools/ArtPipeline'))
from build_runtime_assets import write_manifest
write_manifest(ROOT/'Assets/Runtime')
