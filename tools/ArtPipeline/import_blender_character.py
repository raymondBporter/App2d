"""Import cached Blender frames using the existing baked-sprite transform."""
from __future__ import annotations
import hashlib
import json
from pathlib import Path
from import_stick_figure import transform_frame, write_player_geometry


def import_character(content_root: Path, source: Path) -> None:
    config_path = source/'render.json'
    config = json.loads(config_path.read_text(encoding='utf-8'))
    rendered = source/'rendered'
    report = json.loads((rendered/'render-report.json').read_text(encoding='utf-8'))
    for file, key in [(source/config['blend'],'blendSha256'),(config_path,'configSha256')]:
        if hashlib.sha256(file.read_bytes()).hexdigest()!=report[key]:
            raise ValueError(f'Blender source changed since export: {file}. Re-render with render_blender_character.py -- --config "{config_path}".')
    character = content_root/'characters'/config['characterId']
    manifest_path = character/'character.json'
    manifest = json.loads(manifest_path.read_text(encoding='utf-8'))
    # Validate the complete cache before touching its destination.
    for name,spec in config['clips'].items():
        paths = sorted((rendered/name).glob('frame-*.png'))
        expected = [f'frame-{i:04d}.png' for i in range(1,len(spec['frames'])+1)]
        if [p.name for p in paths] != expected:
            raise ValueError(f'Missing or extra Blender frames for {name}; re-render the source.')
        for path in paths:
            relative = path.relative_to(rendered).as_posix()
            if hashlib.sha256(path.read_bytes()).hexdigest()!=report['files'][relative]:
                raise ValueError(f'Blender frame changed since export: {path}; re-render the source.')
    for name,spec in config['clips'].items():
        destination = character/'animations'/name
        destination.mkdir(parents=True,exist_ok=True)
        for stale in destination.glob('frame-*.png'):
            stale.unlink()
        for path in sorted((rendered/name).glob('frame-*.png')):
            frame = transform_frame(path)
            frame.save(destination/path.name,optimize=True)
        manifest['animations'][name] = {'loop':spec['loop'],'durationSeconds':spec['durationSeconds']}
    manifest_path.write_text(json.dumps(manifest,indent=2)+'\n',encoding='utf-8')
    if 'idle' in config['clips']:
        write_player_geometry(content_root,('player-sword','player-gun','player-unarmed'))
    print(f'Imported {len(config["clips"])} Blender animations for {config["characterId"]}.')


if __name__ == '__main__':
    import argparse
    root = Path(__file__).resolve().parents[2]
    parser = argparse.ArgumentParser()
    parser.add_argument('--content-root',type=Path,default=root/'Assets/Runtime')
    args = parser.parse_args()
    source_root=root/'Assets/Sources/characters/player-sword'
    sources=[source_root]+[path.parent for path in sorted(source_root.glob('*/render.json'))]
    for source in sources:
        import_character(args.content_root,source)
