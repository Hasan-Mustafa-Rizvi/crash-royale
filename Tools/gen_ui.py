"""Generates the game's UI sprites, emote icons and card portraits.

Everything is drawn procedurally (supersampled, then downscaled), so the art is original and ships with the repo.
Card portraits are framed troop sprites from Tiny Swords.

Usage, from the repo root:  python Tools/gen_ui.py [out_assets_dir] [sprite_sheet_dir]
Defaults write into Assets/ and read the sheets from Assets/Art/ThirdParty. Then run
Crash Royale > Rebuild Content and Scenes in Unity.
"""
import math
import os
import sys

from PIL import Image, ImageDraw, ImageFilter, ImageChops

OUT = sys.argv[1] if len(sys.argv) > 1 else "Assets"
STAGE = sys.argv[2] if len(sys.argv) > 2 else None
SHEETS = {
    "Warrior_Blue.png": "Assets/Art/ThirdParty/TinySwords/Units/Warrior_Blue.png",
    "Torch_Blue.png": "Assets/Art/ThirdParty/TinySwords/Units/Torch_Blue.png",
    "TNT_Blue.png": "Assets/Art/ThirdParty/TinySwords/Units/TNT_Blue.png",
    "Archer_Blue.png": "Assets/Art/ThirdParty/TinySwords/Units/Archer_Blue.png",
    "Barrel_Blue.png": "Assets/Art/ThirdParty/TinySwords/Units/Barrel_Blue.png",
    # Optional (Tiny Swords Free Pack, not in the repo).
    "NEW_Lancer_Idle.png": "Assets/Art/ThirdParty/TinySwordsFreePack/Lancer/Blue/Lancer_Idle.png",
}


def sheet_path(name):
    return os.path.join(STAGE, name) if STAGE else SHEETS[name]


UI = os.path.join(OUT, "Art", "UI")
CARDS = os.path.join(OUT, "Art", "Cards")
EMOTES = os.path.join(OUT, "Art", "Emotes")
for d in (UI, CARDS, EMOTES):
    os.makedirs(d, exist_ok=True)

SS = 4  # supersampling factor

GOLD_TOP = (255, 226, 122)
GOLD_BOT = (219, 140, 38)
GOLD_DARK = (110, 62, 14)
OUTLINE = (42, 20, 40)
PURPLE_TOP = (122, 62, 160)
PURPLE_BOT = (70, 33, 104)
PANEL_TOP = (66, 34, 92)
PANEL_BOT = (36, 18, 54)


def lerp(a, b, t):
    return tuple(int(round(a[i] + (b[i] - a[i]) * t)) for i in range(len(a)))


def vgradient(size, top, bottom):
    w, h = size
    img = Image.new("RGBA", size)
    px = img.load()
    for y in range(h):
        c = lerp(top, bottom, y / max(1, h - 1))
        if len(c) == 3:
            c = c + (255,)
        for x in range(w):
            px[x, y] = c
    return img


def rounded_mask(size, radius, inset=0):
    w, h = size
    m = Image.new("L", size, 0)
    ImageDraw.Draw(m).rounded_rectangle((inset, inset, w - 1 - inset, h - 1 - inset), radius=max(0, radius - inset), fill=255)
    return m


def finish(img, size):
    return img.resize(size, Image.LANCZOS)


def save(img, name, folder=UI):
    path = os.path.join(folder, name)
    img.save(path)
    return path


def framed_rect(size, radius, rim, rim_top, rim_bot, fill_top, fill_bot, outline=OUTLINE, outline_w=2, highlight=True, fill_alpha=255):
    """Rounded rectangle: dark outline, gradient rim, gradient fill, optional top highlight."""
    W, H = size[0] * SS, size[1] * SS
    R = radius * SS
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    o = outline_w * SS
    # outline
    img.paste(Image.new("RGBA", (W, H), outline + (255,)), (0, 0), rounded_mask((W, H), R))
    # rim
    rim_img = vgradient((W, H), rim_top, rim_bot)
    img.paste(rim_img, (0, 0), rounded_mask((W, H), R, o))
    # fill
    inner = o + rim * SS
    fill_img = vgradient((W, H), fill_top, fill_bot)
    if fill_alpha < 255:
        fill_img.putalpha(fill_alpha)
    mask = rounded_mask((W, H), R, inner)
    img.paste(fill_img, (0, 0), mask)
    if highlight:
        hl = Image.new("RGBA", (W, H), (255, 255, 255, 0))
        d = ImageDraw.Draw(hl)
        top = inner + 2 * SS
        d.rounded_rectangle((inner + 4 * SS, top, W - inner - 4 * SS, top + (H - 2 * inner) * 0.38), radius=max(1, R - inner - 2 * SS), fill=(255, 255, 255, 46))
        img = Image.alpha_composite(img, hl)
    return finish(img, size)


# ------------------------------------------------------------------ panels and buttons

save(framed_rect((128, 128), 30, 5, GOLD_TOP, GOLD_BOT, PANEL_TOP, PANEL_BOT, outline_w=3, highlight=False), "panel.png")
save(framed_rect((96, 96), 24, 2, (120, 80, 160), (80, 50, 110), (32, 16, 46), (22, 10, 32), outline_w=2, highlight=False, fill_alpha=225), "panel_dark.png")
save(framed_rect((128, 96), 34, 6, GOLD_TOP, GOLD_BOT, PURPLE_TOP, PURPLE_BOT, outline_w=3), "button_gold.png")
save(framed_rect((128, 96), 34, 5, (250, 214, 120), (196, 128, 40), (64, 150, 230), (34, 88, 170), outline_w=3), "button_blue.png")
save(framed_rect((128, 96), 34, 5, (250, 214, 120), (196, 128, 40), (98, 196, 82), (44, 128, 50), outline_w=3), "button_green.png")
save(framed_rect((128, 96), 34, 5, (250, 214, 120), (196, 128, 40), (226, 76, 84), (150, 30, 44), outline_w=3), "button_red.png")
save(framed_rect((128, 96), 34, 4, (170, 120, 210), (110, 70, 150), (92, 54, 124), (58, 30, 84), outline_w=3), "button_purple.png")
save(framed_rect((64, 64), 32, 0, (255, 255, 255), (255, 255, 255), (255, 255, 255), (232, 232, 240), outline_w=0, highlight=False), "pill.png")
save(framed_rect((96, 96), 22, 3, (255, 255, 255), (220, 220, 230), (40, 22, 58), (26, 12, 38), outline_w=2, highlight=False, fill_alpha=235), "bubble.png")


def plain_round(size, radius, color):
    W, H = size[0] * SS, size[1] * SS
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    img.paste(Image.new("RGBA", (W, H), color), (0, 0), rounded_mask((W, H), radius * SS))
    return finish(img, size)


save(plain_round((32, 32), 8, (255, 255, 255, 255)), "rounded_white.png")
save(Image.new("RGBA", (4, 4), (255, 255, 255, 255)), "white.png")

# world health bar pieces (sliced sprites, tiny)
save(plain_round((24, 12), 6, (20, 12, 26, 230)), "hp_bg.png")
save(plain_round((24, 12), 5, (255, 255, 255, 255)), "hp_fill.png")

# soft circle + ring
def soft_circle(size, falloff=0.55):
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    px = img.load()
    c = (size - 1) / 2
    for y in range(size):
        for x in range(size):
            d = math.hypot(x - c, y - c) / c
            a = 0 if d >= 1 else (1 - max(0.0, (d - falloff) / (1 - falloff)))
            px[x, y] = (255, 255, 255, int(255 * max(0.0, min(1.0, a))))
    return img


save(soft_circle(64), "soft_circle.png")


def ring(size, width, color=(255, 255, 255, 255)):
    S = size * SS
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.ellipse((2 * SS, 2 * SS, S - 2 * SS, S - 2 * SS), outline=color, width=width * SS)
    return finish(img, (size, size))


save(ring(96, 7), "ring.png")

# vignette for menus (transparent center, dark top/bottom)
def vignette(w, h):
    img = Image.new("RGBA", (w, h))
    px = img.load()
    for y in range(h):
        t = y / (h - 1)
        a = max(0.0, (0.22 - t) / 0.22) ** 1.4 * 0.75 + max(0.0, (t - 0.62) / 0.38) ** 1.3 * 0.85
        for x in range(w):
            px[x, y] = (14, 6, 22, int(255 * min(1.0, a)))
    return img


save(vignette(64, 256), "vignette.png")

# ------------------------------------------------------------------ cards

RARITY = {
    "common": ((206, 222, 236), (120, 144, 170)),
    "rare": ((255, 206, 112), (214, 120, 30)),
    "epic": ((236, 160, 255), (150, 64, 210)),
}
for name, (top, bot) in RARITY.items():
    save(framed_rect((120, 150), 18, 7, top, bot, (52, 38, 74), (28, 18, 40), outline_w=3, highlight=False), f"card_frame_{name}.png")


def glow_frame(size, radius, width):
    W, H = size[0] * SS, size[1] * SS
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.rounded_rectangle((width * SS // 2, width * SS // 2, W - width * SS // 2, H - width * SS // 2), radius=radius * SS, outline=(255, 255, 255, 255), width=width * SS)
    img = img.filter(ImageFilter.GaussianBlur(SS * 1.2))
    return finish(img, size)


save(glow_frame((120, 150), 20, 6), "card_glow.png")

# charge overlay (dark translucent, used as a vertical Filled image)
save(plain_round((120, 150), 16, (16, 8, 24, 170)), "card_charge.png")


# ------------------------------------------------------------------ icons

def draw_icon(size, painter):
    S = size * SS
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    painter(ImageDraw.Draw(img), S, img)
    return finish(img, (size, size))


def outlined_polygon(d, pts, fill, outline=OUTLINE, width=None, S=None):
    d.polygon(pts, fill=fill)
    d.line(pts + [pts[0]], fill=outline, width=width or max(2, S // 22), joint="curve")


def elixir_drop(d, S, img):
    # teardrop: circle + triangle
    cx, cy, r = S * 0.5, S * 0.6, S * 0.33
    layer = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    ld = ImageDraw.Draw(layer)
    ld.ellipse((cx - r, cy - r, cx + r, cy + r), fill=(255, 255, 255, 255))
    ld.polygon([(cx - r * 0.92, cy - r * 0.38), (cx, S * 0.06), (cx + r * 0.92, cy - r * 0.38)], fill=(255, 255, 255, 255))
    mask = layer.getchannel("A")
    # outline by dilating mask
    grown = mask.filter(ImageFilter.MaxFilter(int(S * 0.07) | 1))
    img.paste(Image.new("RGBA", (S, S), OUTLINE + (255,)), (0, 0), grown)
    grad = vgradient((S, S), (255, 128, 246), (170, 36, 196))
    img.paste(grad, (0, 0), mask)
    hl = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    hd = ImageDraw.Draw(hl)
    hd.ellipse((cx - r * 0.55, cy - r * 0.62, cx - r * 0.05, cy - r * 0.05), fill=(255, 255, 255, 150))
    img.alpha_composite(hl)


save(draw_icon(96, elixir_drop), "elixir_drop.png")


def crown(color_top, color_bot):
    def painter(d, S, img):
        pts = [(S * 0.12, S * 0.78), (S * 0.12, S * 0.32), (S * 0.32, S * 0.52), (S * 0.5, S * 0.2), (S * 0.68, S * 0.52), (S * 0.88, S * 0.32), (S * 0.88, S * 0.78)]
        layer = Image.new("RGBA", (S, S), (0, 0, 0, 0))
        ImageDraw.Draw(layer).polygon(pts, fill=(255, 255, 255, 255))
        mask = layer.getchannel("A")
        grown = mask.filter(ImageFilter.MaxFilter(int(S * 0.08) | 1))
        img.paste(Image.new("RGBA", (S, S), OUTLINE + (255,)), (0, 0), grown)
        img.paste(vgradient((S, S), color_top, color_bot), (0, 0), mask)
        # band
        band = Image.new("RGBA", (S, S), (0, 0, 0, 0))
        bd = ImageDraw.Draw(band)
        bd.rectangle((S * 0.12, S * 0.66, S * 0.88, S * 0.78), fill=(0, 0, 0, 60))
        for gx in (0.3, 0.5, 0.7):
            bd.ellipse((S * (gx - 0.05), S * 0.67, S * (gx + 0.05), S * 0.77), fill=(255, 255, 255, 200))
        img.alpha_composite(band)
        for (px, py) in [(0.12, 0.32), (0.5, 0.2), (0.88, 0.32)]:
            r = S * 0.06
            d.ellipse((S * px - r, S * py - r, S * px + r, S * py + r), fill=(255, 240, 200, 255), outline=OUTLINE, width=max(2, S // 40))
    return painter


save(draw_icon(96, crown((140, 196, 255), (40, 110, 220))), "crown_blue.png")
save(draw_icon(96, crown((255, 150, 150), (200, 40, 56))), "crown_red.png")
save(draw_icon(96, crown(GOLD_TOP, GOLD_BOT)), "crown_gold.png")


def coin(d, S, img):
    m = S * 0.08
    d.ellipse((m, m, S - m, S - m), fill=OUTLINE)
    g = vgradient((S, S), (255, 232, 130), (226, 150, 34))
    mask = Image.new("L", (S, S), 0)
    ImageDraw.Draw(mask).ellipse((m + S * 0.05, m + S * 0.05, S - m - S * 0.05, S - m - S * 0.05), fill=255)
    img.paste(g, (0, 0), mask)
    d.ellipse((S * 0.27, S * 0.27, S * 0.73, S * 0.73), outline=(176, 104, 20, 255), width=int(S * 0.05))
    d.ellipse((S * 0.22, S * 0.2, S * 0.42, S * 0.36), fill=(255, 255, 255, 120))


save(draw_icon(96, coin), "coin.png")


def lock(d, S, img):
    w = S * 0.06
    d.arc((S * 0.28, S * 0.12, S * 0.72, S * 0.62), 180, 360, fill=OUTLINE, width=int(S * 0.16))
    d.arc((S * 0.28, S * 0.12, S * 0.72, S * 0.62), 180, 360, fill=(190, 196, 210), width=int(S * 0.08))
    d.rounded_rectangle((S * 0.18, S * 0.4, S * 0.82, S * 0.9), radius=S * 0.08, fill=OUTLINE)
    g = vgradient((S, S), GOLD_TOP, GOLD_BOT)
    mask = Image.new("L", (S, S), 0)
    ImageDraw.Draw(mask).rounded_rectangle((S * 0.18 + w, S * 0.4 + w, S * 0.82 - w, S * 0.9 - w), radius=S * 0.05, fill=255)
    img.paste(g, (0, 0), mask)
    d.ellipse((S * 0.44, S * 0.55, S * 0.56, S * 0.67), fill=OUTLINE)
    d.rectangle((S * 0.475, S * 0.62, S * 0.525, S * 0.78), fill=OUTLINE)


save(draw_icon(96, lock), "lock.png")


def stroke_icon(lines_fn):
    def painter(d, S, img):
        layer = Image.new("RGBA", (S, S), (0, 0, 0, 0))
        lines_fn(ImageDraw.Draw(layer), S, int(S * 0.2), OUTLINE + (255,))
        lines_fn(ImageDraw.Draw(layer), S, int(S * 0.11), (255, 244, 214, 255))
        img.alpha_composite(layer)
    return painter


def pause_lines(d, S, w, c):
    d.line((S * 0.36, S * 0.24, S * 0.36, S * 0.76), fill=c, width=w)
    d.line((S * 0.64, S * 0.24, S * 0.64, S * 0.76), fill=c, width=w)


def close_lines(d, S, w, c):
    d.line((S * 0.28, S * 0.28, S * 0.72, S * 0.72), fill=c, width=w)
    d.line((S * 0.72, S * 0.28, S * 0.28, S * 0.72), fill=c, width=w)


def back_lines(d, S, w, c):
    d.line((S * 0.62, S * 0.22, S * 0.32, S * 0.5, S * 0.62, S * 0.78), fill=c, width=w, joint="curve")


def edit_lines(d, S, w, c):
    d.line((S * 0.3, S * 0.72, S * 0.72, S * 0.3), fill=c, width=w)
    d.line((S * 0.24, S * 0.8, S * 0.3, S * 0.72), fill=c, width=max(2, w // 2))


def question_lines(d, S, w, c):
    d.arc((S * 0.3, S * 0.16, S * 0.7, S * 0.56), 200, 30, fill=c, width=w)
    d.line((S * 0.66, S * 0.44, S * 0.5, S * 0.56, S * 0.5, S * 0.64), fill=c, width=w, joint="curve")
    r = w * 0.62
    d.ellipse((S * 0.5 - r, S * 0.82 - r, S * 0.5 + r, S * 0.82 + r), fill=c)


def emote_lines(d, S, w, c):
    d.rounded_rectangle((S * 0.16, S * 0.2, S * 0.84, S * 0.68), radius=S * 0.14, outline=c, width=w // 2 + 1)
    d.line((S * 0.36, S * 0.68, S * 0.3, S * 0.84, S * 0.52, S * 0.68), fill=c, width=w // 2 + 1, joint="curve")
    r = w * 0.32
    for x in (0.36, 0.5, 0.64):
        d.ellipse((S * x - r, S * 0.44 - r, S * x + r, S * 0.44 + r), fill=c)


for name, fn in [("icon_pause", pause_lines), ("icon_close", close_lines), ("icon_back", back_lines), ("icon_edit", edit_lines), ("icon_help", question_lines), ("icon_emote", emote_lines)]:
    save(draw_icon(96, stroke_icon(fn)), name + ".png")


def deck_icon(d, S, img):
    for i, (dx, rot) in enumerate([(-0.14, -14), (0.0, 0), (0.14, 14)]):
        card = Image.new("RGBA", (S, S), (0, 0, 0, 0))
        cd = ImageDraw.Draw(card)
        x0, y0, x1, y1 = S * 0.32, S * 0.18, S * 0.68, S * 0.78
        cd.rounded_rectangle((x0, y0, x1, y1), radius=S * 0.06, fill=OUTLINE)
        inset = S * 0.04
        g = vgradient((S, S), GOLD_TOP, GOLD_BOT) if i == 1 else vgradient((S, S), (180, 120, 220), (110, 60, 160))
        m = Image.new("L", (S, S), 0)
        ImageDraw.Draw(m).rounded_rectangle((x0 + inset, y0 + inset, x1 - inset, y1 - inset), radius=S * 0.04, fill=255)
        card.paste(g, (0, 0), m)
        card = card.rotate(-rot, resample=Image.BICUBIC, center=(S * 0.5, S * 0.85))
        card = ImageChops.offset(card, int(dx * S), 0)
        img.alpha_composite(card)


save(draw_icon(128, deck_icon), "icon_deck.png")


def sword_icon(d, S, img):
    layer = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    ld = ImageDraw.Draw(layer)
    for w, c in [(int(S * 0.16), OUTLINE + (255,)), (int(S * 0.08), (235, 240, 250, 255))]:
        ld.line((S * 0.24, S * 0.76, S * 0.78, S * 0.22), fill=c, width=w)
        ld.line((S * 0.76, S * 0.76, S * 0.22, S * 0.22), fill=c, width=w)
    img.alpha_composite(layer)
    for (x, y) in [(0.3, 0.7), (0.7, 0.7)]:
        r = S * 0.08
        d.ellipse((S * x - r, S * y - r, S * x + r, S * y + r), fill=GOLD_BOT, outline=OUTLINE, width=max(2, S // 30))


save(draw_icon(128, sword_icon), "icon_battle.png")


# ------------------------------------------------------------------ emotes

def face(expr):
    def painter(d, S, img):
        m = S * 0.06
        d.ellipse((m, m, S - m, S - m), fill=OUTLINE)
        base = (255, 210, 70) if expr != "angry" else (255, 120, 80)
        g = vgradient((S, S), lerp(base, (255, 255, 255), 0.2), lerp(base, (0, 0, 0), 0.12))
        mask = Image.new("L", (S, S), 0)
        ImageDraw.Draw(mask).ellipse((m + S * 0.05, m + S * 0.05, S - m - S * 0.05, S - m - S * 0.05), fill=255)
        img.paste(g, (0, 0), mask)
        lw = max(3, int(S * 0.05))
        if expr == "laugh":
            d.arc((S * 0.24, S * 0.3, S * 0.44, S * 0.46), 200, 340, fill=OUTLINE, width=lw)
            d.arc((S * 0.56, S * 0.3, S * 0.76, S * 0.46), 200, 340, fill=OUTLINE, width=lw)
            d.chord((S * 0.24, S * 0.42, S * 0.76, S * 0.82), 0, 180, fill=OUTLINE)
            d.chord((S * 0.32, S * 0.58, S * 0.68, S * 0.78), 0, 180, fill=(232, 72, 92))
            # tears of joy
            d.ellipse((S * 0.08, S * 0.38, S * 0.2, S * 0.54), fill=(110, 200, 255), outline=OUTLINE, width=max(2, lw // 2))
            d.ellipse((S * 0.8, S * 0.38, S * 0.92, S * 0.54), fill=(110, 200, 255), outline=OUTLINE, width=max(2, lw // 2))
        elif expr == "angry":
            d.line((S * 0.24, S * 0.3, S * 0.44, S * 0.4), fill=OUTLINE, width=lw)
            d.line((S * 0.76, S * 0.3, S * 0.56, S * 0.4), fill=OUTLINE, width=lw)
            d.ellipse((S * 0.3, S * 0.4, S * 0.4, S * 0.52), fill=OUTLINE)
            d.ellipse((S * 0.6, S * 0.4, S * 0.7, S * 0.52), fill=OUTLINE)
            d.arc((S * 0.32, S * 0.62, S * 0.68, S * 0.86), 200, 340, fill=OUTLINE, width=lw)
        elif expr == "cry":
            d.arc((S * 0.24, S * 0.36, S * 0.44, S * 0.5), 20, 160, fill=OUTLINE, width=lw)
            d.arc((S * 0.56, S * 0.36, S * 0.76, S * 0.5), 20, 160, fill=OUTLINE, width=lw)
            d.arc((S * 0.34, S * 0.64, S * 0.66, S * 0.86), 200, 340, fill=OUTLINE, width=lw)
            for x in (0.3, 0.66):
                d.rounded_rectangle((S * x, S * 0.48, S * (x + 0.08), S * 0.9), radius=S * 0.04, fill=(110, 200, 255), outline=OUTLINE, width=max(2, lw // 2))
        elif expr == "wow":
            d.ellipse((S * 0.28, S * 0.3, S * 0.42, S * 0.48), fill=(255, 255, 255), outline=OUTLINE, width=lw)
            d.ellipse((S * 0.58, S * 0.3, S * 0.72, S * 0.48), fill=(255, 255, 255), outline=OUTLINE, width=lw)
            d.ellipse((S * 0.33, S * 0.36, S * 0.39, S * 0.44), fill=OUTLINE)
            d.ellipse((S * 0.63, S * 0.36, S * 0.69, S * 0.44), fill=OUTLINE)
            d.ellipse((S * 0.42, S * 0.58, S * 0.58, S * 0.8), fill=OUTLINE)
    return painter


for expr in ("laugh", "angry", "cry", "wow"):
    save(draw_icon(128, face(expr)), f"emote_{expr}.png", EMOTES)


# ------------------------------------------------------------------ card portraits from the unit sprites

def frame_crop(sheet, fw, fh, row, col):
    im = Image.open(sheet_path(sheet)).convert("RGBA")
    return im.crop((col * fw, row * fh, (col + 1) * fw, (row + 1) * fh))


PORTRAITS = {
    # name: (sheet, frame w, frame h, row, col, rarity, scale)
    "brump": ("Warrior_Blue.png", 192, 192, 0, 0, "rare", 3.0),
    "jitter": ("Torch_Blue.png", 192, 192, 0, 0, "common", 3.0),
    "lancer": ("NEW_Lancer_Idle.png", 320, 320, 0, 0, "rare", 1.72),
    "kaboom": ("TNT_Blue.png", 192, 192, 0, 0, "rare", 2.45),
    "archers": ("Archer_Blue.png", 192, 192, 0, 0, "common", 3.0),
    "barrelbro": ("Barrel_Blue.png", 128, 128, 3, 1, "epic", 3.4),
    # Stand-in for repos without the optional Lancer art: the knight, tinted like the in-game fallback.
    "lancer_fallback": ("Warrior_Blue.png", 192, 192, 0, 0, "rare", 2.6, (1.0, 0.86, 0.58)),
}
BG = {
    "common": ((92, 132, 176), (34, 46, 78)),
    "rare": ((214, 132, 52), (82, 36, 30)),
    "epic": ((170, 92, 214), (52, 24, 82)),
}


def radial(size, inner, outer):
    w, h = size
    img = Image.new("RGBA", size)
    px = img.load()
    cx, cy = w / 2, h * 0.42
    maxd = math.hypot(w / 2, h * 0.62)
    for y in range(h):
        for x in range(w):
            t = min(1.0, math.hypot(x - cx, y - cy) / maxd)
            px[x, y] = lerp(inner, outer, t ** 0.8) + (255,)
    return img


for name, (sheet, fw, fh, row, col, rarity, scale, *extra) in PORTRAITS.items():
    tint = extra[0] if extra else None
    path = sheet_path(sheet)
    if not os.path.exists(path):
        print("missing", sheet)
        continue
    W, H = 240, 300
    bg = radial((W, H), *BG[rarity])
    # subtle diagonal rays
    rays = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    rd = ImageDraw.Draw(rays)
    for i in range(-6, 12):
        x = i * 34
        rd.polygon([(x, 0), (x + 16, 0), (x - 120 + 16, H), (x - 120, H)], fill=(255, 255, 255, 14))
    bg.alpha_composite(rays)
    fr = frame_crop(sheet, fw, fh, row, col)
    bbox = fr.getchannel("A").getbbox()
    fr = fr.crop(bbox)
    fr = fr.resize((int(fr.width * scale), int(fr.height * scale)), Image.NEAREST)
    if tint:
        r, g, b, a = fr.split()
        r = r.point(lambda v: int(v * tint[0]))
        g = g.point(lambda v: int(v * tint[1]))
        b = b.point(lambda v: int(v * tint[2]))
        fr = Image.merge("RGBA", (r, g, b, a))
    # shadow
    sh = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    ImageDraw.Draw(sh).ellipse((W * 0.2, H * 0.78, W * 0.8, H * 0.9), fill=(0, 0, 0, 90))
    sh = sh.filter(ImageFilter.GaussianBlur(6))
    bg.alpha_composite(sh)
    x = (W - fr.width) // 2
    y = int(H * 0.86) - fr.height
    if fr.height > H * 0.86:
        y = int(H * 0.06)
    bg.alpha_composite(fr, (max(0, x), max(0, y)))
    save(bg, f"portrait_{name}.png", CARDS)

print("ok", len(os.listdir(UI)), "ui sprites,", len(os.listdir(CARDS)), "portraits,", len(os.listdir(EMOTES)), "emotes")
