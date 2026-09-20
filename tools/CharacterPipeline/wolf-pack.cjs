'use strict';
// Keep this supplement when the original Quaternius library is refreshed.
const crypto = require('node:crypto');
const hash = bytes => crypto.createHash('sha256').update(bytes).digest('hex');
const SOURCE = 'tomek_wolf';

function extract(library, bytes) {
  if (hash(bytes) !== library.dataSha256) throw Error('Quadruped binary hash mismatch');
  const clips = {}, chunks = []; let offset = 0;
  for (const [id, clip] of Object.entries(library.clips)) {
    if (clip.source !== SOURCE) continue;
    if (!id.startsWith(SOURCE + '_')) throw Error('Unexpected wolf clip ID: ' + id);
    const chunk = bytes.subarray(clip.byteOffset, clip.byteOffset + clip.byteLength);
    if (chunk.length !== clip.byteLength) throw Error('Invalid wolf byte range: ' + id);
    clips[id] = {...clip, byteOffset: offset}; chunks.push(chunk); offset += chunk.length;
  }
  if (!chunks.length) return null;
  const data = Buffer.concat(chunks);
  return {library: {...library, clips, dataSha256: hash(data),
    provenance: library.provenance.additionalSources?.[SOURCE] ?? library.provenance}, bytes: data};
}

function merge(library, bytes, pack) {
  if (!pack) return {library, bytes};
  if (hash(bytes) !== library.dataSha256 || hash(pack.bytes) !== pack.library.dataSha256)
    throw Error('Quadruped/pack binary hash mismatch');
  if (library.anatomy !== 'hound' || pack.library.anatomy !== 'hound' ||
      JSON.stringify(library.pointNames) !== JSON.stringify(pack.library.pointNames))
    throw Error('Wolf pack does not match the quadruped point contract');
  const clips = {}, chunks = []; let offset = 0;
  for (const [id, clip] of Object.entries(library.clips)) {
    if (clip.source === SOURCE) continue;
    const chunk = bytes.subarray(clip.byteOffset, clip.byteOffset + clip.byteLength);
    if (chunk.length !== clip.byteLength) throw Error('Invalid clip range: ' + id);
    clips[id] = {...clip, byteOffset: offset}; chunks.push(chunk); offset += chunk.length;
  }
  for (const [id, clip] of Object.entries(pack.library.clips)) {
    if (clip.source !== SOURCE || !id.startsWith(SOURCE + '_') || clips[id]) throw Error('Invalid wolf clip: ' + id);
    const chunk = pack.bytes.subarray(clip.byteOffset, clip.byteOffset + clip.byteLength);
    if (chunk.length !== clip.byteLength) throw Error('Invalid wolf range: ' + id);
    clips[id] = {...clip, byteOffset: offset}; chunks.push(chunk); offset += chunk.length;
  }
  const data = Buffer.concat(chunks);
  return {library: {...library, clips, dataSha256: hash(data), provenance: {...library.provenance,
    additionalSources: {...library.provenance.additionalSources, [SOURCE]: pack.library.provenance}}}, bytes: data};
}
module.exports = {extract, merge};
