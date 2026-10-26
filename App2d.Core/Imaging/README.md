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

## Edges and reduced palettes

An image derivative measures change across neighboring pixels. `Sobel` returns
`Image2D<Vector2>` containing `(dI/dx, dI/dy)` for a scalar image. Its 3-by-3 kernels
are divided by eight, so an interior linear ramp rising by one per pixel has a
derivative of one. Y points downward. Sampling clamps to the nearest border pixel,
so a constant image does not acquire a false frame. The source is unchanged.

For three channels, `SobelMagnitude` combines the squared X and Y derivatives of
all channels before taking the square root. This detects color changes that a
grayscale conversion could hide. Its units depend on the chosen channel space.
Neither operation implicitly blurs, thresholds, or thins the result. A thresholded
Sobel response can contain thick edges and texture; it is not an ordered contour.
[Canny](https://docs.opencv.org/4.x/da/d22/tutorial_py_canny.html) adds noise reduction,
non-maximum suppression, and connected weak/strong thresholding.

`GaussianBlur(sigma)` now supplies separable scalar smoothing with clamped borders
and a kernel truncated at three sigma. Sigma is in pixels, in [0, 20]; zero copies.
`CannyEdges(lowThreshold, highThreshold, sigma)` smooths a finite scalar image, uses
the normalized Sobel derivative, suppresses non-maxima by bilinear sampling along the
gradient, and follows eight-connected weak edges from strong seeds. It returns a new
binary image. Cutoffs use **Sobel /8 units**, not byte-valued OpenCV thresholds or a
fraction of the image's maximum response. `HysteresisThreshold(low, high)` exposes the
last stage separately for finite nonnegative strengths; zeros remain background even
with zero cutoffs. Strong must be at least weak. Weak chains without a strong seed
are discarded. None of these operations repairs real gaps or decides which edges
belong to an object rather than its texture.

`CannyEdgeStrengths(sigma)` exposes the smoothing/gradient/ridge preparation as a
scalar image. Reuse it with different `HysteresisThreshold` cutoffs; recompute it
after changing the source pixels or sigma. Core does not cache mutable `Image2D`
inputs implicitly. The Gaussian passes process neighboring float pixels in SIMD
blocks where hardware acceleration is available, with scalar border/tail handling.

`SrgbToHsv` returns normalized `(hue, saturation, value)` with hue in turns [0, 1),
and hue zero for achromatic colors. Hue wraps and becomes unreliable near gray;
its raw scalar gradient is not a perceptual color difference. See the
[HSV conversion equations](https://docs.opencv.org/4.x/de/d25/imgproc_color_conversions.html).

`QuantizePalette` runs deterministic k-means over `Vector3` pixels and returns
`(Palette, Indices)`. It starts with farthest-point seeds and alternates nearest
color assignment with averaging, using Euclidean distance in the supplied space.
The palette has **at most** the requested count; an image with fewer distinct colors
does not invent extra colors, and unused centers are removed. Each index refers to
the returned palette. Empty images keep their dimensions and have an empty palette.
Counts and the iteration limit must be positive, and input channels must be finite.
It uses every pixel, with O(pixels × colors × iterations) work and O(pixels + colors)
temporary storage. It is a straightforward CPU baseline for experiments.

For colors, prefer a perceptual space such as [Oklab](https://bottosson.github.io/posts/oklab/)
over distances in encoded sRGB. Conversion is explicit. `SrgbToOklab` takes normalized
sRGB in [0, 1], including transfer decoding; `OklabToSrgb` includes transfer encoding
and can return out-of-gamut values. Clamp when preparing display colors, not silently
during processing.

```csharp
using System.Numerics;
using App2d.Core.Imaging;

// colors is Image2D<Color4b>, for example from Texture2D.CopyImage().
var rgb = colors.Map(static c => new Vector3(c.R, c.G, c.B) / 255f);
var lab = rgb.Map(ColorSpaces2D.SrgbToOklab);
var edgeStrength = lab.SobelMagnitude();
var edges = edgeStrength.Map(static strength => strength >= .08f); // Tune to the image.

var (palette, indices) = lab.QuantizePalette(colorCount: 8);
var reducedRgb = indices.Map(index => ColorSpaces2D.OklabToSrgb(palette[index]));
var firstColorMask = indices.Map(static index => index == 0);
```

Alpha is a separate choice: composite onto a chosen background or extract only
foreground samples before quantizing, then preserve/map alpha separately. Hidden
RGB values in transparent pixels should not influence a foreground palette.
Dithering is deliberately not part of quantization; it creates small patterns that
are undesirable when the next step is tracing color regions.

For filled vector artwork, a useful next pipeline is **palette indices → connected
regions → boundary loops (including holes) → simplified paths → fitted Béziers**,
with each region retaining its palette index. Quantization alone does not merge
small islands or turn texture into coherent regions. Trace shared boundaries once
when fitting adjoining regions so independent fits do not create cracks.
For stroke centerlines, threshold a foreground mask and use `Skeletonize` instead.
These are separate inputs to later curve-fitting work. Boundary tracing is described
below; simplification and Bézier fitting are not implemented yet. See the
[k-means tutorial](https://docs.opencv.org/4.x/d1/d5c/tutorial_py_kmeans_opencv.html),
[Potrace algorithm paper](https://potrace.sourceforge.net/potrace.pdf) (binary outline tracing),
and [Graphics Gems FitCurves](https://github.com/erich666/GraphicsGems/blob/master/gems/FitCurves.c)
(fitting cubic Béziers to ordered points).

## Pixel groups, cleanup, and ordered boundaries

`LabelComponents` groups foreground pixels by eight-connectivity (including diagonals)
or four-connectivity. It returns an integer label image and `PixelCounts`, indexed by
label ID. Zero is background, including its pixel count; positive IDs are discovered
in row-major order. Empty images return one background count of zero. Labeling is
linear in pixel count and does not edit the source.

```csharp
var groups = edges.LabelComponents(eightConnected: true);
var cleaned = groups.Labels.Map(id => id != 0 && groups.PixelCounts[id] >= 100);
// A UI can instead keep a set of selected IDs and map membership to a new mask.
```

This removes small disconnected noise. Texture attached to the main edge has the
same ID and survives size filtering. A manual cut followed by relabeling can separate
it; with eight-connectivity, the cut must also break diagonal contacts. A connected
group may have branches, loops, and multiple boundaries, so its discovery order is
not a drawable path. IDs describe the current mask and should be rebuilt after edits
or threshold changes rather than reused as persistent object identities.

`TraceBoundaryLoops` converts a binary region into ordered `Vector2i[]` paths on
integer **pixel corners**, with image-border edges included. A foreground pixel at
`(x, y)` covers `[x, x+1] × [y, y+1]`. Each path is implicitly closed without repeating
the starting point. Each exposed cell edge appears once; no simplification or smoothing
is applied. In Y-down coordinates, the signed shoelace area is positive for an outer
loop and negative for a hole. The tracer keeps foreground on the right and takes a
right turn at ambiguous diagonal contacts, separating corner-touching foreground
cells into paths that can touch at a vertex. An eight-connected selection group can
therefore have several paths. The API does not assign parent/child hierarchy.

```csharp
var colorRegion = indices.Map(static index => index == 0);
var paths = colorRegion.TraceBoundaryLoops(); // Includes boundaries of holes.
```

Tracing an edge image traces the perimeters of its pixel-wide strokes, including both
sides when they enclose background. For a filled object outline, first select/segment
its region, then trace that region. Preserve useful internal lines separately.

`FillHoles` flood-labels four-connected background from the image border and fills
background that the exterior cannot reach. On an edge map with a closed outline,
this can fill the enclosed object even when it contains attached interior texture.
Select the desired filled component, then trace its boundary. This produces an
outer-only result: legitimate holes, such as an enclosed gap between limbs, are filled
too. Keep the original mask/loops when those holes matter. An open boundary can leak;
the operation does not repair gaps. The [SciPy explanation](https://docs.scipy.org/doc/scipy/reference/generated/scipy.ndimage.binary_fill_holes.html)
describes the exterior-flood principle, and the [OpenCV contour hierarchy tutorial](https://docs.opencv.org/4.x/d9/d8b/tutorial_py_contours_hierarchy.html)
explains selecting outer boundaries and their nested holes.
