using App2d.Core;
using App2d.Core.Characters.Authored;
using App2d.Core.Geometry;
using App2d.Gameplay.Audio;
using App2d.Gameplay.Combat;
using App2d.Gameplay.Enemies;
using App2d.Gameplay.Persons;
using App2d.Gameplay.Simulation;
using App2d.Gameplay.World;
using App2d.Gameplay.World.Presentation;
using App2d.Levels;
using App2d.Rendering;
using App2d.Rendering.Textures;
using App2d.Tiles;
using Microsoft.Xna.Framework.Graphics;
using System.Numerics;
using Color = Microsoft.Xna.Framework.Color;

namespace App2d.Diagnostics;

/// <summary>Real simulation and renderer, exported at 60 fps for an effects-on/off contact comparison.</summary>
internal static class CombatContactStudy2D
{
    public static void Run(GraphicsDevice device, TextureCache2D textures, string directory, bool timingStudy = false)
    {
        var authored = AuthoredCatalog.Load(Path.Combine(AssetPaths.Characters, "authored"));
        if (authored.Errors.Count > 0) throw new InvalidDataException(string.Join(Environment.NewLine, authored.Errors));
        var traversal = TraversalMetricsLoader2D.Load(textures.ContentRoot);
        var report = new List<string>();
        var camera = new Camera2D { Zoom = 1.7f, Position = new(-255, 52) };
        using var renderer = new Renderer2D(camera, device);
        using var target = new RenderTarget2D(device, 640, 240, false, SurfaceFormat.Color, DepthFormat.Depth24, 4, RenderTargetUsage.DiscardContents);
        foreach (var name in timingStudy ? new[] { "hit", "kill", "run", "reverse" } : new[] { "miss", "hit", "kill", "run", "reverse" })
        {
            var map = new EditableTileMap2D(640, 96, 32, 32, SideScrollerLevel2D.WorldOrigin, ["dark-cave"]);
            for (var x = 0; x < 640; x++) map.SetTileKind(x, 19, TileKind2D.Solid);
            using var game = SideScrollerSimulation2D.Create(new(traversal, map, [],
                [new(1, WorldThingKind2D.PlayerSpawn, null, true, new(-368, 40)),
                 new(2, WorldThingKind2D.Shieldback, null, true, new(name == "miss" ? -175 : -326, 42))])
                { AuthoredCharacters = authored, PlayerMaximumHealth = 30 });
            if (name == "kill")
            {
                var enemy = game.Level.EnemySystem.Combatants[0];
                enemy.Health.Damage(enemy.Health.Current - 2);
            }
            var scene = new Scene2D();
            using var player = new AuthoredPersonPresentation2D(scene, PersonMoves.From(authored), traversal);
            using var heldPlayer = new AuthoredPersonPresentation2D(scene, player.Director.Moves, traversal) { Enabled = false };
            using var exaggeratedPlayer = new AuthoredPersonPresentation2D(scene, player.Director.Moves, traversal) { Enabled = false };
            var history = new PersonFrameHistory2D();
            using var enemies = new EnemyPresentation2D(scene, textures, traversal, new Silent());
            using var contacts = new CombatContactPresentation2D(scene);
            var timing = HitstopTimingStudy2D.CreateVariants();
            var ground = new WorldObject2D(AxisAlignedRectangle2D.FromSize(new(5000, 2)), new SolidColorShader(new Color(70, 88, 93)));
            ground.Transform.Position = new(-250, 0); scene.Add(ground);
            var hits = 0; var firstHit = -1L;
            for (var step = 0; step < 180; step++)
            {
                var tick = game.Session.Tick + 1;
                var movement = name == "run" ? 1 : name == "reverse" && firstHit >= 0 ? -1 : 0;
                var frame = game.Session.Advance(new PlayerInput2D(game.Player.Id, tick, tick,
                    new PersonCommand2D { PrimaryHeld = step == 12, MoveX = movement }));
                if (timingStudy) foreach (var variant in timing) variant.Observe(frame);
                contacts.Advance(1 / 120f);
                foreach (var e in frame.Events.OfType<CombatDamageOccurred2D>())
                {
                    if (e.Damage.Contact?.Kind != CombatImpactKind2D.Sword) continue;
                    if (e.Damage.Contact.Value.AttackerId != game.Player.Id)
                        throw new InvalidOperationException("Sword contact must identify its player, separately from its weapon.");
                    hits++; firstHit = firstHit < 0 ? tick : firstHit;
                    contacts.Present(e.Damage);
                    if (timingStudy) heldPlayer.PresentContact(e.Damage, game.Player.Id, tick);
                    report.Add($"{name}: tick {tick}, target {e.Damage.TargetId}, killed {e.Damage.WasKilled}, contact {e.Damage.Contact.Value.Position}");
                }
                var state = frame.Players[0];
                camera.Position = new(name is "run" or "reverse" ? state.Person.Position.X + 113 : -255, 52);
                player.Equip(state.Equipment);
                player.ApplyState(state.Person, frame.Tick, state.MoveX, false, state.IsMeleeAttackActive);
                if (timingStudy)
                {
                    var live = player.Director.Frame();
                    var time = frame.Tick / 120.0;
                    history.Record(time, live);
                    heldPlayer.Equip(state.Equipment); exaggeratedPlayer.Equip(state.Equipment);
                    heldPlayer.ApplyState(state.Person, frame.Tick, state.MoveX, false, state.IsMeleeAttackActive);
                    exaggeratedPlayer.ApplyState(state.Person, frame.Tick, state.MoveX, false, state.IsMeleeAttackActive, timing[3].SamplePlayer(history, time, live));
                }
                enemies.ApplyState(frame.Enemies, frame.Events.OfType<EnemyOccurred2D>().Select(e => e.Occurrence), frame.Tick);
                if (step % 2 != 0) continue;
                foreach (var variant in timingStudy ? Enumerable.Range(0, timing.Length) : Enumerable.Range(0, 2))
                {
                    var effects = timingStudy || variant == 0;
                    player.Enabled = !timingStudy || variant < 2;
                    heldPlayer.Enabled = timingStudy && variant == 2;
                    exaggeratedPlayer.Enabled = timingStudy && variant == 3;
                    var label = timingStudy ? timing[variant].Name : effects ? "on" : "off";
                    if (timingStudy) enemies.ApplyState(timing[variant].Sample(frame), [], frame.Tick);
                    contacts.Enabled = effects; contacts.Advance(0);
                    device.SetRenderTarget(target);
                    renderer.BeginFrame(640, 240, default); renderer.Clear(new Color(145, 176, 190)); renderer.Draw(scene);
                    renderer.EndFrame(); device.SetRenderTarget(null);
                    using var stream = File.Create(Path.Combine(directory, $"{name}-{label}-{step / 2:000}.png"));
                    target.SaveAsPng(stream, 640, 240);
                }
                contacts.Enabled = true;
            }
            if (hits != (name == "miss" ? 0 : 1)) throw new InvalidOperationException($"{name}: expected {(name == "miss" ? 0 : 1)} confirmed hit, got {hits}.");
            if (name == "run" && game.Player.Position.X <= -368 || name == "reverse" && (game.Player.Position.X >= -368 || game.Player.Facing != -1))
                throw new InvalidOperationException($"{name}: movement failed to continue through contact.");
            report.Add($"{name}: final player X {game.Player.Position.X:0.00}, facing {game.Player.Facing}");
        }
        File.WriteAllLines(Path.Combine(directory, "contact-report.txt"), report);
        File.WriteAllText(Path.Combine(directory, "index.html"), timingStudy ? HitstopTimingStudy2D.Page : Page);
    }

    private sealed class Silent : ISoundEffectSink2D { public void Play(SoundEffect2D effect) { } }

    private const string Page = """
        <!doctype html><html lang="en"><meta charset="utf-8"><title>Combat contact study</title>
        <style>body{margin:28px auto;max-width:1320px;padding:0 20px;background:#14191f;color:#e5eaf0;font:16px system-ui}h1{font-size:26px}p{color:#aab7c6}button,select{font:inherit;padding:7px;background:#293540;color:inherit;border:1px solid #526171;border-radius:4px}label{margin:0 18px}input[type=range]{width:320px;vertical-align:middle}main{display:grid;grid-template-columns:repeat(auto-fit,minmax(400px,1fr));gap:16px}img{width:100%;display:block}figure{margin:0}figcaption{padding:8px;background:#222c36}.controls{position:sticky;top:0;padding:14px 0;background:#14191f}small{color:#aab7c6}</style>
        <h1>Combat contact — first pass</h1><p>Frames from the actual game simulation and renderer. Same opening sword attack; the target supplies the impact. Silent preview.</p>
        <div class="controls"><button id="play">Pause</button><label>Speed <select id="speed"><option value="1">1×</option><option value=".25">¼×</option></select></label><label><input id="effects" type="checkbox" checked> Impact burst</label><input id="frame" type="range" min="0" max="89" value="0"><small id="time"></small></div>
        <main id="grid"></main><p>Ordinary hit: 115 ms burst. Kill: 160 ms burst and recoil into collapse. Player controls and stagger duration remain unchanged. Run and reverse check movement through contact.</p>
        <script>
        const names=['miss','hit','kill','run','reverse'],grid=document.querySelector('#grid'),slider=document.querySelector('#frame'),effects=document.querySelector('#effects'),play=document.querySelector('#play'),speed=document.querySelector('#speed');
        const images=names.map(n=>{let f=document.createElement('figure');f.innerHTML=`<img alt="${n}"><figcaption>${n[0].toUpperCase()+n.slice(1)}</figcaption>`;grid.append(f);return f.querySelector('img')});
        let elapsed=0,running=true,last=performance.now(),shown='';
        function show(){const frame=Math.min(89,Math.floor(elapsed*60)),suffix=`${effects.checked?'on':'off'}-${String(frame).padStart(3,'0')}`;if(suffix===shown)return;shown=suffix;images.forEach((img,i)=>img.src=`${names[i]}-${suffix}.png`);slider.value=frame;document.querySelector('#time').textContent=`${(frame/60).toFixed(3)} s`;}
        play.onclick=()=>{running=!running;play.textContent=running?'Pause':'Play'};
        slider.oninput=()=>{running=false;play.textContent='Play';elapsed=Number(slider.value)/60;show()};effects.onchange=show;
        function loop(now){if(running)elapsed=(elapsed+Math.min(.05,(now-last)/1000)*Number(speed.value))%1.5;last=now;show();requestAnimationFrame(loop)}requestAnimationFrame(loop);
        </script></html>
        """;
}
