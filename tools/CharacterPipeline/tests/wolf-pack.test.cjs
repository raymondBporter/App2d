'use strict';
const {test} = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs'), path = require('node:path'), crypto = require('node:crypto');
const {extract, merge} = require('../wolf-pack.cjs');
const {readPack} = require('../point-json.cjs');
const root = path.resolve(__dirname, '../../..');
const read = file => JSON.parse(fs.readFileSync(path.join(root, file), 'utf8'));
const base = readPack(path.join(root, 'Assets/Characters/quadruped'));
const {library, points} = base;
const pack = extract(base);
const config = read('tools/CharacterPipeline/wolf-mapping.json');
const hash = data => crypto.createHash('sha256').update(data).digest('hex');

test('all nine source actions have complete finite data and correct timing/playback', () => {
  assert.equal(Object.keys(library.clips).length, 21);
  assert.equal(Object.keys(pack.library.clips).length, 9);
  assert.equal(library.pointNames.length, 76);
  assert.equal(hash(fs.readFileSync(path.join(root, config.source))), config.sourceSha256);
  for (const [key, entry] of Object.entries(config.clips)) {
    const id = 'tomek_wolf_' + key, clip = pack.library.clips[id];
    assert.equal(clip.sourceAction, entry.action);
    assert.equal(clip.loop, entry.loop);
    assert.equal(clip.duration, key === 'die' ? 99 / 24 : key === 'cry' ? 36 / 24 : 1);
    assert.equal(clip.times[0], 0);
    assert.equal(clip.times.at(-1), clip.duration);
    assert.ok(clip.midpointError < .002, key + ' sampling error');
    if (clip.loop) assert.ok(clip.loopSeam < .0001, key + ' seam');
    let motion = 0;
    for (const name of library.pointNames) {
      const positions = pack.points[id][name];
      assert.equal(positions.length, clip.times.length);
      for (let sample = 0; sample < clip.times.length; sample++) {
        if (sample) assert.ok(clip.times[sample] > clip.times[sample - 1]);
        assert.equal(positions[sample].length, 3);
        for (let axis = 0; axis < 3; axis++) {
          const value = positions[sample][axis];
          assert.ok(Number.isFinite(value));
          motion = Math.max(motion, Math.abs(value - positions[0][axis]));
        }
      }
    }
    assert.ok(motion > .001, key + ' must not bake an unassigned/static action');
  }
});

test('mapped chains connect, left/right signs are preserved, and feet retain source motion', () => {
  const point = (id, sample, name) => points[id][name][sample];
  for (const [id, clip] of Object.entries(library.clips).filter(([, c]) => c.source === 'tomek_wolf')) {
    for (const sample of [0, Math.floor(clip.times.length / 2), clip.times.length - 1]) {
      const chains = [['Back', 'Torso', 'Torso2', 'Torso3', 'Neck1', 'Neck2', 'Neck3', 'Head'],
        Array.from({length: 8}, (_, i) => 'Tail' + (i + 1))];
      for (const side of ['L', 'R']) {
        chains.push(['FrontUpperLeg', 'FrontLowerLeg', 'IKFrontLeg', 'FF'].map(n => n + '.' + side));
        chains.push(['BackLeg', 'BackUpperLeg', 'BackLowerLeg', 'IKBackLeg', 'FFB'].map(n => n + '.' + side));
      }
      for (const chain of chains) for (let i = 1; i < chain.length; i++) {
        const a = point(id, sample, chain[i - 1] + ':tail'), b = point(id, sample, chain[i] + ':head');
        assert.ok(Math.hypot(...a.map((v, j) => v - b[j])) < .00001, chain[i] + ' disconnected');
      }
    }
  }
  assert.ok(point('tomek_wolf_idle', 0, 'FF.L:head')[0] > 0);
  assert.ok(point('tomek_wolf_idle', 0, 'FF.R:head')[0] < 0);
  const jump = library.clips.tomek_wolf_jump;
  assert.ok(Math.abs(point('tomek_wolf_jump', 0, 'Back:head')[2] -
    point('tomek_wolf_jump', jump.times.length - 1, 'Back:head')[2]) > .1);
});

test('pack reimport is idempotent and a base refresh preserves every wolf coordinate', () => {
  assert.deepEqual(merge(base, pack), base);
  const baseClips = Object.fromEntries(Object.entries(library.clips).filter(([, c]) => c.source !== 'tomek_wolf'));
  const refreshed = merge({library: {...library, clips: baseClips},
    points: Object.fromEntries(Object.keys(baseClips).map(id => [id, points[id]]))}, pack);
  assert.deepEqual(refreshed, base);
  assert.deepEqual(extract(refreshed), pack);
});

test('mismatched point contracts and invalid point data fail before merging', () => {
  assert.throws(() => merge({...base, library: {...library, pointNames: [...library.pointNames].reverse()}}, pack), /contract/);
  assert.throws(() => merge(base, {...pack, points: {}}), /points/);
});

test('catalog and suggested look bindings resolve to the mapped runtime pack', () => {
  const catalog = read('Assets/Characters/catalog.json').libraries.find(l => l.id === 'quadruped');
  assert.equal(catalog.bytes, Object.keys(library.clips).reduce((sum, id) =>
    sum + fs.statSync(path.join(root, 'Assets/Characters/quadruped/points', id + '.json')).size, 0));
  assert.equal(catalog.clipCount, Object.keys(library.clips).length);
  const look = read('Assets/Characters/looks/tomek-wolf.json');
  for (const [role, id] of Object.entries(look.bindings)) assert.equal(library.clips[id].suggestedRole, role);
  assert.equal(look.bindings.Death, 'tomek_wolf_die');
});
