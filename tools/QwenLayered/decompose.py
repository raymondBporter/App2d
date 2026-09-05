"""Run the official Qwen layered workflow through a temporary local ComfyUI backend."""
from __future__ import annotations

import argparse
from datetime import datetime
import hashlib
import json
import os
from pathlib import Path
import shutil
import socket
import subprocess
import sys
import time
import urllib.error
import urllib.request

from install import BACKEND, HOME, PYTHON


def graph(filename, caption, width, height, layers, steps, cfg, seed):
    """Same nodes/connections as Comfy-Org's image_qwen_image_layered template."""
    def node(kind, **inputs):
        return {"class_type": kind, "inputs": inputs}
    return {
        "1": node("LoadImage", image=filename),
        "2": node("UNETLoader", unet_name="qwen_image_layered_fp8mixed.safetensors", weight_dtype="default"),
        "3": node("CLIPLoader", clip_name="qwen_2.5_vl_7b_fp8_scaled.safetensors", type="qwen_image", device="default"),
        "4": node("VAELoader", vae_name="qwen_image_layered_vae.safetensors"),
        "5": node("CLIPTextEncode", clip=["3", 0], text=caption),
        "6": node("CLIPTextEncode", clip=["3", 0], text=""),
        "7": node("VAEEncode", pixels=["1", 0], vae=["4", 0]),
        "8": node("ReferenceLatent", conditioning=["5", 0], latent=["7", 0]),
        "9": node("ReferenceLatent", conditioning=["6", 0], latent=["7", 0]),
        "10": node("EmptyQwenImageLayeredLatentImage", width=width, height=height, layers=layers, batch_size=1),
        "11": node("ModelSamplingAuraFlow", model=["2", 0], shift=1.0),
        "12": node("KSampler", model=["11", 0], positive=["8", 0], negative=["9", 0],
                   latent_image=["10", 0], seed=seed, steps=steps, cfg=cfg,
                   sampler_name="euler", scheduler="simple", denoise=1.0),
        "13": node("LatentCut", samples=["12", 0], dim="t", index=1, amount=layers),
        "14": node("LatentCutToBatch", samples=["13", 0], dim="t", slice_size=1),
        "15": node("VAEDecode", samples=["14", 0], vae=["4", 0]),
        "16": node("SaveImage", images=["15", 0], filename_prefix="layer"),
    }


def api(base, path, data=None):
    body = json.dumps(data).encode() if data is not None else None
    req = urllib.request.Request(base + path, data=body, headers={"Content-Type": "application/json"})
    try:
        with urllib.request.urlopen(req, timeout=10) as response:
            return json.load(response)
    except urllib.error.HTTPError as error:
        raise RuntimeError(error.read().decode()) from error


def review(output, paths, original):
    from PIL import Image, ImageDraw
    import numpy as np
    images = [Image.open(path).convert("RGBA") for path in paths]
    if not images:
        raise RuntimeError("Backend returned no layers")
    composite = Image.new("RGBA", images[0].size)
    metrics = []
    for path, layer in zip(paths, images):
        alpha = np.asarray(layer.getchannel("A"))
        metrics.append({"file": path.name, "mode": Image.open(path).mode,
                        "alpha_range": [int(alpha.min()), int(alpha.max())],
                        "transparent_fraction": float((alpha == 0).mean()),
                        "partial_alpha_fraction": float(((alpha > 0) & (alpha < 255)).mean()),
                        "opaque_fraction": float((alpha == 255).mean())})
        composite = Image.alpha_composite(composite, layer)
    composite.save(output / "recomposed.png")
    tile = 256
    sheet = Image.new("RGB", (tile * (len(images) + 2), tile + 32), "#242731")
    draw = ImageDraw.Draw(sheet)
    for index, (name, picture) in enumerate([("Input", original), *[(f"Layer {i+1}", im) for i, im in enumerate(images)], ("Recomposed", composite)]):
        x = index * tile
        for yy in range(0, tile, 16):
            for xx in range(0, tile, 16):
                color = "#90949c" if (xx // 16 + yy // 16) % 2 else "#c5c8ce"
                draw.rectangle((x + xx, 32 + yy, x + xx + 15, 32 + yy + 15), fill=color)
        picture = picture.copy().convert("RGBA")
        picture.thumbnail((tile, tile), Image.Resampling.LANCZOS)
        sheet.paste(picture, (x + (tile-picture.width)//2, 32 + (tile-picture.height)//2), picture)
        draw.text((x+8, 9), name, fill="white")
    sheet.save(output / "review.png")
    return metrics


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("input", type=Path)
    parser.add_argument("--caption", default="")
    parser.add_argument("--caption-file", type=Path)
    parser.add_argument("--layers", type=int, choices=range(1, 9), default=3)
    parser.add_argument("--size", type=int, choices=[256, 384, 512, 640, 1024], default=640)
    parser.add_argument("--steps", type=int, default=20)
    parser.add_argument("--cfg", type=float, default=2.5)
    parser.add_argument("--seed", type=int, default=777)
    parser.add_argument("--timeout", type=int, default=1800, help="Maximum seconds after starting the backend")
    args = parser.parse_args()
    if args.steps < 1 or args.steps > 100 or args.timeout < 1:
        parser.error("steps must be 1-100; timeout must be positive")
    if not PYTHON.exists():
        parser.error("Run install.py first")
    args.input = args.input.resolve(strict=True)
    caption = args.caption_file.read_text(encoding="utf-8").strip() if args.caption_file else args.caption
    from PIL import Image
    original = Image.open(args.input).convert("RGBA")
    # Model consumes an opaque reference; preserve the source and composite alpha correctly.
    prepared = Image.new("RGBA", original.size, "white")
    prepared.alpha_composite(original)
    ratio = args.size / max(prepared.size)
    width, height = (max(16, round(d * ratio / 16) * 16) for d in prepared.size)
    prepared = prepared.convert("RGB").resize((width, height), Image.Resampling.LANCZOS)
    stamp = datetime.now().strftime("%Y%m%d-%H%M%S-%f")
    output = HOME / "runs" / stamp
    output.mkdir(parents=True)
    prepared.save(output / "input.png")
    input_dir = output / "backend-input"
    input_dir.mkdir()
    shutil.copy2(output / "input.png", input_dir / "input.png")
    workflow = graph("input.png", caption, width, height, args.layers, args.steps, args.cfg, args.seed)
    (output / "workflow-api.json").write_text(json.dumps(workflow, indent=2), encoding="utf-8")
    parameters = {**vars(args), "input": str(args.input), "caption_file": str(args.caption_file) if args.caption_file else None,
                  "caption": caption, "width": width, "height": height,
                  "input_sha256": hashlib.sha256(args.input.read_bytes()).hexdigest()}
    (output / "parameters.json").write_text(json.dumps(parameters, indent=2), encoding="utf-8")
    with socket.socket() as probe:
        probe.bind(("127.0.0.1", 0))
        port = probe.getsockname()[1]
    base = f"http://127.0.0.1:{port}"
    env = os.environ.copy()
    env.update(HF_HOME=str(HOME / "huggingface"), HF_HUB_OFFLINE="1", TRANSFORMERS_OFFLINE="1", PYTHONUTF8="1")
    command = [str(PYTHON), "-u", str(BACKEND / "main.py"), "--listen", "127.0.0.1", "--port", str(port),
               "--disable-auto-launch", "--disable-all-custom-nodes", "--disable-api-nodes",
               "--lowvram", "--reserve-vram", "3.0", "--cache-none", "--preview-method", "none",
               "--input-directory", str(input_dir), "--output-directory", str(output),
               "--user-directory", str(HOME / "user"), "--temp-directory", str(HOME / "temp")]
    print(f"Run directory: {output}", flush=True)
    print(f"Starting local backend; {width}x{height}, {args.layers} layers, {args.steps} steps. Log: backend.log", flush=True)
    start = time.monotonic()
    process = None
    (HOME / "user").mkdir(exist_ok=True)
    (HOME / "temp").mkdir(exist_ok=True)
    try:
        with (output / "backend.log").open("w", encoding="utf-8") as log:
            process = subprocess.Popen(command, cwd=BACKEND, env=env, stdout=log, stderr=subprocess.STDOUT,
                                       creationflags=subprocess.CREATE_NO_WINDOW if os.name == "nt" else 0)
            while time.monotonic() - start < min(180, args.timeout):
                if process.poll() is not None:
                    raise RuntimeError(f"Backend exited with {process.returncode}; see backend.log")
                try:
                    api(base, "/system_stats")
                    break
                except (urllib.error.URLError, TimeoutError):
                    time.sleep(1)
            else:
                raise TimeoutError("Backend startup timed out")
            submission = api(base, "/prompt", {"prompt": workflow})
            prompt_id = submission["prompt_id"]
            print(f"Queued {prompt_id}", flush=True)
            last = time.monotonic()
            while time.monotonic() - start < args.timeout:
                if process.poll() is not None:
                    raise RuntimeError("Backend exited during generation")
                result = api(base, f"/history/{prompt_id}").get(prompt_id)
                if result:
                    (output / "history.json").write_text(json.dumps(result, indent=2), encoding="utf-8")
                    if result.get("status", {}).get("status_str") == "error":
                        raise RuntimeError(f"Generation failed: {result['status']['messages']}")
                    paths = [output / image["subfolder"] / image["filename"] for image in result["outputs"]["16"]["images"]]
                    if len(paths) != args.layers:
                        raise RuntimeError(f"Expected {args.layers} layers, received {len(paths)}")
                    metrics = review(output, paths, prepared)
                    report = {"elapsed_seconds": time.monotonic()-start, "layers": metrics}
                    (output / "report.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
                    print(json.dumps(report, indent=2), flush=True)
                    print(f"Finished: {output / 'review.png'}", flush=True)
                    return
                now = time.monotonic()
                if now-last >= 30:
                    print(f"Working: {now-start:.0f}s elapsed. See backend.log for model loading and sampling progress.", flush=True)
                    last = now
                time.sleep(2)
            raise TimeoutError(f"Stopped after {args.timeout}s; inspect backend.log before increasing timeout")
    except Exception as error:
        (output / "error.txt").write_text(str(error), encoding="utf-8")
        raise
    finally:
        if process is not None and process.poll() is None:
            process.terminate()
            try:
                process.wait(timeout=15)
            except subprocess.TimeoutExpired:
                process.kill()
                process.wait(timeout=10)


if __name__ == "__main__":
    main()
