"""Add Wan 2.2 5B to the existing local ComfyUI installation."""
import hashlib
import json
import os
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SHARED = ROOT / "Assets/Work/qwen-layered"
WORK = ROOT / "Assets/Work/video-sprites"
REPO = "Comfy-Org/Wan_2.2_ComfyUI_Repackaged"
REVISION = "c4f60d30c55a624e35427060fdd217579a6c1d77"
MODELS = [
    ("diffusion_models/wan2.2_ti2v_5B_fp16.safetensors", 9999658848, "456f901338bd9eadbded3828b819109a9b68e8a525ca5cf8d0049a69fcfeca1e"),
    ("text_encoders/umt5_xxl_fp8_e4m3fn_scaled.safetensors", 6735906897, "c3355d30191f1f066b26d93fba017ae9809dce6c627dda5f6a66eaa651204f68"),
    ("vae/wan2.2_vae.safetensors", 1409400960, "e40321bd36b9709991dae2530eb4ac303dd168276980d3e9bc4b6e2b75fed156"),
]


def main():
    if not (SHARED / "ComfyUI/main.py").exists():
        raise SystemExit("Install tools/QwenLayered first; this experiment reuses its backend.")
    os.environ["HF_HOME"] = str(SHARED / "huggingface")
    os.environ["HF_XET_HIGH_PERFORMANCE"] = "1"
    from huggingface_hub import hf_hub_download
    WORK.mkdir(parents=True, exist_ok=True)
    manifest = {"repository": REPO, "revision": REVISION, "models": MODELS}
    (WORK / "models.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    print(f"Additional model weights: {sum(m[1] for m in MODELS)/1e9:.2f} GB", flush=True)
    for filename, size, checksum in MODELS:
        target = SHARED / "ComfyUI/models" / filename
        if target.exists():
            source = target
        else:
            print(f"Downloading {filename}", flush=True)
            source = Path(hf_hub_download(REPO, "split_files/" + filename, revision=REVISION,
                                          local_dir=WORK / "model-downloads"))
        print(f"Verifying {filename}", flush=True)
        if source.stat().st_size != size:
            raise RuntimeError(f"Wrong file size: {source}")
        with source.open("rb") as stream:
            if hashlib.file_digest(stream, "sha256").hexdigest() != checksum:
                raise RuntimeError(f"Checksum mismatch: {source}")
        if source != target:
            target.parent.mkdir(parents=True, exist_ok=True)
            source.rename(target)
    print("Wan models installed and verified.", flush=True)


if __name__ == "__main__":
    main()
