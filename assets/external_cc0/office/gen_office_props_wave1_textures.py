"""Generate high-resolution lived-in textures for Office Props Wave 1:
  1. whiteboard_front.png  (1024x1024) - Russian startup architecture diagram + crossed-out sprint deadlines
  2. whiteboard_back.png   (1024x1024) - Russian Kanban war-room board + hotfix notes
  3. sticky_notes_atlas.png (512x512)  - 2x2 atlas of 4 handwritten colorful sticky notes
  4. cooler_body_tex.png    (512x512)  - Matte ivory powder-coat micro-grain + subtle seam/splash wear
  5. cooler_jug_tex.png     (512x512)  - Sky-blue ribbed polycarbonate caustics + meniscus sheen
  6. copier_panel_tex.png   (512x512)  - Russian MFP touchscreen UI (paper jam alert) + physical keypad
  7. copier_body_tex.png    (512x512)  - Warm off-white copier shell grain + seams + Russian inventory barcode
  8. printed_sheet_tex.png  (512x512)  - Freshly printed Russian temporary pass / memo sheet
"""
import math
import os
import random
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont, ImageFilter

OUT_DIR = Path(__file__).resolve().parent / "textures_wave1"
OUT_DIR.mkdir(parents=True, exist_ok=True)


def load_font(candidates, size):
    font_dir = Path(r"C:\Windows\Fonts")
    for name in candidates:
        p = font_dir / name
        if p.exists():
            try:
                return ImageFont.truetype(str(p), size)
            except Exception:
                pass
    return ImageFont.load_default()


HAND_BOLD = ["segoeprb.ttf", "comicbd.ttf", "segoescb.ttf", "arialbd.ttf"]
HAND_REG = ["segoepr.ttf", "comic.ttf", "segoesc.ttf", "arial.ttf"]
UI_BOLD = ["segoeuib.ttf", "arialbd.ttf", "calibrib.ttf"]
UI_MONO = ["consolab.ttf", "consola.ttf", "courbd.ttf"]


def jitter_line(draw, p0, p1, fill, width=4, jitter=2.2, steps=14, rng=None):
    rng = rng or random.Random(42)
    pts = []
    for i in range(steps + 1):
        t = i / steps
        x = p0[0] + (p1[0] - p0[0]) * t
        y = p0[1] + (p1[1] - p0[1]) * t
        if 0 < i < steps:
            x += rng.uniform(-jitter, jitter)
            y += rng.uniform(-jitter, jitter)
        pts.append((x, y))
    draw.line(pts, fill=fill, width=width, joint="curve")


def hand_rect(draw, box, outline, fill=None, width=5, jitter=2.5, rng=None):
    rng = rng or random.Random(101)
    x0, y0, x1, y1 = box
    if fill is not None:
        draw.rounded_rectangle(box, radius=10, fill=fill)
    jitter_line(draw, (x0, y0), (x1, y0), outline, width, jitter, 14, rng)
    jitter_line(draw, (x1, y0), (x1, y1), outline, width, jitter, 10, rng)
    jitter_line(draw, (x1, y1), (x0, y1), outline, width, jitter, 14, rng)
    jitter_line(draw, (x0, y1), (x0, y0), outline, width, jitter, 10, rng)


def hand_arrow(draw, p0, p1, fill, width=5, head=18, rng=None):
    rng = rng or random.Random(77)
    jitter_line(draw, p0, p1, fill, width, jitter=2.4, steps=14, rng=rng)
    ang = math.atan2(p1[1] - p0[1], p1[0] - p0[0])
    a1 = ang + math.pi * 0.80
    a2 = ang - math.pi * 0.80
    draw.line([p1, (p1[0] + head * math.cos(a1), p1[1] + head * math.sin(a1))], fill=fill, width=width)
    draw.line([p1, (p1[0] + head * math.cos(a2), p1[1] + head * math.sin(a2))], fill=fill, width=width)


def make_whiteboard_panel(W=1024, H=512, seed=1):
    """Porcelain off-white enamel base (2:1 aspect matching 1.86m x 0.96m board) with dry-erase smudges & frame AO."""
    rng = random.Random(seed)
    img = Image.new("RGB", (W, H), (234, 237, 240))
    px = img.load()

    for y in range(H):
        v = y / H
        for x in range(W):
            u = x / W
            glare = math.exp(-(((u * 0.85 + v * 0.45) - 0.58) / 0.14) ** 2) * 11.0
            edge_x = min(u, 1.0 - u) * 1.9
            edge_y = min(v, 1.0 - v)
            edge = min(edge_x, edge_y)
            ao = 1.0 - 0.14 * math.exp(-edge / 0.032)
            dust = 1.0 - 0.045 * math.exp(-((1.0 - v) / 0.09) ** 2)
            r = int(max(0, min(255, (230 + glare - 4 * v) * ao * dust)))
            g = int(max(0, min(255, (234 + glare - 3 * v) * ao * dust)))
            b = int(max(0, min(255, (238 + glare - 2 * v) * ao * dust)))
            px[x, y] = (r, g, b)

    smudge = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    sd = ImageDraw.Draw(smudge)
    ghost_cols = [(120, 145, 175, 22), (175, 120, 130, 18), (110, 130, 125, 18)]
    for _ in range(24):
        cx = rng.randint(70, W - 70)
        cy = rng.randint(50, H - 50)
        rx = rng.randint(36, 130)
        ry = rng.randint(14, 48)
        sd.ellipse((cx - rx, cy - ry, cx + rx, cy + ry), fill=rng.choice(ghost_cols))
    smudge = smudge.filter(ImageFilter.GaussianBlur(radius=14))
    img = Image.alpha_composite(img.convert("RGBA"), smudge).convert("RGB")
    return img


def gen_whiteboard_front_panel():
    rng = random.Random(2026)
    img = make_whiteboard_panel(1024, 512, seed=11)
    draw = ImageDraw.Draw(img)

    c_black = (34, 38, 46)
    c_blue = (24, 92, 186)
    c_red = (196, 36, 48)
    c_green = (26, 134, 72)
    c_purple = (118, 46, 156)
    c_orange = (205, 102, 22)

    f_title = load_font(HAND_BOLD, 28)
    f_sub = load_font(HAND_BOLD, 20)
    f_box = load_font(HAND_BOLD, 20)
    f_small = load_font(HAND_REG, 17)
    f_tiny = load_font(HAND_BOLD, 15)

    # 1. Header with cleanly spaced crossed-out versions
    draw.text((34, 16), "АРХИТЕКТУРА MVP", fill=c_black, font=f_title)
    draw.text((380, 21), "v1", fill=(115, 122, 132), font=f_sub)
    jitter_line(draw, (374, 38), (412, 26), c_red, width=3, jitter=1.2, rng=rng)
    draw.text((426, 21), "v2", fill=(115, 122, 132), font=f_sub)
    jitter_line(draw, (420, 38), (460, 26), c_red, width=3, jitter=1.2, rng=rng)
    draw.text((480, 16), "v4.2 (ФИНАЛ_ТОЧНО!)", fill=c_red, font=f_title)
    jitter_line(draw, (32, 56), (988, 58), c_blue, width=3, jitter=1.5, rng=rng)

    # 2. Main startup pipeline (left-to-right upper band)
    hand_rect(draw, (36, 76, 240, 174), c_blue, fill=(224, 236, 248), width=4, jitter=1.5, rng=rng)
    draw.text((52, 86), "ФРОНТЕНД", fill=c_blue, font=f_box)
    draw.text((52, 114), "вайб-код 99%", fill=c_black, font=f_small)
    draw.text((52, 140), "(не дышать!)", fill=c_red, font=f_tiny)

    hand_arrow(draw, (244, 124), (318, 124), c_black, width=4, head=12, rng=rng)
    draw.text((254, 96), "JSON?", fill=c_purple, font=f_tiny)

    hand_rect(draw, (324, 72, 586, 178), c_purple, fill=(238, 228, 246), width=4, jitter=1.5, rng=rng)
    draw.text((344, 82), "ИИ-АГЕНТ v9", fill=c_purple, font=f_box)
    draw.text((344, 112), "промпт 40 стр.", fill=c_black, font=f_small)
    draw.text((344, 140), "+ 100500 костылей", fill=c_red, font=f_tiny)

    hand_arrow(draw, (590, 124), (666, 124), c_red, width=4, head=13, rng=rng)
    draw.text((602, 96), "авось", fill=c_red, font=f_tiny)

    hand_rect(draw, (672, 74, 892, 178), c_red, fill=(249, 226, 228), width=5, jitter=1.5, rng=rng)
    draw.text((696, 84), "ПРОД !!!", fill=c_red, font=f_title)
    draw.text((692, 122), "ГОРИТ С ПТ", fill=c_black, font=f_small)
    draw.text((692, 148), "CPU: 146%", fill=c_red, font=f_tiny)

    # Loop-back red arrow ("бесконечный хотфикс")
    pts_arc = [(780, 180), (748, 214), (602, 222), (470, 212), (446, 182)]
    draw.line(pts_arc, fill=c_red, width=3, joint="curve")
    draw.line([(446, 182), (438, 198)], fill=c_red, width=3)
    draw.line([(446, 182), (460, 194)], fill=c_red, width=3)
    draw.text((514, 192), "бесконечный хотфикс", fill=c_red, font=f_tiny)

    # 3. Database Excel box (middle-left)
    hand_rect(draw, (38, 206, 272, 304), c_green, fill=(224, 244, 232), width=4, jitter=1.5, rng=rng)
    draw.text((54, 216), "БАЗА ДАННЫХ", fill=c_green, font=f_box)
    draw.text((54, 246), "base_final_2.xlsx", fill=c_black, font=f_small)
    draw.text((54, 274), "НЕ УДАЛЯТЬ!!!", fill=c_red, font=f_tiny)
    hand_arrow(draw, (274, 232), (350, 182), c_green, width=3, head=11, rng=rng)

    # 4. Crossed-out Sprint Deadlines (middle-right)
    hand_rect(draw, (398, 234, 962, 392), c_black, fill=(242, 243, 245), width=3, jitter=1.5, rng=rng)
    draw.text((416, 242), "СПРИНТ #14 — ДЕДЛАЙНЫ:", fill=c_black, font=f_sub)
    lines = [
        ("1. Релиз: понедельник", True, c_blue),
        ("2. Релиз: среда (точно)", True, c_blue),
        ("3. Релиз: ВЧЕРА В 18:00!", False, c_red),
        ("4. Починить кофемашину (P0!)", False, c_green),
        ("5. Догнать сбежавший ИИ", False, c_purple),
    ]
    for idx, (txt, struck, col) in enumerate(lines):
        ly = 272 + idx * 23
        draw.text((420, ly), txt, fill=col if not struck else (120, 126, 134), font=f_small)
        if struck:
            jitter_line(draw, (414, ly + 13), (684, ly + 12), c_red, width=3, jitter=0.9, rng=rng)
            draw.text((704, ly + 2), "ПЕРЕНОС", fill=c_red, font=f_tiny)

    # 5. Burndown chart (bottom-left)
    draw.text((44, 318), "СГОРАНИЕ ЗАДАЧ:", fill=c_black, font=f_sub)
    hand_arrow(draw, (52, 476), (52, 350), c_black, width=3, head=10, rng=rng)
    hand_arrow(draw, (52, 476), (344, 476), c_black, width=3, head=10, rng=rng)
    for s in range(9):
        t0, t1 = s / 9.0, (s + 0.55) / 9.0
        draw.line([(60 + t0 * 265, 376 + t0 * 92), (60 + t1 * 265, 376 + t1 * 92)], fill=c_green, width=3)
    draw.text((238, 452), "план", fill=c_green, font=f_tiny)
    actual_pts = [(60, 384), (108, 398), (154, 388), (198, 408), (242, 372), (284, 350), (332, 332)]
    draw.line(actual_pts, fill=c_red, width=4, joint="curve")
    draw.text((224, 336), "ФАКТ!", fill=c_red, font=f_box)

    # 6. Bottom-right office law + frog doodle
    draw.text((416, 404), "ЗАКОН ОФИСА:  Кофе x 3 = Код", fill=c_blue, font=f_sub)
    draw.text((416, 434), "Код x 0.1 = 12 багов в проде", fill=c_red, font=f_sub)
    draw.text((416, 464), "НЕ СТИРАТЬ ДО ПЯТНИЦЫ!!", fill=c_orange, font=f_sub)
    jitter_line(draw, (414, 494), (764, 494), c_orange, width=3, jitter=0.9, rng=rng)

    bx, by = 890, 444
    draw.ellipse((bx - 32, by - 22, bx + 32, by + 22), outline=c_green, width=3)
    draw.ellipse((bx - 22, by - 32, bx - 6, by - 16), outline=c_green, width=3)
    draw.ellipse((bx + 6, by - 32, bx + 22, by - 16), outline=c_green, width=3)
    draw.ellipse((bx - 16, by - 26, bx - 11, by - 21), fill=c_black)
    draw.ellipse((bx + 11, by - 26, bx + 16, by - 21), fill=c_black)
    draw.arc((bx - 16, by - 4, bx + 16, by + 14), 15, 165, fill=c_green, width=3)
    return img


def gen_whiteboard_back_panel():
    rng = random.Random(3030)
    img = make_whiteboard_panel(1024, 512, seed=22)
    draw = ImageDraw.Draw(img)

    c_black = (34, 38, 46)
    c_blue = (24, 92, 186)
    c_red = (196, 36, 48)
    c_green = (26, 134, 72)

    f_title = load_font(HAND_BOLD, 28)
    f_col = load_font(HAND_BOLD, 21)
    f_txt = load_font(HAND_REG, 18)

    draw.text((34, 16), "КАНБАН / ПОЖАРЫ ЭТАЖА", fill=c_black, font=f_title)
    jitter_line(draw, (32, 54), (988, 56), c_black, width=3, jitter=1.2, rng=rng)

    jitter_line(draw, (356, 62), (354, 356), c_blue, width=3, jitter=1.2, rng=rng)
    jitter_line(draw, (678, 62), (680, 356), c_blue, width=3, jitter=1.2, rng=rng)

    draw.text((48, 66), "НАДО ВЧЕРА", fill=c_red, font=f_col)
    draw.text((378, 66), "В РАБОТЕ", fill=c_blue, font=f_col)
    draw.text((704, 66), "ГОТОВО", fill=c_green, font=f_col)

    todo = ["- Фикс принтера", "- Кулер (пустой!)", "- Прод упал x4", "- Пароль от Wi-Fi", "- ЗП за март?"]
    wip = ["- Переписать всё", "  на Rust (3 мес)", "- Поймать ИИ-босса", "- Созвон про созвон"]
    done = ["- Купили пиццу", "- Выпили кофе", "- Закрыли 1 тикет", "  (открыли 7)"]
    for i, t in enumerate(todo):
        draw.text((46, 106 + i * 44), t, fill=c_black, font=f_txt)
    for i, t in enumerate(wip):
        draw.text((374, 106 + i * 48), t, fill=c_blue, font=f_txt)
    for i, t in enumerate(done):
        draw.text((700, 106 + i * 48), t, fill=c_green, font=f_txt)

    hand_rect(draw, (42, 368, 982, 488), c_red, fill=(248, 232, 234), width=4, jitter=1.5, rng=rng)
    draw.text((66, 382), "ВНИМАНИЕ: СЕРВЕРНАЯ ПЕРЕГРЕТА!", fill=c_red, font=f_title)
    draw.text((66, 422), "НЕ СТАВИТЬ ЧАЙНИК В СТОЙКУ #3 (СНОВА)", fill=c_black, font=f_col)
    draw.text((66, 454), "Подпись: Админ (ушёл в отпуск)", fill=c_blue, font=f_txt)
    return img


def gen_whiteboard_atlas():
    """Single 1024x1024 atlas: top 1024x512 = Front board, bottom 1024x512 = Back board."""
    atlas = Image.new("RGB", (1024, 1024), (234, 237, 240))
    atlas.paste(gen_whiteboard_front_panel(), (0, 0))
    atlas.paste(gen_whiteboard_back_panel(), (0, 512))
    atlas.save(OUT_DIR / "whiteboard_atlas.png")


def gen_sticky_notes_atlas():
    """2x2 atlas of 4 colorful paper sticky notes with subtle paper shading & Russian notes."""
    W, H = 512, 512
    img = Image.new("RGB", (W, H), (240, 220, 110))
    draw = ImageDraw.Draw(img)
    f_note = load_font(HAND_BOLD, 28)
    f_small = load_font(HAND_REG, 22)

    notes = [
        # (qx, qy, bg, adhesive, ink, lines)
        (0, 0, (248, 222, 92), (228, 198, 68), (38, 36, 34), ["ПРОД", "СНОВА", "ЛЕЖИТ!!"]),
        (256, 0, (246, 134, 152), (222, 108, 128), (44, 24, 30), ["ДЕМО", "В 15:00", "СРОЧНО"]),
        (0, 256, (132, 224, 156), (108, 198, 132), (24, 48, 32), ["КУПИТЬ", "КОФЕ", "И СЫР"]),
        (256, 256, (128, 204, 246), (102, 178, 222), (22, 42, 66), ["КТО", "СЛОМАЛ", "MAIN?!"]),
    ]
    rng = random.Random(55)
    for qx, qy, bg, adh, ink, lines in notes:
        draw.rectangle((qx, qy, qx + 255, qy + 255), fill=bg)
        # Subtle paper fiber speckle
        px = img.load()
        for y in range(qy, qy + 256):
            curl = (y - qy) / 255.0
            shade = 0.94 + 0.08 * math.sin(curl * math.pi * 0.7)
            for x in range(qx, qx + 256):
                n = rng.randint(-4, 4)
                base = adh if (y - qy) < 34 else bg
                px[x, y] = (
                    max(0, min(255, int(base[0] * shade + n))),
                    max(0, min(255, int(base[1] * shade + n))),
                    max(0, min(255, int(base[2] * shade + n))),
                )
        for li, line in enumerate(lines):
            draw.text((qx + 28, qy + 58 + li * 52), line, fill=ink, font=f_note if li == 0 else f_small)
        # Subtle bottom-right corner curl shadow
        draw.polygon([(qx + 220, qy + 255), (qx + 255, qy + 220), (qx + 255, qy + 255)], fill=(int(bg[0]*0.82), int(bg[1]*0.82), int(bg[2]*0.82)))

    img.save(OUT_DIR / "sticky_notes_atlas.png")


def gen_cooler_body_tex():
    """Warm matte-ivory powder-coated finish with fine micro-grain & subtle vertical shading."""
    W, H = 512, 512
    img = Image.new("RGB", (W, H), (222, 219, 212))
    px = img.load()
    rng = random.Random(88)
    for y in range(H):
        v = y / H
        # Slight grounding gradient near floor (bottom) and soft studio highlight upper body
        v_shade = 0.91 + 0.09 * math.sin(v * math.pi * 0.85)
        for x in range(W):
            u = x / W
            grain = rng.uniform(-3.0, 3.0) + 2.2 * math.sin(u * math.tau * 24.0) * math.cos(v * math.tau * 24.0)
            # Subtle brushed horizontal sheen
            sheen = 4.0 * math.sin((u - 0.35) * math.pi)
            r = int(max(0, min(255, (218 + sheen) * v_shade + grain)))
            g = int(max(0, min(255, (214 + sheen) * v_shade + grain)))
            b = int(max(0, min(255, (206 + sheen) * v_shade + grain)))
            px[x, y] = (r, g, b)

    # Subtle brand badge / care strip painted near top front
    draw = ImageDraw.Draw(img)
    f_brand = load_font(UI_BOLD, 20)
    draw.rounded_rectangle((176, 40, 336, 72), radius=6, fill=(44, 52, 62))
    draw.text((196, 45), "AQUA-VIBE", fill=(176, 218, 242), font=f_brand)
    img.save(OUT_DIR / "cooler_body_tex.png")


def gen_cooler_jug_tex():
    """Sky-blue translucent polycarbonate jug texture with caustics, rib highlights & water meniscus."""
    W, H = 512, 512
    img = Image.new("RGB", (W, H), (120, 195, 242))
    px = img.load()
    for y in range(H):
        # v=0 top of image -> high Z; v=1 bottom -> low Z (remember PIL y=0 is top, Blender UV v=1 is top!)
        v = 1.0 - (y / (H - 1))
        # Meniscus band around v = 0.72 (water level inside 19L jug!)
        meniscus = math.exp(-((v - 0.72) / 0.022) ** 2)
        # Subtle rib refraction bands matching 3 jug ribs
        rib_hl = (
            math.exp(-((v - 0.24) / 0.03) ** 2)
            + math.exp(-((v - 0.45) / 0.03) ** 2)
            + math.exp(-((v - 0.65) / 0.03) ** 2)
        ) * 0.20
        # Below water level = richer deeper sky-cyan; above = airy icy translucent glass
        in_water = 1.0 / (1.0 + math.exp((v - 0.72) * 28.0))
        for x in range(W):
            u = x / W
            # Vertical glossy studio specular streaks
            streak = (
                math.exp(-((u - 0.22) / 0.055) ** 2) * 0.36
                + math.exp(-((u - 0.74) / 0.075) ** 2) * 0.22
            )
            caustic = 0.06 * math.sin(u * math.tau * 8.0 + math.sin(v * math.tau * 6.0)) * in_water
            r = 88 * in_water + 162 * (1.0 - in_water) + 95 * meniscus + 85 * streak + 30 * rib_hl + 25 * caustic
            g = 178 * in_water + 216 * (1.0 - in_water) + 75 * meniscus + 58 * streak + 25 * rib_hl + 25 * caustic
            b = 236 * in_water + 244 * (1.0 - in_water) + 25 * meniscus + 28 * streak + 16 * rib_hl + 15 * caustic
            px[x, y] = (int(max(0, min(255, r))), int(max(0, min(255, g))), int(max(0, min(255, b))))

    # Subtle condensation droplets in air pocket above water line (y near top)
    draw = ImageDraw.Draw(img)
    rng = random.Random(71)
    for _ in range(95):
        dx = rng.randint(10, W - 10)
        dy = rng.randint(24, int(H * 0.26))
        rad = rng.randint(2, 5)
        draw.ellipse((dx - rad, dy - rad, dx + rad, dy + rad), fill=(224, 246, 255))

    img.save(OUT_DIR / "cooler_jug_tex.png")


def gen_copier_panel_tex():
    """High-craft Russian MFP copier touchscreen UI + physical keypad graphics."""
    W, H = 512, 512
    img = Image.new("RGB", (W, H), (30, 34, 42))
    draw = ImageDraw.Draw(img)

    f_hdr = load_font(UI_BOLD, 22)
    f_warn = load_font(UI_BOLD, 24)
    f_btn = load_font(UI_BOLD, 19)
    f_key = load_font(UI_MONO, 22)
    f_tiny = load_font(UI_MONO, 15)

    # Outer dark slate bezel
    draw.rounded_rectangle((10, 10, 502, 502), radius=18, fill=(24, 28, 36), outline=(58, 66, 80), width=4)

    # Left-side glowing LCD touchscreen (x: 24..332, y: 28..484)
    draw.rounded_rectangle((24, 28, 332, 484), radius=10, fill=(14, 26, 40), outline=(66, 94, 124), width=3)
    # Top status bar
    draw.rectangle((27, 31, 329, 76), fill=(24, 58, 92))
    draw.text((38, 40), "XEROX-9000 PRO", fill=(212, 238, 255), font=f_hdr)
    draw.ellipse((298, 42, 318, 62), fill=(52, 235, 132))

    # Amber/Red paper jam alert banner!
    draw.rounded_rectangle((38, 92, 318, 198), radius=8, fill=(92, 32, 22), outline=(245, 162, 42), width=3)
    draw.polygon([(58, 174), (82, 118), (106, 174)], fill=(250, 186, 46))
    draw.text((77, 134), "!", fill=(30, 18, 12), font=f_warn)
    draw.text((120, 108), "ЗАМЯТИЕ!", fill=(255, 206, 98), font=f_warn)
    draw.text((120, 142), "ЛОТОК 2 / ПРОПУСК", fill=(252, 230, 205), font=f_tiny)
    draw.text((120, 166), "КОД: ERR-042", fill=(240, 140, 120), font=f_tiny)

    # 2x2 interactive function tiles on touchscreen
    tiles = [
        (38, 218, 170, 314, (32, 96, 152), "КОПИЯ"),
        (184, 218, 318, 314, (28, 112, 98), "СКАН"),
        (38, 328, 170, 424, (124, 78, 32), "ПРОПУСК"),
        (184, 328, 318, 424, (86, 46, 114), "СЕТЬ"),
    ]
    for x0, y0, x1, y1, col, lbl in tiles:
        draw.rounded_rectangle((x0, y0, x1, y1), radius=8, fill=col, outline=(140, 195, 235), width=2)
        draw.text((x0 + 18, y0 + 34), lbl, fill=(238, 248, 255), font=f_btn)

    # Bottom toner level bars on LCD
    draw.text((40, 444), "ТОНЕР: C  M  Y  K", fill=(168, 202, 228), font=f_tiny)
    for idx, (tc, lvl) in enumerate([((42, 188, 236), 0.72), ((224, 58, 148), 0.55), ((242, 204, 48), 0.85), ((200, 210, 220), 0.18)]):
        bx = 196 + idx * 30
        draw.rectangle((bx, 444, bx + 20, 468), fill=(26, 36, 48), outline=(88, 108, 128))
        h_fill = int(20 * lvl)
        draw.rectangle((bx + 2, 466 - h_fill, bx + 18, 466), fill=tc)

    # Right-side physical keypad + glowing START/STOP buttons (x: 348..490)
    keys = ["1", "2", "3", "4", "5", "6", "7", "8", "9", "*", "0", "#"]
    for idx, k in enumerate(keys):
        r, c = divmod(idx, 3)
        kx0 = 352 + c * 46
        ky0 = 44 + r * 54
        draw.rounded_rectangle((kx0, ky0, kx0 + 38, ky0 + 44), radius=6, fill=(52, 58, 70), outline=(92, 100, 116), width=2)
        draw.text((kx0 + 12, ky0 + 10), k, fill=(228, 234, 244), font=f_key)

    # Big amber Reset/Clear button + glowing Green START button + Red STOP
    draw.rounded_rectangle((352, 276, 484, 326), radius=8, fill=(214, 146, 32), outline=(255, 212, 118), width=2)
    draw.text((380, 290), "СБРОС", fill=(32, 22, 10), font=f_btn)

    draw.rounded_rectangle((352, 342, 484, 408), radius=10, fill=(32, 186, 92), outline=(146, 252, 182), width=3)
    draw.text((380, 363), "СТАРТ", fill=(242, 255, 246), font=f_hdr)

    draw.rounded_rectangle((352, 422, 484, 476), radius=8, fill=(196, 42, 48), outline=(248, 134, 138), width=2)
    draw.text((386, 438), "СТОП", fill=(255, 240, 240), font=f_btn)

    img.save(OUT_DIR / "copier_panel_tex.png")


def gen_copier_body_tex():
    """Warm industrial office copier polymer shell with micro-grain, seam lines & Russian asset tag."""
    W, H = 512, 512
    img = Image.new("RGB", (W, H), (206, 208, 212))
    px = img.load()
    rng = random.Random(99)
    for y in range(H):
        v = y / H
        shade = 0.92 + 0.08 * math.sin(v * math.pi * 0.9)
        for x in range(W):
            u = x / W
            grain = rng.uniform(-3.5, 3.5)
            # Subtle molded panel ribs / seams
            seam = 0.92 if (abs(v - 0.33) < 0.006 or abs(v - 0.66) < 0.006) else 1.0
            r = int(max(0, min(255, 206 * shade * seam + grain)))
            g = int(max(0, min(255, 207 * shade * seam + grain)))
            b = int(max(0, min(255, 209 * shade * seam + grain)))
            px[x, y] = (r, g, b)

    draw = ImageDraw.Draw(img)
    f_tag = load_font(UI_MONO, 13)
    # Tiny metallic Russian inventory barcode sticker
    draw.rounded_rectangle((340, 410, 480, 470), radius=4, fill=(235, 238, 242), outline=(92, 98, 108), width=2)
    draw.text((348, 415), "ИНВ № 04-8821", fill=(28, 32, 40), font=f_tag)
    for bx in range(350, 470, 5):
        bw = 2 if (bx * 7) % 3 else 1
        draw.rectangle((bx, 435, bx + bw, 462), fill=(26, 28, 34))

    img.save(OUT_DIR / "copier_body_tex.png")


def gen_printed_sheet_tex():
    """Freshly printed Russian temporary security pass / office memo sheet."""
    W, H = 512, 512
    img = Image.new("RGB", (W, H), (242, 240, 234))
    draw = ImageDraw.Draw(img)
    f_hdr = load_font(UI_BOLD, 30)
    f_txt = load_font(UI_MONO, 20)
    f_stamp = load_font(UI_BOLD, 28)

    draw.rectangle((26, 26, 486, 486), outline=(44, 50, 62), width=4)
    draw.rectangle((26, 26, 486, 98), fill=(36, 54, 82))
    draw.text((62, 46), "ПРОПУСК № 404", fill=(240, 246, 255), font=f_hdr)

    # Photo silhouette box
    draw.rectangle((52, 128, 192, 294), fill=(214, 220, 228), outline=(58, 66, 80), width=3)
    draw.ellipse((96, 152, 148, 208), fill=(92, 102, 118))
    draw.chord((72, 214, 172, 310), 180, 360, fill=(92, 102, 118))

    # Fake printed lines
    draw.text((216, 134), "ОТДЕЛ: ВАЙБ-ИТ", fill=(32, 36, 44), font=f_txt)
    draw.text((216, 172), "ДОСТУП: ЭТАЖ 1", fill=(32, 36, 44), font=f_txt)
    draw.text((216, 210), "СТАТУС: СТАЖЁР", fill=(32, 36, 44), font=f_txt)
    for ly in range(258, 304, 18):
        draw.rectangle((216, ly, 454, ly + 8), fill=(116, 122, 132))

    # Body text lines
    for i in range(4):
        ly = 330 + i * 26
        draw.rectangle((52, ly, 440 - (i % 2) * 80, ly + 10), fill=(92, 98, 108))

    # Red stamp "ОДОБРЕНО"
    draw.rounded_rectangle((260, 392, 458, 456), radius=6, outline=(196, 38, 44), width=5)
    draw.text((278, 408), "ОДОБРЕНО", fill=(196, 38, 44), font=f_stamp)

    img.save(OUT_DIR / "printed_sheet_tex.png")


if __name__ == "__main__":
    gen_whiteboard_atlas()
    gen_sticky_notes_atlas()
    gen_cooler_body_tex()
    gen_cooler_jug_tex()
    gen_copier_panel_tex()
    gen_copier_body_tex()
    gen_printed_sheet_tex()
    print("Generated all Wave 1 prop textures in:", OUT_DIR)
