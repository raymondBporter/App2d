"""Exercise cache freshness and normalization without launching Blender."""
import hashlib
import json
import sys
import tempfile
import unittest
from pathlib import Path
from PIL import Image, ImageDraw

PIPELINE = Path(__file__).resolve().parents[1]
sys.path.insert(0,str(PIPELINE))
from import_blender_character import import_character
from import_stick_figure import transform_frame


class ImportBlenderCharacterTests(unittest.TestCase):
    def setUp(self):
        work = (PIPELINE.parents[1]/'Assets/Work/blender-import-tests').resolve()
        work.mkdir(parents=True,exist_ok=True)
        self.temp = tempfile.TemporaryDirectory(dir=work)
        self.root = Path(self.temp.name).resolve()
        assert self.root.is_relative_to(work)
        self.source = self.root/'source'
        self.output = self.root/'content'
        rendered = self.source/'rendered/balance-left-foot'
        rendered.mkdir(parents=True)
        self.blend = self.source/'test.blend'
        self.blend.write_bytes(b'editable source fixture')
        config={'characterId':'player-sword','blend':'test.blend','clips':{
            'balance-left-foot':{'frames':[1,4],'loop':True,'durationSeconds':2}}}
        config_path=self.source/'render.json'
        config_path.write_text(json.dumps(config))
        self.report={'blendSha256':hashlib.sha256(self.blend.read_bytes()).hexdigest(),
                     'configSha256':hashlib.sha256(config_path.read_bytes()).hexdigest(),'files':{}}
        for i in [1,2]:
            image=Image.new('RGBA',(512,512))
            ImageDraw.Draw(image).line((240,300,240+i,378),fill='black',width=5)
            path=rendered/f'frame-{i:04d}.png'
            image.save(path)
            self.report['files'][f'balance-left-foot/{path.name}']=hashlib.sha256(path.read_bytes()).hexdigest()
        (self.source/'rendered/render-report.json').write_text(json.dumps(self.report))
        self.manifest=self.output/'characters/player-sword/character.json'
        self.manifest.parent.mkdir(parents=True)
        self.initial_manifest=json.dumps({'id':'player-sword','animations':{'idle':{'loop':True,'framesPerSecond':12}}})
        self.manifest.write_text(self.initial_manifest)

    def tearDown(self):
        self.temp.cleanup()

    def test_uses_existing_transform_and_preserves_animation_definitions(self):
        import_character(self.output,self.source)
        manifest=json.loads(self.manifest.read_text())
        self.assertEqual(manifest['animations']['idle'],{'loop':True,'framesPerSecond':12})
        self.assertEqual(manifest['animations']['balance-left-foot'],{'loop':True,'durationSeconds':2})
        for i in [1,2]:
            name=f'frame-{i:04d}.png'
            expected=transform_frame(self.source/'rendered/balance-left-foot'/name)
            with Image.open(self.manifest.parent/'animations/balance-left-foot'/name) as result:
                self.assertEqual(result.tobytes(),expected.tobytes())

    def test_unsaved_export_does_not_touch_existing_manifest(self):
        self.blend.write_bytes(b'new edit that has not been rendered')
        with self.assertRaisesRegex(ValueError,'source changed'):
            import_character(self.output,self.source)
        self.assertEqual(self.manifest.read_text(),self.initial_manifest)

    def test_corrupt_cache_does_not_touch_existing_manifest(self):
        (self.source/'rendered/balance-left-foot/frame-0002.png').write_bytes(b'broken image')
        with self.assertRaisesRegex(ValueError,'frame changed'):
            import_character(self.output,self.source)
        self.assertEqual(self.manifest.read_text(),self.initial_manifest)

    def test_missing_frame_does_not_touch_existing_manifest(self):
        (self.source/'rendered/balance-left-foot/frame-0002.png').unlink()
        with self.assertRaisesRegex(ValueError,'Missing or extra'):
            import_character(self.output,self.source)
        self.assertEqual(self.manifest.read_text(),self.initial_manifest)


if __name__=='__main__':
    unittest.main()
