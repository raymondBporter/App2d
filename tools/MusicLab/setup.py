"""Download pinned authoring dependencies into the ignored workspace; no system install."""
from pathlib import Path
import hashlib
import platform
import subprocess
import sys
import urllib.request
import zipfile

ROOT=Path(__file__).resolve().parents[2]
DEPS=ROOT/"Assets/Work/music-lab/deps"
REVISION="684543d5e5efaef08d02be50dcda8d552478fa60"
BASE=f"https://raw.githubusercontent.com/mrbumpy409/GeneralUser-GS/{REVISION}"


def download(url,path,expected=None):
    if path.exists() and (expected is None or hashlib.sha256(path.read_bytes()).hexdigest()==expected):
        print(f"Already available: {path.name}");return
    print(f"Downloading {path.name}",flush=True)
    request=urllib.request.Request(url,headers={"User-Agent":"App2d-MusicLab"})
    with urllib.request.urlopen(request,timeout=120) as response:
        data=response.read()
    if expected and hashlib.sha256(data).hexdigest()!=expected:
        raise RuntimeError(f"SHA-256 mismatch for {path.name}")
    path.write_bytes(data)


def main():
    if platform.system()!="Windows" or platform.machine().lower() not in ["amd64","x86_64"]:
        raise SystemExit("This setup pins Windows x64. Install a native FluidSynth build for other platforms.")
    DEPS.mkdir(parents=True,exist_ok=True)
    download("https://github.com/FluidSynth/fluidsynth/releases/download/v2.6.1/fluidsynth-v2.6.1-win10-x64-cpp11.zip",
             DEPS/"fluidsynth.zip","fab7a2e4b85675b66970f97a39bbc239729c5e0f237198b5922a6a73cbc8677c")
    with zipfile.ZipFile(DEPS/"fluidsynth.zip") as archive:
        destination=(DEPS/"fluidsynth").resolve()
        for member in archive.infolist():
            if not (destination/member.filename).resolve().is_relative_to(destination):
                raise RuntimeError("Archive path leaves its destination")
        archive.extractall(destination)
    download(BASE+"/GeneralUser-GS.sf2",DEPS/"GeneralUser-GS.sf2",
             "9575028c7a1f589f5770fccc8cff2734566af40cd26ed836944e9a5152688cfe")
    download(BASE+"/documentation/LICENSE.txt",DEPS/"GeneralUser-GS-LICENSE.txt")
    subprocess.run([sys.executable,"-m","pip","install","--disable-pip-version-check",
                    "--target",str(DEPS/"python"),"numpy==2.5.3","scipy==1.18.1","soundfile==0.14.0",
                    "mido==1.3.3","imageio-ffmpeg==0.6.0"],check=True)
    print("Ready. Run python tools/MusicLab/build.py")


if __name__=="__main__":main()
