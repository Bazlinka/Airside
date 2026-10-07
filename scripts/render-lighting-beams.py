#!/usr/bin/env python3
"""Side-view SVG of each lighting family's landing and taxi beams over level ground, plus the
aim/range checks the EditMode tests make. Offline evidence only, not a Unity capture.
Numbers mirror Presentation/AircraftLightingProfile.cs; lamp heights are approximate.

  python3 scripts/render-lighting-beams.py [out.svg]
"""
import math, sys

# family: (landing height m, landing pitch, spot, range, taxi pitch, taxi spot, taxi range)
F = {
    "Turboprop":    (2.6, 3.0, 44, 70, 4.0, 66, 32),
    "Regional jet": (3.2, 3.0, 36, 100, 4.5, 62, 45),
    "Narrowbody":   (3.2, 3.0, 32, 120, 4.5, 62, 55),
    "Widebody":     (4.5, 2.5, 28, 150, 4.5, 58, 70),
    "Helicopter":   (2.0, 25.0, 18, 110, 0, 0, 0),
}
TAXI_H = 1.6

def hit(h, pitch):
    return math.inf if pitch <= 0 else h / math.tan(math.radians(pitch))

def check():
    bad = []
    for n, (h, lp, ls, lr, tp, ts, tr) in F.items():
        g = hit(h, lp)
        if not 3 < g < lr * 0.9:
            bad.append(f"{n}: landing axis hits at {g:.0f} m, range {lr}")
        if tp:
            t = hit(TAXI_H, tp)
            if not 10 <= t <= tr * 0.8:
                bad.append(f"{n}: taxi axis hits at {t:.0f} m, range {tr}")
    return bad

def svg():
    W, rowh, x0, scale = 960, 190, 70, 5.2
    H = rowh * len(F) + 50
    o = [f'<svg xmlns="http://www.w3.org/2000/svg" width="{W}" height="{H}" font-family="sans-serif" font-size="12">',
         f'<rect width="{W}" height="{H}" fill="#14181f"/>']
    for i, (n, (h, lp, ls, lr, tp, ts, tr)) in enumerate(F.items()):
        gy = 30 + rowh * i + 150
        o.append(f'<text x="10" y="{gy-130}" fill="#e8ecf2">{n}</text>')
        o.append(f'<line x1="{x0}" y1="{gy}" x2="{W-20}" y2="{gy}" stroke="#4a5568"/>')
        for d in range(0, 160, 20):
            x = x0 + d * scale
            o.append(f'<line x1="{x}" y1="{gy}" x2="{x}" y2="{gy+5}" stroke="#4a5568"/>'
                     f'<text x="{x-8}" y="{gy+18}" fill="#8896a8" font-size="10">{d} m</text>')
        def beam(height, pitch, spot, rng, colour):
            lamp = (x0, gy - height * scale)
            lo, hi = pitch - spot / 2, pitch + spot / 2
            pts = [lamp]
            for a in (hi, pitch, lo):
                # ray length: to ground or to range, whichever first
                L = rng if a <= 0 else min(rng, height / math.sin(math.radians(a)))
                pts.append((x0 + L * math.cos(math.radians(a)) * scale,
                            gy - height * scale + L * math.sin(math.radians(a)) * scale))
            o.append(f'<polygon points="{" ".join(f"{x:.1f},{y:.1f}" for x,y in pts)}" fill="{colour}" fill-opacity="0.28"/>')
            if pitch > 0:
                g = hit(height, pitch)
                if g <= rng:
                    o.append(f'<circle cx="{x0+g*scale:.1f}" cy="{gy}" r="3" fill="{colour}"/>'
                             f'<text x="{x0+g*scale+6:.1f}" y="{gy-6}" fill="{colour}" font-size="10">axis {g:.0f} m</text>')
        beam(h, lp, ls, lr, "#ffe9a8")
        if tp:
            beam(TAXI_H, tp, ts, tr, "#ffb347")
        o.append(f'<circle cx="{x0}" cy="{gy-h*scale:.1f}" r="3" fill="#fff"/>')
    o.append(f'<text x="10" y="{H-10}" fill="#8896a8" font-size="10">Landing (pale) and nose-gear taxi (amber) beam cones, level ground. Offline geometry, not a Unity capture.</text></svg>')
    return "\n".join(o)

if __name__ == "__main__":
    problems = check()
    for p in problems:
        print("FAIL", p)
    out = sys.argv[1] if len(sys.argv) > 1 else "docs/testing/aircraft-lighting-2026-10-07/beam-geometry.svg"
    open(out, "w").write(svg())
    print("wrote", out, "- checks:", "FAILED" if problems else "all pass")
    sys.exit(1 if problems else 0)
