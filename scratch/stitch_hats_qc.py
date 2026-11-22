import os
from PIL import Image, ImageDraw

PROJ = r"C:\Users\se-20\.gemini\antigravity\scratch\HowToVibeCoder"
TMP = os.path.join(PROJ, "scratch", "qc_tiles")

def make_sheet(items, cols, out_path, title):
    tw, th = 420, 420
    pad = 12
    lbl_h = 30
    rows = (len(items) + cols - 1) // cols
    W = cols * tw + (cols + 1) * pad
    H = 44 + rows * (th + lbl_h + pad) + pad
    canvas = Image.new("RGB", (W, H), (24, 26, 34))
    draw = ImageDraw.Draw(canvas)
    draw.text((pad, 12), title, fill=(240, 220, 150))
    for idx, (fname, label) in enumerate(items):
        r, c = divmod(idx, cols)
        x = pad + c * (tw + pad)
        y = 44 + r * (th + lbl_h + pad)
        im = Image.open(os.path.join(TMP, fname)).convert("RGB")
        canvas.paste(im, (x, y))
        draw.rectangle([(x, y + th), (x + tw, y + th + lbl_h)], fill=(36, 39, 50))
        draw.text((x + 10, y + th + 8), label, fill=(235, 240, 248))
    canvas.save(out_path)
    print(f"Saved {out_path} ({W}x{H})")

# Main contact sheet: all 8 hats (3/4) + all 4 eyewear (3/4) = 12 tiles (4x3 grid)
main_items = [
    ("hat_tinfoil_34.png", "1. hat_tinfoil (3/4)"),
    ("hat_ushanka_34.png", "2. hat_ushanka (3/4)"),
    ("hat_crown_34.png", "3. hat_crown (3/4)"),
    ("hat_traffic_cone_34.png", "4. hat_traffic_cone (3/4)"),
    ("hat_cowboy_34.png", "5. hat_cowboy (3/4)"),
    ("hat_chef_34.png", "6. hat_chef (3/4)"),
    ("hat_pirate_34.png", "7. hat_pirate (3/4)"),
    ("hat_frog_head_34.png", "8. hat_frog_head (3/4)"),
    ("eye_aviators_34.png", "9. eye_aviators (3/4)"),
    ("eye_matrix_34.png", "10. eye_matrix (3/4)"),
    ("eye_accountant_34.png", "11. eye_accountant (3/4)"),
    ("eye_dealwithit_34.png", "12. eye_dealwithit (3/4)"),
]
make_sheet(main_items, 4, os.path.join(PROJ, "scratch", "char_hats_qc.png"),
           "QC CONTACT SHEET — 8 HATS & 4 EYEWEAR MOUNTED ON char_partner_90s.glb (3/4 VIEW)")

# Side-profile QC sheet: all 8 hats + 4 eyewear side profiles to verify ZERO floating/clipping!
side_items = [
    ("hat_tinfoil_side.png", "1. tinfoil SIDE"),
    ("hat_ushanka_side.png", "2. ushanka SIDE"),
    ("hat_crown_side.png", "3. crown SIDE"),
    ("hat_traffic_cone_side.png", "4. traffic_cone SIDE"),
    ("hat_cowboy_side.png", "5. cowboy SIDE"),
    ("hat_chef_side.png", "6. chef SIDE"),
    ("hat_pirate_side.png", "7. pirate SIDE"),
    ("hat_frog_head_side.png", "8. frog_head SIDE"),
    ("eye_aviators_side.png", "9. aviators SIDE"),
    ("eye_matrix_side.png", "10. matrix SIDE"),
    ("eye_accountant_side.png", "11. accountant SIDE"),
    ("eye_dealwithit_side.png", "12. dealwithit SIDE"),
]
make_sheet(side_items, 4, os.path.join(PROJ, "scratch", "char_hats_side_qc.png"),
           "SIDE PROFILE FIT QC — ZERO FLOATING / ZERO HAIR OR EAR CLIPPING")
