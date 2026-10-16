'use strict';
const fs = require('node:fs'), path = require('node:path'), crypto = require('node:crypto');

function readPack(folder) {
  const read = name => JSON.parse(fs.readFileSync(path.join(folder, name), 'utf8'));
  const library = read('library.json');
  return {library, points: Object.fromEntries(Object.keys(library.clips).map(id => [id, read('points/' + id + '.json')]))};
}

function writePack(folder, {library, points}) {
  fs.mkdirSync(folder, {recursive: true});
  fs.writeFileSync(path.join(folder, 'library.json'), JSON.stringify(library, null, 2) + '\n');
  fs.mkdirSync(path.join(folder, 'points'), {recursive: true});
  let size = 0;
  // One XYZ position per line; each clip has its own file with searchable point names.
  for (const [id, tracks] of Object.entries(points)) {
    const lines = ['{'];
    for (const [name, positions] of Object.entries(tracks)) {
      lines.push(`  ${JSON.stringify(name)}: [`);
      for (const position of positions) {
        const coordinates = position.map(v => {
          if (!Number.isFinite(v)) throw Error(`Nonfinite position: ${id}/${name}`);
          // Nine significant digits round-trip a float without exposing double precision noise.
          return Object.is(v, -0) ? '-0' : Number(Math.fround(v).toPrecision(9));
        });
        lines.push(`    [${coordinates.join(', ')}],`);
      }
      lines[lines.length - 1] = lines.at(-1).replace(/,$/, '');
      lines.push('  ],');
    }
    lines[lines.length - 1] = lines.at(-1).replace(/,$/, '');
    lines.push('}', '');
    const text = lines.join('\n');
    fs.writeFileSync(path.join(folder, 'points', id + '.json'), text);
    size += Buffer.byteLength(text);
  }
  return size;
}

function pointsFromFrames(names, frames) {
  return Object.fromEntries(names.map((name, point) => [name, frames.map(frame => {
    if (frame.length !== names.length * 3) throw Error('Wrong pose size');
    const xyz = frame.slice(point * 3, point * 3 + 3).map(Math.fround);
    if (!xyz.every(Number.isFinite)) throw Error('Nonfinite point: ' + name);
    return xyz;
  })]));
}

// The sibling exporter still supplies packed data. Decode it once at the import boundary.
function decodeSource(library, bytes) {
  if (library.dataSha256 && crypto.createHash('sha256').update(bytes).digest('hex') !== library.dataSha256.toLowerCase())
    throw Error('Source point data hash mismatch');
  const clips = {}, points = {};
  for (const [id, clip] of Object.entries(library.clips)) {
    const {byteOffset, byteLength, encoding, sampleCount, offset, bytes: packedBytes, ...metadata} = clip;
    const width = encoding.type === 'uint16-le' ? 2 : encoding.type === 'float32-le' ? 4 : 0;
    if (!width || byteOffset < 0 || byteLength !== clip.times.length * library.pointNames.length * 3 * width || byteOffset + byteLength > bytes.length)
      throw Error('Invalid source range: ' + id);
    points[id] = Object.fromEntries(library.pointNames.map((name, point) => [name, clip.times.map((_, sample) =>
      [0, 1, 2].map(axis => {
        const at = byteOffset + ((sample * library.pointNames.length + point) * 3 + axis) * width;
        return width === 4 ? bytes.readFloatLE(at)
          : Math.fround(Math.fround(encoding.origin[axis]) + Math.fround(bytes.readUInt16LE(at) * Math.fround(encoding.step)));
      }))]));
    clips[id] = metadata;
  }
  const {dataSha256, ...metadata} = library;
  return {library: {...metadata, version: 2, clips}, points};
}

module.exports = {readPack, writePack, pointsFromFrames, decodeSource};
