import os, math, random
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

OUT_DIR = r"C:\Users\se-20\.gemini\antigravity\scratch\HowToVibeCoder\scratch\hat_textures"
os.makedirs(OUT_DIR, exist_ok=True)
random.seed(1998)
np.random.seed(1998)

def save_tex(img, name):
    path = os.path.join(OUT_DIR, name)
    img.save(path)
    print(f"Saved {name} ({img.size[0]}x{img.size[1]} {img.mode})")

def fbm_noise(h, w, octaves=(4, 8, 16, 32), weights=(0.45, 0.30, 0.15, 0.10), seed=42):
    rng = np.random.default_rng(seed)
    acc = np.zeros((h, w), dtype=np.float32)
    for freq, wt in zip(octaves, weights):
        small = rng.random((freq, freq), dtype=np.float32)
        im = Image.fromarray((small * 255).astype(np.uint8), mode='L').resize((w, h), Image.BICUBIC)
        acc += (np.asarray(im, dtype=np.float32) / 255.0) * wt
    acc = (acc - acc.min()) / max(1e-6, acc.max() - acc.min())
    return acc

# 1. TINFOIL: faceted crinkled aluminum foil with sharp crease highlights
def make_tinfoil():
    S = 512
    # Create voronoi-like faceted crinkle cells
    rng = np.random.default_rng(101)
    pts = rng.uniform(0, S, size=(140, 2))
    yy, xx = np.mgrid[0:S, 0:S]
    # Periodic distance for tileability
    best1 = np.full((S, S), 1e9, dtype=np.float32)
    best2 = np.full((S, S), 1e9, dtype=np.float32)
    cell_shade = np.zeros((S, S), dtype=np.float32)
    shades = rng.uniform(0.68, 0.98, size=len(pts))
    for i, (px, py) in enumerate(pts):
        dx = np.abs(xx - px)
        dx = np.minimum(dx, S - dx)
        dy = np.abs(yy - py)
        dy = np.minimum(dy, S - dy)
        d = np.sqrt(dx * dx + dy * dy)
        m1 = d < best1
        best2 = np.where(m1, best1, np.minimum(best2, d))
        best1 = np.where(m1, d, best1)
        cell_shade = np.where(m1, shades[i], cell_shade)
    edge = np.clip((best2 - best1) / 5.5, 0.0, 1.0)
    # Sharp bright/dark creases at cell boundaries
    crease = 0.72 + 0.28 * edge
    fine = fbm_noise(S, S, (16, 32, 64), (0.5, 0.3, 0.2), seed=102)
    val = np.clip(cell_shade * crease * (0.90 + 0.16 * fine), 0.48, 1.0)
    # Cool silver-blue metallic aluminum tint
    r = np.clip(val * 226, 0, 255)
    g = np.clip(val * 232, 0, 255)
    b = np.clip(val * 242, 0, 255)
    arr = np.stack([r, g, b], axis=-1).astype(np.uint8)
    save_tex(Image.fromarray(arr, 'RGB'), "tex_tinfoil.png")

# 2. USHANKA FUR: plush dark charcoal-brown sculpted faux fur
def make_ushanka_fur():
    S = 512
    base = fbm_noise(S, S, (6, 14, 28, 64), (0.35, 0.30, 0.20, 0.15), seed=201)
    # Combed directional fur streaks (vertical/slightly wavy)
    img = Image.new('RGB', (S, S))
    arr = np.zeros((S, S, 3), dtype=np.float32)
    # Charcoal-brown palette: (46,35,29) to (92,72,58)
    arr[:, :, 0] = 44 + base * 48
    arr[:, :, 1] = 33 + base * 38
    arr[:, :, 2] = 27 + base * 30
    img = Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8), 'RGB')
    draw = ImageDraw.Draw(img)
    rng = random.Random(202)
    for _ in range(5500):
        x = rng.randint(0, S - 1)
        y = rng.randint(0, S - 1)
        length = rng.randint(8, 22)
        dx = rng.randint(-4, 4)
        shade = rng.choice([-18, -10, 12, 22, 32])
        c0 = int(np.clip(58 + shade, 20, 130))
        c1 = int(np.clip(45 + shade * 0.85, 16, 108))
        c2 = int(np.clip(36 + shade * 0.72, 14, 92))
        draw.line([(x, y), (x + dx, y + length)], fill=(c0, c1, c2), width=rng.choice([1, 2]))
    img = img.filter(ImageFilter.GaussianBlur(radius=0.5))
    save_tex(img, "tex_ushanka_fur.png")

# 3. USHANKA CROWN CLOTH: dark Soviet olive-brown quilted wool
def make_ushanka_cloth():
    S = 512
    n = fbm_noise(S, S, (8, 24, 64), (0.4, 0.35, 0.25), seed=205)
    arr = np.zeros((S, S, 3), dtype=np.float32)
    arr[:, :, 0] = 52 + n * 24
    arr[:, :, 1] = 48 + n * 22
    arr[:, :, 2] = 38 + n * 18
    img = Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8), 'RGB')
    draw = ImageDraw.Draw(img)
    step = 48
    for o in range(-S, S * 2, step):
        draw.line([(o, 0), (o + S, S)], fill=(34, 31, 24), width=2)
        draw.line([(o, S), (o + S, 0)], fill=(34, 31, 24), width=2)
    save_tex(img, "tex_ushanka_cloth.png")

# 4. VINTAGE BRASS / GOLD
def make_brass():
    S = 512
    n = fbm_noise(S, S, (4, 16, 48), (0.4, 0.35, 0.25), seed=301)
    yy = np.linspace(0, 1, S)[:, None]
    brush = np.sin(yy * 180.0) * 0.04 + (np.random.default_rng(302).random((S, 1)) - 0.5) * 0.06
    v = np.clip(0.75 + 0.25 * n + brush, 0.45, 1.0)
    arr = np.stack([v * 226, v * 176, v * 72], axis=-1)
    save_tex(Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8), 'RGB'), "tex_brass.png")

# 5. BURGER KING CROWN CARDBOARD FOIL
def make_crown_gold():
    S = 512
    n = fbm_noise(S, S, (6, 18, 48), (0.45, 0.35, 0.20), seed=401)
    arr = np.zeros((S, S, 3), dtype=np.float32)
    # Rich metallic honey-gold foil paper
    arr[:, :, 0] = 212 + n * 40
    arr[:, :, 1] = 160 + n * 44
    arr[:, :, 2] = 34 + n * 28
    # Horizontal subtle cardboard fold lines & embossed fast-food band near bottom (v in 0..0.28)
    img = Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8), 'RGB')
    draw = ImageDraw.Draw(img)
    # Paper crease lines vertical (for the 7 fold seams)
    for k in range(14):
        x = int((k + 0.5) * S / 14.0)
        draw.line([(x, 0), (x, S)], fill=(182, 130, 24), width=2)
        draw.line([(x + 2, 0), (x + 2, S)], fill=(252, 218, 92), width=1)
    # Embossed royal crimson-gold headband strip near bottom of UV (high Y in PIL = low V in Blender!)
    y_top = int(S * 0.74)
    y_bot = int(S * 0.90)
    draw.rectangle([(0, y_top), (S, y_bot)], fill=(156, 26, 28))
    draw.rectangle([(0, y_top), (S, y_top + 6)], fill=(248, 210, 82))
    draw.rectangle([(0, y_bot - 6), (S, y_bot)], fill=(248, 210, 82))
    # Little gold diamond motifs along band
    for i in range(14):
        cx = int((i + 0.5) * S / 14.0)
        cy = (y_top + y_bot) // 2
        draw.polygon([(cx, cy - 12), (cx + 9, cy), (cx, cy + 12), (cx - 9, cy)], fill=(246, 202, 74))
    save_tex(img, "tex_crown_gold.png")

# 6. TRAFFIC CONE SAFETY ORANGE RUBBER (with subtle street scuffs)
def make_traffic_cone():
    S = 512
    n = fbm_noise(S, S, (6, 16, 48), (0.45, 0.35, 0.20), seed=501)
    arr = np.zeros((S, S, 3), dtype=np.float32)
    arr[:, :, 0] = 238 + n * 17
    arr[:, :, 1] = 82 + n * 24
    arr[:, :, 2] = 14 + n * 12
    img = Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8), 'RGB')
    draw = ImageDraw.Draw(img)
    rng = random.Random(502)
    # Subtle road scuffs
    for _ in range(90):
        x = rng.randint(0, S - 1)
        y = rng.randint(int(S * 0.45), S - 1)
        w = rng.randint(6, 24)
        h = rng.randint(2, 6)
        draw.ellipse([(x, y), (x + w, y + h)], fill=(185, 68, 22))
    img = img.filter(ImageFilter.GaussianBlur(radius=0.8))
    save_tex(img, "tex_traffic_orange.png")

    # Crisp white retroreflective stripe texture (micro-prismatic honeycomb feel)
    n2 = fbm_noise(S, S, (32, 64), (0.5, 0.5), seed=503)
    warr = np.zeros((S, S, 3), dtype=np.float32)
    warr[:, :, 0] = 236 + n2 * 19
    warr[:, :, 1] = 240 + n2 * 15
    warr[:, :, 2] = 246 + n2 * 9
    save_tex(Image.fromarray(np.clip(warr, 0, 255).astype(np.uint8), 'RGB'), "tex_reflective_white.png")

# 7. COWBOY DARK ESPRESSO LEATHER + BRAIDED BAND
def make_cowboy_leather():
    S = 512
    n1 = fbm_noise(S, S, (6, 16, 40, 96), (0.35, 0.30, 0.20, 0.15), seed=601)
    arr = np.zeros((S, S, 3), dtype=np.float32)
    # Rich dark espresso leather (#362016 .. #583524)
    arr[:, :, 0] = 52 + n1 * 42
    arr[:, :, 1] = 30 + n1 * 26
    arr[:, :, 2] = 20 + n1 * 18
    save_tex(Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8), 'RGB'), "tex_cowboy_leather.png")

    # Braided leather + gold strand hatband
    img = Image.new('RGB', (S, 128), (74, 42, 24))
    draw = ImageDraw.Draw(img)
    for x in range(-128, S + 128, 20):
        draw.line([(x, 0), (x + 80, 128)], fill=(124, 74, 38), width=7)
        draw.line([(x + 10, 0), (x + 90, 128)], fill=(44, 24, 14), width=4)
        draw.line([(x + 80, 0), (x, 128)], fill=(198, 152, 62), width=4)
    save_tex(img, "tex_cowboy_band.png")

# 8. CHEF CRISP STARCHED WHITE COTTON TWILL
def make_chef_cotton():
    S = 512
    n = fbm_noise(S, S, (8, 24, 64), (0.4, 0.35, 0.25), seed=701)
    yy, xx = np.mgrid[0:S, 0:S]
    twill = 0.5 + 0.5 * np.sin((xx + yy) * 0.9)
    v = np.clip(0.92 + 0.05 * n + 0.025 * twill, 0.85, 1.0)
    arr = np.stack([v * 252, v * 250, v * 244], axis=-1)
    save_tex(Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8), 'RGB'), "tex_chef_cotton.png")

# 9. PIRATE WEATHERED LEATHER + ANTIQUE GOLD BRAID
def make_pirate_leather():
    S = 512
    n = fbm_noise(S, S, (5, 14, 36, 80), (0.40, 0.28, 0.20, 0.12), seed=801)
    arr = np.zeros((S, S, 3), dtype=np.float32)
    # Deep weathered charcoal-mahogany leather
    arr[:, :, 0] = 38 + n * 38
    arr[:, :, 1] = 28 + n * 26
    arr[:, :, 2] = 24 + n * 20
    img = Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8), 'RGB')
    draw = ImageDraw.Draw(img)
    rng = random.Random(802)
    for _ in range(180):
        x, y = rng.randint(0, S-1), rng.randint(0, S-1)
        dx, dy = rng.randint(-28, 28), rng.randint(-12, 12)
        draw.line([(x, y), (x+dx, y+dy)], fill=(78, 56, 42), width=1)
    save_tex(img, "tex_pirate_leather.png")

    # Antique gold bullion braid
    bimg = Image.new('RGB', (S, 128), (156, 114, 38))
    bdraw = ImageDraw.Draw(bimg)
    for x in range(-128, S + 128, 16):
        bdraw.line([(x, 0), (x + 64, 128)], fill=(238, 194, 78), width=6)
        bdraw.line([(x + 7, 0), (x + 71, 128)], fill=(104, 74, 22), width=3)
    save_tex(bimg, "tex_pirate_braid.png")

# 10. JABA-FROG EMERALD GLOSSY SKIN
def make_frog_skin():
    S = 512
    n = fbm_noise(S, S, (6, 16, 40), (0.45, 0.35, 0.20), seed=901)
    arr = np.zeros((S, S, 3), dtype=np.float32)
    arr[:, :, 0] = 28 + n * 36
    arr[:, :, 1] = 148 + n * 62
    arr[:, :, 2] = 62 + n * 34
    img = Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8), 'RGB')
    draw = ImageDraw.Draw(img)
    rng = random.Random(902)
    # Cute darker jade organic frog mottling spots
    for _ in range(65):
        cx, cy = rng.randint(20, S - 20), rng.randint(20, S - 20)
        rx = rng.randint(10, 28)
        ry = rng.randint(8, 22)
        draw.ellipse([(cx - rx, cy - ry), (cx + rx, cy + ry)], fill=(22, 112, 48))
    img = img.filter(ImageFilter.GaussianBlur(radius=3.0))
    save_tex(img, "tex_frog_skin.png")

# 11. ACCOUNTANT TORTOISESHELL ACETATE
def make_tortoiseshell():
    S = 512
    n = fbm_noise(S, S, (6, 12, 24), (0.5, 0.3, 0.2), seed=1001)
    # Rich dark espresso + warm honey-amber tortoiseshell flecks
    amber = np.clip((n - 0.52) * 3.8, 0.0, 1.0)
    r = 34 * (1 - amber) + 178 * amber
    g = 20 * (1 - amber) + 92 * amber
    b = 12 * (1 - amber) + 24 * amber
    arr = np.stack([r, g, b], axis=-1)
    save_tex(Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8), 'RGB'), "tex_tortoiseshell.png")

# 12. AVIATOR SMOKED AMBER GRADIENT LENS
def make_aviator_lens():
    S = 256
    yy = np.linspace(0, 1, S)[:, None] # 0=top (darkest), 1=bottom (warmer amber)
    xx = np.linspace(0, 1, S)[None, :]
    grad = np.clip(yy, 0, 1)
    # Subtle diagonal specular sheen baked slightly + dark smoked brown-amber
    sheen = np.exp(-((xx * 0.8 + yy * 0.6 - 0.42) ** 2) / 0.008) * 0.28
    r = np.clip(38 + grad * 72 + sheen * 160, 0, 255)
    g = np.clip(22 + grad * 42 + sheen * 130, 0, 255)
    b = np.clip(10 + grad * 14 + sheen * 75, 0, 255)
    a = np.full((S, S), 226, dtype=np.float32) # slightly smoked translucent feel
    arr = np.stack([r, g, b, a], axis=-1).astype(np.uint8)
    save_tex(Image.fromarray(arr, 'RGBA'), "tex_aviator_lens.png")

# 13. ACCOUNTANT SEMI-TRANSPARENT GLASS LENS
def make_accountant_glass():
    S = 256
    yy, xx = np.mgrid[0:S, 0:S] / float(S)
    # Crisp subtle diagonal reflection bands on clear optical glass
    s1 = np.exp(-((xx + yy - 0.62) ** 2) / 0.004)
    s2 = np.exp(-((xx + yy - 0.78) ** 2) / 0.0012) * 0.7
    glint = np.clip(s1 + s2, 0.0, 1.0)
    r = np.clip(185 + glint * 70, 0, 255)
    g = np.clip(218 + glint * 37, 0, 255)
    b = np.clip(236 + glint * 19, 0, 255)
    a = np.clip(72 + glint * 115, 0, 230) # semi-transparent glass!
    arr = np.stack([r, g, b, a], axis=-1).astype(np.uint8)
    save_tex(Image.fromarray(arr, 'RGBA'), "tex_accountant_glass.png")

make_tinfoil()
make_ushanka_fur()
make_ushanka_cloth()
make_brass()
make_crown_gold()
make_traffic_cone()
make_cowboy_leather()
make_chef_cotton()
make_pirate_leather()
make_frog_skin()
make_tortoiseshell()
make_aviator_lens()
make_accountant_glass()
print("ALL_TEXTURES_READY")
