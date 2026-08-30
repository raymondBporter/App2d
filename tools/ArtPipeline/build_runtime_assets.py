#!/usr/bin/env python3
"""Rebuild the generated runtime art required by App2d."""

from __future__ import annotations

import subprocess
import sys
from pathlib import Path


def run(repository: Path, description: str, *arguments: str) -> None:
    print(f"\n==> {description}", flush=True)
    subprocess.run(
        [sys.executable, *arguments],
        cwd=repository,
        check=True,
    )


def main() -> None:
    repository = Path(__file__).resolve().parents[2]
    pipeline = repository / "tools/ArtPipeline"

    run(
        repository,
        "Importing baked stick-figure sword and gun sprites",
        str(pipeline / "import_stick_figure.py"),
    )
    run(
        repository,
        "Importing Maaot DarkCave and Mossy Cavern environments",
        str(pipeline / "import_maaot_caves.py"),
    )

    print("\nRuntime assets are ready. Run: dotnet run --project App2d", flush=True)


if __name__ == "__main__":
    main()
