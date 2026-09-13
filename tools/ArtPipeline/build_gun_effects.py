#!/usr/bin/env python3
"""Bake deterministic blue gun textures, charge HUD frames, and mono PCM cues.

No synthesis or image generation occurs during play. Charge audio is exactly 0.6s;
keep this in sync with GunPersonWeapon2D.ChargeSeconds.
"""
from __future__ import annotations

import argparse
import math
from pathlib import Path
import random
import struct
import wave

from PIL import Image, ImageDraw, ImageFilter


def energy_texture(path: Path, width: int, height: int, kind: str) -> None:
    image = Image.new("RGBA", (width, height))
    pixels = []
    for y in range(height):
        for x in range(width):
            nx = (x + 0.5) / width * 2 - 1
            ny = (y + 0.5) / height * 2 - 1
            if kind == "bolt":
                # A luminous head at the right, with a narrow tail to the left.
                radius = ((nx - 0.35) / 0.55) ** 2 + (ny / 0.42) ** 2
                core = math.exp(-radius * 5)
                tail = math.exp(-(ny / 0.15) ** 2) * max(0, 1 - abs(nx + 0.1))
                glow = math.exp(-radius) * 0.7 + tail * 0.6
            else:
                radius = nx * nx + ny * ny
                core = math.exp(-radius * 65)
                glow = math.exp(-radius * 5) * 0.8
                if kind == "flash":
                    core += math.exp(-abs(nx) * 4 - ny * ny * 350) * 0.8
                    core += math.exp(-abs(ny) * 7 - nx * nx * 350) * 0.45
                else:
                    glow += math.exp(-((math.sqrt(radius) - 0.5) / 0.035) ** 2) * 0.45
            core = min(1, core)
            edge = max(0, min(1, (1 - abs(nx)) * 12, (1 - abs(ny)) * 12))
            alpha = min(1, glow + core) * edge
            pixels.append((int(40 + 200 * core), int(155 + 95 * core), 255, int(255 * alpha)))
    image.putdata(pixels)
    path.parent.mkdir(parents=True, exist_ok=True)
    image.save(path)


def ready_frames(root: Path) -> None:
    """Seamless 0.8s loop: breathing core, orbiting wisps, and two tiny glints."""
    output = root / "effects/gun/ready"
    output.mkdir(parents=True, exist_ok=True)
    size = 128
    for frame in range(24):
        phase = math.tau * frame / 24
        pixels = []
        for y in range(size):
            ny = (y + 0.5) / size * 2 - 1
            for x in range(size):
                nx = (x + 0.5) / size * 2 - 1
                radius = math.hypot(nx, ny)
                angle = math.atan2(ny, nx)
                core = math.exp(-radius * radius * 65) * (0.94 + 0.06 * math.sin(phase * 2))
                halo = math.exp(-radius * radius * 5) * (0.73 + 0.07 * math.sin(phase))
                ring_radius = 0.51 + 0.025 * math.sin(phase) + 0.018 * math.sin(angle * 3 - phase)
                arc = (0.5 + 0.5 * math.cos(angle - phase)) ** 5
                ring = math.exp(-((radius - ring_radius) / 0.033) ** 2) * (0.16 + 0.48 * arc)
                glint = 0.0
                for offset, strength in ((0.0, 0.8), (math.pi, 0.5)):
                    theta = phase + offset
                    gx, gy = 0.6 * math.cos(theta), 0.6 * math.sin(theta)
                    glint += math.exp(-((nx - gx) ** 2 + (ny - gy) ** 2) / 0.0015) * strength
                light = min(1, core + glint)
                edge = max(0, min(1, (1 - abs(nx)) * 12, (1 - abs(ny)) * 12))
                alpha = min(1, core + halo + ring + glint) * edge
                pixels.append((round(40 + 200 * light), round(155 + 95 * light), 255, round(255 * alpha)))
        image = Image.new("RGBA", (size, size))
        image.putdata(pixels)
        image.save(output / f"frame-{frame:04d}.png")


def trail_textures(root: Path) -> None:
    """Thin tapered afterimage, with eight baked opacity steps for impact fading."""
    width, height = 256, 32
    pixels = []
    for y in range(height):
        ny = (y + 0.5) / height * 2 - 1
        for x in range(width):
            along = (x + 0.5) / width
            thickness = 0.08 + 0.38 * along
            core = math.exp(-(ny / thickness) ** 2 * 2)
            halo = math.exp(-(ny / (thickness * 2)) ** 2 * 2)
            alpha = (0.38 * core + 0.12 * halo) * along ** 1.2
            pixels.append((110, 211, 255, round(255 * alpha)))
    image = Image.new("RGBA", (width, height))
    image.putdata(pixels)
    for frame in range(8):
        faded = image.copy()
        faded.putalpha(image.getchannel("A").point(lambda a: round(a * (1 - frame / 8))))
        faded.save(root / f"effects/gun/trail-{frame:02d}.png")


def hud_frames(root: Path) -> None:
    size = 160
    output = root / "ui/hud/gun-charge"
    output.mkdir(parents=True, exist_ok=True)
    for frame in range(61):
        progress = frame / 60
        image = Image.new("RGBA", (size, size))
        draw = ImageDraw.Draw(image)
        draw.ellipse((11, 11, 149, 149), fill=(9, 22, 38, 245), outline=(44, 79, 102, 255), width=2)
        draw.ellipse((18, 18, 142, 142), outline=(27, 58, 78, 255), width=7)
        draw.ellipse((32, 32, 128, 128), fill=(15, int(28 + 35 * progress), int(43 + 55 * progress), 255))
        light = Image.new("RGBA", image.size)
        ring = ImageDraw.Draw(light)
        if frame:
            ring.arc((18, 18, 142, 142), -90, -90 + 360 * progress,
                     fill=(68, 201, 255, 255), width=7)
            angle = -math.pi / 2 + math.tau * progress
            x, y = 80 + math.cos(angle) * 62, 80 + math.sin(angle) * 62
            ring.ellipse((x - 5, y - 5, x + 5, y + 5), fill=(230, 252, 255, 255))
        image = Image.alpha_composite(image, light.filter(ImageFilter.GaussianBlur(5)))
        image = Image.alpha_composite(image, light)
        # Draw at HUD scale: the old source crop includes a hand and much padding.
        draw = ImageDraw.Draw(image)
        draw.polygon(((55, 77), (76, 81), (65, 108), (44, 103)),
                     fill=(164, 77, 37, 255), outline=(5, 15, 25, 255), width=4)
        draw.rounded_rectangle((64, 78, 92, 96), radius=6,
                               outline=(110, 143, 155, 255), width=4)
        draw.rounded_rectangle((48, 61, 122, 83), radius=4,
                               fill=(148, 176, 187, 255), outline=(5, 15, 25, 255), width=4)
        draw.line((55, 66, 111, 66), fill=(218, 242, 248, 255), width=3)
        draw.line((76, 62, 76, 73, 87, 73, 87, 62), fill=(25, 48, 62, 255), width=3)
        draw.line((119, 66, 119, 77), fill=(80, int(160 + 90 * progress), 255, 255), width=4)
        image.save(output / f"frame-{frame:04d}.png")


def sound(path: Path, kind: str, duration: float) -> None:
    rate = 44100
    rng = random.Random(704)
    samples = []
    phase = 0.0
    for i in range(round(duration * rate)):
        t = i / rate
        p = t / duration
        if kind == "charge":
            frequency = 180 + 950 * p * p
            envelope = min(1, t / 0.025) * (0.12 + 0.45 * p) * min(1, (duration - t) / 0.012)
            phase += math.tau * frequency / rate
            value = math.sin(phase) * 0.7 + math.sin(phase * 2) * 0.2 + math.sin(phase * 3) * 0.1
        else:
            frequency = (1250 * math.exp(-t * 24) + 90) if kind == "fire" else (
                700 * (1 - p) + 80 if kind == "cancel" else 160 + 700 * math.exp(-t * 40))
            phase += math.tau * frequency / rate
            envelope = min(1, t / 0.003) * (1 - p) ** 2
            value = math.sin(phase) * 0.65 + rng.uniform(-1, 1) * (0.35 if kind != "cancel" else 0.12)
        samples.append(int(max(-1, min(1, value * envelope * 0.85)) * 32767))
    path.parent.mkdir(parents=True, exist_ok=True)
    with wave.open(str(path), "wb") as wav:
        wav.setnchannels(1)
        wav.setsampwidth(2)
        wav.setframerate(rate)
        wav.writeframes(struct.pack(f"<{len(samples)}h", *samples))


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--content-root", type=Path,
                        default=Path(__file__).resolve().parents[2] / "Assets/Runtime")
    root = parser.parse_args().content_root
    energy_texture(root / "effects/gun/bolt.png", 192, 64, "bolt")
    energy_texture(root / "effects/gun/charge.png", 128, 128, "charge")
    energy_texture(root / "effects/gun/flash.png", 192, 128, "flash")
    ready_frames(root)
    trail_textures(root)
    hud_frames(root)
    for kind, duration in (("charge", 0.6), ("fire", 0.22), ("cancel", 0.13), ("impact", 0.12)):
        sound(root / f"audio/sfx/gun-{kind}.wav", kind, duration)
    print("Baked gun effects, 24 ready shimmer frames, 61 charge HUD frames, and four sound cues.")


if __name__ == "__main__":
    main()
