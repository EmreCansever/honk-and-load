import math
exec(open("lib.py").read())
from playwright.sync_api import sync_playwright

RAYS_BG = lambda w,h,cx,cy,c1,c2: (f'''<defs><radialGradient id="g" cx="{cx/w*100:.0f}%" cy="{cy/h*100:.0f}%" r="80%"><stop offset="0" stop-color="{c1}"/><stop offset="1" stop-color="{c2}"/></radialGradient></defs>
<rect width="{w}" height="{h}" fill="url(#g)"/>''' + "".join(
    f'<path d="M{cx} {cy} L{cx+3000*math.cos(math.radians(a))} {cy+3000*math.sin(math.radians(a))} L{cx+3000*math.cos(math.radians(a+13))} {cy+3000*math.sin(math.radians(a+13))} Z" fill="#fff" opacity=".08"/>'
    for a in range(0,360,30)))

def truck_art(shadow=True):
    s = ""
    if shadow: s += '<ellipse cx="512" cy="900" rx="330" ry="40" fill="#7a2a00" opacity=".22"/>'
    return s + front_truck(512,380,480,GRN) + waves(250,640,-1) + waves(774,640,1)
# art bbox approx x 130..894, y 230..930 -> center (512, 580)

def page(w,h,body): return f'<svg xmlns="http://www.w3.org/2000/svg" width="{w}" height="{h}" viewBox="0 0 {w} {h}">{body}</svg>'
def place(body, scale, tx, ty, cx=512, cy=580):
    return f'<g transform="translate({tx} {ty}) scale({scale}) translate({-cx} {-cy})">{body}</g>'

TITLE_STYLE='font-family="Poppins" font-weight="800" fill="#fff" stroke="#1E2A4A" stroke-linejoin="round" paint-order="stroke"'

jobs = {}
# full icon (store 512 + master 1024)
jobs["icon_master_1024"] = (1024,1024, RAYS_BG(1024,1024,512,470,"#FFE27A","#FF9A2E") + place(truck_art(),1.12,512,560), False)
# adaptive background / foreground (432)
jobs["adaptive_bg_432"] = (432,432, RAYS_BG(432,432,216,200,"#FFE27A","#FF9A2E"), False)
jobs["adaptive_fg_432"] = (432,432, place(truck_art(False),0.36,216,222), True)
# splash logo: truck + title, transparent
jobs["splash_logo"] = (1024,1024, place(truck_art(False),0.78,512,400) +
    f'<text x="512" y="900" text-anchor="middle" font-size="128" stroke-width="30" {TITLE_STYLE}>Honk &amp; Load!</text>', True)
# feature graphic 1024x500
jobs["feature_1024x500"] = (1024,500, RAYS_BG(1024,500,300,240,"#FFE27A","#FF9A2E")
    + place(truck_art(),0.58,270,250)
    + f'<text x="720" y="215" text-anchor="middle" font-size="104" stroke-width="26" {TITLE_STYLE}>Honk &amp;</text>'
    + f'<text x="720" y="335" text-anchor="middle" font-size="120" stroke-width="28" {TITLE_STYLE}>Load!</text>'
    + crate_iso(900,430,40,PUR) + crate_iso(545,440,34,ORA), False)

with sync_playwright() as p:
    br=p.chromium.launch(); pg=br.new_page()
    for name,(w,h,body,transp) in jobs.items():
        pg.set_viewport_size({"width":w,"height":h})
        pg.set_content(f'<html><body style="margin:0;background:transparent">{page(w,h,body)}</body></html>')
        pg.screenshot(path=f"out/{name}.png", omit_background=transp)
    br.close()
