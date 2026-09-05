"""Generate a short image-to-video clip using the official Wan 2.2 5B workflow."""
from __future__ import annotations
import argparse
from datetime import datetime
import hashlib
import json
import os
from pathlib import Path
import socket
import subprocess
import time
import urllib.error
import urllib.request

from install_models import ROOT, SHARED, WORK


def graph(width, height, length, steps, seed, prompt):
    def node(kind, **inputs):
        return {"class_type": kind, "inputs": inputs}
    return {
        "1": node("LoadImage", image="input.png"),
        "2": node("UNETLoader", unet_name="wan2.2_ti2v_5B_fp16.safetensors", weight_dtype="default"),
        "3": node("CLIPLoader", clip_name="umt5_xxl_fp8_e4m3fn_scaled.safetensors", type="wan", device="default"),
        "4": node("VAELoader", vae_name="wan2.2_vae.safetensors"),
        "5": node("CLIPTextEncode", clip=["3", 0], text=prompt),
        "6": node("CLIPTextEncode", clip=["3", 0], text="camera movement, zoom, pan, cuts, changing viewpoint, photorealism, 3D rendering, gradients, motion blur, blurry outlines, scenery, floor, cast shadows, text, watermark, extra characters, disappearing eyes, changing anatomy, cropped body, static image"),
        "7": node("Wan22ImageToVideoLatent", vae=["4", 0], width=width, height=height,
                  length=length, batch_size=1, start_image=["1", 0]),
        "8": node("ModelSamplingSD3", model=["2", 0], shift=8.0),
        "9": node("KSampler", model=["8", 0], positive=["5", 0], negative=["6", 0],
                  latent_image=["7", 0], seed=seed, steps=steps, cfg=5.0,
                  sampler_name="uni_pc", scheduler="simple", denoise=1.0),
        "10": node("VAEDecode", samples=["9", 0], vae=["4", 0]),
        "11": node("SaveImage", images=["10", 0], filename_prefix="raw/frame"),
    }


def api(base, path, data=None):
    body = json.dumps(data).encode() if data is not None else None
    request = urllib.request.Request(base+path, data=body, headers={"Content-Type": "application/json"})
    try:
        with urllib.request.urlopen(request, timeout=10) as response:
            return json.load(response)
    except urllib.error.HTTPError as error:
        raise RuntimeError(error.read().decode()) from error


def make_previews(output, paths):
    import av
    import numpy as np
    from PIL import Image, ImageDraw
    images = [Image.open(path).convert("RGB") for path in paths]
    with av.open(str(output / "raw-video.mp4"), mode="w") as container:
        stream = container.add_stream("libx264", rate=24)
        stream.width, stream.height = images[0].size
        stream.pix_fmt = "yuv420p"
        stream.options = {"crf": "16", "preset": "fast"}
        for image in images:
            frame = av.VideoFrame.from_ndarray(np.asarray(image), format="rgb24")
            for packet in stream.encode(frame):
                container.mux(packet)
        for packet in stream.encode():
            container.mux(packet)
    tile, columns = 128, 7
    rows = (len(images)+columns-1)//columns
    sheet = Image.new("RGB", (tile*columns, (tile+22)*rows), "#22252c")
    draw = ImageDraw.Draw(sheet)
    for index, image in enumerate(images):
        x, y = (index % columns)*tile, (index // columns)*(tile+22)
        preview = image.copy()
        preview.thumbnail((tile, tile))
        sheet.paste(preview, (x, y+22))
        draw.text((x+5, y+4), f"{index:02d} | {index/24:.2f}s", fill="white")
    sheet.save(output / "all-frames.png")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("input", type=Path)
    parser.add_argument("--prompt-file", type=Path, default=Path(__file__).with_name("gurgle-lunge.txt"))
    parser.add_argument("--size", type=int, choices=[320, 384, 448, 512, 640, 704], default=512)
    parser.add_argument("--frames", type=int, default=49)
    parser.add_argument("--steps", type=int, default=20)
    parser.add_argument("--seed", type=int, default=777)
    parser.add_argument("--timeout", type=int, default=1200)
    args = parser.parse_args()
    if not 5 <= args.frames <= 121 or (args.frames-1) % 4:
        parser.error("frames must be 4n+1, between 5 and 121")
    if not 1 <= args.steps <= 100 or args.timeout < 1:
        parser.error("steps must be 1-100; timeout must be positive")
    from PIL import Image
    image = Image.open(args.input).convert("RGBA")
    background = Image.new("RGBA", image.size, (0, 255, 0, 255))
    background.alpha_composite(image)
    scale = args.size / max(image.size)
    width, height = (max(32, round(d*scale/32)*32) for d in image.size)
    prepared = background.convert("RGB").resize((width, height), Image.Resampling.LANCZOS)
    output = WORK / "runs" / datetime.now().strftime("%Y%m%d-%H%M%S-%f")
    input_dir = output / "input"
    input_dir.mkdir(parents=True)
    prepared.save(input_dir / "input.png")
    prompt = args.prompt_file.read_text(encoding="utf-8").strip()
    workflow = graph(width, height, args.frames, args.steps, args.seed, prompt)
    parameters = {**vars(args), "input": str(args.input.resolve()), "prompt_file": str(args.prompt_file.resolve()),
                  "prompt": prompt, "width": width, "height": height, "fps": 24,
                  "input_sha256": hashlib.sha256(args.input.read_bytes()).hexdigest()}
    (output / "parameters.json").write_text(json.dumps(parameters, indent=2), encoding="utf-8")
    (output / "workflow-api.json").write_text(json.dumps(workflow, indent=2), encoding="utf-8")
    with socket.socket() as probe:
        probe.bind(("127.0.0.1", 0))
        port = probe.getsockname()[1]
    base = f"http://127.0.0.1:{port}"
    env = os.environ.copy()
    env.update(HF_HOME=str(SHARED / "huggingface"), HF_HUB_OFFLINE="1", TRANSFORMERS_OFFLINE="1", PYTHONUTF8="1")
    python = SHARED / ".venv/Scripts/python.exe"
    backend = SHARED / "ComfyUI"
    command = [str(python), "-u", str(backend / "main.py"), "--listen", "127.0.0.1", "--port", str(port),
               "--disable-auto-launch", "--disable-all-custom-nodes", "--disable-api-nodes", "--lowvram",
               "--reserve-vram", "3", "--cache-none", "--preview-method", "none",
               "--input-directory", str(input_dir), "--output-directory", str(output),
               "--user-directory", str(SHARED / "user"), "--temp-directory", str(WORK / "temp")]
    (WORK / "temp").mkdir(exist_ok=True)
    print(f"Run directory: {output}", flush=True)
    start = time.monotonic()
    process = None
    try:
        with (output / "backend.log").open("w", encoding="utf-8") as log:
            process = subprocess.Popen(command, cwd=backend, env=env, stdout=log, stderr=subprocess.STDOUT,
                                       creationflags=subprocess.CREATE_NO_WINDOW if os.name == "nt" else 0)
            while time.monotonic()-start < min(180, args.timeout):
                if process.poll() is not None:
                    raise RuntimeError("Backend exited; inspect backend.log")
                try:
                    api(base, "/system_stats")
                    break
                except (urllib.error.URLError, TimeoutError):
                    time.sleep(1)
            else:
                raise TimeoutError("Backend startup timed out")
            prompt_id = api(base, "/prompt", {"prompt": workflow})["prompt_id"]
            print(f"Queued {prompt_id}: {args.frames} frames at {width}x{height}", flush=True)
            last = time.monotonic()
            while time.monotonic()-start < args.timeout:
                if process.poll() is not None:
                    raise RuntimeError("Backend exited during generation")
                result = api(base, f"/history/{prompt_id}").get(prompt_id)
                if result:
                    (output / "history.json").write_text(json.dumps(result, indent=2), encoding="utf-8")
                    if result.get("status", {}).get("status_str") == "error":
                        raise RuntimeError(str(result["status"]["messages"]))
                    paths = [output / item["subfolder"] / item["filename"] for item in result["outputs"]["11"]["images"]]
                    if len(paths) != args.frames:
                        raise RuntimeError(f"Expected {args.frames} frames; got {len(paths)}")
                    make_previews(output, paths)
                    report = {"elapsed_seconds": time.monotonic()-start, "frames": len(paths),
                              "fps": 24, "duration_seconds": len(paths)/24}
                    (output / "report.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
                    print(json.dumps(report), flush=True)
                    print(f"Finished: {output / 'raw-video.mp4'}", flush=True)
                    return
                if time.monotonic()-last >= 30:
                    print(f"Working: {time.monotonic()-start:.0f}s; see backend.log", flush=True)
                    last = time.monotonic()
                time.sleep(2)
            raise TimeoutError(f"Stopped at {args.timeout}s; inspect backend.log")
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
