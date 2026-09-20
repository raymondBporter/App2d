// Mechanical parity snapshots from the original JS runtime, kept small enough to review.
'use strict';
const fs=require('node:fs'),path=require('node:path');
const source=path.resolve(process.argv[2]||'../sprite-renderer'),out=path.resolve(__dirname,'../../Assets/Characters');
const read=f=>JSON.parse(fs.readFileSync(path.join(source,f)));
global.window={devicePixelRatio:1};global.devicePixelRatio=1;
const {DepthClip,SpinRenderer}=require(path.join(source,'scripts/spin-renderer.js'));
const {PointLibrary,PointAppearance,pointWeapons}=require(path.join(source,'scripts/point-library.js'));
const Person=require(path.join(source,'scripts/point-depth-renderer.js'));
const {HoundRenderer}=require(path.join(source,'scripts/quadruped-renderer.js'));
const Monster=require(path.join(source,'scripts/monster-doodle-renderer.js'));
const Inventory=require(path.join(source,'scripts/creature-inventory-renderer.js'));
function renderer(type){
  const r=Object.create(type.prototype);r.vertices=new Float32Array(1048576);r.canvas={width:600,height:600,getBoundingClientRect:()=>({width:600,height:600})};
  r.gl=new Proxy({NO_ERROR:0,getError:()=>0},{get:(o,k)=>k in o?o[k]:(()=>{})});return r;
}
const personOnly=process.argv.includes('--person-only');
const cases=personOnly?JSON.parse(fs.readFileSync(path.join(out,'reference-checks.json'),'utf8')).filter(c=>c.library!=='person'):[];
function save(library,clip,time,pose,r,appearance={}){
  const count=r.count/7,indices=new Set([0,count-1,...Array.from({length:150},(_,i)=>Math.floor(i*(count-1)/149))]);
  cases.push({library,clip,time,appearance,pose:Array.from(pose),vertexCount:count,vertices:[...indices].map(i=>[i,...r.vertices.slice(i*7,i*7+3)])});
}
const pointer=read('output/universal/point-library/latest.json'),spec=read(pointer.run+'/library.json');
const library=new PointLibrary(spec,fs.readFileSync(path.join(source,pointer.run,'points.bin'))),appearance=new PointAppearance(spec);
for(const id of ['walk','sword_attack','kaykit_melee_2h_attack_spin','wall_grip','kevin_humanm_attack1h01_r','kevin_humanm_attack1h01_l','kevin_humanm_attack2h01','kevin_humanf_bowidle02','kevin_humanm_dance01'].filter(id=>spec.animations[id])){
  const clip=library.get(id),time=clip.manifest.duration*.37,r=renderer(Person);
  r.library=spec;r.spec={headRadius:appearance.radius/spec.pixelsPerUnit,lineWidth:spec.style.lineWidth/spec.pixelsPerUnit,weaponLength:spec.weaponLength,weaponArt:spec.weaponArt};
  r.baseRadius=r.spec.headRadius;r.raw=new Float32Array(72);r.depths=new Float32Array(24);
  const xy=clip.sample(time),z=clip.sampleDepth(time),pose=[];for(let i=0;i<24;i++)pose.push(xy[i*2],xy[i*2+1],z[i]);
  r.render(clip,time,appearance,xy,appearance.transform(xy),{weapons:true,face:'relaxed'});save('person',id,time,pose,r);
  const altered={size:1.3,width:1.6,height:.7,head:1.4,cornerRadius:.6,flip:true,bladeLength:1.3};
  appearance.set(altered);r.render(clip,time,appearance,xy,appearance.transform(xy),{weapons:true,face:'relaxed'});save('person',id,time,pose,r,altered);
  appearance.set({size:1,width:1,height:1,head:1,cornerRadius:0,flip:false,bladeLength:spec.weaponLength});
  for(const weapon of ['rapier','mace','hammer']) for(const changed of [false,true]) {
    const look={...(changed?altered:{size:1,width:1,height:1,head:1,cornerRadius:0,flip:false}),weapon,bladeLength:changed?.7:pointWeapons[weapon].length,weaponHeadSize:changed?1.5:1};
    appearance.set(look);r.render(clip,time,appearance,xy,appearance.transform(xy),{weapons:true,face:'relaxed'});save('person',id,time,pose,r,look);
  }
  appearance.set({size:1,width:1,height:1,head:1,cornerRadius:0,flip:false,bladeLength:spec.weaponLength,weapon:'sword',weaponHeadSize:1});
}
function uniform(clip,time){const f=time/clip.duration*(clip.frames.length-1),a=Math.min(Math.floor(f),clip.frames.length-2),t=f-a;return clip.frames[a].map((v,i)=>v+(clip.frames[a+1][i]-v)*t);}
if(!personOnly) {
const hound=read('output/universal/quadruped/motion.json');
for(const clip of hound.clips.filter(c=>['Walk','Gallop_Jump'].includes(c.name))){
  const time=clip.duration*.37,pose=uniform(clip,time),r=renderer(HoundRenderer);r.names=Object.fromEntries(hound.pointNames.map((n,i)=>[n,i]));
  // Ground is a separate studio backdrop, not part of character geometry.
  r.line=function(a,b,...rest){if(a[0]===-3.4&&b[0]===3.4&&a[2]===4)return;return SpinRenderer.prototype.line.call(this,a,b,...rest);};
  r.drawHound(pose,{yaw:15,zoom:1,legLength:1,neckLength:1,tailLength:1,head:1.1,headWidth:1,headHeight:1,headRoundness:.35,body:.32,thickness:.24,spread:0,mode:'hound',curves:true,farTint:true});
  save('quadruped',clip.name,time,pose,r);
  const altered={legLength:.65,neckLength:.5,tailLength:.4,headWidth:1.4,headHeight:.8};
  r.drawHound(pose,{yaw:15,zoom:1,head:1.1,headRoundness:.35,body:.32,thickness:.24,spread:0,mode:'hound',curves:true,farTint:true,...altered});
  save('quadruped',clip.name,time,pose,r,altered);
}
const monster=read('output/universal/monster-mapping/motion.json');
for(const family of ['blob','flying']){
  const [id,clip]=Object.entries(monster.clips).find(([,c])=>c.family===family),time=clip.duration*.37,pose=uniform(clip,time),r=renderer(Monster);
  r.drawDoodle(pose,monster.drawings[family],{wingSize:.3});save(family,id,time,pose,r);
  r.drawDoodle(pose,monster.drawings[family],{wingSize:1,yaw:25});save(family,id,time,pose,r,{wingSize:1,yaw:25});
}
const inventory=read('output/universal/creature-inventory/inventory.json');
for(const creature of inventory.creatures.filter(c=>!c.duplicateOf)){
  const clip=creature.clips.find(c=>c.label==='Walk')||creature.clips[0],time=clip.duration*.37;
  const bytes=fs.readFileSync(path.join(source,'output/universal/creature-inventory',creature.data));
  const decoded=new DepthClip({...clip,pointNames:creature.pointNames},bytes.subarray(clip.offset,clip.offset+clip.bytes));
  const pose=decoded.sample(time),r=renderer(Inventory);r.drawCreature(pose,creature,{yaw:creature.defaultYaw});save(creature.id,clip.action,time,pose,r);
}
}
fs.writeFileSync(path.join(out,'reference-checks.json'),JSON.stringify(cases));
console.log('Captured '+cases.length+' source parity cases.');
if(!personOnly) {
const Head=require(path.join(source,'scripts/head-shape.js'));
const headCases=[{}, {width:.7,height:1.3,muzzle:1.25,depth:.3,drop:.12,jaw:.1}, {width:1.6,height:.6,muzzle:.2,depth:.65,jaw:.55}, {width:.8,height:.8,muzzle:.9,depth:.8,brow:.35,jaw:.8,roundness:.4}, {roundness:0}, {faceX:1.7,faceY:.8,faceSize:1.8,faceAngle:75}, {offsets:Head.names.map((_,i)=>i===2?[.15,-.2]:[0,0])}].map(values=>{
  const shape=Head.sanitize({...Head.defaults(),...values});return {shape,points:Head.points(shape),curve:Head.curve(shape),simple:Head.simple(shape)};
});
fs.writeFileSync(path.join(out,'head-reference-checks.json'),JSON.stringify(headCases));

}
