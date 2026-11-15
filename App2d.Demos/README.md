# App2d Demos

Run `dotnet run --project App2d.Demos -c Release` from the repository root. Use the demo
buttons or **Next demo** to switch.

- **Conic pencil**. Drag the four points and sweep the Ratio slider. The two basis buttons show
  the line pairs used to build the conic.
- **Hex wheel**. Drag a tile to stretch the ring. The motor turns a dynamic hub connected to the
  inner tiles. The ground is a half-space and the tiles use the regular hexagon polygon.
  Use **Retile and reset** to apply a new tile radius, hole radius, or drop height.

The wheel starts from lattice centers inside an annulus. Each shared edge has two soft point
joints with coincident anchors in the rest pose. The world applies gravity, resolves polygon
contacts, and integrates rigid-body motion at 120 Hz. The spring controls act on the edge joints
and the links to the hub.
