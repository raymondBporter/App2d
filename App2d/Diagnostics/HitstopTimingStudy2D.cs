using App2d.Core;
using App2d.Core.Characters.Authored;
using App2d.Gameplay.Combat;
using App2d.Gameplay.Enemies;
using App2d.Gameplay.Persons;
using App2d.Gameplay.Simulation;
using App2d.Gameplay.World.Presentation;
using System.Collections.Immutable;

namespace App2d.Diagnostics;

/// <summary>Opt-in art study: displayed attack/reaction clocks change while simulation stays live. Contact Hold uses the runtime preset.</summary>
internal static class HitstopTimingStudy2D
{
    public static Variant[] CreateVariants() =>
    [
        new("normal", null),
        new("strong-plus", new(0, .125f, 2)),
        new("contact", CombatHitstop2D.Curve, true),
        new("exaggerated", new(0, .025f, 3.5f, .14f), true),
    ];

    internal sealed class Variant(string name, ReactionTimeCurve2D? curve, bool affectPlayer = false)
    {
        public string Name => name;
        private readonly EnemyContactHold2D? _enemies = curve is null ? null : new(curve);
        private readonly PlayerContactHold2D? _player = affectPlayer && curve is not null ? new(curve) : null;

        public PersonFrame SamplePlayer(PersonFrameHistory2D history, double time, PersonFrame live) =>
            _player?.Sample(history, time, live) ?? live;

        public void Observe(SessionFrame2D frame)
        {
            foreach (var fact in frame.Events.OfType<CombatDamageOccurred2D>())
            {
                _enemies?.Present(fact.Damage);
                _player?.Present(fact.Damage, frame.Players[0].Id, frame.Tick / 120.0);
            }
        }

        public ImmutableArray<EnemyState2D> Sample(SessionFrame2D frame) => _enemies?.Sample(frame.Enemies) ?? frame.Enemies;
    }

    public const string Page = """
        <!doctype html><html lang="en"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Contact timing curves</title>
        <style>
        *{box-sizing:border-box}body{margin:24px auto;max-width:1320px;padding:0 20px;background:#14191f;color:#e5eaf0;font:15px system-ui}h1{font-size:26px;margin-bottom:8px}p{color:#aab7c6;line-height:1.5}button,select{font:inherit;padding:7px 10px;background:#293540;color:inherit;border:1px solid #526171;border-radius:5px}label{display:inline-flex;gap:8px;align-items:center}input[type=range]{width:220px}main{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:16px}figure{margin:0;border:1px solid #354250;border-radius:7px;overflow:hidden}canvas{width:100%;display:block;aspect-ratio:8/3}figcaption{padding:10px 14px;background:#222c36}strong{font-size:17px}.details{color:#b3c2d1;font-size:13px;margin-top:4px}.controls{display:flex;flex-wrap:wrap;align-items:center;gap:14px;position:sticky;top:0;padding:12px 0;background:#14191ff5;z-index:2}.chart{width:100%;height:68px;display:block;background:#192129}.curve{fill:none;stroke-width:2.5}.dot{fill:#fff}.axis{stroke:#576776;stroke-dasharray:3 4}small{color:#aab7c6}a{color:#a9ccff}@media(max-width:750px){main{grid-template-columns:1fr}body{padding:0 10px}.controls{position:static}}
        </style>
        <h1>Slow → catch up → normal</h1><p>Watch the small player’s sword as well as the enemy. Contact hold pauses both animations immediately on a confirmed hit; Exaggerated doubles the hold to make it unmistakable. Position, movement, hit detection and the burst keep running. Old Strong+ still affects only the enemy. Full recovery plays by default.</p>
        <div class="controls"><button id="play" disabled>Loading…</button><label>Action <select id="scene"><option value="hit">Ordinary hit</option><option value="kill">Kill</option><option value="run">Run through</option><option value="reverse">Reverse after hit</option></select></label><label>Playback <select id="speed"><option value="1">1×</option><option value=".5">½×</option><option value=".25">¼×</option></select></label><label><input id="focus" type="checkbox">Loop contact only</label><button id="contact">At contact</button><input id="frame" aria-label="Frame" type="range" min="0" max="89" value="0"><small id="time"></small></div>
        <main id="grid"></main><p id="status" role="status">Loading rendered frames…</p><p>The graphs show animation speed over the first 400 ms after impact. The dotted line is normal speed. Every curve repays its delay and returns to the live animation clock. Contact hold freezes the poses for 70 ms; Exaggerated uses 140 ms. Contact Hold is now the game default; the other cards are diagnostic comparisons. Preview is silent; camera shake and controller rumble are not included here. <a href="../index.html">Original contact comparison</a></p>
        <script>
        const presets=[{id:'normal',name:'Normal',min:1,slow:0,peak:1,hold:0,color:'#9aa8b7'}, {id:'contact',name:'Contact hold · game default',min:0,slow:.025,peak:2.5,hold:.07,color:'#7ad8ad'}, {id:'strong-plus',name:'Old Strong+ · enemy only',min:0,slow:.125,peak:2,hold:0,color:'#c0a7ff'}, {id:'exaggerated',name:'Exaggerated · both actors',min:0,slow:.025,peak:3.5,hold:.14,color:'#efc273'}];
        const $=s=>document.querySelector(s),slider=$('#frame'),scene=$('#scene'),speed=$('#speed'),focus=$('#focus'),button=$('#play');
        function recovery(p){return p.hold?(1-p.min)*(p.hold+p.slow/2)*Math.PI/(2*(p.peak-1)):p.slow*(1-p.min)/(p.peak-1)}
        function rate(p,t){let r=recovery(p);if(!p.slow||t<0||t>=p.hold+p.slow+r)return 1;if(p.hold){if(t<=p.hold)return p.min;t-=p.hold;return t<p.slow?1-(1-p.min)*(1+Math.cos(Math.PI*t/p.slow))/2:1+(p.peak-1)*Math.sin(Math.PI*(t-p.slow)/r)}return t<p.slow?1-(1-p.min)*Math.sin(Math.PI*t/p.slow):1+(p.peak-1)*Math.sin(Math.PI*(t-p.slow)/r)}
        const cards=presets.map(p=>{let f=document.createElement('figure'),duration=p.slow?p.hold+p.slow+recovery(p):0;let detail=p.slow?`${p.hold?Math.round(p.hold*1000)+" ms hold · ":""}${p.min.toFixed(2)}× minimum · ${p.peak.toFixed(1)}× catch-up · normal by ${Math.round(duration*1000)} ms`:'Unmodified reaction animation';let points=Array.from({length:121},(_,i)=>`${i*4},${60-rate(p,i/300)*14}`).join(' ');f.innerHTML=`<canvas width="640" height="240" aria-label="${p.name} timing"></canvas><figcaption><strong style="color:${p.color}">${p.name}</strong><div class="details">${detail}</div></figcaption><svg class="chart" viewBox="0 0 480 68" preserveAspectRatio="none" aria-label="Playback speed curve"><line class="axis" x1="0" x2="480" y1="46" y2="46"/><polyline class="curve" stroke="${p.color}" points="${points}"/><circle class="dot" r="3" cx="0" cy="46"/></svg>`;$('#grid').append(f);return{context:f.querySelector('canvas').getContext('2d'),dot:f.querySelector('.dot')}});
        const cache=new Map();let images=null,elapsed=0,running=true,ready=false,last=performance.now(),shown=-1,generation=0;
        async function load(){const token=++generation;ready=false;button.disabled=true;$('#status').textContent='Loading this action…';const name=scene.value;if(!cache.has(name))cache.set(name,Promise.all(presets.map(p=>Promise.all(Array.from({length:90},(_,i)=>new Promise((resolve,reject)=>{let img=new Image();img.onload=()=>resolve(img);img.onerror=()=>reject(new Error('A rendered frame could not be loaded.'));img.src=`${name}-${p.id}-${String(i).padStart(3,'0')}.png`;}))))));try{const next=await cache.get(name);if(token!==generation)return;images=next;ready=true;button.disabled=false;button.textContent=running?'Pause':'Play';shown=-1;show();$('#status').textContent='All four views are synchronized. Start at 1×, then use slow playback or scrubbing to inspect the difference.'}catch(e){if(token===generation)$('#status').textContent=e.message}}
        function show(){if(!ready)return;const frame=Math.min(89,Math.floor(elapsed*60));if(frame===shown)return;shown=frame;cards.forEach((c,i)=>{c.context.drawImage(images[i][frame],0,0);const t=frame/60-.15;c.dot.style.visibility=t>=0&&t<=.40?'visible':'hidden';c.dot.setAttribute('cx',Math.max(0,t)*1200);c.dot.setAttribute('cy',60-rate(presets[i],t)*14)});slider.value=frame;$('#time').textContent=`${(frame/60).toFixed(3)} s${frame===9?' · contact':''}`;}
        button.onclick=()=>{running=!running;button.textContent=running?'Pause':'Play'};slider.oninput=()=>{running=false;button.textContent='Play';elapsed=Number(slider.value)/60;show()};$('#contact').onclick=()=>{running=false;button.textContent='Play';elapsed=.15;show()};scene.onchange=load;focus.onchange=()=>{if(focus.checked)elapsed=0;shown=-1;show()};
        function loop(now){if(running&&ready){const length=focus.checked?.55:1.5;elapsed=(elapsed+Math.min(.05,(now-last)/1000)*Number(speed.value))%length}last=now;show();requestAnimationFrame(loop)}load();requestAnimationFrame(loop);
        </script></html>
        """;
}
