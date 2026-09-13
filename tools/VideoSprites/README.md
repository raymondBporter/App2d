# Short video to cartoon sprite poses

This experiment uses a reference sprite and Wan 2.2 TI2V 5B to generate candidate
motion. Select a few strong poses, remove the green background, and assign each
pose its own game timing. No puppet layers or LoRA training are required.

The model creates RGB video, **not native transparent animation**. Background
removal here is an approximate chroma key for Gurgle's red/white/black palette.
Use a different key or segmentation method for characters containing green.

## Setup

Reuse the isolated environment and native ComfyUI backend installed by
`tools/QwenLayered/install.py`. From the repository root in PowerShell:

```powershell
& .\Assets\Work\qwen-layered\.venv\Scripts\python.exe tools/VideoSprites/install_models.py
```

This adds approximately 18.15 GB of official model files. The installer pins the
Hugging Face revision and checks every file's SHA-256. Models live alongside the
Qwen files; they are loaded only when their workflow runs. All weights, caches,
and generated outputs are under ignored `Assets/Work` directories.

## Generate once

```powershell
& .\Assets\Work\qwen-layered\.venv\Scripts\python.exe tools/VideoSprites/generate.py Assets/Library/characters/gurgle/transparent-reference-test/frames/frame-0001.png --size 512 --frames 49 --steps 20
```

The process starts an owned, hidden backend on a temporary loopback port and
stops it on completion or failure. The default limit is 20 minutes. It saves
the input, exact prompt, seed, API workflow, backend log, raw PNG frames,
`raw-video.mp4`, `all-frames.png`, and timing report in a timestamped run folder.
The 49-frame clip plays at 24 fps. Supported lengths are `4n+1` frames.

Review `all-frames.png` for anticipation, action, and recovery poses. Reject
frames with changing anatomy, unreadable silhouettes, or unintended camera
motion. The initial prompt is a lunge request, not a guarantee of that action.

## Select and retime without inference

```powershell
& .\Assets\Work\qwen-layered\.venv\Scripts\python.exe tools/VideoSprites/extract.py Assets/Work/video-sprites/runs/RUN_DIRECTORY --frames 0,10,20,30,48 --durations 120,70,60,100,150
```

The indices above are examples; use the zero-based labels on the contact sheet
to select meaningful poses. The example timing totals 500 ms. Re-extracting
takes seconds and does not load any models. Use `--name another-study` for a
second selection without overwriting the first.

Outputs include full-alpha PNG frames, a horizontal sprite sheet, GIF previews
on dark and light backgrounds, a pose contact sheet, and `animation.json` with
per-pose timings. The JSON is experiment metadata, not an App2d runtime asset
manifest. Every frame retains its original canvas and registration: automatic
per-frame centering would erase deliberate motion or introduce foot sliding.

The GIF previews loop for comparison; this does not establish a seamless idle
loop. A one-shot attack can have different final and initial poses. In a game,
set the hit event/hitbox independently, at the chosen contact pose.

## Why this pipeline

For three to five expressive frames, a video model can propose deformations
that are awkward with separated cutouts. It can also change details or invent
an unsuitable motion. Judge the extracted poses at actual game size before
spending time on more generation. Try a small number of candidates before
considering a different model or cloud service; LoRA training is not the first
step in resolving motion/timing problems.

## Palette cleanup without inference

```powershell
& .\Assets\Work\qwen-layered\.venv\Scripts\python.exe tools/VideoSprites/clean_palette.py Assets/Work/video-sprites/runs/20260905-114850-578874/five-pose-study
```

This Gurgle-specific experiment flattens the same five poses to black, white,
and red. It classifies colors consistently across frames, smooths the coverage
of each color at boundaries, and resizes with premultiplied alpha. Interior
colors become flat; antialiased boundaries intentionally retain intermediate
colors and alpha. Strictly limiting every stored pixel to three RGB values
would reintroduce jagged internal boundaries.

It exports 512, 256, and 128 pixel RGBA frames and sprite sheets, dark/light
GIF previews, and before/after comparisons under `five-pose-study/palette-cleanup`.
The original frames are preserved. The initial cleanup and all exports took
1.10 seconds. The comparison uses equal 256-pixel sizes on both sides and was
visually checked on light and dark backgrounds. GIF is for preview; use the
PNG assets to retain full color/alpha precision.

This removes unwanted shading, but can discard small details or classify a
dark red area too aggressively. It does not correct anatomical drift or recover
the intended shape of an already distorted line. The short spatial smoothing
does not average across time, so it cannot cause temporal motion trails.

## Measured first test (2026-09-05)

Run: `Assets/Work/video-sprites/runs/20260905-114850-578874`.
On the RTX 2000 Ada laptop GPU (8 GB) with 64 GB system RAM, the 512x512,
49-frame, 20-step run took **84.66 seconds** including backend startup and
export. ComfyUI reported 70.60 seconds for the workflow; sampling took about
51 seconds. A sampled GPU reading showed 7,060 MiB in use. These numbers apply
to this particular small test, not to full-resolution or longer clips.

The requested sideways lunge became an upward stretch/roar and recovery. The
silhouette is expressive, but horns and facial details change, and the final
pose does not exactly match the initial pose. This is useful evidence for
the approach, not a finished directional attack or seamless loop.

The `five-pose-study` selection uses source frames `0,6,14,42,48` held for
`120,60,100,70,200` ms: **five frames over 550 ms**. The long, nearly static
middle portion of the generated clip is omitted. The pose sheet was visually
reviewed, and the PNGs have real alpha after green-screen removal. Fine edges
remain approximate. Game integration has not been changed by this experiment.

References:

- [ComfyUI's official Wan 2.2 workflow](https://docs.comfy.org/tutorials/video/wan/wan2_2)
  documents the 5B model with native offloading for 8 GB VRAM.
- [Scenario's sprite-sheet workflow](https://help.scenario.com/articles/9088582240-create-spritesheets-with-scenario)
  also uses generated video as a source of animation frames.

## Locomotion experiments (2026-09-05)

Three further 512x512, 49-frame, 20-step tests were evaluated visually:

| Prompt | Seed | Run | Seconds | Observation |
| --- | --- | --- | --- | --- |
| `gurgle-crawl.txt` | 778 | `20260905-115845-441636` | 79.02 | Mostly static body; appendage motion rather than a coordinated crawl. |
| `gurgle-inchworm.txt` | 779 | `20260905-120049-917043` | 80.14 | Body compresses/extends, but horns deform severely and no cycle completes. |
| `gurgle-hop.txt` | 780 | `20260905-120248-773059` | 81.08 | Invents leg-like anatomy and shrinks the subject. Rejected. |

Runs are under `Assets/Work/video-sprites/runs`. The latter two use
`Assets/Work/video-sprites/inputs/gurgle-crawl-room.png`: the original RGBA image
uniformly reduced to 384x384 and placed at (0,115) on a transparent 512x512
canvas to provide travel/headroom. No individual parts were altered.

The inchworm run's `crawl-study` selects frames `0,12,24,36,48`, held for 120 ms
each (600 ms). Its `palette-cleanup` folder contains the same cleanup/export
variants as the roar experiment. This is a failed locomotion study with a
visible last-to-first discontinuity, not an approved movement asset. The GIF
repeats only for viewing. No registration, shape correction, reverse playback,
or game movement was added to conceal the failed cycle. Raw videos and complete
frame sheets are retained to assess prompt adherence independently of editing.
