"""Split the 4-icon design sheet and apply full-bleed launcher icons to Capacitor apps."""
from __future__ import annotations

from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
APPS = Path(__file__).resolve().parent
ASSETS = APPS / "assets"
SOURCE_CANDIDATES = [
    ASSETS / "icon-sheet-4roles.png",
    Path(
        r"C:\Users\touhama\.cursor\projects\d-SkyBeam-MyApp-RadTik-RadTik-20260225-Full-01"
        r"\assets\c__Users_touhama_AppData_Roaming_Cursor_User_workspaceStorage_"
        r"21e649d962415751c5829143bc5bf05a_images_image-45a4a2cb-46be-4d5d-a955-6554b4062e5a.png"
    ),
]
SOURCE = next((p for p in SOURCE_CANDIDATES if p.exists()), SOURCE_CANDIDATES[0])
SHEET_COPY = ASSETS / "icon-sheet-4roles.png"

# Left → right mapping from the design sheet.
ROLES = [
    ("radatik-client", "icon-client.png"),       # white
    ("radatik-company", "icon-company.png"),     # dark navy
    ("radatik-collection", "icon-collection.png"),  # gray
    ("radatik-employee", "icon-employee.png"),   # light steel blue
]


def split_tiles(sheet: Image.Image) -> list[Image.Image]:
    rgb = sheet.convert("RGB")
    w, h = rgb.size
    return [rgb.crop((i * w // 4, 0, (i + 1) * w // 4, h)) for i in range(4)]


def color_dist(a: tuple[int, ...], b: tuple[int, ...]) -> float:
    return ((a[0] - b[0]) ** 2 + (a[1] - b[1]) ** 2 + (a[2] - b[2]) ** 2) ** 0.5


def extract_icon_face(tile: Image.Image, size: int = 1024) -> Image.Image:
    """
    Crop a centered square around each sheet icon, sample the face color from the
    top-interior (inside the rounded rect), then paint page-matte corners with that face.
    """
    rgb = tile.convert("RGB")
    tw, th = rgb.size
    side = 180
    x0 = (tw - side) // 2
    y0 = max(0, (th - side) // 2 - 12)
    crop = rgb.crop((x0, y0, x0 + side, y0 + side)).convert("RGBA")

    # Face color: top-center, slightly below the rounded edge.
    face = crop.getpixel((side // 2, 14))[:3]
    face_rgba = (*face, 255)

    # Page matte from the tile border (outside the icon), not from crop corners.
    page_samples = [
        rgb.getpixel((2, 2)),
        rgb.getpixel((tw - 3, 2)),
        rgb.getpixel((2, th - 3)),
        rgb.getpixel((tw - 3, th - 3)),
    ]
    page_bg = tuple(sum(c[i] for c in page_samples) // 4 for i in range(3))

    px = crop.load()
    for y in range(side):
        for x in range(side):
            c = px[x, y]
            if color_dist(c, page_bg) <= 42:
                px[x, y] = face_rgba

    return crop.resize((size, size), Image.Resampling.LANCZOS)


def fit_on_canvas(mark: Image.Image, size: int, pad_ratio: float = 0.0) -> Image.Image:
    if pad_ratio <= 0:
        return mark.resize((size, size), Image.Resampling.LANCZOS)
    face = mark.getpixel((mark.width // 2, max(2, mark.height // 12)))
    canvas = Image.new("RGBA", (size, size), face)
    inner = max(1, int(size * (1 - 2 * pad_ratio)))
    logo = mark.resize((inner, inner), Image.Resampling.LANCZOS)
    canvas.alpha_composite(logo, ((size - logo.width) // 2, (size - logo.height) // 2))
    return canvas


def write_icons(dest_app: Path, mark: Image.Image) -> None:
    android = dest_app / "android" / "app" / "src" / "main" / "res"
    ios_icon = (
        dest_app
        / "ios"
        / "App"
        / "App"
        / "Assets.xcassets"
        / "AppIcon.appiconset"
        / "AppIcon-512@2x.png"
    )
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

    inset = max(8, mark.width // 20)
    bg_sample = mark.getpixel((inset, inset))
    bg_hex = "#{:02X}{:02X}{:02X}".format(bg_sample[0], bg_sample[1], bg_sample[2])

    for folder, size in sizes.items():
        dest = android / folder
        dest.mkdir(parents=True, exist_ok=True)
        icon = fit_on_canvas(mark, size)
        icon.save(dest / "ic_launcher.png")
        icon.save(dest / "ic_launcher_round.png")

    for folder, size in fg_sizes.items():
        dest = android / folder
        dest.mkdir(parents=True, exist_ok=True)
        fit_on_canvas(mark, size, 0.08).save(dest / "ic_launcher_foreground.png")

    if ios_icon.parent.exists():
        fit_on_canvas(mark, 1024).save(ios_icon)

    www_icon = dest_app / "www" / "icon.png"
    www_icon.parent.mkdir(parents=True, exist_ok=True)
    fit_on_canvas(mark, 512).save(www_icon)

    web_preview = ROOT / "RadaTik" / "wwwroot" / "images" / "apps" / f"{dest_app.name}.png"
    web_preview.parent.mkdir(parents=True, exist_ok=True)
    fit_on_canvas(mark, 512).save(web_preview)

    bg_color = android / "values" / "ic_launcher_background.xml"
    if bg_color.exists():
        bg_color.write_text(
            '<?xml version="1.0" encoding="utf-8"?>\n'
            "<resources>\n"
            f'    <color name="ic_launcher_background">{bg_hex}</color>\n'
            "</resources>\n",
            encoding="utf-8",
        )


def main() -> None:
    ASSETS.mkdir(parents=True, exist_ok=True)
    if not SOURCE.exists():
        raise SystemExit(f"Icon sheet not found: {SOURCE}")
    sheet = Image.open(SOURCE).convert("RGB")
    if SOURCE.resolve() != SHEET_COPY.resolve():
        sheet.save(SHEET_COPY)

    for (folder, asset_name), tile in zip(ROLES, split_tiles(sheet), strict=True):
        square = extract_icon_face(tile, 1024)
        square.save(ASSETS / asset_name)
        dest = APPS / folder
        if not dest.exists():
            print(f"skip missing {dest}")
            continue
        write_icons(dest, square)
        print(
            f"icons {folder} <- {asset_name} "
            f"corner={square.getpixel((20, 20))} "
            f"top={square.getpixel((512, 40))} "
            f"center={square.getpixel((512, 512))}"
        )


if __name__ == "__main__":
    main()
