"""Serve only the rendered experiment, on loopback. No game or source files exposed."""
import argparse
from functools import partial
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path

parser=argparse.ArgumentParser()
parser.add_argument("--port",type=int,default=8766)
parser.add_argument("--directory",type=Path,default=Path(__file__).resolve().parents[2]/"Assets/Work/music-lab/copper-circuit")
args=parser.parse_args()
if not (args.directory/"index.html").is_file():
    raise SystemExit("Run build.py first.")
handler=partial(SimpleHTTPRequestHandler,directory=str(args.directory.resolve()))
print(f"Music Lab: http://127.0.0.1:{args.port}",flush=True)
ThreadingHTTPServer(("127.0.0.1",args.port),handler).serve_forever()
