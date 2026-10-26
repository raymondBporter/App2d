# Image Lab

A small raster workbench using the existing Core imaging operations and shared EditorUi.

```powershell
dotnet run --project App2d.ImageLab
dotnet run --project App2d.ImageLab -- "C:\art\character.png"
```

Load an image (or drop one on the window), choose a transform, and adjust its sliders.
Changes preview without changing the stack. **Apply to stack** commits one step.
Toggle **Preview** to compare with the current applied step, or **Original** to see
the loaded image. Previews run on a background worker after a short slider debounce;
only the latest request can be applied.

Canny previews retain one selected channel and one prepared edge-strength image.
Weak/strong cutoff changes only rerun edge linking, with a 35 ms debounce. Smoothing
changes rebuild the strengths while reusing the channel; changing the image/stage
or selected color channel invalidates the dependent data. Preparation changes keep
the longer 160 ms debounce. The cache holds at most two float images (8 bytes/pixel,
about 5.5 MiB for a 736×983 image), rather than accumulating entries for slider values.
Region cleanup also caches its labels and size ordering for the current source and
connectivity, so changing N or minimum size only filters those labels. The optional
region label cache adds 4 bytes per pixel plus its component counts.

**Undo/Redo** or click any stack entry to move through applied steps. Applying from
an earlier entry discards its redo branch. The original remains available at step 0.
Loading another image starts a fresh stack. History is in memory for this session;
each step retains its pixels. There is no project/recipe format yet.

**Save applied PNG** exports the selected applied step, preserving alpha for color
images. Apply a preview before saving it. Single channels export as clamped grayscale;
masks export as white foreground on black. Float processing retains its precision
between steps and is only clamped/quantized for display or PNG export.

Available transforms: one channel (luminance, alpha, RGB, HSV, Oklab), invert, Sobel or Canny edges,
threshold, Oklab palette reduction, Fill Holes, small-region removal, largest N regions,
raster contours, and skeletonization. Binary operations require an applied mask;
the UI explains when Threshold is needed. Palette reduction uses visible pixels
and preserves the original alpha. RGB channel extraction and color edge finding
composite onto white; choose Alpha to extract transparent-object coverage.

For a transparent character, try **1 channel: Alpha → Threshold → Contours**.
For dark artwork on white, try **1 channel: Luminance → Threshold (Dark foreground)**.
For internal edges, try **Sobel → Threshold**. Fill Holes is optional: it also fills
meaningful enclosed gaps. Contours retain hole boundaries and rasterize the foreground
side of each ordered pixel edge. This version does not save vectors, split body parts,
add joints, or rotate parts.

For weak edges in textured photos, select **Original → Find connected edges (Canny)**.
This combines Gaussian smoothing, interpolated ridge thinning, and two-cutoff edge
linking. **Weak** admits faint continuations of strong edges; **Strong** selects the
starting edges. Lower cutoffs retain more detail and more noise. More smoothing
suppresses small surface texture, but can remove small features too. Threshold units
match Core's Sobel kernels divided by eight; they are not OpenCV's byte thresholds.
For the helmet photograph, luminance, smoothing **2.0**, weak **0.006**, strong **0.025**
is a useful starting point. Real gaps or edges with almost no contrast can still need
manual work. Start from the original: repeated processing cannot recover discarded
grayscale information. See the [Canny overview](https://docs.opencv.org/4.9.0/da/d5c/tutorial_canny_detector.html).

HSV saturation/value and Oklab lightness/a/b can isolate different contrasts.
HSV hue is circular (0 and 1 meet at red) and unstable near gray; scalar Sobel/Canny
on that channel can create false edges at the seam. Oklab a/b add 0.5 for display
without changing their derivatives. Pick these on the original color step, either
through Convert to 1 channel or directly in Canny's Channel selector.

For small repairs, choose **Pixel brush** or **Line** above the canvas. Both are one
pixel wide and edit the selected applied step. The brush joins samples during a drag;
the line previews from the drag's first pixel to its current endpoint. Release to
commit one stroke/line as one undo step, or Escape to discard the draft. Each draft
owns a copy, so earlier steps remain unchanged. Color images, scalar channels, and
binary masks retain their existing type.

Choose a swatch from **Image palette** (up to 24 common displayed colors), or use
**Eyedropper** to sample an actual pixel and return to your brush/line. Sampling retains
RGBA alpha and the original scalar precision. Display overlays never become brush
colors. A mask offers black/background and white/foreground; select white to bridge
an edge gap. Enlarged images use nearest sampling for clear individual pixels;
fitted/downscaled images retain smooth sampling.

**Erase mask** keeps its adjustable radius for broader binary cleanup. **Overlay
original** displays mask pixels over the photo for guidance. The overlay affects the
view only; PNG export keeps the binary mask. Switch back to **View / tools** to keep
transforming. Edits use the same undo/redo branching as transforms.

**Inspect regions** highlights the whole connected component near the pointer in
yellow and shows its ID and pixel count. A small, bounded hit tolerance helps with
one-pixel lines when zoomed out. Other components dim while one is highlighted;
there is no need to distinguish hundreds of palette colors. This view never changes
pixels or adds a history entry. Labels refresh after committing a stroke, not during
each drag sample. IDs belong to the current mask and may change after edits.

**8-neighbor** treats diagonal contacts as connected; turn it off for 4-neighbor edge
contacts only. This setting is shared with the region cleanup transforms. A one-pixel
diagonal line connects under 8-neighbor rules. **Keep largest N regions** defaults to
N=1 and provides a 1–100 slider; ties use stable row-major discovery order. N above
the number of components keeps them all. A useful repair flow is **Canny → Keep
largest N (for example 4) → Apply → Line**, then hover to verify the new connection.

Wheel zooms; arrow keys or middle drag pan. Hover or click the image to use arrows;
hold an arrow to keep moving, or Shift+arrow for three times the distance. Directions
move the image on screen (Right moves it right, Down moves it down), at the same
screen distance at every zoom. Text fields, active controls, popups, and keyboard
widget navigation keep their arrow keys. Panning does not change pixels or add
undo steps. Fit and 100% reset the view. Ctrl+O loads, Ctrl+S saves,
Ctrl+Z undoes, and Ctrl+Y / Ctrl+Shift+Z redo (text fields retain their editing shortcuts).
E selects Erase, B the pixel brush, L Line, I Eyedropper, and V returns to transforms.

Verification:

```powershell
dotnet test App2d.Tests --filter FullyQualifiedName~Imaging
dotnet run --project App2d.ImageLab -- --smoke-ui artifacts/image-lab
```

The smoke command renders the real UI at two sizes with generated sample art,
including palette preview, an applied contour stack, and the empty state. It uses
a hidden Direct3D window and writes only to the requested output directory.
It also checks Canny, the overlay, a committed erase stroke, and its undo/redo.
Repair studies exercise eyedropper-to-line return, one-line history/undo/redo,
component merging, largest-four selection, hover highlighting, and palette swatches.
The navigation study checks all four arrows, held-key repeat, Shift, and the same
screen movement at 100% and 800% zoom without changing image history.
Pass an image path after the output directory to use your own source for the smoke.
`--study-edges image-path output-directory` exports a small set of reproducible
Canny comparisons and their settings without changing the source image.
`--benchmark-edges image-path output-json` measures the stages, full processing, and
cached cutoff processing on the supplied image. Compare the same build configuration;
the JSON records Debug/Release, SIMD availability, cache size, and preparation count.
Repeated timings are the median of five warmed samples; firstPreviewMs is one cold
cache sample. Timings exclude slider debounce and GPU display work.
