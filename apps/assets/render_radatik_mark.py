"""High-resolution RT monogram so app icons are not upscaled from 98px."""
from __future__ import annotations

from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter

NAVY = (8, 58, 98, 255)
NAVY_EDGE = (4, 36, 66, 255)
ORANGE = (220, 128, 34, 255)
ORANGE_EDGE = (176, 92, 18, 255)
TRACE_R = (168, 206, 226, 230)
TRACE_T = (255, 214, 156, 230)
WHITE = (255, 255, 255, 255)

SIZE = 1024


def _line(draw: ImageDraw.ImageDraw, pts: list[tuple[float, float]], color, width: int) -> None:
    draw.line([(int(x), int(y)) for x, y in pts], fill=color, width=width, joint="curve")
    r = max(3, width // 2 + 2)
    for x, y in (pts[0], pts[-1]):
        draw.ellipse((x - r, y - r, x + r, y + r), fill=color)


def render_mark(t_color: tuple[int, int, int] | None = None) -> Image.Image:
    hi = _render_at(SIZE * 2, t_color)
    return hi.resize((SIZE, SIZE), Image.Resampling.LANCZOS)


def _render_at(size: int, t_color: tuple[int, int, int] | None) -> Image.Image:
    scale = size / SIZE
    orange = (*t_color, 255) if t_color else ORANGE
    orange_edge = tuple(max(0, c - 40) for c in orange[:3]) + (255,)
    image = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)

    def p(pts: list[tuple[float, float]]) -> list[tuple[int, int]]:
        return [(int(x * scale), int(y * scale)) for x, y in pts]

    def n(v: float) -> int:
        return max(1, int(v * scale))

    # Geometry matches the official 98px mark silhouette, drawn large and sharp.
    r_outer = p([
        (168, 198), (508, 198), (548, 228), (548, 368), (508, 404),
        (430, 404), (548, 786), (448, 786), (352, 470), (318, 470),
        (318, 786), (168, 786),
    ])
    r_hole = p([(286, 286), (430, 286), (458, 310), (430, 336), (286, 336)])
    t_body = p([
        (508, 198), (848, 198), (848, 318), (668, 318), (668, 786),
        (518, 786), (518, 404), (548, 404), (548, 228),
    ])

    shadow = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    sdraw = ImageDraw.Draw(shadow)
    sdraw.polygon([(x + n(10), y + n(14)) for x, y in r_outer], fill=(0, 0, 0, 38))
    sdraw.polygon([(x + n(10), y + n(14)) for x, y in t_body], fill=(0, 0, 0, 28))
    shadow = shadow.filter(ImageFilter.GaussianBlur(n(8)))
    image.alpha_composite(shadow)

    draw.polygon(r_outer, fill=NAVY)
    hole = Image.new("L", (size, size), 0)
    ImageDraw.Draw(hole).polygon(r_hole, fill=255)
    image.paste(Image.new("RGBA", (size, size), (0, 0, 0, 0)), (0, 0), hole)
    draw = ImageDraw.Draw(image)
    draw.polygon(t_body, fill=orange)
    draw.polygon(p([(148, 212), (168, 198), (168, 786), (148, 772)]), fill=NAVY_EDGE)
    draw.polygon(p([(848, 198), (862, 214), (862, 304), (848, 318)]), fill=orange_edge)

    _line(draw, p([(214, 248), (214, 736)]), TRACE_R, n(10))
    _line(draw, p([(214, 248), (390, 248), (420, 278), (390, 308), (214, 308)]), TRACE_R, n(8))
    _line(draw, p([(214, 430), (300, 430), (390, 720)]), TRACE_R, n(8))
    draw.ellipse((*p([(204, 238)])[0], *p([(224, 258)])[0]), fill=TRACE_R)
    draw.ellipse((*p([(204, 726)])[0], *p([(224, 746)])[0]), fill=TRACE_R)
    _line(draw, p([(596, 248), (786, 248)]), TRACE_T, n(8))
    _line(draw, p([(668, 248), (668, 736)]), TRACE_T, n(10))
    draw.ellipse((*p([(776, 238)])[0], *p([(796, 258)])[0]), fill=TRACE_T)
    draw.ellipse((*p([(658, 726)])[0], *p([(678, 746)])[0]), fill=TRACE_T)

    return image


def save_master(path: Path) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    canvas = Image.new("RGBA", (SIZE, SIZE), WHITE)
    canvas.alpha_composite(render_mark())
    canvas.save(path)


if __name__ == "__main__":
    dest = Path(__file__).resolve().parent / "radatik-mark-hires.png"
    save_master(dest)
    print(dest)
