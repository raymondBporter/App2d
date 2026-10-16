// Import exported runtime data only. No Blender execution or source scene mutation.
'use strict';
const fs = require('node:fs'), path = require('node:path'), crypto = require('node:crypto');
const source = path.resolve(process.argv[2] || '../sprite-renderer');
const destination = path.resolve(__dirname, '../../Assets/Characters');
const read = file => JSON.parse(fs.readFileSync(path.join(source, file), 'utf8'));
const hash = bytes => crypto.createHash('sha256').update(bytes).digest('hex');
const personOnly=process.argv.includes('--person-only');
const wolfPack = require('./wolf-pack.cjs');
const {readPack, writePack, pointsFromFrames, decodeSource} = require('./point-json.cjs');
const quadrupedPath = path.join(destination, 'quadruped');
const retainedWolf = !personOnly && fs.existsSync(path.join(quadrupedPath, 'library.json'))
  ? wolfPack.extract(readPack(quadrupedPath)) : null;
const previousCatalog=personOnly?JSON.parse(fs.readFileSync(path.join(destination,'catalog.json'),'utf8')).libraries:[];
const catalog = [];
fs.mkdirSync(destination, {recursive:true});
function write(id, label, anatomy, pointNames, clips, drawing, points, provenance) {
  const folder = path.join(destination,id); fs.mkdirSync(folder,{recursive:true});
  let pack={library:{version:2,format:'app2d-point-library',id,label,anatomy,pointNames,clips,drawing,provenance},points};
  if(id==='quadruped' && retainedWolf) {
    pack=wolfPack.merge(pack,retainedWolf);
  }
  const bytes=writePack(folder,pack);
  catalog.push({id,label,anatomy,path:id+'/library.json',clipCount:Object.keys(pack.library.clips).length,bytes});
}
function packFrames(entries, pointNames) {
  const clips={}, points={}; let maxFloatError=0;
  for(const [id,c] of entries) {
    points[id]=pointsFromFrames(pointNames,c.frames);
    for(const frame of c.frames)for(const value of frame)maxFloatError=Math.max(maxFloatError,Math.abs(Math.fround(value)-value));
    const {frames,bones,...meta}=c;
    clips[id]={...meta,label:c.label||id,
      times:frames.map((_,i)=>i*c.duration/(frames.length-1)),
      warnings:c.warnings||[...(c.midpointError>.001?['Source midpoint error: '+c.midpointError+' units']:[]),...(c.loop&&c.loopSeam>.02?['Source loop seam: '+c.loopSeam+' units']:[])]};
  }
  return {clips,points,maxFloatError};
}
const pointer=read('output/universal/point-library/latest.json'),person=read(pointer.run+'/library.json');
const personBytes=fs.readFileSync(path.join(source,pointer.run,'points.bin'));
if(hash(personBytes)!==person.dataSha256)throw Error('Person binary hash mismatch');
const {animations,sourceHashes,data,dataSha256,...drawing}=person;
drawing.weapons = require(path.join(source,'scripts/point-library.js')).pointWeapons;
drawing.weapons.pistol = {label:'Pistol (placeholder)',length:1};
const decodedPerson=decodeSource({pointNames:person.pointNames,clips:animations},personBytes);
write('person','Person','person',person.pointNames,decodedPerson.library.clips,drawing,decodedPerson.points,{run:pointer.run,sourceHashes});

if(!personOnly) {
const hound=read('output/universal/quadruped/motion.json');
const h=packFrames(hound.clips.map(c=>[c.name,c]),hound.pointNames);
write('quadruped','Quadruped','hound',hound.pointNames,h.clips,{},h.points,
  {source:hound.source,license:hound.license,sha256:hound.sha256,floatConversionMaxError:h.maxFloatError});
const monster=read('output/universal/monster-mapping/motion.json');
for(const family of ['blob','flying']) {
  const names=Array.from({length:monster.drawings[family].pointCount},(_,i)=>'point'+i);
  const p=packFrames(Object.entries(monster.clips).filter(([,c])=>c.family===family),names);
  write(family,family==='blob'?'Blob':'Flying','monster',names,p.clips,monster.drawings[family],p.points,
    {source:'Quaternius monster mapping',floatConversionMaxError:p.maxFloatError,sourceHashes:[...new Set(Object.values(p.clips).map(c=>c.sourceSha256))]});
}
const inventory=read('output/universal/creature-inventory/inventory.json');
const Reference=require(path.join(source,'scripts/creature-inventory-renderer.js'));
for(const c of inventory.creatures.filter(c=>!c.duplicateOf)) {
  const bytes=fs.readFileSync(path.join(source,'output/universal/creature-inventory',c.data));
  if(hash(bytes)!==c.sha256)throw Error('Inventory hash mismatch '+c.id);
  const clips=Object.fromEntries(c.clips.map(clip=>[clip.action,{...clip,
    byteOffset:clip.offset,byteLength:clip.bytes,encoding:{...clip.encoding,type:'uint16-le',layout:'sample,point,xyz'},
    warnings:[...(clip.unresolvedIntervals?['Sampling review: '+clip.unresolvedIntervals+' intervals; worst '+clip.midpointErrorUnits+' units']:[]),
      ...(clip.loop&&clip.loopSeamUnits>.02?['Source loop seam: '+clip.loopSeamUnits+' units']:[])]}]));
  const decoded=decodeSource({pointNames:c.pointNames,clips},bytes);
  write(c.id,c.label+' (study)','inventory',c.pointNames,decoded.library.clips,
    {edges:c.edges,neutral:c.neutral,defaultYaw:c.defaultYaw,pack:c.pack,binding:Reference.anatomy(c),defaults:Reference.appearance(c.id)},decoded.points,c.source);
}
}
if(personOnly)catalog.push(...previousCatalog.filter(c=>c.id!=='person'));
// Preserve attribution and original license text alongside runtime data.
const licenses=[
  'assets/rgs-stick-figure/README.md','assets/kaykit/SOURCE.md',
  'assets/kaykit/character-animations-1.1/KayKit_Character_Animations_1.1/License.txt',
  'assets/quaternius/universal/SOURCE.md',
  'assets/quaternius/universal/universal-animation-library/Universal Animation Library[Standard]/License.txt',
  'assets/quaternius/quadruped-study/SOURCE.md','assets/quaternius/quadruped-study/License.txt',
  'assets/quaternius/monster-mapping-study/SOURCE.md',
  'assets/creature-inventory/easy-enemy/License.txt','assets/creature-inventory/dinosaurs/License.txt'];
if(!personOnly) for(const file of licenses){const target=path.join(destination,'provenance',file);fs.mkdirSync(path.dirname(target),{recursive:true});fs.copyFileSync(path.join(source,file),target);}
if(!personOnly) fs.copyFileSync(path.resolve(__dirname,'../../Assets/Sources/third-party/rgs-stick-figure/Stick Figure Character Sprites 2D/License.txt'),path.join(destination,'provenance/RGS-License.txt'));
if(!personOnly) for(const face of ['happy','grumpy']) {
  fs.mkdirSync(path.join(destination,'faces'),{recursive:true});
  fs.copyFileSync(path.join(source,'output/universal/quadruped/faces',face+'.png'),path.join(destination,'faces',face+'.png'));
}
if(Object.values(animations).some(c=>c.source?.startsWith('kevin_'))) {
  const kevin=read('assets/kevin-iglesias/sources.json');
  const files=['KEVIN_ANIMATIONS.md','assets/kevin-iglesias/sources.json','assets/kevin-iglesias/AUTHOR-README.txt'];
  for(const pack of Object.values(kevin.packs))for(const file of pack.files)if(file.file.endsWith('.pdf'))files.push(file.file);
  for(const file of files){const out=path.join(destination,'provenance',file);fs.mkdirSync(path.dirname(out),{recursive:true});fs.copyFileSync(path.join(source,file),out);}
}
fs.writeFileSync(path.join(destination,'catalog.json'),JSON.stringify({version:1,libraries:catalog},null,2));
if(personOnly){
  const previous=JSON.parse(fs.readFileSync(path.join(destination,'import.json'),'utf8'));
  previous.inputs.person=pointer.signature;fs.writeFileSync(path.join(destination,'import.json'),JSON.stringify(previous,null,2));
}else {
fs.writeFileSync(path.join(destination,'import.json'),JSON.stringify({version:1,inputs:{person:pointer.signature,
  quadruped:hash(fs.readFileSync(path.join(source,'output/universal/quadruped/motion.json'))),
  monsters:hash(fs.readFileSync(path.join(source,'output/universal/monster-mapping/motion.json'))),
  inventory:hash(fs.readFileSync(path.join(source,'output/universal/creature-inventory/inventory.json')))}},null,2));
}
console.log(`Imported ${catalog.length} libraries / ${catalog.reduce((n,c)=>n+c.clipCount,0)} clips / ${catalog.reduce((n,c)=>n+c.bytes,0)} JSON coordinate bytes.`);
