# Resource registry

`ResourceManager2D` keeps one named definition for each shared resource. A definition
has a key, a loader, and an optional source path. `Load<T>` preloads it;
`GetLoad<T>` loads on first access and returns it; `Get<T>` returns only an already
loaded value. `Remove` drops one definition and `Clear` drops all of them. Owned
`IDisposable` values are disposed when removed or cleared.

```csharp
using var resources = new ResourceManager2D();
resources.Register("level/cavern", () => LoadMap("cavern"), "levels/cavern/level.db");
var map = resources.GetLoad<MyMap>("level/cavern", owner: "simulation");
var sameMap = resources.Get<MyMap>("level/cavern", owner: "editor");
var report = resources.Snapshot(); // keys, types, sources, loaded state, owners
```

Owner labels are for inspection. `ForgetOwner` removes a label when a scene or
chunk stops using the value; it does not evict resources. Register borrowed values
with `ownsResource: false`. The registry runs on the calling thread and does not
coordinate concurrent loads.

The side-scroller registers its level, authored character catalog, sound bank,
soundtrack, and texture cache. The developer console's `resources` command lists
their sources and consumers; `resources audio` filters by key. The catalog holds
shared character definitions. Each actor still creates its own mutable shader
instance from those definitions.
