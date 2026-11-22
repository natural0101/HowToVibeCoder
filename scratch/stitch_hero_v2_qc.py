import os
from PIL import Image, ImageDraw

PROJ = r"C:\Users\se-20\.gemini\antigravity\scratch\HowToVibeCoder"
TILES = os.path.join(PROJ, "scratch", "hero_v2_qc_tiles")


def make_sheet(items, cols, cell_w, cell_h, out_path, title):
    rows = (len(items) + cols - 1) // cols
    pad = 12
    header = 40
    W = cols * cell_w + (cols + 1) * pad
    H = header + rows * (cell_h + 26) + (rows + 1) * pad
    canvas = Image.new("RGB", (W, H), (22, 24, 30))
    draw = ImageDraw.Draw(canvas)
    draw.text((pad, 12), title, fill=(235, 228, 210))
    for i, (fname, label) in enumerate(items):
        r, c = divmod(i, cols)
        x = pad + c * (cell_w + pad)
        y = header + pad + r * (cell_h + 26 + pad)
        p = os.path.join(TILES, fname)
        if os.path.exists(p):
            im = Image.open(p).convert("RGB")
            im.thumbnail((cell_w, cell_h), Image.Resampling.LANCZOS)
            ox = x + (cell_w - im.width) // 2
            oy = y + (cell_h - im.height) // 2
            canvas.paste(im, (ox, oy))
        draw.rectangle([x, y, x + cell_w, y + cell_h], outline=(68, 72, 84), width=1)
        draw.text((x + 4, y + cell_h + 5), label, fill=(215, 210, 198))
    canvas.save(out_path)
    print("SAVED:", out_path, canvas.size)


turnaround_items = [
    ("t_front.png", "1. FULL FRONT (+Z)"),
    ("t_34.png", "2. FULL 3/4 HERO"),
    ("t_side.png", "3. FULL SIDE (+X)"),
    ("t_back.png", "4. FULL BACK (-Z)"),
    ("t_face_front.png", "5. FACE CLOSE-UP (FRONT)"),
    ("t_face_34.png", "6. FACE CLOSE-UP (3/4)"),
    ("t_hand_r.png", "7. RIGHT HAND (5 FINGERS + SIGNET)"),
    ("t_hand_l.png", "8. LEFT HAND (5 FINGERS + WATCH)"),
    ("t_white_sochi.png", "9. WHITE 'SOCHI-98' TINT + AO FOLDS"),
]
make_sheet(
    turnaround_items, 3, 460, 560,
    os.path.join(PROJ, "scratch", "hero_v2_turnaround_qc.png"),
    "HERO V2 TURNAROUND QC — SCULPTED ANATOMY, FACE, 5-FINGERED HANDS & TAILORED BLAZER"
)

hats_eyes_items = [
    ("he_hat_crown.png", "1. hat_crown"),
    ("he_hat_ushanka.png", "2. hat_ushanka"),
    ("he_hat_frog.png", "3. hat_frog_head"),
    ("he_hat_cowboy.png", "4. hat_cowboy"),
    ("he_hat_tinfoil.png", "5. hat_tinfoil"),
    ("he_hat_cone.png", "6. hat_traffic_cone"),
    ("he_hat_chef.png", "7. hat_chef"),
    ("he_hat_pirate.png", "8. hat_pirate"),
    ("he_eye_aviators.png", "9. eye_aviators"),
    ("he_eye_pixel.png", "10. eye_dealwithit"),
    ("he_eye_accountant.png", "11. eye_accountant"),
    ("he_eye_matrix.png", "12. eye_matrix"),
    ("he_bare_blue.png", "13. BARE BLUE IRIS"),
    ("he_bare_green.png", "14. BARE GREEN IRIS"),
    ("he_combo_side.png", "15. SIDE PROFILE: CROWN + AVIATORS"),
    ("he_combo_sochi.png", "16. COMBO: WHITE SOCHI + CROWN + PIXEL"),
]
make_sheet(
    hats_eyes_items, 4, 380, 380,
    os.path.join(PROJ, "scratch", "hero_v2_hats_eyes_qc.png"),
    "HERO V2 HATS & EYEWEAR FIT QC — ALL 8 HATS, 4 EYEWEAR & BARE IRIS TINTS"
)
