// Import exported runtime data only. No Blender execution or source scene mutation.
'use strict';
const fs = require('node:fs'), path = require('node:path'), crypto = require('node:crypto');
const source = path.resolve(process.argv[2] || '../sprite-renderer');
const destination = path.resolve(__dirname, '../../Assets/Characters');
const read = file => JSON.parse(fs.readFileSync(path.join(source, file), 'utf8'));
const hash = bytes => crypto.createHash('sha256').update(bytes).digest('hex');
const personOnly=process.argv.includes('--person-only');
const wolfPack = require('./wolf-pack.cjs');
const quadrupedPath = path.join(destination, 'quadruped');
const retainedWolf = !personOnly && fs.existsSync(path.join(quadrupedPath, 'library.json'))
  ? wolfPack.extract(JSON.parse(fs.readFileSync(path.join(quadrupedPath, 'library.json'), 'utf8')),
      fs.readFileSync(path.join(quadrupedPath, 'points.bin'))) : null;
const previousCatalog=personOnly?JSON.parse(fs.readFileSync(path.join(destination,'catalog.json'),'utf8')).libraries:[];
const catalog = [];
fs.mkdirSync(destination, {recursive:true});
function write(id, label, anatomy, pointNames, clips, drawing, bytes, provenance) {
  const folder = path.join(destination,id); fs.mkdirSync(folder,{recursive:true});
  let spec={version:1,format:'app2d-point-library',id,label,anatomy,pointNames,clips,drawing,dataSha256:hash(bytes),provenance};
  if(id==='quadruped' && retainedWolf) {
    const merged=wolfPack.merge(spec,bytes,retainedWolf); spec=merged.library; bytes=merged.bytes;
  }
  fs.writeFileSync(path.join(folder,'points.bin'),bytes);
  fs.writeFileSync(path.join(folder,'library.json'),JSON.stringify(spec));
  catalog.push({id,label,anatomy,path:id+'/library.json',clipCount:Object.keys(spec.clips).length,bytes:bytes.length});
}
function packFrames(entries) {
  const chunks=[], clips={}; let offset=0, maxFloatError=0;
  for(const [id,c] of entries) {
    const stride=c.frames[0].length, bytes=Buffer.alloc(c.frames.length*stride*4);
    c.frames.forEach((frame,n)=>frame.forEach((v,i)=>{
      if(!Number.isFinite(v)||frame.length!==stride)throw Error('Invalid frame '+id);
      bytes.writeFloatLE(v,(n*stride+i)*4);maxFloatError=Math.max(maxFloatError,Math.abs(bytes.readFloatLE((n*stride+i)*4)-v));
    }));
    const {frames,bones,...meta}=c;
    clips[id]={...meta,label:c.label||id,sampleCount:frames.length,
      times:frames.map((_,i)=>i*c.duration/(frames.length-1)),byteOffset:offset,byteLength:bytes.length,
      encoding:{type:'float32-le',layout:'sample,point,xyz',origin:[0,0,0],step:1},
      warnings:c.warnings||[...(c.midpointError>.001?['Source midpoint error: '+c.midpointError+' units']:[]),...(c.loop&&c.loopSeam>.02?['Source loop seam: '+c.loopSeam+' units']:[])]};
    chunks.push(bytes);offset+=bytes.length;
  }
  return {clips,bytes:Buffer.concat(chunks),maxFloatError};
}
const pointer=read('output/universal/point-library/latest.json'),person=read(pointer.run+'/library.json');
const personBytes=fs.readFileSync(path.join(source,pointer.run,'points.bin'));
if(hash(personBytes)!==person.dataSha256)throw Error('Person binary hash mismatch');
const {animations,sourceHashes,...drawing}=person;
drawing.weapons = require(path.join(source,'scripts/point-library.js')).pointWeapons;
drawing.weapons.pistol = {label:'Pistol (placeholder)',length:1};
write('person','Person','person',person.pointNames,animations,drawing,personBytes,{run:pointer.run,sourceHashes});

if(!personOnly) {
const hound=read('output/universal/quadruped/motion.json');
const h=packFrames(hound.clips.map(c=>[c.name,c]));
write('quadruped','Quadruped','hound',hound.pointNames,h.clips,{},h.bytes,
  {source:hound.source,license:hound.license,sha256:hound.sha256,floatConversionMaxError:h.maxFloatError});
const monster=read('output/universal/monster-mapping/motion.json');
for(const family of ['blob','flying']) {
  const p=packFrames(Object.entries(monster.clips).filter(([,c])=>c.family===family));
  write(family,family==='blob'?'Blob':'Flying','monster',Array.from({length:monster.drawings[family].pointCount},(_,i)=>'point'+i),p.clips,monster.drawings[family],p.bytes,
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
  write(c.id,c.label+' (study)','inventory',c.pointNames,clips,
    {edges:c.edges,neutral:c.neutral,defaultYaw:c.defaultYaw,pack:c.pack,binding:Reference.anatomy(c),defaults:Reference.appearance(c.id)},bytes,c.source);
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
console.log(`Imported ${catalog.length} libraries / ${catalog.reduce((n,c)=>n+c.clipCount,0)} clips / ${catalog.reduce((n,c)=>n+c.bytes,0)} packed bytes.`);
