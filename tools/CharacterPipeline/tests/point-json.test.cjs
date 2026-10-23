'use strict';
const {test} = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs'), os = require('node:os'), path = require('node:path');
const {decodeSource, readPack, writePack, pointsFromFrames} = require('../point-json.cjs');

test('packed source imports decode per-axis origins with the runtime float arithmetic', () => {
  const bytes = Buffer.from([0, 0, 10, 0, 20, 0, 10, 0, 20, 0, 30, 0]);
  const source = {version: 1, pointNames: ['root'], clips: {walk: {times: [0, 1], sampleCount: 2,
    byteOffset: 0, byteLength: 12, encoding: {type: 'uint16-le', origin: [-5, 10, 100], step: .5}}}};
  const pack = decodeSource(source, bytes);
  assert.deepEqual(pack.points, {walk: {root: [[-5, 15, 110], [0, 20, 115]]}});
  assert.deepEqual(pack.library, {version: 2, pointNames: ['root'], clips: {walk: {times: [0, 1]}}});
  assert.throws(() => decodeSource(source, bytes.subarray(0, 10)), /range/);
  assert.throws(() => decodeSource({...source, dataSha256: 'wrong'}, bytes), /hash/);
});

test('float source imports and JSON writing round-trip floats including signed zero', () => {
  const values = [Math.fround(1 / 3), -0, Math.fround(-1e-25), Math.fround(1e30), 2, 3];
  const bytes = Buffer.alloc(values.length * 4);
  values.forEach((value, i) => bytes.writeFloatLE(value, i * 4));
  const source = {version: 1, pointNames: ['root'], clips: {walk: {times: [0, 1],
    byteOffset: 0, byteLength: bytes.length, encoding: {type: 'float32-le'}}}};
  const pack = decodeSource(source, bytes);
  assert.deepEqual(pack.points.walk.root.flat(), values);
  const folder = fs.mkdtempSync(path.join(os.tmpdir(), 'app2d-point-json-'));
  try {
    const size = writePack(folder, pack);
    assert.equal(size, fs.statSync(path.join(folder, 'points/walk.json')).size);
    const reread = readPack(folder);
    assert.deepEqual(reread.library, pack.library);
    assert.deepEqual(reread.points.walk.root.flat().map(Math.fround), values);
    const file = path.join(folder, 'points/walk.json');
    fs.writeFileSync(file, fs.readFileSync(file, 'utf8').replace(', 2, 3]', ', 22, 33]'));
    assert.deepEqual(readPack(folder).points.walk.root[1].slice(1), [22, 33]);
  } finally {
    fs.rmSync(folder, {recursive: true, force: true});
  }
});

test('frame imports retain named XYZ tracks and reject malformed or nonfinite poses', () => {
  assert.deepEqual(pointsFromFrames(['root', 'hand'], [[1, 2, 3, 4, 5, 6], [7, 8, 9, 10, 11, 12]]),
    {root: [[1, 2, 3], [7, 8, 9]], hand: [[4, 5, 6], [10, 11, 12]]});
  assert.throws(() => pointsFromFrames(['root'], [[1, 2]]), /size/);
  assert.throws(() => pointsFromFrames(['root'], [[1, NaN, 3]]), /Nonfinite/);
});
