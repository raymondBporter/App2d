/* Browser + Node: particle cape in the character's translating reference frame.
   Positive X is travel; Y is up; positive Z is away (the puppet convention).
   Air drag is deliberately stylized, not a calibrated aerodynamic model. */
class PuppetCapeSimulation {
  constructor(data, settings = {}) {
    this.data=data;
    this.settings=Object.assign({gait:'run',rate:1,headwind:0,air:true,collision:true,arms:true,length:1.6,bend:40,mesh:4},settings);
    this.settings.mesh=Math.max(1,Math.min(8,Math.round(this.settings.mesh)));
    this.cols=2*this.settings.mesh;this.rows=3*this.settings.mesh;this.dt=1/120;this.time=0;this.phase=0;this.travel=0;
    this.blend=this.settings.gait==='walk'?0:1;this.speed=0;this.previousSpeed=null;
    this.ids=Object.fromEntries(data.run.controls.map((id,i)=>[id,i*3]));
    this.pose=new Float64Array(data.run.frames[0].length);this.attachments=new Float64Array((this.cols+1)*3);
    this.p=new Float64Array((this.cols+1)*(this.rows+1)*3);this.old=this.p.slice();this.constraints=[];
    this.updatePose(0);this.resetCloth();
  }
  point(id){const i=this.ids[id];return [this.pose[i],this.pose[i+1],this.pose[i+2]];}
  setResolution(level){
    level=Math.max(1,Math.min(8,Math.round(level)));
    if(this.cols===level*2)return;
    const before={p:this.p,old:this.old,cols:this.cols,rows:this.rows};
    this.settings.mesh=level;this.cols=level*2;this.rows=level*3;
    this.attachments=new Float64Array((this.cols+1)*3);
    this.p=new Float64Array((this.cols+1)*(this.rows+1)*3);this.old=this.p.slice();
    this.updatePose(0);this.resetCloth();this.colliders();
    // Resample position and previous position together to retain shape and velocity.
    for(let v=0;v<=this.rows;v++)for(let u=0;u<=this.cols;u++){
      const x=u/this.cols*before.cols,y=v/this.rows*before.rows,x0=Math.floor(x),y0=Math.floor(y),tx=x-x0,ty=y-y0,i=(v*(this.cols+1)+u)*3;
      for(const key of ['p','old'])for(let d=0;d<3;d++){
        let value=0;
        for(let dy=0;dy<=1;dy++)for(let dx=0;dx<=1;dx++)value+=before[key][(Math.min(before.rows,y0+dy)*(before.cols+1)+Math.min(before.cols,x0+dx))*3+d]*(dx?tx:1-tx)*(dy?ty:1-ty);
        this[key][i+d]=value;
      }
      if(v>0){const prior=[this.p[i],this.p[i+1],this.p[i+2]];this.project(this.p,i,.025);for(let d=0;d<3;d++)this.old[i+d]+=this.p[i+d]-prior[d];}
    }
    this.pin();
  }
  updatePose(dt) {
    let goal=this.settings.gait==='walk'?0:1;
    if(this.settings.gait==='transition')goal=(this.time%12>=4&&this.time%12<9)?1:0;
    const change=Math.max(-dt*2,Math.min(dt*2,goal-this.blend));this.blend+=change;
    const b=this.blend,w=this.data.walk,r=this.data.run;
    // Blend cycle frequency, with speed from each clip's authored root travel.
    const frequency=((1-b)/w.duration+b/r.duration)*this.settings.rate;
    this.phase=(this.phase+dt*frequency)%1;
    this.speed=((1-b)*w.speed+b*r.speed)*this.settings.rate;
    this.travel+=dt*this.speed;
    for(let j=0;j<this.pose.length;j++){
      const sample=clip=>{const f=this.phase*(clip.frames.length-1),i=Math.floor(f),t=f-i;return clip.frames[i][j]*(1-t)+clip.frames[Math.min(i+1,clip.frames.length-1)][j]*t;};
      this.pose[j]=sample(w)*(1-b)+sample(r)*b;
    }
    const a=this.point('left-shoulder'),z=this.point('right-shoulder'),chest=this.point('chest'),hips=this.point('hips');
    const dx=chest[0]-hips[0],dy=chest[1]-hips[1],len=Math.hypot(dx,dy);
    this.up=[dx/len,dy/len,0];
    const lateral=[z[0]-a[0],z[1]-a[1],z[2]-a[2]];
    const forward=[lateral[2]*this.up[1],-lateral[2]*this.up[0],lateral[1]*this.up[0]-lateral[0]*this.up[1]];
    const fl=Math.hypot(...forward);this.forward=forward.map(x=>x/fl);
    this.lateral=[this.up[1]*this.forward[2]*-1,this.up[0]*this.forward[2],this.up[1]*this.forward[0]-this.up[0]*this.forward[1]];
    this.neck=a.map((x,d)=>(x+z[d])*.5+this.up[d]*.16);
    for(let u=0;u<=this.cols;u++){
      const p=this.pattern(u/this.cols,0);this.attachments.set(p,u*3);
    }
  }
  pattern(u,v){
    // A 225-degree flared collar wraps beyond both shoulders toward the chest.
    // Only the neckline is pinned. The shoulder mantle and everything below it are free.
    const angle=(u-.5)*Math.PI*1.25,radius=.13+.25*Math.sin(Math.min(v*this.settings.length/.35,1)*Math.PI/2)+.12*v;
    return this.neck.map((x,d)=>x+this.lateral[d]*Math.sin(angle)*radius-this.forward[d]*(Math.cos(angle)*radius+.22*v)-this.up[d]*this.settings.length*v);
  }
  resetCloth(){
    const c=this.cols;this.constraints=[];
    for(let v=0;v<=this.rows;v++)for(let u=0;u<=c;u++){
      const f=v/this.rows,i=(v*(c+1)+u)*3;this.p.set(this.pattern(u/c,f),i);
    }
    this.old.set(this.p);this.previousSpeed=this.speed;
    const add=(a,b,type)=>{const i=a*3,j=b*3;this.constraints.push({a,b,type,length:Math.hypot(this.p[i]-this.p[j],this.p[i+1]-this.p[j+1],this.p[i+2]-this.p[j+2]),lambda:0});};
    for(let v=0;v<=this.rows;v++)for(let u=0;u<=c;u++){const a=v*(c+1)+u;if(u<c)add(a,a+1,0);if(v<this.rows)add(a,a+c+1,0);if(u<c&&v<this.rows){add(a,a+c+2,1);add(a+1,a+c+1,1);}if(u<c-1)add(a,a+2,2);if(v<this.rows-1)add(a,a+2*(c+1),2);}
    this.pin();
  }
  pin(){for(let i=0;i<this.attachments.length;i++)this.p[i]=this.attachments[i];}
  colliders(){
    const chest=this.point('chest'),torsoTop=chest.map((x,d)=>x-this.up[d]*.15);
    this.shoulderCapsules=['left','right'].map(side=>{const shoulder=this.point(side+'-shoulder');return [shoulder,shoulder,.115];});
    this.armCapsules=[];
    if(this.settings.arms)for(const side of ['left','right']){
      this.armCapsules.push([this.point(side+'-shoulder'),this.point(side+'-elbow'),.065]);
      this.armCapsules.push([this.point(side+'-elbow'),this.point(side+'-hand'),.05]);
    }
    this.capsules=[[this.point('hips'),torsoTop,.19],[this.point('head'),this.point('head'),.305],...this.shoulderCapsules,...this.armCapsules];
    for(const side of ['left','right'])for(const [a,b] of [['hip','knee'],['knee','foot']])this.capsules.push([this.point(side+'-'+a),this.point(side+'-'+b),.065]);
  }
  project(array,i,margin=0,capsules=this.capsules){
    if(this.settings.collision)for(const [a,b,r] of capsules){
      const x=b[0]-a[0],y=b[1]-a[1],z=b[2]-a[2],l=x*x+y*y+z*z;
      const t=l?Math.max(0,Math.min(1,((array[i]-a[0])*x+(array[i+1]-a[1])*y+(array[i+2]-a[2])*z)/l)):0;
      const cx=a[0]+x*t,cy=a[1]+y*t,cz=a[2]+z*t;
      let nx=array[i]-cx,ny=array[i+1]-cy,nz=array[i+2]-cz,d=Math.hypot(nx,ny,nz);
      if(d<r+margin){if(d<1e-9){nx=-1;ny=nz=0;d=1;}const k=(r+margin)/d;array[i]=cx+nx*k;array[i+1]=cy+ny*k;array[i+2]=cz+nz*k;}
    }
    array[i+1]=Math.max(.028,array[i+1]);
  }
  armEdges(){
    if(!this.settings.collision||!this.settings.arms)return;
    const p=this.p,c=this.cols+1,point=[0,0,0];
    // Extra edge samples help coarse meshes meet thin, moving arms between particles.
    for(const q of this.constraints){if(q.type===2)continue;const i=q.a*3,j=q.b*3,wa=q.a<c?0:1,wb=q.b<c?0:1;if(!wa&&!wb)continue;
      for(const t of [.25,.5,.75]){const s=1-t,w=wa*s*s+wb*t*t;
        for(let d=0;d<3;d++)point[d]=p[i+d]*s+p[j+d]*t;
        const before=point.slice();this.project(point,0,.022,this.armCapsules);
        for(let d=0;d<3;d++){const correction=(point[d]-before[d])/w;p[i+d]+=wa*s*correction;p[j+d]+=wb*t*correction;}
      }
    }
  }
  step(){
    const dt=this.dt,p=this.p,old=this.old,c=this.cols+1;this.time+=dt;this.updatePose(dt);this.colliders();
    const acceleration=(this.speed-this.previousSpeed)/dt;this.previousSpeed=this.speed;
    const air=this.settings.air?-(this.speed+this.settings.headwind):0;
    for(let a=c;a<p.length/3;a++){
      const i=a*3,v=Math.floor(a/c)/this.rows,vx=(p[i]-old[i])/dt,vy=(p[i+1]-old[i+1])/dt,vz=(p[i+2]-old[i+2])/dt;
      const rx=air-vx,ry=-vy,rz=-vz,flow=Math.min(8,Math.hypot(rx,ry,rz));
      const flutter=this.settings.air?.48*Math.abs(air)*Math.sin(this.time*9-v*5+a%c*.38)*v:0;
      const ax=2.2*flow*rx-acceleration,ay=-8+2.2*flow*ry,az=2.2*flow*rz+flutter;
      for(let d=0;d<3;d++){const prev=p[i+d];p[i+d]+=(p[i+d]-old[i+d])*.998+[ax,ay,az][d]*dt*dt;old[i+d]=prev;}
    }
    this.pin();for(const q of this.constraints)q.lambda=0;
    const bend=Math.pow(10,-3-this.settings.bend/100*3);
    for(let iter=0;iter<8;iter++){
      for(const q of this.constraints){const i=q.a*3,j=q.b*3,wa=q.a<c?0:1,wb=q.b<c?0:1,w=wa+wb;if(!w)continue;
        const x=p[i]-p[j],y=p[i+1]-p[j+1],z=p[i+2]-p[j+2],l=Math.hypot(x,y,z);if(l<1e-8)continue;
        const alpha=(q.type===2?bend:q.type===1?3e-7:5e-8)/(dt*dt),dl=(-(l-q.length)-alpha*q.lambda)/(w+alpha);q.lambda+=dl;const k=dl/l;
        p[i]+=wa*x*k;p[i+1]+=wa*y*k;p[i+2]+=wa*z*k;p[j]-=wb*x*k;p[j+1]-=wb*y*k;p[j+2]-=wb*z*k;
      }
      if(iter>=6)this.armEdges();
      for(let a=c;a<p.length/3;a++)this.project(p,a*3,.025);
      this.pin();
    }
  }
}
if(typeof module!=='undefined')module.exports={PuppetCapeSimulation};
