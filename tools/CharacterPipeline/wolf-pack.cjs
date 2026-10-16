'use strict';
// Keep this supplement when the original Quaternius library is refreshed.
const SOURCE = 'tomek_wolf';

function extract({library, points}) {
  const clips = Object.fromEntries(Object.entries(library.clips).filter(([, clip]) => clip.source === SOURCE));
  if (!Object.keys(clips).length) return null;
  return {library: {...library, clips,
    provenance: library.provenance.additionalSources?.[SOURCE] ?? library.provenance},
    points: Object.fromEntries(Object.keys(clips).map(id => [id, points[id]]))};
}

function merge(base, pack) {
  if (!pack) return base;
  const {library, points} = base;
  if (library.anatomy !== 'hound' || pack.library.anatomy !== 'hound' ||
      JSON.stringify(library.pointNames) !== JSON.stringify(pack.library.pointNames))
    throw Error('Wolf pack does not match the quadruped point contract');
  const clips = Object.fromEntries(Object.entries(library.clips).filter(([, clip]) => clip.source !== SOURCE));
  const mergedPoints = Object.fromEntries(Object.keys(clips).map(id => [id, points[id]]));
  for (const [id, clip] of Object.entries(pack.library.clips)) {
    if (clip.source !== SOURCE || !id.startsWith(SOURCE + '_') || clips[id]) throw Error('Invalid wolf clip: ' + id);
    const tracks = pack.points[id];
    if (!tracks || Object.keys(tracks).length !== library.pointNames.length || library.pointNames.some(name =>
      !Array.isArray(tracks[name]) || tracks[name].length !== clip.times.length || tracks[name].some(xyz =>
        !Array.isArray(xyz) || xyz.length !== 3 || !xyz.every(Number.isFinite))))
      throw Error('Invalid wolf points: ' + id);
    clips[id] = clip;
    mergedPoints[id] = tracks;
  }
  return {library: {...library, clips, provenance: {...library.provenance,
    additionalSources: {...library.provenance.additionalSources, [SOURCE]: pack.library.provenance}}}, points: mergedPoints};
}
module.exports = {extract, merge};
