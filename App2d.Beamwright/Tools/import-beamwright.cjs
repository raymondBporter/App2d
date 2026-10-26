// Read the original checkout without modifying it. Node is needed only to regenerate these snapshots.
// node App2d.Beamwright/Tools/import-beamwright.cjs C:/path/to/beamwright
const fs = require('node:fs');
const path = require('node:path');
const root = path.resolve(process.argv[2] || '../beamwright');
const { LEVELS } = require(path.join(root, 'src/levels.js'));
const engine = require(path.join(root, 'src/engine.js'));
const solutions = require(path.join(root, 'data/solutions.json'));
const pkg = require(path.join(root, 'package.json'));
const output = path.join(__dirname, '../Content');
fs.mkdirSync(output, { recursive: true });
const angles = { mirror: 135, splitter: 135, convex: 90, concave: 90, cmirror: 90,
  xmirror: 90, block: 90, prism: 0, dblock: 0, cateye: 0, droplet: 0, diamond: 270, fiber: 0, bend: 0 };
let chapter = 1;
const levels = LEVELS.map((level, i) => {
  chapter = level.chapter || chapter;
  const solution = level.handSol || solutions[i]?.sol || [];
  if (!engine.trace(level, solution).win) throw new Error('Broken upstream solution: ' + level.name);
  return { ...level, tip: level.tip?.replace(/\btap\b/g, 'click'), chapter, solution, par: solution.length };
});
const pieces = engine.PIECE_ORDER.map(type => ({ type, ...engine.PIECES[type], defaultAngle: angles[type] }));
fs.writeFileSync(path.join(output, 'campaign.json'), JSON.stringify({ version: pkg.version, pieces, levels }, null, 2) + '\n');
const references = [];
for (let i = 0; i < levels.length; i++) {
  for (const solved of [false, true]) {
    const result = engine.trace(levels[i], solved ? levels[i].solution : []);
    const profiles = result.profiles.map((profile, ti) => {
      if (!profile) return null;
      const target = levels[i].targets[ti], angle = (target.a || 0) * Math.PI / 180;
      const seams = Array(profile.bins.length + 1).fill(0), half = target.half || .4;
      for (const segment of result.segs.filter(s => s.ti === ti)) {
        const dx = segment.x2 - segment.x1, dy = segment.y2 - segment.y1, length = Math.hypot(dx, dy);
        const incidence = Math.acos(Math.max(-1, Math.min(1, (dx * Math.cos(angle) + dy * Math.sin(angle)) / length))) * 180 / Math.PI;
        if (incidence > (target.tolerance ?? 89)) continue;
        const u = -(segment.x2 - target.x - .5 - (target.ox || 0)) * Math.sin(angle)
          + (segment.y2 - target.y - .5 - (target.oy || 0)) * Math.cos(angle);
        const bin = (u + half) / (2 * half) * profile.bins.length, nearest = Math.round(bin);
        if (nearest > 0 && nearest < profile.bins.length && Math.abs(bin - nearest) < .0001)
          seams[nearest] += segment.I * 100;
      }
      return { ...profile, seamPower: seams };
    });
    references.push({ level: i, solved, win: result.win, lm: result.lm, quality: result.quality,
      lit: result.lit, profiles });
  }
}
fs.writeFileSync(path.join(output, 'reference-traces.json'), JSON.stringify(references, null, 2) + '\n');
console.log(`Imported ${pieces.length} parts, ${levels.length} levels, and ${references.length} reference traces.`);
