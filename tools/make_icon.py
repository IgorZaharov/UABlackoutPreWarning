"""Render the UABlackoutPreWarning icon (crossed-out light bulb).

Writes:
  assets/app.ico        16, 20, 24, 32, 40, 48, 64 px as 32-bit DIB, 256 px as PNG
  assets/icon-256.png   256 px preview (used at the top of README.md)
  assets/icon-128.png   128 px preview

Usage: python3 tools/make_icon.py [repo-root]
Requires Pillow. Geometry is defined on a 64 x 64 grid.
"""
import io
import os
import struct
import sys

from PIL import Image, ImageDraw

GLASS = (0xFF, 0xC8, 0x3D, 255)
OUTLINE = (0x1F, 0x1F, 0x1F, 255)
BASE = (0x7A, 0x7A, 0x7A, 255)
FILAMENT = (0xB0, 0x7A, 0x00, 255)
RED = (0xD1, 0x34, 0x38, 255)

SIZES = [16, 20, 24, 32, 40, 48, 64, 256]


def cubic(p0, p1, p2, p3, steps=24):
    pts = []
    for i in range(1, steps + 1):
        t = i / steps
        mt = 1 - t
        x = mt ** 3 * p0[0] + 3 * mt * mt * t * p1[0] + 3 * mt * t * t * p2[0] + t ** 3 * p3[0]
        y = mt ** 3 * p0[1] + 3 * mt * mt * t * p1[1] + 3 * mt * t * t * p2[1] + t ** 3 * p3[1]
        pts.append((x, y))
    return pts


def glass_outline():
    # Same shape as the SVG used in the prototype:
    # M32 6 C20 6 12 15 12 26 c0 8 5 13 9 17 2 2 3 4 3 7 h16 c0-3 1-5 3-7 4-4 9-9 9-17 C52 15 44 6 32 6z
    pts = [(32, 6)]
    pts += cubic((32, 6), (20, 6), (12, 15), (12, 26))
    pts += cubic((12, 26), (12, 34), (17, 39), (21, 43))
    pts += cubic((21, 43), (23, 45), (24, 47), (24, 50))
    pts.append((40, 50))
    pts += cubic((40, 50), (40, 47), (41, 45), (43, 43))
    pts += cubic((43, 43), (47, 39), (52, 34), (52, 26))
    pts += cubic((52, 26), (52, 15), (44, 6), (32, 6))
    return pts


def stroke(img, pts, width, color):
    """Centred stroke with round joins: sweep a disc along the path."""
    mask = Image.new("L", img.size, 0)
    md = ImageDraw.Draw(mask)
    r = width / 2.0
    step = max(0.5, r / 4.0)
    for (x0, y0), (x1, y1) in zip(pts, pts[1:]):
        dist = ((x1 - x0) ** 2 + (y1 - y0) ** 2) ** 0.5
        n = max(1, int(dist / step))
        for i in range(n + 1):
            t = i / float(n)
            x = x0 + (x1 - x0) * t
            y = y0 + (y1 - y0) * t
            md.ellipse([x - r, y - r, x + r, y + r], fill=255)
    img.paste(Image.new("RGBA", img.size, color), (0, 0), mask)


def render(size):
    """Render one icon image of size x size pixels."""
    simple = size <= 24
    ss = max(8, 1024 // size)          # supersampling factor
    canvas = size * ss
    k = canvas / 64.0

    def P(pts):
        return [(x * k, y * k) for x, y in pts]

    img = Image.new("RGBA", (canvas, canvas), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)

    outline_w = (5.0 if simple else 3.0) * k
    glass = P(glass_outline())
    d.polygon(glass, fill=GLASS)
    stroke(img, glass + [glass[0]], outline_w, OUTLINE)

    if not simple:
        d.line(P([(27, 50), (27, 40), (32, 34), (37, 40), (37, 50)]), fill=FILAMENT,
               width=int(round(1.6 * k)), joint="curve")
        d.rounded_rectangle([24 * k, 52 * k, 40 * k, 56 * k], radius=1.5 * k, fill=BASE)
        d.rounded_rectangle([26 * k, 57 * k, 38 * k, 61 * k], radius=2 * k, fill=BASE)
    else:
        d.rounded_rectangle([23 * k, 52 * k, 41 * k, 62 * k], radius=2 * k, fill=BASE)

    # Transparent gap around the strike so it reads on any background.
    gap_w = (14.0 if simple else 10.0) * k
    red_w = (8.0 if simple else 5.5) * k
    a, b = (10 * k, 10 * k), (54 * k, 54 * k)
    mask = Image.new("L", (canvas, canvas), 0)
    md = ImageDraw.Draw(mask)
    md.line([a, b], fill=255, width=int(round(gap_w)))
    r = gap_w / 2
    md.ellipse([a[0] - r, a[1] - r, a[0] + r, a[1] + r], fill=255)
    md.ellipse([b[0] - r, b[1] - r, b[0] + r, b[1] + r], fill=255)
    alpha = img.getchannel("A")
    alpha.paste(0, mask=mask)
    img.putalpha(alpha)

    d = ImageDraw.Draw(img)
    d.line([a, b], fill=RED, width=int(round(red_w)))
    r = red_w / 2
    d.ellipse([a[0] - r, a[1] - r, a[0] + r, a[1] + r], fill=RED)
    d.ellipse([b[0] - r, b[1] - r, b[0] + r, b[1] + r], fill=RED)

    return img.resize((size, size), Image.LANCZOS)


def dib_entry(img):
    """32-bit BGRA DIB with AND mask, as stored inside .ico files."""
    w, h = img.size
    header = struct.pack("<IiiHHIIiiII", 40, w, h * 2, 1, 32, 0, 0, 0, 0, 0, 0)
    px = img.load()
    rows = []
    for y in range(h - 1, -1, -1):
        row = bytearray()
        for x in range(w):
            r, g, b, a = px[x, y]
            row += bytes((b, g, r, a))
        rows.append(bytes(row))
    mask_row_bytes = ((w + 31) // 32) * 4
    and_mask = b"\x00" * (mask_row_bytes * h)  # alpha channel carries transparency
    return header + b"".join(rows) + and_mask


def png_entry(img):
    buf = io.BytesIO()
    img.save(buf, "PNG", optimize=True)
    return buf.getvalue()


def write_ico(path, images):
    entries = []
    for img in images:
        data = png_entry(img) if img.size[0] >= 256 else dib_entry(img)
        entries.append((img.size[0], data))
    offset = 6 + 16 * len(entries)
    out = struct.pack("<HHH", 0, 1, len(entries))
    blobs = b""
    for size, data in entries:
        dim = 0 if size >= 256 else size
        out += struct.pack("<BBBBHHII", dim, dim, 0, 0, 1, 32, len(data), offset)
        offset += len(data)
        blobs += data
    with open(path, "wb") as f:
        f.write(out + blobs)


def main():
    root = sys.argv[1] if len(sys.argv) > 1 else os.path.join(os.path.dirname(__file__), "..")
    assets = os.path.join(root, "assets")
    os.makedirs(assets, exist_ok=True)
    images = [render(s) for s in SIZES]
    write_ico(os.path.join(assets, "app.ico"), images)
    images[-1].save(os.path.join(assets, "icon-256.png"), optimize=True)
    render(128).save(os.path.join(assets, "icon-128.png"), optimize=True)
    print("wrote", os.path.join(assets, "app.ico"), [im.size[0] for im in images])


if __name__ == "__main__":
    main()
