"""Install an isolated, pinned ComfyUI backend for local Qwen layer experiments."""
from __future__ import annotations

import argparse
import hashlib
import json
import os
from pathlib import Path
import subprocess
import sys
import time
import urllib.request
import zipfile

ROOT = Path(__file__).resolve().parents[2]
HOME = ROOT / "Assets" / "Work" / "qwen-layered"
COMMIT = "250b2e9551a7bc7a8ebb5beb07e0fecd2983e04a"
BACKEND = HOME / "ComfyUI"
PYTHON = HOME / ".venv" / "Scripts" / "python.exe"
MODELS = [
    ("Comfy-Org/Qwen-Image-Layered_ComfyUI", "split_files/diffusion_models/qwen_image_layered_fp8mixed.safetensors"),
    ("Comfy-Org/HunyuanVideo_1.5_repackaged", "split_files/text_encoders/qwen_2.5_vl_7b_fp8_scaled.safetensors"),
    ("Comfy-Org/Qwen-Image-Layered_ComfyUI", "split_files/vae/qwen_image_layered_vae.safetensors"),
]


def read_json(url):
    with urllib.request.urlopen(url, timeout=60) as response:
        return json.load(response)


def download(url, destination, size=None, sha256=None):
    destination = Path(destination)
    destination.parent.mkdir(parents=True, exist_ok=True)
    if destination.exists():
        if size is not None and destination.stat().st_size != size:
            raise RuntimeError(f"Existing file has wrong size: {destination}")
        if sha256:
            print(f"Verifying {destination.name}", flush=True)
            with destination.open("rb") as source:
                actual = hashlib.file_digest(source, "sha256").hexdigest()
            if actual != sha256:
                raise RuntimeError(f"Existing file checksum mismatch: {destination}")
        return
    part = destination.with_suffix(destination.suffix + ".part")
    for attempt in range(5):
        offset = part.stat().st_size if part.exists() else 0
        headers = {"Range": f"bytes={offset}-"} if offset else {}
        print(f"Downloading {destination.name} (resume {offset / 1e9:.2f} GB)", flush=True)
        try:
            with urllib.request.urlopen(urllib.request.Request(url, headers=headers), timeout=90) as response:
                if offset and response.status != 206:
                    offset = 0
                expected = size or (offset + int(response.headers.get("Content-Length", 0)))
                current = offset
                start = last = time.monotonic()
                with part.open("ab" if offset else "wb") as output:
                    while block := response.read(8 * 1024 * 1024):
                        output.write(block)
                        current += len(block)
                        now = time.monotonic()
                        if now - last >= 15:
                            print(f"  {current / 1e9:.2f}/{expected / 1e9:.2f} GB, {(current-offset)/1e6/(now-start):.1f} MB/s", flush=True)
                            last = now
            if size and part.stat().st_size != size:
                raise IOError("Incomplete download; will resume")
            if sha256:
                print(f"Checking SHA256: {destination.name}", flush=True)
                with part.open("rb") as source:
                    actual = hashlib.file_digest(source, "sha256").hexdigest()
                if actual != sha256:
                    raise ValueError(f"Checksum mismatch: {part}; file retained for diagnosis")
            part.rename(destination)
            return
        except (OSError, TimeoutError) as error:
            if attempt == 4:
                raise
            print(f"Download interrupted: {error}; retrying", flush=True)
            time.sleep(3)


def install_backend():
    HOME.mkdir(parents=True, exist_ok=True)
    if not BACKEND.exists():
        archive = HOME / f"comfyui-{COMMIT}.zip"
        download(f"https://codeload.github.com/Comfy-Org/ComfyUI/zip/{COMMIT}", archive)
        with zipfile.ZipFile(archive) as source:
            for entry in source.infolist():
                target = (HOME / entry.filename).resolve()
                if not target.is_relative_to(HOME.resolve()):
                    raise ValueError("Unsafe archive path")
            source.extractall(HOME)
        (HOME / f"ComfyUI-{COMMIT}").rename(BACKEND)
        (HOME / "backend-commit.txt").write_text(COMMIT + "\n")
    elif not (HOME / "backend-commit.txt").exists() or (HOME / "backend-commit.txt").read_text().strip() != COMMIT:
        raise RuntimeError("Existing backend is not this installer's pinned checkout")
    if not PYTHON.exists():
        subprocess.run([sys.executable, "-m", "venv", str(HOME / ".venv")], check=True)
    env = os.environ.copy()
    env["PIP_CACHE_DIR"] = str(HOME / "pip-cache")
    env["HF_HOME"] = str(HOME / "huggingface")
    env["PYTHONUTF8"] = "1"
    lock = Path(__file__).with_name("requirements-windows.lock.txt")
    commands = [["-m", "pip", "install", "--upgrade", "pip"]]
    if lock.exists():
        commands.append(["-m", "pip", "install", "-r", str(lock), "--extra-index-url", "https://download.pytorch.org/whl/cu130"])
    else:
        commands.extend([
            ["-m", "pip", "install", "torch", "torchvision", "torchaudio", "--index-url", "https://download.pytorch.org/whl/cu130"],
            ["-m", "pip", "install", "-r", str(BACKEND / "requirements.txt")],
        ])
    commands.append(["-m", "pip", "check"])
    for command in commands:
        subprocess.run([str(PYTHON), *command], check=True, env=env)
    freeze = subprocess.check_output([str(PYTHON), "-m", "pip", "freeze"], env=env, text=True)
    (HOME / "installed-packages.txt").write_text(freeze, encoding="utf-8")
    print("Backend installed.", flush=True)


def install_models(accelerated=False):
    manifest_file = HOME / "models.json"
    if manifest_file.exists():
        manifest = json.loads(manifest_file.read_text())
    elif Path(__file__).with_name("models.lock.json").exists():
        manifest = json.loads(Path(__file__).with_name("models.lock.json").read_text())
        HOME.mkdir(parents=True, exist_ok=True)
        manifest_file.write_text(json.dumps(manifest, indent=2) + "\n")
    else:
        manifest = []
        for repository, filename in MODELS:
            info = read_json(f"https://huggingface.co/api/models/{repository}?blobs=true")
            entry = next(item for item in info["siblings"] if item["rfilename"] == filename)
            manifest.append({"repository": repository, "revision": info["sha"], "file": filename,
                             "size": entry["size"], "sha256": entry["lfs"]["sha256"]})
        HOME.mkdir(parents=True, exist_ok=True)
        manifest_file.write_text(json.dumps(manifest, indent=2) + "\n")
    print(f"Model downloads: {sum(item['size'] for item in manifest)/1e9:.2f} GB total", flush=True)
    for item in manifest:
        relative = item["file"].removeprefix("split_files/")
        url = f"https://huggingface.co/{item['repository']}/resolve/{item['revision']}/{item['file']}"
        destination = BACKEND / "models" / relative
        if accelerated and not destination.exists():
            os.environ["HF_HOME"] = str(HOME / "huggingface")
            os.environ["HF_XET_HIGH_PERFORMANCE"] = "1"
            from huggingface_hub import hf_hub_download
            print(f"Hugging Face download: {item['file']}", flush=True)
            staged = Path(hf_hub_download(repo_id=item["repository"], filename=item["file"],
                                         revision=item["revision"], local_dir=HOME / "hf-stage"))
            download(url, staged, item["size"], item["sha256"])
            destination.parent.mkdir(parents=True, exist_ok=True)
            staged.rename(destination)
        else:
            download(url, destination, item["size"], item["sha256"])
    print("All model weights downloaded and verified.", flush=True)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("stage", choices=["backend", "models", "all"], default="all", nargs="?")
    parser.add_argument("--http-downloads", action="store_true", help="Use the resumable stdlib downloader instead of Hugging Face Xet")
    args = parser.parse_args()
    if args.stage in ("backend", "all"):
        install_backend()
    if args.stage in ("models", "all"):
        if not args.http_downloads and Path(sys.executable).resolve() != PYTHON.resolve():
            subprocess.run([str(PYTHON), "-X", "utf8", str(Path(__file__).resolve()), "models"], check=True)
        else:
            install_models(accelerated=not args.http_downloads)
