"""Extract and perspective-dewarp all 8 graphic-novel intro comic panels cleanly to 960x540 PNGs."""
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw

ARTIFACT_DIR = Path(r"C:\Users\se-20\.gemini\antigravity\brain\50fc13b9-24ea-4541-8bcc-1ae74f353f5a")
OUT_DIR = Path(r"C:\Users\se-20\.gemini\antigravity\scratch\HowToVibeCoder\assets\ui")
OUT_DIR.mkdir(parents=True, exist_ok=True)

P1_SRC = ARTIFACT_DIR / "comic_page1_clean_1795161292613.jpg"
P2_SRC = ARTIFACT_DIR / "comic_page2_clean_final_1795161340795.jpg"

PANEL_W, PANEL_H = 960, 540


def find_perspective_coeffs(dst_pts, src_pts):
    """Return 8-element PIL PERSPECTIVE transform coeffs mapping dst -> src."""
    matrix = []
    for (x, y), (u, v) in zip(dst_pts, src_pts):
        matrix.append([x, y, 1, 0, 0, 0, -u * x, -u * y])
        matrix.append([0, 0, 0, x, y, 1, -v * x, -v * y])
    A = np.array(matrix, dtype=np.float64)
    B = np.array([c for pt in src_pts for c in pt], dtype=np.float64)
    res = np.linalg.solve(A, B)
    return tuple(res.tolist())


def fit_line_y_of_x(xs, ys):
    """Robust linear fit y = m*x + b."""
    m, b = np.polyfit(xs, ys, 1)
    # One outlier-rejection pass
    resid = np.abs(ys - (m * xs + b))
    keep = resid <= max(2.0, np.percentile(resid, 75) * 1.5)
    if np.sum(keep) >= 5:
        m, b = np.polyfit(xs[keep], ys[keep], 1)
    return float(m), float(b)


def fit_line_x_of_y(ys, xs):
    """Robust linear fit x = m*y + b."""
    m, b = np.polyfit(ys, xs, 1)
    resid = np.abs(xs - (m * ys + b))
    keep = resid <= max(2.0, np.percentile(resid, 75) * 1.5)
    if np.sum(keep) >= 5:
        m, b = np.polyfit(ys[keep], xs[keep], 1)
    return float(m), float(b)


def intersect(hy_line, vx_line):
    """Intersect y = m_h*x + b_h and x = m_v*y + b_v."""
    m_h, b_h = hy_line
    m_v, b_v = vx_line
    # y = m_h * (m_v * y + b_v) + b_h => y * (1 - m_h*m_v) = m_h*b_v + b_h
    y = (m_h * b_v + b_h) / (1.0 - m_h * m_v)
    x = m_v * y + b_v
    return (float(x), float(y))


def detect_page_quads(img: Image.Image, inset: float = 8.0):
    arr = np.array(img.convert("RGB"))
    h, w, _ = arr.shape
    cream = (arr[:, :, 0] > 205) & (arr[:, :, 1] > 195) & (arr[:, :, 2] > 165)

    # Outer edges: top, bottom, left, right (outer border of black frame)
    # Sample multiple x along top and bottom
    xs_top, ys_top_l, ys_top_r = [], [], []
    ys_bot_l, ys_bot_r = [], []
    for x in range(int(w * 0.08), int(w * 0.42), 8):
        col = np.where(~cream[:int(h * 0.12), x])[0]
        if len(col):
            xs_top.append(x)
            ys_top_l.append(col[0])
        colb = np.where(~cream[int(h * 0.88):, x])[0]
        if len(colb):
            ys_bot_l.append(int(h * 0.88) + colb[-1])

    xs_top_r = []
    for x in range(int(w * 0.58), int(w * 0.92), 8):
        col = np.where(~cream[:int(h * 0.12), x])[0]
        if len(col):
            xs_top_r.append(x)
            ys_top_r.append(col[0])
        colb = np.where(~cream[int(h * 0.88):, x])[0]
        if len(colb):
            ys_bot_r.append(int(h * 0.88) + colb[-1])

    top_line = fit_line_y_of_x(np.array(xs_top + xs_top_r), np.array(ys_top_l + ys_top_r))
    bot_line = fit_line_y_of_x(np.array(xs_top[:len(ys_bot_l)] + xs_top_r[:len(ys_bot_r)]), np.array(ys_bot_l + ys_bot_r))

    # Left and right outer edges
    ys_side, xs_left, xs_right = [], [], []
    for y in list(range(int(h * 0.08), int(h * 0.38), 6)) + list(range(int(h * 0.62), int(h * 0.92), 6)):
        row_l = np.where(~cream[y, :int(w * 0.10)])[0]
        row_r = np.where(~cream[y, int(w * 0.90):])[0]
        if len(row_l) and len(row_r):
            ys_side.append(y)
            xs_left.append(row_l[0])
            xs_right.append(int(w * 0.90) + row_r[-1])
    left_line = fit_line_x_of_y(np.array(ys_side), np.array(xs_left))
    right_line = fit_line_x_of_y(np.array(ys_side), np.array(xs_right))

    # Central horizontal gutter (split top/bottom)
    xs_hg, y_hg_top, y_hg_bot = [], [], []
    for x in list(range(int(w * 0.08), int(w * 0.42), 6)) + list(range(int(w * 0.58), int(w * 0.92), 6)):
        ys = np.where(cream[int(h * 0.36):int(h * 0.62), x])[0]
        if len(ys) >= 4:
            xs_hg.append(x)
            y_hg_top.append(int(h * 0.36) + ys[0])
            y_hg_bot.append(int(h * 0.36) + ys[-1])
    hg_top_line = fit_line_y_of_x(np.array(xs_hg), np.array(y_hg_top))
    hg_bot_line = fit_line_y_of_x(np.array(xs_hg), np.array(y_hg_bot))

    # Central vertical gutter — find ONLY narrow cream band (6..26px wide) bounded by dark ink (<55) on both sides!
    def fit_vg(y_start, y_end):
        ys_v, xl, xr = [], [], []
        lum = arr.mean(axis=2)
        for y in range(y_start, y_end, 4):
            row_cream = cream[y, int(w * 0.45):int(w * 0.55)]
            # Find contiguous runs of cream pixels
            padded = np.concatenate([[False], row_cream, [False]])
            starts = np.where(~padded[:-1] & padded[1:])[0]
            ends = np.where(padded[:-1] & ~padded[1:])[0]
            best = None
            for s, e in zip(starts, ends):
                width = e - s
                if 6 <= width <= 28:
                    gx0 = int(w * 0.45) + s
                    gx1 = int(w * 0.45) + e - 1
                    left_dark = lum[y, max(0, gx0 - 6):gx0].min() < 65
                    right_dark = lum[y, gx1 + 1:min(w, gx1 + 7)].min() < 65
                    if left_dark and right_dark:
                        best = (gx0, gx1)
                        break
            if best is not None:
                ys_v.append(y)
                xl.append(best[0])
                xr.append(best[1])
        return fit_line_x_of_y(np.array(ys_v), np.array(xl)), fit_line_x_of_y(np.array(ys_v), np.array(xr))

    vg_top_l, vg_top_r = fit_vg(int(h * 0.06), int(h * 0.40))
    vg_bot_l, vg_bot_r = fit_vg(int(h * 0.56), int(h * 0.94))

    # Inset each line slightly inward to skip the 5-6px black comic border
    def shift_h(line, dy):
        return (line[0], line[1] + dy)

    def shift_v(line, dx):
        return (line[0], line[1] + dx)

    quads = [
        # Top-Left
        (shift_h(top_line, +inset), shift_h(hg_top_line, -inset), shift_v(left_line, +inset), shift_v(vg_top_l, -inset)),
        # Top-Right
        (shift_h(top_line, +inset), shift_h(hg_top_line, -inset), shift_v(vg_top_r, +inset), shift_v(right_line, -inset)),
        # Bottom-Left (slightly gentler top inset so slot-machine arch has breathing room)
        (shift_h(hg_bot_line, +inset * 0.65), shift_h(bot_line, -inset), shift_v(left_line, +inset), shift_v(vg_bot_l, -inset)),
        # Bottom-Right
        (shift_h(hg_bot_line, +inset), shift_h(bot_line, -inset), shift_v(vg_bot_r, +inset), shift_v(right_line, -inset)),
    ]

    pts_list = []
    for t_l, b_l, l_l, r_l in quads:
        tl = intersect(t_l, l_l)
        tr = intersect(t_l, r_l)
        br = intersect(b_l, r_l)
        bl = intersect(b_l, l_l)
        pts_list.append([tl, tr, br, bl])
    return pts_list


def main():
    sheet = Image.new("RGB", (PANEL_W * 2 + 60, PANEL_H * 4 + 100), (239, 229, 207))
    panel_idx = 1
    dst_pts = [(0, 0), (PANEL_W - 1, 0), (PANEL_W - 1, PANEL_H - 1), (0, PANEL_H - 1)]

    for page_num, src_path in [(1, P1_SRC), (2, P2_SRC)]:
        im = Image.open(src_path).convert("RGB")
        # Also save clean 1920x1080 full page backup
        im.resize((1920, 1080), Image.Resampling.LANCZOS).save(OUT_DIR / f"comic_page{page_num}.png", optimize=True)
        quads = detect_page_quads(im, inset=8.5)
        for q in quads:
            coeffs = find_perspective_coeffs(dst_pts, q)
            warped = im.transform((PANEL_W, PANEL_H), Image.Transform.PERSPECTIVE, coeffs, Image.Resampling.BICUBIC)
            out_path = OUT_DIR / f"comic_p{panel_idx}.png"
            warped.save(out_path, optimize=True)
            row = (panel_idx - 1) // 2
            col = (panel_idx - 1) % 2
            sheet.paste(warped, (20 + col * (PANEL_W + 20), 20 + row * (PANEL_H + 20)))
            print(f"Saved {out_path.name} | quad={[tuple(round(v,1) for v in p) for p in q]}")
            panel_idx += 1

    sheet_path = ARTIFACT_DIR / "comic_panels_8_verify.png"
    sheet.resize((1280, int(1280 * sheet.height / sheet.width)), Image.Resampling.LANCZOS).save(sheet_path)
    print(f"Contact sheet -> {sheet_path}")


if __name__ == "__main__":
    main()
