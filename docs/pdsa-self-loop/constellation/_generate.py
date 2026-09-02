#!/usr/bin/env python3
"""Draw the whole PDSA graph as a constellation.

This is not an artist's impression — it is the graph. Every star and every line
below is one node or one edge read out of the project's Kùzu memory, the same
data `pdsa view` renders in a browser:

    pdsa view --port 5177 --project akka-graph-loop --no-open &
    curl -s "http://localhost:5177/api/graph?project=akka-graph-loop" -o graph-snapshot.json

(The viewer binds `localhost` specifically — `127.0.0.1` is rejected as an
invalid hostname.) A trimmed snapshot lives beside this script so the picture is
reproducible without a running viewer; regenerate it with the two lines above,
then:

    python docs/pdsa-self-loop/constellation/_generate.py

Layout is a deterministic force-directed simulation (Fruchterman–Reingold with a
fixed seed), which is what gives the graph its constellation shape — the same
force layout the viewer uses. English only: one image is shared by both language
versions of the write-up.

Color follows the reserved status palette (good / warning / critical) rather than
a categorical one, because a verdict is a state, not a series. Verdict is never
carried by color alone: each state also has its own glyph and a direct label.
"""

import json
import math
import os
import random

HERE = os.path.dirname(os.path.abspath(__file__))
SNAPSHOT = os.path.join(HERE, "graph-snapshot.json")
OUT = os.path.join(HERE, "constellation.svg")

W, H = 1600, 1000
SURFACE_TOP, SURFACE_BOTTOM = "#0d1426", "#070a14"

# Reserved status palette (dataviz reference instance). Validated on this surface:
# CVD separation ΔE 11.3 (protan) / 24.4 (tritan), all four ≥ 3:1 contrast.
VERDICT = {
    "met":     ("#0ca30c", "star"),      # good
    "partial": ("#fab219", "diamond"),   # warning
    "unmet":   ("#d03b3b", "square"),    # critical
    "none":    ("#8a93a8", "ring"),      # absence of a verdict — deliberately neutral
}
INK, INK_DIM, INK_FAINT = "#e8edf9", "#9aa6c2", "#5d6885"
PHASE_COLOR = "#7c8bb5"
NEXT_COLOR = "#5f7bd6"
REINFORCE_COLOR = "#e0725f"


# ── data ────────────────────────────────────────────────────────────────────
def load():
    g = json.load(open(SNAPSHOT, encoding="utf-8"))
    nodes = {n["id"]: n for n in g["nodes"]}
    return nodes, g["edges"], g


def verdict_of(cycle_id, nodes, edges):
    """A cycle's verdict lives on its study phase."""
    p = nodes.get(f"Phase:{cycle_id}-study")
    v = (p or {}).get("props", {}).get("verdict", "") if p else ""
    return v if v in VERDICT else "none"


# ── layout: deterministic Fruchterman–Reingold ──────────────────────────────
def layout(nodes, edges, iterations=600, seed=7):
    rng = random.Random(seed)
    ids = list(nodes)
    idx = {n: i for i, n in enumerate(ids)}
    n = len(ids)

    # Seed cycles on a spiral so the chronological spine is legible before forces
    # start; phases start near their cycle. A pure random seed gives a valid but
    # unreadable tangle.
    pos = []
    for nid in ids:
        kind = nodes[nid]["kind"]
        if kind == "Cycle":
            k = int(nid.split(":")[1])
            a = k * 0.62
            r = 40 + 17 * k
            pos.append([math.cos(a) * r, math.sin(a) * r])
        elif kind == "Phase":
            k = int(nid.split(":")[1].split("-")[0])
            a = k * 0.62
            r = 40 + 17 * k
            pos.append([math.cos(a) * r + rng.uniform(-14, 14),
                        math.sin(a) * r + rng.uniform(-14, 14)])
        else:
            pos.append([rng.uniform(-6, 6), rng.uniform(-6, 6)])

    links = [(idx[e["from"]], idx[e["to"]]) for e in edges
             if e["from"] in idx and e["to"] in idx]

    area = 900.0 * 900.0
    k = math.sqrt(area / n)
    temp = 180.0
    for step in range(iterations):
        disp = [[0.0, 0.0] for _ in range(n)]

        for i in range(n):
            for j in range(i + 1, n):
                dx = pos[i][0] - pos[j][0]
                dy = pos[i][1] - pos[j][1]
                d2 = dx * dx + dy * dy
                if d2 < 1e-6:
                    dx, dy, d2 = rng.uniform(-1, 1), rng.uniform(-1, 1), 1.0
                d = math.sqrt(d2)
                f = (k * k) / d
                disp[i][0] += dx / d * f
                disp[i][1] += dy / d * f
                disp[j][0] -= dx / d * f
                disp[j][1] -= dy / d * f

        for a, b in links:
            dx = pos[a][0] - pos[b][0]
            dy = pos[a][1] - pos[b][1]
            d = math.hypot(dx, dy) or 1e-6
            f = (d * d) / k
            disp[a][0] -= dx / d * f
            disp[a][1] -= dy / d * f
            disp[b][0] += dx / d * f
            disp[b][1] += dy / d * f

        for i in range(n):
            d = math.hypot(*disp[i]) or 1e-6
            lim = min(d, temp)
            pos[i][0] += disp[i][0] / d * lim
            pos[i][1] += disp[i][1] / d * lim
        temp = max(temp * 0.975, 0.6)

    return {nid: pos[idx[nid]] for nid in ids}


def fit(pos, pad=70, top=110, bottom=150):
    """Scale to fit and then centre — a min(sx, sy) fit alone letterboxes the graph."""
    xs = [p[0] for p in pos.values()]
    ys = [p[1] for p in pos.values()]
    w, h = (max(xs) - min(xs)) or 1, (max(ys) - min(ys)) or 1
    box_w, box_h = W - 2 * pad, H - top - bottom
    s = min(box_w / w, box_h / h)
    off_x = pad + (box_w - w * s) / 2
    off_y = top + (box_h - h * s) / 2
    return {nid: (off_x + (p[0] - min(xs)) * s, off_y + (p[1] - min(ys)) * s)
            for nid, p in pos.items()}


# ── glyphs (secondary encoding: verdict is never color alone) ───────────────
def glyph(shape, x, y, r, fill):
    if shape == "star":
        pts = []
        for i in range(8):
            rr = r if i % 2 == 0 else r * 0.4
            a = -math.pi / 2 + i * math.pi / 4
            pts.append(f"{x + math.cos(a) * rr:.1f},{y + math.sin(a) * rr:.1f}")
        return f'<polygon points="{" ".join(pts)}" fill="{fill}"/>'
    if shape == "diamond":
        return (f'<polygon points="{x:.1f},{y-r:.1f} {x+r*0.8:.1f},{y:.1f} '
                f'{x:.1f},{y+r:.1f} {x-r*0.8:.1f},{y:.1f}" fill="{fill}"/>')
    if shape == "square":
        s = r * 0.78
        return f'<rect x="{x-s:.1f}" y="{y-s:.1f}" width="{2*s:.1f}" height="{2*s:.1f}" rx="1.5" fill="{fill}"/>'
    return (f'<circle cx="{x:.1f}" cy="{y:.1f}" r="{r*0.72:.1f}" fill="none" '
            f'stroke="{fill}" stroke-width="2"/>')


def arc(p1, p2, bow=0.32):
    """A curved path — used for REINFORCES so a back-link never hides under the spine."""
    (x1, y1), (x2, y2) = p1, p2
    mx, my = (x1 + x2) / 2, (y1 + y2) / 2
    dx, dy = x2 - x1, y2 - y1
    return f"M{x1:.1f},{y1:.1f} Q{mx - dy * bow:.1f},{my + dx * bow:.1f} {x2:.1f},{y2:.1f}"


def main():
    nodes, edges, g = load()
    pos = fit(layout(nodes, edges))
    counts = {t: sum(1 for e in edges if e["type"] == t)
              for t in ("HAS_CYCLE", "HAS_PHASE", "NEXT", "REINFORCES")}

    s = [f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {W} {H}" width="{W}" height="{H}" '
         f'font-family="Inter, Segoe UI, Helvetica, Arial, sans-serif" role="img" '
         f'aria-label="The PDSA graph as a constellation: {len(nodes)} nodes and {len(edges)} edges, '
         f'22 cycles chained by NEXT with 5 REINFORCES arcs looping back">',
         '<defs>',
         '<radialGradient id="sky" cx="50%" cy="45%" r="75%">'
         f'<stop offset="0%" stop-color="{SURFACE_TOP}"/><stop offset="100%" stop-color="{SURFACE_BOTTOM}"/>'
         '</radialGradient>',
         '<radialGradient id="core" cx="50%" cy="50%" r="50%">'
         '<stop offset="0%" stop-color="#cdd8f5" stop-opacity="0.85"/>'
         '<stop offset="60%" stop-color="#7f8fc4" stop-opacity="0.18"/>'
         '<stop offset="100%" stop-color="#7f8fc4" stop-opacity="0"/></radialGradient>',
         '</defs>',
         f'<rect width="{W}" height="{H}" fill="url(#sky)"/>']

    # star dust — decorative only, deterministic
    rng = random.Random(11)
    for _ in range(150):
        x, y, r = rng.uniform(0, W), rng.uniform(0, H), rng.uniform(0.4, 1.1)
        s.append(f'<circle cx="{x:.0f}" cy="{y:.0f}" r="{r:.1f}" fill="#ffffff" opacity="{rng.uniform(0.04,0.16):.2f}"/>')

    # title
    s.append(f'<text x="60" y="62" fill="{INK}" font-size="30" font-weight="700">'
             'The loop&#8217;s compounding becomes a constellation</text>')
    s.append(f'<text x="60" y="92" fill="{INK_DIM}" font-size="15">'
             f'{len(nodes)} nodes &#183; {len(edges)} edges &#183; read from the project&#8217;s own graph memory '
             f'(<tspan font-style="italic">pdsa view</tspan> renders the same data)</text>')

    proj = next(n for n in nodes if nodes[n]["kind"] == "Project")

    # edges, faintest first so the spine stays on top
    for e in edges:
        if e["from"] not in pos or e["to"] not in pos:
            continue
        a, b = pos[e["from"]], pos[e["to"]]
        t = e["type"]
        if t == "HAS_PHASE":
            s.append(f'<line x1="{a[0]:.1f}" y1="{a[1]:.1f}" x2="{b[0]:.1f}" y2="{b[1]:.1f}" '
                     f'stroke="{PHASE_COLOR}" stroke-width="0.8" opacity="0.30"/>')
        elif t == "HAS_CYCLE":
            s.append(f'<line x1="{a[0]:.1f}" y1="{a[1]:.1f}" x2="{b[0]:.1f}" y2="{b[1]:.1f}" '
                     f'stroke="{INK_FAINT}" stroke-width="0.7" opacity="0.20"/>')
    for e in edges:
        if e["type"] == "NEXT" and e["from"] in pos:
            a, b = pos[e["from"]], pos[e["to"]]
            s.append(f'<line x1="{a[0]:.1f}" y1="{a[1]:.1f}" x2="{b[0]:.1f}" y2="{b[1]:.1f}" '
                     f'stroke="{NEXT_COLOR}" stroke-width="2" opacity="0.85"/>')
    for e in edges:
        if e["type"] == "REINFORCES" and e["from"] in pos:
            s.append(f'<path d="{arc(pos[e["from"]], pos[e["to"]])}" fill="none" '
                     f'stroke="{REINFORCE_COLOR}" stroke-width="2.2" stroke-dasharray="6 4" opacity="0.95"/>')

    # project core
    px, py = pos[proj]
    s.append(f'<circle cx="{px:.1f}" cy="{py:.1f}" r="46" fill="url(#core)"/>')
    s.append(f'<circle cx="{px:.1f}" cy="{py:.1f}" r="7" fill="{INK}" opacity="0.9"/>')
    s.append(f'<text x="{px:.1f}" y="{py + 30:.1f}" fill="{INK_DIM}" font-size="13" '
             f'text-anchor="middle">akka-graph-loop</text>')

    # phase dots
    for nid, node in nodes.items():
        if node["kind"] != "Phase":
            continue
        x, y = pos[nid]
        s.append(f'<circle cx="{x:.1f}" cy="{y:.1f}" r="2.6" fill="{PHASE_COLOR}" opacity="0.75"/>')

    # cycle stars + direct labels
    for nid, node in nodes.items():
        if node["kind"] != "Cycle":
            continue
        cid = int(nid.split(":")[1])
        color, shape = VERDICT[verdict_of(cid, nodes, edges)]
        x, y = pos[nid]
        s.append(f'<circle cx="{x:.1f}" cy="{y:.1f}" r="13" fill="{color}" opacity="0.16"/>')
        s.append(glyph(shape, x, y, 7.5, color))
        s.append(f'<text x="{x:.1f}" y="{y - 15:.1f}" fill="{INK}" font-size="13" font-weight="600" '
                 f'text-anchor="middle">#{cid}</text>')

    # legend — status colors always ship with a glyph and a label
    lx, ly = 60, H - 132
    s.append(f'<rect x="{lx-16}" y="{ly-30}" width="560" height="118" rx="12" fill="#0a1020" opacity="0.72" '
             f'stroke="{INK_FAINT}" stroke-opacity="0.35"/>')
    s.append(f'<text x="{lx}" y="{ly-8}" fill="{INK_DIM}" font-size="12" font-weight="700" '
             f'letter-spacing="2.4">CYCLE VERDICT</text>')
    for i, (name, label) in enumerate([("met", "met"), ("partial", "partial"),
                                       ("unmet", "unmet"), ("none", "no verdict")]):
        color, shape = VERDICT[name]
        gx = lx + 10 + i * 132
        s.append(glyph(shape, gx, ly + 16, 7.5, color))
        s.append(f'<text x="{gx + 15}" y="{ly + 21}" fill="{INK}" font-size="14">{label}</text>')

    s.append(f'<text x="{lx}" y="{ly + 52}" fill="{INK_DIM}" font-size="12" font-weight="700" '
             f'letter-spacing="2.4">EDGES</text>')
    items = [(NEXT_COLOR, "solid", f'NEXT &#215;{counts["NEXT"]}'),
             (REINFORCE_COLOR, "dash", f'REINFORCES &#215;{counts["REINFORCES"]}'),
             (PHASE_COLOR, "thin", f'HAS_PHASE &#215;{counts["HAS_PHASE"]}'),
             (INK_FAINT, "thin", f'HAS_CYCLE &#215;{counts["HAS_CYCLE"]}')]
    for i, (color, style, label) in enumerate(items):
        gx = lx + 10 + i * 132
        dash = ' stroke-dasharray="6 4"' if style == "dash" else ""
        wdt = 2.2 if style != "thin" else 1
        s.append(f'<line x1="{gx-8}" y1="{ly+68}" x2="{gx+8}" y2="{ly+68}" stroke="{color}" '
                 f'stroke-width="{wdt}"{dash} opacity="0.95"/>')
        s.append(f'<text x="{gx + 15}" y="{ly + 72}" fill="{INK}" font-size="13">{label}</text>')

    s.append(f'<text x="{W-60}" y="{H-40}" fill="{INK_FAINT}" font-size="13" text-anchor="end">'
             'Every cycle carries its four phases; NEXT chains them in time; '
             'REINFORCES loops back to what was left unproven.</text>')
    s.append("</svg>")

    with open(OUT, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(s) + "\n")
    print(f"{OUT}: {len(nodes)} nodes, {len(edges)} edges, {counts}")


if __name__ == "__main__":
    main()
