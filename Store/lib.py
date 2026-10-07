import math, sys
from playwright.sync_api import sync_playwright

RED=("#F24D4D","#FF7A6E","#C93636"); BLUE=("#408CF2","#6FB0FF","#2E68C2")
YEL=("#FFCC33","#FFE07A","#D9A41A"); GRN=("#4DCC66","#7FE592","#33A04A")
PUR=("#B366E6","#CC8FF2","#8A45BD"); ORA=("#FF8C26","#FFB060","#D46A10")
OUT="#1E2A4A"; SW=14

def crate_iso(cx, cy, s, col, sym=None):
    base, top, dark = col
    k=0.866
    T=[(cx,cy-s),(cx+k*s,cy-s/2),(cx,cy),(cx-k*s,cy-s/2)]
    L=[(cx-k*s,cy-s/2),(cx,cy),(cx,cy+s),(cx-k*s,cy+s/2)]
    R=[(cx,cy),(cx+k*s,cy-s/2),(cx+k*s,cy+s/2),(cx,cy+s)]
    P=lambda pts:" ".join(f"{x:.1f},{y:.1f}" for x,y in pts)
    tw=s*0.22
    # tape across top (from left-edge mid to right-edge mid direction) + down left face
    a=((T[3][0]+T[0][0])/2,(T[3][1]+T[0][1])/2); b=((T[1][0]+T[2][0])/2,(T[1][1]+T[2][1])/2)
    # direction perpendicular offsets along top face axis (T0->T1)
    ux,uy=(T[1][0]-T[0][0])/s,(T[1][1]-T[0][1])/s
    tape=[(a[0]-ux*tw/2,a[1]-uy*tw/2),(a[0]+ux*tw/2,a[1]+uy*tw/2),(b[0]+ux*tw/2,b[1]+uy*tw/2),(b[0]-ux*tw/2,b[1]-uy*tw/2)]
    tape2=[(b[0]-ux*tw/2,b[1]-uy*tw/2),(b[0]+ux*tw/2,b[1]+uy*tw/2),(b[0]+ux*tw/2,b[1]+uy*tw/2+s*0.45),(b[0]-ux*tw/2,b[1]-uy*tw/2+s*0.45)]
    g=f'''<polygon points="{P(L)}" fill="{base}"/>
<polygon points="{P(R)}" fill="{dark}"/>
<polygon points="{P(T)}" fill="{top}"/>
<polygon points="{P(tape)}" fill="#ffffff" opacity=".55"/>
<polygon points="{P(tape2)}" fill="#ffffff" opacity=".35"/>
<polygon points="{P([T[0],T[1],R[2],R[3],L[3],L[0]])}" fill="none" stroke="{OUT}" stroke-width="{SW}" stroke-linejoin="round"/>
<polyline points="{P([L[0],T[2],T[1]])}" fill="none" stroke="{OUT}" stroke-width="{SW*0.55}" stroke-linejoin="round"/>
<line x1="{cx}" y1="{cy}" x2="{cx}" y2="{cy+s}" stroke="{OUT}" stroke-width="{SW*0.55}"/>'''
    return g

def crate_side(x,y,s,col):
    base,top,dark=col; r=s*0.12
    return f'''<rect x="{x}" y="{y}" width="{s}" height="{s}" rx="{r}" fill="{base}" stroke="{OUT}" stroke-width="{SW}"/>
<rect x="{x+SW/2}" y="{y+SW/2}" width="{s-SW}" height="{s*0.2}" rx="{r*0.6}" fill="{top}"/>
<rect x="{x+s*0.4}" y="{y+SW/2}" width="{s*0.2}" height="{s-SW}" fill="#fff" opacity=".45"/>
<rect x="{x}" y="{y}" width="{s}" height="{s}" rx="{r}" fill="none" stroke="{OUT}" stroke-width="{SW}"/>'''

def burst(cx,cy,r,n=12,fill="#FFFFFF",text="HONK!",fs=None,rot=-10):
    pts=[]
    for i in range(n*2):
        a=math.pi*i/n; rr=r if i%2==0 else r*0.78
        pts.append((cx+rr*math.cos(a),cy+rr*math.sin(a)*0.72))
    P=" ".join(f"{x:.1f},{y:.1f}" for x,y in pts)
    fs=fs or r*0.5
    return f'''<g transform="rotate({rot} {cx} {cy})"><polygon points="{P}" fill="{fill}" stroke="{OUT}" stroke-width="{SW}" stroke-linejoin="round"/>
<text x="{cx}" y="{cy+fs*0.36}" text-anchor="middle" font-family="Poppins" font-weight="800" font-size="{fs}" fill="{OUT}">{text}</text></g>'''

def truck(x,y,w,col,crates):
    """side view truck facing right; x,y = top-left of bed area; w = total length"""
    base,top,dark=col
    bedw=w*0.62; cabw=w*0.30; h=w*0.20
    by=y  # bed floor y
    s=bedw/3-6
    g=""
    # crates on bed
    for i,c in enumerate(crates):
        g+=crate_side(x+6+i*(s+0)+0, by-s, s, c)
    # bed
    g+=f'<rect x="{x-10}" y="{by}" width="{bedw+20}" height="{h*0.45}" rx="12" fill="{dark}" stroke="{OUT}" stroke-width="{SW}"/>'
    # cab
    cx=x+bedw+18; ch=h*1.9
    g+=f'''<path d="M{cx} {by+h*0.45} L{cx} {by+h*0.45-ch+30} Q{cx} {by+h*0.45-ch} {cx+30} {by+h*0.45-ch} L{cx+cabw*0.55} {by+h*0.45-ch} Q{cx+cabw*0.75} {by+h*0.45-ch} {cx+cabw*0.85} {by+h*0.45-ch*0.55} L{cx+cabw} {by+h*0.45-ch*0.45} Q{cx+cabw+10} {by+h*0.45-ch*0.42} {cx+cabw+10} {by+h*0.45-ch*0.3} L{cx+cabw+10} {by+h*0.45} Z" fill="{base}" stroke="{OUT}" stroke-width="{SW}" stroke-linejoin="round"/>
<path d="M{cx+22} {by+h*0.45-ch+24} L{cx+cabw*0.52} {by+h*0.45-ch+24} Q{cx+cabw*0.66} {by+h*0.45-ch+24} {cx+cabw*0.74} {by+h*0.45-ch*0.52} L{cx+22} {by+h*0.45-ch*0.52} Z" fill="#BFE6FF" stroke="{OUT}" stroke-width="{SW*0.7}" stroke-linejoin="round"/>
<rect x="{cx+cabw-6}" y="{by+h*0.45-ch*0.28}" width="22" height="26" rx="8" fill="#FFE07A" stroke="{OUT}" stroke-width="{SW*0.6}"/>'''
    # chassis
    g+=f'<rect x="{x-14}" y="{by+h*0.42}" width="{w+10}" height="{h*0.3}" rx="14" fill="#3A4566" stroke="{OUT}" stroke-width="{SW}"/>'
    # wheels
    wr=h*0.42
    for wx in (x+bedw*0.25, x+bedw*0.78, cx+cabw*0.55):
        g+=f'<circle cx="{wx}" cy="{by+h*0.78}" r="{wr}" fill="#2A3150" stroke="{OUT}" stroke-width="{SW}"/><circle cx="{wx}" cy="{by+h*0.78}" r="{wr*0.42}" fill="#D7DCE8" stroke="{OUT}" stroke-width="{SW*0.5}"/>'
    return g

def bg(c1,c2,rays=None):
    s=f'''<defs><radialGradient id="bg" cx="50%" cy="38%" r="75%"><stop offset="0" stop-color="{c1}"/><stop offset="1" stop-color="{c2}"/></radialGradient></defs>
<rect width="1024" height="1024" fill="url(#bg)"/>'''
    if rays:
        for i in range(12):
            a0=i*30; s+=f'<path d="M512 470 L{512+1400*math.cos(math.radians(a0))} {470+1400*math.sin(math.radians(a0))} L{512+1400*math.cos(math.radians(a0+13))} {470+1400*math.sin(math.radians(a0+13))} Z" fill="#fff" opacity=".07"/>'
    return s

def svg(body): return f'<svg xmlns="http://www.w3.org/2000/svg" width="1024" height="1024" viewBox="0 0 1024 1024">{body}</svg>'


# A: big truck + HONK burst
A=svg(bg("#8FD0FF","#3E8FE0",True)
      + '<ellipse cx="512" cy="835" rx="420" ry="44" fill="#0b2a5a" opacity=".22"/>'
      + truck(110,665,800,GRN,[RED,BLUE,YEL])
      + burst(700,215,185,text="HONK!",fs=84,rot=-8))


# B: front-view cute truck, crates peeking over the cab, honk waves
def front_truck(cx, top, w, col):
    base,light,dark=col; h=w*0.95
    x0=cx-w/2; g=""
    # cargo crates behind cab
    cs=w*0.34
    g+=crate_side(cx-cs*1.5+6, top-cs*0.55, cs, RED)+crate_side(cx-cs/2, top-cs*0.95, cs, YEL)+crate_side(cx+cs/2-6, top-cs*0.55, cs, BLUE)
    # wheels
    for wx in (x0+w*0.18, x0+w*0.82):
        g+=f'<rect x="{wx-w*0.09}" y="{top+h*0.78}" width="{w*0.18}" height="{h*0.25}" rx="22" fill="#2A3150" stroke="{OUT}" stroke-width="{SW}"/>'
    # cab body
    g+=f'<rect x="{x0}" y="{top}" width="{w}" height="{h*0.9}" rx="{w*0.12}" fill="{base}" stroke="{OUT}" stroke-width="{SW}"/>'
    g+=f'<rect x="{x0+SW}" y="{top+SW}" width="{w-2*SW}" height="{h*0.08}" rx="{w*0.06}" fill="{light}" opacity=".9"/>'
    # windshield
    g+=f'<rect x="{x0+w*0.1}" y="{top+h*0.1}" width="{w*0.8}" height="{h*0.3}" rx="{w*0.06}" fill="#BFE6FF" stroke="{OUT}" stroke-width="{SW}"/>'
    g+=f'<path d="M{x0+w*0.2} {top+h*0.36} L{x0+w*0.42} {top+h*0.14} L{x0+w*0.52} {top+h*0.14} L{x0+w*0.3} {top+h*0.36} Z" fill="#fff" opacity=".7"/>'
    # grille
    g+=f'<rect x="{cx-w*0.2}" y="{top+h*0.5}" width="{w*0.4}" height="{h*0.2}" rx="16" fill="{dark}" stroke="{OUT}" stroke-width="{SW*0.8}"/>'
    for i in range(1,4):
        gy=top+h*0.5+h*0.2*i/4
        g+=f'<line x1="{cx-w*0.16}" y1="{gy}" x2="{cx+w*0.16}" y2="{gy}" stroke="{OUT}" stroke-width="8" stroke-linecap="round" opacity=".6"/>'
    # headlights
    for hx in (x0+w*0.16, x0+w*0.84):
        g+=f'<circle cx="{hx}" cy="{top+h*0.6}" r="{w*0.085}" fill="#FFF3B0" stroke="{OUT}" stroke-width="{SW}"/><circle cx="{hx-8}" cy="{top+h*0.6-8}" r="{w*0.03}" fill="#fff"/>'
    # bumper
    g+=f'<rect x="{x0-14}" y="{top+h*0.76}" width="{w+28}" height="{h*0.12}" rx="18" fill="#D7DCE8" stroke="{OUT}" stroke-width="{SW}"/>'
    return g

def waves(cx,cy,side):
    g=""
    for i,r in enumerate((60,110)):
        a0,a1=(-35,35) if side>0 else (145,215)
        import math
        p0=(cx+r*math.cos(math.radians(a0)),cy+r*math.sin(math.radians(a0)))
        p1=(cx+r*math.cos(math.radians(a1)),cy+r*math.sin(math.radians(a1)))
        g+=f'<path d="M{p0[0]:.1f} {p0[1]:.1f} A{r} {r} 0 0 1 {p1[0]:.1f} {p1[1]:.1f}" fill="none" stroke="{OUT}" stroke-width="22" stroke-linecap="round"/>'
    return g

B=svg(bg("#FFE27A","#FF9A2E",True)
      + '<ellipse cx="512" cy="900" rx="330" ry="40" fill="#7a2a00" opacity=".22"/>'
      + front_truck(512,380,480,GRN)
      + waves(250,640,-1)+waves(774,640,1))
# C: crate flying into truck with motion trail
lines="".join(f'<line x1="{110+i*30}" y1="{300+i*95}" x2="{290+i*30}" y2="{300+i*95}" stroke="#fff" stroke-width="24" stroke-linecap="round" opacity=".65"/>' for i in range(3))
C=svg(bg("#7FE0B0","#1FA37A",True)
      + '<ellipse cx="512" cy="855" rx="400" ry="46" fill="#003322" opacity=".25"/>'
      + truck(130,700,760,BLUE,[RED,RED])
      + lines + crate_iso(470,360,150,RED)
      + burst(790,200,150,text="HONK!",fs=66,rot=8))
