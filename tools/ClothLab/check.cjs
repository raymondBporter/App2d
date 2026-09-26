const fs=require('node:fs'),assert=require('node:assert/strict');
const {PuppetCapeSimulation}=require('./cape-simulation.js');
const html=fs.readFileSync(process.argv[2]||'artifacts/cloth-lab/puppet-cape.html','utf8');
const data=JSON.parse(html.match(/const data=(\{[^\n]+\});/)[1]);
const results=[];
for(const settings of [{gait:'walk'},{gait:'run'},{gait:'run',air:false},{gait:'transition'},{gait:'run',rate:1.5,headwind:3,bend:0},{gait:'walk',rate:.5,bend:100},{gait:'run',mesh:1,bend:0},{gait:'run',mesh:8,bend:0}]){
  const sim=new PuppetCapeSimulation(data,settings);let trail=0,samples=0,maxStep=0,maxAttachmentError=0;
  for(let k=0;k<1800;k++){
    const previous=sim.p.slice();sim.step();
    for(let i=0;i<sim.p.length;i++){assert(Number.isFinite(sim.p[i]));assert(Math.abs(sim.p[i])<6);maxStep=Math.max(maxStep,Math.abs(sim.p[i]-previous[i]));}
    for(let i=0;i<sim.attachments.length;i++)maxAttachmentError=Math.max(maxAttachmentError,Math.abs(sim.p[i]-sim.attachments[i]));
    if(k>1560){for(let u=0;u<=sim.cols;u++)trail+=sim.attachments[u*3]-sim.p[(sim.rows*(sim.cols+1)+u)*3];samples+=sim.cols+1;}
  }
  assert.equal(maxAttachmentError,0,'Cape lost an attachment');
  assert(maxStep<.25,'Discontinuous cape motion');
  results.push({...settings,meanTrailingDistance:trail/samples,maxStep,maxAttachmentError});
}
assert(results[1].meanTrailingDistance>results[0].meanTrailingDistance+.2,'Running should trail farther than walking');
assert(results[1].meanTrailingDistance>results[2].meanTrailingDistance+.2,'Airflow should visibly affect the cape');
assert(Math.abs(data.walk.speed-.5/1.2)<1e-6);
assert(Math.abs(data.run.speed-1.6/.72)<1e-6);
const remesh=new PuppetCapeSimulation(data,{gait:'transition'});
for(let i=0;i<240;i++)remesh.step();
for(const level of [1,8,2,7,3,6,4,5]){
  const phase=remesh.phase,time=remesh.time;remesh.setResolution(level);
  assert.equal(remesh.p.length/3,(2*level+1)*(3*level+1));
  assert.equal(remesh.phase,phase);assert.equal(remesh.time,time);
  for(let i=0;i<120;i++)remesh.step();
  assert(remesh.p.every(x=>Number.isFinite(x)&&Math.abs(x)<6));
  for(let i=0;i<remesh.attachments.length;i++)assert.equal(remesh.p[i],remesh.attachments[i]);
}
// Upper arms and forearms must each exclude a point placed inside their capsule.
// Running's backward arm swing reaches the mantle; walking can remain clear of it.
const armsOn=new PuppetCapeSimulation(data,{gait:'run',mesh:3});
const armsOff=new PuppetCapeSimulation(data,{gait:'run',mesh:3,arms:false});
armsOn.colliders();assert.equal(armsOn.armCapsules.length,4);
assert.equal(armsOn.shoulderCapsules.length,2);
for(const capsule of armsOn.armCapsules){
  const [a,b,r]=capsule,point=a.map((x,d)=>(x+b[d])*.5);
  const before=point.slice();armsOn.project(point,0,.025,[capsule]);
  assert(Math.hypot(...point.map((x,d)=>x-before[d]))>=r+.0249);
}
for(let i=0;i<720;i++){armsOn.step();armsOff.step();}
assert.equal(armsOff.armCapsules.length,0);
const armResponse=Math.sqrt(armsOn.p.reduce((sum,x,i)=>sum+(x-armsOff.p[i])**2,0)/armsOn.p.length);
assert(armResponse>.005,'Moving arms should affect the free cloth');
const topSpan=Math.hypot(...armsOn.pattern(0,0).map((x,d)=>x-armsOn.pattern(1,0)[d]));
const mantleSpan=Math.hypot(...armsOn.pattern(0,.28).map((x,d)=>x-armsOn.pattern(1,.28)[d]));
assert(mantleSpan>topSpan*2,'The neckline must flare out over the shoulders');
console.log(JSON.stringify(results,null,2));
console.log('Arm collision response (RMS difference): '+armResponse.toFixed(4)+' units');
console.log('PASS: finite states, bounded steps through loop seams and gait transitions, exact attachment pins, authored speeds, gait/airflow response, and live remeshing at all slider levels.');
