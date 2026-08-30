"""Regenerate Android/iOS/web icons: larger RT mark, white background, T colored per role."""
from __future__ import annotations

import colorsys
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
APPS = Path(__file__).resolve().parent
MARK = ROOT / "RadaTik" / "wwwroot" / "images" / "brand" / "radatik-mark.png"

WHITE = (255, 255, 255, 255)
ROLES = [
    {
        "folder": "radatik-client",
        "t_color": None,
    },
    {
        "folder": "radatik-collection",
        "t_color": (4, 120, 87),
    },
    {
        "folder": "radatik-employee",
        "t_color": (29, 78, 216),
    },
]


def recolor_orange_t(src: Image.Image, target_rgb: tuple[int, int, int] | None) -> Image.Image:
    image = src.convert("RGBA")
    if target_rgb is None:
        return image

    pixels = image.load()
    th, ts, tv = colorsys.rgb_to_hsv(target_rgb[0] / 255, target_rgb[1] / 255, target_rgb[2] / 255)
    for y in range(image.height):
        for x in range(image.width):
            r, g, b, a = pixels[x, y]
            if a < 10:
                continue
            h, s, v = colorsys.rgb_to_hsv(r / 255, g / 255, b / 255)
            if 0.02 <= h <= 0.13 and s >= 0.32 and v >= 0.28:
                nr, ng, nb = colorsys.hsv_to_rgb(th, min(1.0, max(s, ts * 0.72)), v)
                pixels[x, y] = (int(nr * 255), int(ng * 255), int(nb * 255), a)
    return image


def fit_logo(mark: Image.Image, size: int, pad_ratio: float) -> Image.Image:
    canvas = Image.new("RGBA", (size, size), WHITE)
    inner = max(1, int(size * (1 - 2 * pad_ratio)))
    logo = mark.copy()
    logo.thumbnail((inner, inner), Image.Resampling.LANCZOS)
    x = (size - logo.width) // 2
    y = (size - logo.height) // 2
    canvas.alpha_composite(logo, (x, y))
    return canvas


def write_icons(dest_app: Path, mark: Image.Image) -> None:
    android = dest_app / "android" / "app" / "src" / "main" / "res"
    ios_icon = dest_app / "ios" / "App" / "App" / "Assets.xcassets" / "AppIcon.appiconset" / "AppIcon-512@2x.png"
    sizes = {
        "mipmap-mdpi": 48,
        "mipmap-hdpi": 72,
        "mipmap-xhdpi": 96,
        "mipmap-xxhdpi": 144,
        "mipmap-xxxhdpi": 192,
    }
    fg_sizes = {
        "mipmap-mdpi": 108,
        "mipmap-hdpi": 162,
        "mipmap-xhdpi": 216,
        "mipmap-xxhdpi": 324,
        "mipmap-xxxhdpi": 432,
    }
    for folder, size in sizes.items():
        dest = android / folder
        dest.mkdir(parents=True, exist_ok=True)
        icon = fit_logo(mark, size, 0.06)
        icon.save(dest / "ic_launcher.png")
        icon.save(dest / "ic_launcher_round.png")
    for folder, size in fg_sizes.items():
        dest = android / folder
        dest.mkdir(parents=True, exist_ok=True)
        fit_logo(mark, size, 0.15).save(dest / "ic_launcher_foreground.png")
    if ios_icon.parent.exists():
        fit_logo(mark, 1024, 0.06).save(ios_icon)
    www_icon = dest_app / "www" / "icon.png"
    www_icon.parent.mkdir(parents=True, exist_ok=True)
    fit_logo(mark, 512, 0.05).save(www_icon)

    bg_color = android / "values" / "ic_launcher_background.xml"
    if bg_color.exists():
        bg_color.write_text(
            '<?xml version="1.0" encoding="utf-8"?>\n'
            "<resources>\n"
            "    <color name=\"ic_launcher_background\">#FFFFFF</color>\n"
            "</resources>\n",
            encoding="utf-8",
        )


def main() -> None:
    source = Image.open(MARK).convert("RGBA")
    for role in ROLES:
        dest = APPS / role["folder"]
        if not dest.exists():
            print(f"skip missing {dest}")
            continue
        write_icons(dest, recolor_orange_t(source, role["t_color"]))
        print(f"icons {role['folder']}")


if __name__ == "__main__":
    main()
