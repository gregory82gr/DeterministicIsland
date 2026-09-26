"""Figures for the guide. The network below mirrors ProbabilisticCore/MiniNeuralNetwork.cs
exactly (same weights, same inverted dropout), so every chart is computed, not drawn by hand."""

import itertools
import math
import os

import matplotlib

matplotlib.use("Agg")
import matplotlib.pyplot as plt  # noqa: E402
import numpy as np  # noqa: E402
from matplotlib.patches import FancyArrowPatch, FancyBboxPatch, Circle  # noqa: E402

INK, MUTED, ACCENT, STOCH, SAFE, ALERT, RULE = "#1f2933", "#52606d", "#1d4e89", "#c05621", "#2f855a", "#b83280", "#cbd2d9"
LIGHT_ACCENT, LIGHT_STOCH, LIGHT_SAFE, LIGHT_ALERT, PANEL = "#dbe8f5", "#fbe3d3", "#dcefe3", "#f7dbeb", "#f0f4f8"

plt.rcParams.update({
    "font.family": "DejaVu Sans", "font.size": 9, "axes.edgecolor": MUTED, "axes.labelcolor": INK,
    "xtick.color": MUTED, "ytick.color": MUTED, "axes.spines.top": False, "axes.spines.right": False,
    "axes.titleweight": "bold", "axes.titlesize": 10, "figure.dpi": 100,
})

# --- MiniNeuralNetwork.cs, reproduced -------------------------------------------------
HIDDEN_W = [[1.8, 0.6], [-0.7, 1.9], [1.1, 1.3], [0.4, -1.2]]
HIDDEN_B = [-0.9, -0.8, -1.0, 0.3]
OUT_W = [1.4, 1.6, 0.9, -1.1]
OUT_B = -1.2
DROPOUT = 0.2


def hidden(t, p):
    x = [t / 100.0, p / 10.0]
    zs = [HIDDEN_B[h] + HIDDEN_W[h][0] * x[0] + HIDDEN_W[h][1] * x[1] for h in range(4)]
    return x, zs, [math.tanh(z) for z in zs]


def forward(t, p, mask):
    _, _, a = hidden(t, p)
    logit = OUT_B + sum(OUT_W[h] * a[h] / (1 - DROPOUT) for h in range(4) if mask[h])
    return logit, 1 / (1 + math.exp(-logit))


def mask_distribution(t, p):
    out = []
    for m in itertools.product([1, 0], repeat=4):
        prob = math.prod((1 - DROPOUT) if k else DROPOUT for k in m)
        out.append((m, prob, forward(t, p, m)[1]))
    return out


def exact_mc(t, p):
    d = mask_distribution(t, p)
    mean = sum(pr * y for _, pr, y in d)
    sd = math.sqrt(sum(pr * (y - mean) ** 2 for _, pr, y in d))
    return mean, sd, 1.96 * sd


# --- helpers ------------------------------------------------------------------------------

def _save(fig, out_dir, name):
    path = os.path.join(out_dir, name)
    fig.savefig(path, dpi=220, bbox_inches="tight", facecolor="white")
    plt.close(fig)
    return path


def _box(ax, x, y, w, h, text, fc, ec, fs=8.6, bold=False, tc=INK, r=0.02):
    ax.add_patch(FancyBboxPatch((x, y), w, h, boxstyle=f"round,pad=0.004,rounding_size={r}",
                                fc=fc, ec=ec, lw=1.1))
    ax.text(x + w / 2, y + h / 2, text, ha="center", va="center", fontsize=fs, color=tc,
            fontweight="bold" if bold else "normal", wrap=True)


def _arrow(ax, x1, y1, x2, y2, color=MUTED, style="-|>", lw=1.1, ls="-", rad=0.0):
    ax.add_patch(FancyArrowPatch((x1, y1), (x2, y2), arrowstyle=style, mutation_scale=10,
                                 color=color, lw=lw, linestyle=ls, connectionstyle=f"arc3,rad={rad}"))


def _canvas(w, h):
    fig, ax = plt.subplots(figsize=(w, h))
    ax.set_xlim(0, 1)
    ax.set_ylim(0, 1)
    ax.axis("off")
    return fig, ax


# --- figures --------------------------------------------------------------------------------

def fig_architecture(d):
    fig, ax = _canvas(7.2, 4.6)
    _box(ax, 0.02, 0.40, 0.15, 0.20, "Sensor\nreading\n(T, P, leak,\nnotes)", PANEL, MUTED)
    _box(ax, 0.26, 0.66, 0.25, 0.26, "Stochastic core\nMiniNeuralNetwork\n2-4-1 MLP + dropout\n(MC Dropout ×200)", LIGHT_STOCH, STOCH)
    _box(ax, 0.26, 0.08, 0.25, 0.44, "", LIGHT_ACCENT, ACCENT)
    ax.text(0.385, 0.49, "Deterministic shell", ha="center", fontsize=8.8, fontweight="bold", color=ACCENT)
    for i, t in enumerate(["Static Vault (SHA-256)", "Protection islands", "Shield  min(p, 75%)", "Causality Lock"]):
        _box(ax, 0.28, 0.37 - i * 0.075, 0.21, 0.058, t, "white", ACCENT, fs=6.9)
    _box(ax, 0.60, 0.34, 0.18, 0.32, "System\nArbiter\n\nprecedence ·\npriority rings ·\nSCRAM ·\nescalation", "white", INK, bold=False)
    _box(ax, 0.86, 0.56, 0.13, 0.14, "Valve\ncommand", LIGHT_SAFE, SAFE)
    _box(ax, 0.86, 0.30, 0.13, 0.16, "Human\n(operator,\nreview)", LIGHT_ALERT, ALERT)
    _box(ax, 0.60, 0.05, 0.39, 0.14, "Domain events → audit trail (JSONL) · event log\nhash-chained vault file", PANEL, MUTED, fs=7.4)
    _arrow(ax, 0.17, 0.52, 0.26, 0.76)
    _arrow(ax, 0.17, 0.48, 0.26, 0.32)
    _arrow(ax, 0.51, 0.78, 0.60, 0.58, STOCH)
    ax.text(0.53, 0.72, "proposal\n± uncertainty", fontsize=7, color=STOCH)
    _arrow(ax, 0.51, 0.30, 0.60, 0.42, ACCENT)
    ax.text(0.515, 0.25, "facts,\nlimits", fontsize=7, color=ACCENT)
    _arrow(ax, 0.78, 0.56, 0.86, 0.63, SAFE)
    _arrow(ax, 0.78, 0.44, 0.86, 0.39, ALERT, rad=0.0)
    _arrow(ax, 0.86, 0.35, 0.78, 0.40, ALERT, ls="--")
    ax.text(0.785, 0.33, "override", fontsize=6.8, color=ALERT)
    _arrow(ax, 0.69, 0.34, 0.69, 0.19, MUTED, ls="--")
    return _save(fig, d, "architecture.png")


def fig_control_cycle(d):
    fig, ax = _canvas(7.0, 8.4)
    steps = [
        ("1. Read sensors; engage the Causality Lock\nif the notes request a 'deterministic island'", PANEL, MUTED),
        ("2. AI proposal: MC Dropout mean of 200 passes\n(or one Frozen Snapshot pass under the lock)", LIGHT_STOCH, STOCH),
        ("3. Build islands from the vault at time t;\nkeep the triggered ones; raise IslandTriggered", LIGHT_ACCENT, ACCENT),
    ]
    y = 0.93
    for t, fc, ec in steps:
        _box(ax, 0.1, y - 0.055, 0.8, 0.055, t, fc, ec, fs=7.7)
        _arrow(ax, 0.5, y - 0.055, 0.5, y - 0.075)
        y -= 0.085
    _box(ax, 0.3, y - 0.06, 0.4, 0.06, "Any island triggered?", "white", INK, bold=True)
    yq = y - 0.03
    # left branch: no island
    ax.text(0.18, yq + 0.01, "no", color=MUTED, fontsize=8)
    ax.text(0.78, yq + 0.01, "yes", color=MUTED, fontsize=8)
    _arrow(ax, 0.3, yq, 0.24, yq - 0.085)
    _arrow(ax, 0.7, yq, 0.76, yq - 0.085)
    left = [
        ("Operator override given?\n→ apply it (audit: OperatorOverride)", LIGHT_ALERT, ALERT),
        ("Lock engaged and no Frozen Snapshot?\n→ escalate (DeterminismViolation)", LIGHT_ALERT, ALERT),
        ("95% half-width > vault limit (0.20)?\n→ escalate (UncertaintyEscalation)", LIGHT_ALERT, ALERT),
        ("Shield: min(proposal, 75%)\n→ AiApproved / AiShielded", LIGHT_SAFE, SAFE),
    ]
    right = [
        ("Sort by priority ring\nCriticalSafety > Structural > Operational", LIGHT_ACCENT, ACCENT),
        ("Top tier disagrees?\n→ FAIL-SAFE SCRAM, valve 0%,\nRequiresHumanReview", LIGHT_ALERT, ALERT),
        ("Otherwise the top island wins\n→ IslandOverride", LIGHT_ACCENT, ACCENT),
        ("Operator override? recorded,\nnot applied: islands keep authority", PANEL, MUTED),
    ]
    yy = yq - 0.14
    for (lt, lfc, lec), (rt, rfc, rec) in zip(left, right):
        _box(ax, 0.02, yy - 0.04, 0.44, 0.085, lt, lfc, lec, fs=7.3)
        _box(ax, 0.54, yy - 0.04, 0.44, 0.085, rt, rfc, rec, fs=7.3)
        yy -= 0.115
        if yy > 0.1:
            _arrow(ax, 0.24, yy + 0.075, 0.24, yy + 0.05)
            _arrow(ax, 0.76, yy + 0.075, 0.76, yy + 0.05)
    _box(ax, 0.1, 0.005, 0.8, 0.05, "Every path publishes exactly one ControlDecisionMade → audit trail", PANEL, MUTED, fs=7.8)
    return _save(fig, d, "control_cycle.png")


def fig_neuron(d):
    fig, ax = _canvas(6.6, 2.6)
    for i, (lbl, y) in enumerate([("x₁ = T/100", 0.75), ("x₂ = P/10", 0.25)]):
        ax.add_patch(Circle((0.1, y), 0.07, fc=PANEL, ec=MUTED))
        ax.text(0.1, y, lbl.split(" ")[0], ha="center", va="center", fontsize=9)
        ax.text(0.1, y - 0.14, lbl, ha="center", fontsize=7.5, color=MUTED)
        _arrow(ax, 0.17, y, 0.42, 0.5, ACCENT)
        ax.text(0.28, (y + 0.5) / 2 + (0.06 if y > 0.5 else -0.09), f"w{i + 1}", fontsize=8.5, color=ACCENT)
    ax.add_patch(Circle((0.5, 0.5), 0.09, fc=LIGHT_ACCENT, ec=ACCENT, lw=1.3))
    ax.text(0.5, 0.5, "Σ + b", ha="center", va="center", fontsize=9.5, fontweight="bold")
    _arrow(ax, 0.59, 0.5, 0.7, 0.5)
    _box(ax, 0.7, 0.38, 0.13, 0.24, "tanh(z)", "white", ACCENT, fs=9)
    _arrow(ax, 0.83, 0.5, 0.95, 0.5)
    ax.text(0.965, 0.5, "a", fontsize=11, va="center")
    ax.text(0.5, 0.2, "z = w₁x₁ + w₂x₂ + b", ha="center", fontsize=9, color=INK)
    return _save(fig, d, "neuron.png")


def fig_network(d):
    fig, ax = _canvas(7.0, 4.2)
    inputs = [(0.08, 0.68, "x₁\nT/100"), (0.08, 0.32, "x₂\nP/10")]
    hid = [(0.46, 0.86), (0.46, 0.62), (0.46, 0.38), (0.46, 0.14)]
    outp = (0.84, 0.5)
    for h, (hx, hy) in enumerate(hid):
        for i, (ix, iy, _) in enumerate(inputs):
            w = HIDDEN_W[h][i]
            ax.plot([ix + 0.05, hx - 0.05], [iy, hy], color=ACCENT if w > 0 else STOCH,
                    lw=0.6 + 1.3 * abs(w) / 2, alpha=0.8)
        w = OUT_W[h]
        ax.plot([hx + 0.05, outp[0] - 0.06], [hy, outp[1]], color=ACCENT if w > 0 else STOCH,
                lw=0.6 + 1.3 * abs(w) / 2, alpha=0.8)
        ax.text((hx + outp[0]) / 2 + 0.02, (hy + outp[1]) / 2 + 0.02, f"{w:+.1f}", fontsize=7, color=MUTED)
    for ix, iy, t in inputs:
        ax.add_patch(Circle((ix, iy), 0.05, fc=PANEL, ec=MUTED))
        ax.text(ix, iy, t, ha="center", va="center", fontsize=7)
    for h, (hx, hy) in enumerate(hid):
        ax.add_patch(Circle((hx, hy), 0.05, fc=LIGHT_ACCENT, ec=ACCENT))
        ax.text(hx, hy, f"h{h + 1}", ha="center", va="center", fontsize=8, fontweight="bold")
        ax.text(hx + 0.065, hy + 0.045, f"b={HIDDEN_B[h]:+.1f}", fontsize=6.8, color=MUTED)
        ax.add_patch(FancyBboxPatch((hx - 0.03, hy - 0.105), 0.06, 0.03, boxstyle="round,pad=0.002",
                                    fc=LIGHT_STOCH, ec=STOCH, lw=0.6))
        ax.text(hx, hy - 0.09, "p=0.2", fontsize=5.8, ha="center", va="center", color=STOCH)
    ax.add_patch(Circle(outp, 0.06, fc=LIGHT_SAFE, ec=SAFE))
    ax.text(outp[0], outp[1], "σ", ha="center", va="center", fontsize=12, fontweight="bold")
    ax.text(outp[0], outp[1] - 0.12, "valve\nopening\n(0…1)", ha="center", va="top", fontsize=7.5)
    ax.text(outp[0] + 0.07, outp[1] + 0.07, f"b={OUT_B:+.1f}", fontsize=6.8, color=MUTED)
    ax.set_ylim(-0.06, 1)
    ax.text(0.08, -0.04, "input", ha="center", color=MUTED, fontsize=8)
    ax.text(0.46, -0.04, "hidden (tanh) + dropout", ha="center", color=MUTED, fontsize=8)
    ax.text(0.84, -0.04, "output (sigmoid)", ha="center", color=MUTED, fontsize=8)
    return _save(fig, d, "network.png")


def fig_activations(d):
    z = np.linspace(-4, 4, 400)
    fig, axes = plt.subplots(1, 2, figsize=(7.0, 2.4))
    axes[0].plot(z, np.tanh(z), color=ACCENT, lw=2)
    axes[0].set_title("tanh — hidden layer")
    axes[0].axhline(0, color=RULE, lw=0.8)
    axes[1].plot(z, 1 / (1 + np.exp(-z)), color=SAFE, lw=2)
    axes[1].set_title("sigmoid — output layer")
    axes[1].axhline(0.5, color=RULE, lw=0.8)
    for a in axes:
        a.set_xlabel("z")
        a.grid(alpha=0.2)
    fig.tight_layout()
    return _save(fig, d, "activations.png")


def fig_mask_distribution(d):
    dist = sorted(mask_distribution(60, 5), key=lambda r: r[2])
    mean, sd, hw = exact_mc(60, 5)
    fig, ax = plt.subplots(figsize=(7.0, 3.0))
    for m, pr, y in dist:
        ax.bar(y, pr, width=0.009, color=ACCENT if sum(m) == 4 else STOCH, alpha=0.9)
        if pr > 0.02:
            ax.text(y, pr + 0.008, "".join(str(k) for k in m), ha="center", fontsize=6.5, color=MUTED, rotation=90)
    ax.axvline(mean, color=INK, lw=1.2)
    ax.axvspan(mean - hw, mean + hw, color=ACCENT, alpha=0.08)
    ax.text(mean + 0.004, 0.43, f"mean {mean:.3f}", fontsize=8)
    ax.text(mean - hw + 0.004, 0.36, f"95% band ±{hw:.3f}", fontsize=8, color=ACCENT)
    ax.set_xlabel("valve opening proposed by the network (reading: 60 °C, 5 bar)")
    ax.set_ylabel("probability")
    ax.set_ylim(0, 0.48)
    fig.tight_layout()
    return _save(fig, d, "mask_distribution.png")


def _grid():
    T = np.linspace(20, 100, 81)
    P = np.linspace(1, 10, 91)
    return T, P


def fig_uncertainty_map(d):
    T, P = _grid()
    H = np.array([[exact_mc(t, p)[2] for t in T] for p in P])
    fig, ax = plt.subplots(figsize=(7.0, 4.2))
    im = ax.imshow(H, origin="lower", extent=[T[0], T[-1], P[0], P[-1]], aspect="auto", cmap="Oranges")
    cs = ax.contour(T, P, H, levels=[0.20], colors=[INK], linewidths=1.4)
    ax.clabel(cs, fmt={0.20: "±0.20 limit"}, fontsize=7.5)
    ax.axvline(90, color=ACCENT, ls="--", lw=1)
    ax.axhline(8, color=ACCENT, ls="--", lw=1)
    ax.text(91, 1.3, "Structural island\nT ≥ 90 °C", color=ACCENT, fontsize=7.5)
    ax.text(21, 8.2, "Pressure island  P ≥ 8 bar", color=ACCENT, fontsize=7.5)
    for (t, p, lbl) in [(60, 5, "S1"), (85, 7, "S9"), (30, 5, "tests")]:
        ax.plot(t, p, "o", color=INK, ms=4)
        ax.text(t + 1, p + 0.15, lbl, fontsize=8, fontweight="bold")
    cb = fig.colorbar(im, ax=ax)
    cb.set_label("95% half-width of the MC Dropout interval")
    ax.set_xlabel("temperature (°C)")
    ax.set_ylabel("pressure (bar)")
    fig.tight_layout()
    return _save(fig, d, "uncertainty_map.png")


def fig_policy_map(d):
    T, P = _grid()
    M = np.array([[exact_mc(t, p)[0] for t in T] for p in P])
    fig, ax = plt.subplots(figsize=(7.0, 4.2))
    im = ax.imshow(M, origin="lower", extent=[T[0], T[-1], P[0], P[-1]], aspect="auto", cmap="Blues", vmin=0, vmax=1)
    cs = ax.contour(T, P, M, levels=[0.75], colors=[STOCH], linewidths=1.3)
    ax.clabel(cs, fmt={0.75: "shield 75%"}, fontsize=7.5)
    ax.add_patch(plt.Rectangle((90, 1), 10, 9, fc="none", ec=ACCENT, hatch="//", lw=0.8))
    ax.add_patch(plt.Rectangle((20, 8), 80, 2, fc="none", ec=ALERT, hatch="\\\\", lw=0.8))
    ax.text(91, 2, "islands take\ncontrol here", fontsize=7.5, color=ACCENT)
    cb = fig.colorbar(im, ax=ax)
    cb.set_label("expected valve opening (MC Dropout mean)")
    ax.set_xlabel("temperature (°C)")
    ax.set_ylabel("pressure (bar)")
    fig.tight_layout()
    return _save(fig, d, "policy_map.png")


def fig_vault_pipeline(d):
    fig, ax = _canvas(7.2, 1.9)
    items = [("' High Pressure\nRelief Setpoint\nin bar '", PANEL, MUTED), ("normalise\ntrim + lower", "white", ACCENT),
             ("SHA-256\n6294ef16…c33f", "white", ACCENT), ("versions\nv1, v2, …", LIGHT_ACCENT, ACCENT),
             ("valid at t?\n→ vector", "white", ACCENT), ("8.0 bar\n(v1, approver)", LIGHT_SAFE, SAFE)]
    w = 0.145
    for i, (t, fc, ec) in enumerate(items):
        x = 0.005 + i * (w + 0.022)
        _box(ax, x, 0.25, w, 0.5, t, fc, ec, fs=7.2)
        if i < len(items) - 1:
            _arrow(ax, x + w, 0.5, x + w + 0.022, 0.5)
    return _save(fig, d, "vault_pipeline.png")


def fig_version_timeline(d):
    fig, ax = plt.subplots(figsize=(7.0, 2.7))
    rows = [
        ("PT-2 limit", [(0, 8.6, "v1 · 8.0 bar", LIGHT_ACCENT), (8.6, 12, "v2 · 7.6 bar", LIGHT_SAFE)]),
        ("relief setpoint\n(derived: min)", [(0, 8.6, "v1 · 8.0", LIGHT_ACCENT), (8.6, 12, "v2 · 7.6 (auto)", LIGHT_SAFE)]),
        ("backup-line pointer", [(1, 8.6, "→ setpoint v1", PANEL), (8.6, 12, "STALE", LIGHT_ALERT)]),
    ]
    for r, (lbl, segs) in enumerate(rows):
        for a, b, t, c in segs:
            ax.barh(r, b - a, left=a, color=c, edgecolor=MUTED, height=0.55)
            ax.text((a + b) / 2, r, t, ha="center", va="center", fontsize=7.6)
    ax.axvline(8.6, color=ALERT, ls="--", lw=1)
    ax.text(8.65, 2.45, "PT-2 recalibrated", color=ALERT, fontsize=7.5)
    ax.set_yticks(range(len(rows)))
    ax.set_yticklabels([r[0] for r in rows], fontsize=8)
    ax.invert_yaxis()
    ax.set_xticks([0, 1, 8.6])
    ax.set_xticklabels(["1 Jan", "1 Feb", "update date"], fontsize=7.5)
    ax.set_xlim(0, 12)
    ax.set_xlabel("time →  (ValidFrom inclusive, ValidTo exclusive)")
    fig.tight_layout()
    return _save(fig, d, "version_timeline.png")


def fig_pointer_chain(d):
    fig, ax = _canvas(7.2, 3.0)
    _box(ax, 0.02, 0.62, 0.2, 0.22, "'backup line B\nsetpoint'\nIsDeterministic=false\nPointerTo →", PANEL, MUTED, fs=7)
    _box(ax, 0.3, 0.62, 0.2, 0.22, "'relief setpoint'\nv1 · 8.0 bar\nIsDeterministic=true", LIGHT_SAFE, SAFE, fs=7)
    _arrow(ax, 0.22, 0.73, 0.3, 0.73, ACCENT, lw=1.6)
    ax.text(0.53, 0.73, "resolved ✓", fontsize=9, color=SAFE, va="center")
    fails = [("cycle\nA → B → A", 0.02), ("> 8 hops", 0.26), ("dangling\n(target missing)", 0.5), ("stale\n(target superseded)", 0.74)]
    for t, x in fails:
        _box(ax, x, 0.08, 0.22, 0.28, t + "\n→ fail loudly", LIGHT_ALERT, ALERT, fs=7.4)
    ax.text(0.5, 0.45, "Failure modes: none of them may fall back to a stochastic answer", ha="center", fontsize=8, color=ALERT)
    return _save(fig, d, "pointer_chain.png")


def fig_derived_dag(d):
    fig, ax = _canvas(7.2, 2.9)
    for i, (t, v) in enumerate([("PT-1", "8.4"), ("PT-2", "8.0 → 7.6"), ("PT-3", "8.2")]):
        _box(ax, 0.02, 0.76 - i * 0.26, 0.2, 0.18, f"{t} limit\n{v} bar", LIGHT_ACCENT, ACCENT, fs=7.6)
        _arrow(ax, 0.22, 0.85 - i * 0.26, 0.36, 0.5, ACCENT)
    _box(ax, 0.36, 0.38, 0.16, 0.24, "min(·)\nrule 'minimum'", "white", INK, fs=8)
    _arrow(ax, 0.52, 0.5, 0.6, 0.5)
    _box(ax, 0.6, 0.36, 0.18, 0.28, "relief setpoint\n8.0 → 7.6 bar\nDerivedFrom = [ids]", LIGHT_SAFE, SAFE, fs=7.4)
    _arrow(ax, 0.78, 0.5, 0.84, 0.5)
    _box(ax, 0.84, 0.36, 0.15, 0.28, "High Pressure\nEmergency\nLoop island", LIGHT_ACCENT, ACCENT, fs=7.2)
    ax.text(0.6, 0.08, "a new PT version re-registers the derived island automatically (cascading)",
            ha="center", fontsize=7.6, color=MUTED)
    return _save(fig, d, "derived_dag.png")


def fig_authority(d):
    fig, ax = _canvas(7.0, 3.4)
    levels = [
        ("Fail-safe SCRAM (same-tier conflict)", LIGHT_ALERT, ALERT),
        ("Triggered safety island (priority ring)", LIGHT_ACCENT, ACCENT),
        ("Named operator override", "#fde8f3", ALERT),
        ("Frozen Snapshot (only under Causality Lock)", PANEL, MUTED),
        ("Stochastic AI, bounded by Shield and uncertainty limit", LIGHT_STOCH, STOCH),
    ]
    for i, (t, fc, ec) in enumerate(levels):
        w = 0.5 + i * 0.1
        _box(ax, 0.5 - w / 2, 0.8 - i * 0.18, w, 0.14, t, fc, ec, fs=8)
    ax.text(0.02, 0.86, "more\nauthority", fontsize=8, color=MUTED)
    ax.text(0.02, 0.06, "more\nflexibility", fontsize=8, color=MUTED)
    _arrow(ax, 0.06, 0.2, 0.06, 0.8, MUTED)
    return _save(fig, d, "authority.png")


def fig_hash_chain(d):
    fig, ax = _canvas(7.2, 2.0)
    prev = "000…000"
    for i in range(3):
        x = 0.02 + i * 0.33
        _box(ax, x, 0.2, 0.28, 0.62, f"line {i + 1}\nPreviousHash = {prev}\nRecord = {{…}}\nHash = SHA-256(Prev + Record)", PANEL if i else LIGHT_ACCENT, ACCENT, fs=6.8)
        prev = ["98c8…", "3fa1…", "…"][i]
        if i < 2:
            _arrow(ax, x + 0.28, 0.51, x + 0.33, 0.51, ACCENT, lw=1.4)
    return _save(fig, d, "hash_chain.png")


def fig_layers(d):
    fig, ax = _canvas(7.0, 3.0)
    layers = [("Presentation", "DeterministicIsland.Api (minimal API) · Program.cs demo (CLI)", "#e2e8f0"),
              ("Application", "NexusRuntime: wiring, commissioning, request serialisation", "#d9e2ec"),
              ("Domain", "Arbiter · StaticVault · islands · Shield · Causality Lock\nMiniNeuralNetwork · Neural Constitution", LIGHT_ACCENT),
              ("Infrastructure", "JsonLinesIslandRepository (hash chain) · audit/event listeners", "#e8e1f5")]
    for i, (n, t, c) in enumerate(layers):
        _box(ax, 0.02, 0.76 - i * 0.24, 0.18, 0.19, n, c, MUTED, bold=True, fs=8)
        _box(ax, 0.23, 0.76 - i * 0.24, 0.75, 0.19, t, "white", MUTED, fs=7.8)
    return _save(fig, d, "layers.png")


def fig_events(d):
    fig, ax = _canvas(7.2, 2.8)
    _box(ax, 0.02, 0.6, 0.2, 0.25, "StaticVault", LIGHT_ACCENT, ACCENT)
    _box(ax, 0.02, 0.15, 0.2, 0.25, "Arbiter", LIGHT_ACCENT, ACCENT)
    _box(ax, 0.36, 0.3, 0.22, 0.4, "Domain\nEventBus\n(Observer)", "white", INK, bold=True)
    for (y, t) in [(0.73, "IslandAdded"), (0.36, "IslandTriggered\nControlDecisionMade")]:
        _arrow(ax, 0.22, y, 0.38, 0.5, ACCENT)
        ax.text(0.23, y + (0.04 if y > 0.5 else -0.16), t, fontsize=7, color=ACCENT)
    for i, t in enumerate(["AuditLogListener\n→ nexus1-audit.jsonl", "JsonLinesEventLog\n→ nexus1-events.jsonl", "InMemoryEventRecorder\nor your own listener"]):
        _box(ax, 0.68, 0.72 - i * 0.3, 0.3, 0.2, t, PANEL, MUTED, fs=7.2)
        _arrow(ax, 0.58, 0.5, 0.68, 0.82 - i * 0.3)
    return _save(fig, d, "events.png")


def fig_float(d):
    fig, ax = plt.subplots(figsize=(7.0, 2.2))
    a, b, c = 1e16, -1e16, 1.0
    vals = [("(a + b) + c", (a + b) + c), ("a + (b + c)", a + (b + c))]
    ax.barh([0, 1], [v for _, v in vals], color=[SAFE, ALERT], height=0.5)
    ax.set_yticks([0, 1])
    ax.set_yticklabels([n for n, _ in vals])
    for i, (_, v) in enumerate(vals):
        ax.text(v + 0.02, i, f"= {v}", va="center")
    ax.set_xlim(0, 1.4)
    ax.set_title("a = 1e16, b = −1e16, c = 1.0 in IEEE-754 double")
    fig.tight_layout()
    return _save(fig, d, "float.png")


def build_all(out_dir):
    os.makedirs(out_dir, exist_ok=True)
    names = [fig_architecture, fig_control_cycle, fig_neuron, fig_network, fig_activations,
             fig_mask_distribution, fig_uncertainty_map, fig_policy_map, fig_vault_pipeline,
             fig_version_timeline, fig_pointer_chain, fig_derived_dag, fig_authority, fig_hash_chain,
             fig_layers, fig_events, fig_float]
    return {f.__name__[4:]: f(out_dir) for f in names}
