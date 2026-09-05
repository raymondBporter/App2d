# Local Qwen layer experiment

This is a script-driven installation of **Qwen-Image-Layered**, the image decomposition
model, using ComfyUI's official workflow and native nodes. It does not install a Qwen
chat model, train a LoRA, generate an animation, or change the game runtime.

The first question is whether one approved cartoon can be separated into useful
transparent pieces. Rigging comes after inspecting that result.

## Installation and storage

From the repository root, with Python 3.12:

```powershell
python tools/QwenLayered/install.py
```

Everything large or machine-specific stays under the ignored
`Assets/Work/qwen-layered/` directory:

- `.venv/`: isolated Python and CUDA-enabled PyTorch dependencies.
- `ComfyUI/`: pinned upstream source and model weights.
- `models.json`: model revisions, download sizes, and upstream SHA256 checksums.
- `installed-packages.txt`: the actual installed package versions.
- `runs/`: dated inputs, parameters, workflow, logs, RGBA layers, and review images.

The three model files total approximately **30.17 GB**. Python, CUDA libraries,
download cache, and working files require additional space. The installer uses Hugging
Face's accelerated downloader after installing dependencies. Downloads can be resumed
by running the installer again, and completed weights are SHA256-verified.
`--http-downloads` selects the slower standard-library fallback if needed.
The installer does not modify global Python packages or the GPU driver.

The checked-in `requirements-windows.lock.txt` and `models.lock.json` record the
tested dependencies and upstream model revisions for subsequent installs.

The target machine has an RTX 2000 Ada 8 GB GPU and 64 GB system RAM. FP8 storage
reduces model size; CPU/disk offloading makes a larger model runnable on a smaller
GPU at the expense of speed. The whole model does not fit in 8 GB VRAM.
GPU compatibility, successful inference, and useful artwork are separate milestones.

## Verified on this machine — 2026-09-05

Both runs completed on the RTX 2000 Ada Laptop GPU (8 GB) with 64 GB system RAM,
ComfyUI 0.34.0, and PyTorch 2.11.0+cu130. All three model files passed SHA256
verification, the dependency check passed, and the backend stopped after each run.

| Test | Settings | Total wall time, including backend startup | Backend execution |
| --- | --- | --- | --- |
| Mechanical smoke test | 256 px, 2 layers, 2 steps, CFG 2.5 | 51.9 seconds | 25.9 seconds |
| Gurgle draft | 640 px, 3 layers, 20 steps, CFG 2.5 | 286.6 seconds | 273.3 seconds |

The draft produced actual RGBA output. The second layer has 74.6% fully transparent
pixels; the third has 50.7%. The first layer is the nearly opaque white background.
The recomposition looks close to the input, but the decomposition mostly separates
red details from the main character. It does **not** provide independent, complete
tongue/eye/tentacle parts suitable for immediate rigging. This is an installation and
decomposition success, not a completed animation asset.

Local results:

- [Draft review](../../Assets/Work/qwen-layered/runs/20260905-113412-824543/review.png)
- [Draft parameters](../../Assets/Work/qwen-layered/runs/20260905-113412-824543/parameters.json)
- [Measured transparency and timing](../../Assets/Work/qwen-layered/runs/20260905-113412-824543/report.json)

These timings are measurements of these settings on this machine, not guarantees
for other images, layer counts, or GPU workloads. Two-step output is not useful for
assessing image quality. A reasonable next experiment is one simpler character with
clearly separated appendages, before investing in a custom rig or training a model.

## Run

```powershell
$qwenPython = '.\Assets\Work\qwen-layered\.venv\Scripts\python.exe'
& $qwenPython tools/QwenLayered/decompose.py `
  Assets/Library/characters/gurgle/transparent-reference-test/frames/frame-0001.png `
  --caption-file tools/QwenLayered/gurgle-caption.txt `
  --size 640 --layers 3 --steps 20
```

The script starts a hidden backend on a temporary loopback port, runs one job, saves
the results, and stops its backend to release resources. No browser is needed.
Third-party custom nodes and cloud API nodes are disabled. Inference runs locally;
model and package installation requires internet access.

The source is preserved. A prepared copy is composited onto white, scaled with its
aspect ratio approximately retained, and rounded to dimensions divisible by 16.
Outputs use a fresh run directory, so prior work is not overwritten.

For a **mechanical smoke test**, use `--size 256 --layers 2 --steps 2`.
That only tests loading, execution, and RGBA export; its images are not a fair quality
test. The official template uses 20 steps and CFG 2.5; the original model recipe uses
50 steps and CFG 4.0. The model's recommended resolution is 640. Increasing layers,
resolution, or steps increases work and potentially memory usage.

The default timeout is 30 minutes. On timeout the script stops its backend and keeps
the log. Use `--timeout SECONDS` deliberately after examining performance, rather than
leaving an unexplained run going overnight. `Ctrl+C` also stops the backend.

## Read the result

- `review.png`: original, individual layers over a checkerboard, and recomposition.
- `layer_*.png`: actual RGBA images, in backend output order.
- `recomposed.png`: layers composited from back to front.
- `report.json`: elapsed time and measured transparency for each layer.
- `workflow-api.json` and `parameters.json`: the exact generation recipe.
- `backend.log`: memory behavior, loading, per-step sampling time, and errors.

A checkerboard is only a preview backdrop. Actual PNG alpha is measured separately.
An opaque background layer is normal; usable foreground layers should contain real
transparency. The first reconstructed-input latent is removed as in the official
workflow; it is not mistakenly counted as a separate layer.

## What makes a useful sprite pipeline

Start with **one character, one view, one short motion**. Change one setting at a time.
The caption describes what is already in the image. Qwen's ordinary decomposition
prompt does not directly assign semantic content to numbered layers.

Before making a rig, judge these things at the actual size the sprite appears in game:

1. **Identity:** does the recomposed image preserve the face, outline, and main colors?
2. **Useful separation:** can a part move without tearing another part with it?
   Mouth, tongue, eyes, and tentacles are useful; arbitrary color regions usually are not.
3. **Overlap:** is there enough hidden artwork behind a moving part to avoid a hole?
4. **Edges:** do white and dark game backgrounds reveal halos or ragged outlines?

For an initial Gurgle puppet, keep the main mouth/body recognizable while adding a
small body squash, alternating tentacle movement, and delayed tongue/eye movement.
Keep a fixed ground anchor. A good loop needs both position and motion to connect at
the seam. Large turns, new viewpoints, and major silhouette changes are harder because
a flat image does not provide the newly revealed artwork.

If automatic separation does not yield useful body parts, do not immediately train a
model or build an elaborate rig editor. First compare a second layer count and one
simpler image. If those still fail, use a whole-image deformation for a minimal idle,
or test the short-video route. The useful benchmark is an acceptable in-game motion
and the manual cleanup it needed, not merely whether the model can produce a PNG.

## Sources

- [Official Qwen model](https://huggingface.co/Qwen/Qwen-Image-Layered)
- [Official ComfyUI workflow](https://github.com/Comfy-Org/workflow_templates/blob/main/templates/image_qwen_image_layered.json)
- [Compressed model and RGBA VAE](https://huggingface.co/Comfy-Org/Qwen-Image-Layered_ComfyUI)
- [Text encoder](https://huggingface.co/Comfy-Org/HunyuanVideo_1.5_repackaged)
- [ComfyUI source](https://github.com/Comfy-Org/ComfyUI)

Qwen-Image-Layered's official model card specifies Apache 2.0. ComfyUI and dependencies
retain their own licenses in the installation. Source-image permissions are separate.
