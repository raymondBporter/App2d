'use strict';
const {test} = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs'), path = require('node:path'), crypto = require('node:crypto');
const {extract, merge} = require('../wolf-pack.cjs');
const root = path.resolve(__dirname, '../../..');
const read = file => JSON.parse(fs.readFileSync(path.join(root, file), 'utf8'));
const library = read('Assets/Characters/quadruped/library.json');
const bytes = fs.readFileSync(path.join(root, 'Assets/Characters/quadruped/points.bin'));
const hash = data => crypto.createHash('sha256').update(data).digest('hex');
const pack = extract(library, bytes);
const config = read('tools/CharacterPipeline/wolf-mapping.json');

test('all nine source actions have complete finite data and correct timing/playback', () => {
  assert.equal(Object.keys(library.clips).length, 21);
  assert.equal(Object.keys(pack.library.clips).length, 9);
  assert.equal(library.pointNames.length, 76);
  assert.equal(hash(bytes), library.dataSha256);
  assert.equal(hash(fs.readFileSync(path.join(root, config.source))), config.sourceSha256);
  for (const [key, entry] of Object.entries(config.clips)) {
    const clip = pack.library.clips['tomek_wolf_' + key];
    assert.equal(clip.sourceAction, entry.action);
    assert.equal(clip.loop, entry.loop);
    assert.equal(clip.duration, key === 'die' ? 99 / 24 : key === 'cry' ? 36 / 24 : 1);
    assert.equal(clip.times[0], 0);
    assert.equal(clip.times.at(-1), clip.duration);
    assert.equal(clip.times.length, clip.sampleCount);
    assert.equal(clip.byteLength, clip.sampleCount * 76 * 12);
    assert.ok(clip.midpointError < .002, key + ' sampling error');
    if (clip.loop) assert.ok(clip.loopSeam < .0001, key + ' seam');
    let motion = 0;
    for (let sample = 0; sample < clip.sampleCount; sample++) {
      if (sample) assert.ok(clip.times[sample] > clip.times[sample - 1]);
      for (let coordinate = 0; coordinate < 76 * 3; coordinate++) {
        const value = pack.bytes.readFloatLE(clip.byteOffset + (sample * 76 * 3 + coordinate) * 4);
        assert.ok(Number.isFinite(value));
        motion = Math.max(motion, Math.abs(value - pack.bytes.readFloatLE(clip.byteOffset + coordinate * 4)));
      }
    }
    assert.ok(motion > .001, key + ' must not bake an unassigned/static action');
  }
});

test('mapped chains connect, left/right signs are preserved, and feet retain source motion', () => {
  const point = (clip, sample, name) => {
    const offset = clip.byteOffset + (sample * 76 + library.pointNames.indexOf(name)) * 12;
    return [0, 4, 8].map(i => bytes.readFloatLE(offset + i));
  };
  for (const clip of Object.values(library.clips).filter(c => c.source === 'tomek_wolf')) {
    for (const sample of [0, Math.floor(clip.sampleCount / 2), clip.sampleCount - 1]) {
      const chains = [['Back', 'Torso', 'Torso2', 'Torso3', 'Neck1', 'Neck2', 'Neck3', 'Head'],
        Array.from({length: 8}, (_, i) => 'Tail' + (i + 1))];
      for (const side of ['L', 'R']) {
        chains.push(['FrontUpperLeg', 'FrontLowerLeg', 'IKFrontLeg', 'FF'].map(n => n + '.' + side));
        chains.push(['BackLeg', 'BackUpperLeg', 'BackLowerLeg', 'IKBackLeg', 'FFB'].map(n => n + '.' + side));
      }
      for (const chain of chains) for (let i = 1; i < chain.length; i++) {
        const a = point(clip, sample, chain[i - 1] + ':tail'), b = point(clip, sample, chain[i] + ':head');
        assert.ok(Math.hypot(...a.map((v, j) => v - b[j])) < .00001, chain[i] + ' disconnected');
      }
    }
  }
  const idle = library.clips.tomek_wolf_idle;
  assert.ok(point(idle, 0, 'FF.L:head')[0] > 0);
  assert.ok(point(idle, 0, 'FF.R:head')[0] < 0);
  const jump = library.clips.tomek_wolf_jump;
  assert.ok(Math.abs(point(jump, 0, 'Back:head')[2] - point(jump, jump.sampleCount - 1, 'Back:head')[2]) > .1);
});

test('pack reimport is idempotent and a base refresh preserves every wolf byte', () => {
  const repeated = merge(library, bytes, pack);
  assert.deepEqual(repeated.library, library);
  assert.deepEqual(repeated.bytes, bytes);
  const baseClips = Object.fromEntries(Object.entries(library.clips).filter(([, c]) => c.source !== 'tomek_wolf'));
  const baseLength = Math.max(...Object.values(baseClips).map(c => c.byteOffset + c.byteLength));
  const baseBytes = bytes.subarray(0, baseLength);
  const base = {...library, clips: baseClips, dataSha256: hash(baseBytes)};
  const refreshed = merge(base, baseBytes, pack);
  assert.deepEqual(refreshed.bytes, bytes);
  for (const [id, clip] of Object.entries(baseClips)) assert.deepEqual(refreshed.library.clips[id], clip);
  assert.deepEqual(extract(refreshed.library, refreshed.bytes), pack);
});

test('mismatched point contracts and corrupt data fail before merging', () => {
  assert.throws(() => merge({...library, pointNames: [...library.pointNames].reverse()}, bytes, pack), /contract/);
  assert.throws(() => merge(library, bytes, {...pack, bytes: Buffer.alloc(pack.bytes.length)}), /hash/);
});

test('catalog and suggested look bindings resolve to the mapped runtime pack', () => {
  const catalog = read('Assets/Characters/catalog.json').libraries.find(l => l.id === 'quadruped');
  assert.equal(catalog.bytes, bytes.length);
  assert.equal(catalog.clipCount, Object.keys(library.clips).length);
  const look = read('Assets/Characters/looks/tomek-wolf.json');
  for (const [role, id] of Object.entries(look.bindings)) assert.equal(library.clips[id].suggestedRole, role);
  assert.equal(look.bindings.Death, 'tomek_wolf_die');
});
