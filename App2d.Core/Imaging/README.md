# CPU images

`Image2D<T>` is width, height, and one row-major array of unmanaged pixel values.
It has no cell size, origin, world bounds, loading, rendering, or pixel objects.
Use `float` or `double` for scalar processing, `Vector3`/`Vector4` for float channels,
and `bool` for masks and skeletons. Values are neither quantized nor implicitly clamped.

```csharp
using App2d.Core.Imaging;
using App2d.Core.Rendering.Textures;

using var texture = Texture2D.Load("silhouette.png");
var colors = texture.CopyImage();
var channel = colors.Map(static color => color.A / 255f);
channel.Transform(static value => MathF.Pow(Math.Clamp(value * 1.2f, 0f, 1f), 2f));
var mask = channel.Map(static value => value >= .5f);
var skeleton = mask.Skeletonize();
```

The example uses alpha for a transparent silhouette. For dark artwork on an opaque
white background, choose a different map, for example
`color => 1f - (color.R + color.G + color.B) / (3f * 255f)`.
The color-to-channel function, arithmetic, and threshold are explicit choices.

`image[x, y]` returns a reference, so arithmetic and edits to struct fields work
directly. `AsSpan()` and `GetRowSpan(y)` access the same storage. `Map` creates a new
image; `Transform` edits values in place. The delegate is called once per pixel
without allocating a pixel object. Span loops are available for custom hot paths.
`Clone`, the span constructor, and `Texture2D.CopyImage` copy their pixels, so CPU
edits do not invalidate a renderer's cached texture. `Image2D<bool>` uses one byte
per pixel, as a .NET `bool[]`; it does not pack bits.

Coordinates start at the top left, with X rightward and Y downward. Both coordinates
are bounds checked independently. `GetValueOrDefault(x, y, outside)` samples a
neighbor with an explicit outside value; its default is zero/false. No sampling
operation implicitly wraps coordinates. Empty dimensions are supported, and
oversized pixel counts are rejected before allocation.

`Skeletonize` returns a new binary image after Guo-Hall thinning reaches a fixed
point. It treats true pixels as foreground and the outside as background, including
when the shape touches an image edge. Removal decisions are applied simultaneously
within each of the two phases. It allocates an output array and one reusable removal
array, with no per-pixel or per-iteration heap allocations. See the
[Guo-Hall paper](https://doi.org/10.1145/62065.62074) and the
[scikit-image thinning documentation](https://scikit-image.org/docs/stable/api/skimage.morphology.html#skimage.morphology.thin).

Pixel-to-meter transforms, rasterization, joint/branch identification, curve fitting,
and animation can consume these arrays separately. The binary skeleton is a raster
centerline; it does not identify anatomical joints. Resolution, thresholding, and
silhouette noise affect the branches it produces. No `IPixel` contract is needed
for this pipeline; pixel types already supply their own arithmetic where applicable.
