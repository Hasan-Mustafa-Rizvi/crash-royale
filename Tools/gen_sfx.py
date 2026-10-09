"""Synthesizes every sound effect in the game (no samples, so no licensing questions).

Usage, from the repo root:  python Tools/gen_sfx.py [out_dir]     (default: Assets/Audio/SFX)
"""
import os
import sys
import wave

import numpy as np
from scipy import signal

SR = 44100
OUT = sys.argv[1] if len(sys.argv) > 1 else "Assets/Audio/SFX"
os.makedirs(OUT, exist_ok=True)
rng = np.random.default_rng(7)


def t_axis(dur):
    return np.arange(int(SR * dur)) / SR


def env(n, attack=0.005, decay=None, curve=4.0):
    """Linear attack then exponential-ish decay to zero over the rest."""
    a = max(1, int(SR * attack))
    e = np.ones(n)
    e[:a] = np.linspace(0, 1, a)
    rest = n - a
    if rest > 0:
        x = np.linspace(0, 1, rest)
        e[a:] = (1 - x) ** curve
    return e


def lowpass(x, cutoff, order=2):
    b, a = signal.butter(order, min(cutoff, SR * 0.45) / (SR / 2), "low")
    return signal.lfilter(b, a, x)


def highpass(x, cutoff, order=2):
    b, a = signal.butter(order, cutoff / (SR / 2), "high")
    return signal.lfilter(b, a, x)


def bandpass(x, lo, hi, order=2):
    b, a = signal.butter(order, [lo / (SR / 2), min(hi, SR * 0.45) / (SR / 2)], "band")
    return signal.lfilter(b, a, x)


def sweep_lowpass(x, start, end):
    """Time-varying one-pole low-pass (for booms whose brightness fades)."""
    out = np.zeros_like(x)
    cut = np.geomspace(start, end, len(x))
    alpha = 1 - np.exp(-2 * np.pi * cut / SR)
    y = 0.0
    for i, v in enumerate(x):
        y += alpha[i] * (v - y)
        out[i] = y
    return out


def noise(dur):
    return rng.uniform(-1, 1, int(SR * dur))


def sine_sweep(f0, f1, dur, geometric=True):
    n = int(SR * dur)
    f = np.geomspace(f0, f1, n) if geometric else np.linspace(f0, f1, n)
    return np.sin(2 * np.pi * np.cumsum(f) / SR)


def tone(freq, dur, kind="sine", vibrato=0.0):
    t = t_axis(dur)
    f = freq * (1 + vibrato * np.sin(2 * np.pi * 5.5 * t))
    ph = 2 * np.pi * np.cumsum(f) / SR
    if kind == "sine":
        return np.sin(ph)
    if kind == "square":
        return signal.square(ph) * 0.6
    if kind == "saw":
        return signal.sawtooth(ph) * 0.6
    if kind == "tri":
        return signal.sawtooth(ph, 0.5)
    raise ValueError(kind)


def bell(freq, dur, partials=((1, 1.0), (2.76, 0.45), (5.4, 0.25), (8.9, 0.12))):
    t = t_axis(dur)
    out = np.zeros_like(t)
    for ratio, amp in partials:
        out += amp * np.sin(2 * np.pi * freq * ratio * t) * np.exp(-t * (3 + ratio * 1.8))
    return out


def brass(freq, dur, brightness=2500):
    x = tone(freq, dur, "saw") + 0.5 * tone(freq * 1.003, dur, "saw")
    n = len(x)
    e = np.minimum(1, np.arange(n) / (SR * 0.03)) * np.exp(-np.arange(n) / (SR * dur * 0.9))
    return lowpass(x, brightness) * e


def mix(*parts):
    n = max(len(p) for p in parts)
    out = np.zeros(n)
    for p in parts:
        out[: len(p)] += p
    return out


def at(x, start, total):
    out = np.zeros(int(SR * total))
    s = int(SR * start)
    end = min(len(out), s + len(x))
    out[s:end] += x[: end - s]
    return out


def reverb(x, amount=0.18, length=0.35):
    n = int(SR * length)
    ir = rng.normal(0, 1, n) * np.exp(-np.linspace(0, 6, n))
    ir = lowpass(ir, 5000)
    wet = signal.fftconvolve(x, ir)
    wet = np.concatenate([wet, np.zeros(max(0, len(x) + n - len(wet)))])[: len(x) + n]
    wet /= max(1e-9, np.max(np.abs(wet)))
    dry = np.concatenate([x, np.zeros(n)])
    return dry + amount * wet * np.max(np.abs(x))


def write(name, x, peak=0.89, rev=0.0, rev_len=0.35):
    if rev > 0:
        x = reverb(x, rev, rev_len)
    # fade tail to avoid clicks
    fade = min(len(x) // 4, int(SR * 0.01))
    if fade > 0:
        x[-fade:] *= np.linspace(1, 0, fade)
    x = x - np.mean(x)
    m = np.max(np.abs(x))
    if m > 0:
        x = x / m * peak
    data = (x * 32767).astype(np.int16)
    with wave.open(os.path.join(OUT, name + ".wav"), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(data.tobytes())


# ------------------------------------------------------------------ combat

def deploy():
    d = 0.32
    thump = sine_sweep(180, 70, d) * env(int(SR * d), 0.004, curve=3.5)
    air = bandpass(noise(d), 300, 2500) * env(int(SR * d), 0.01, curve=2.5) * 0.35
    sparkle = sum(np.sin(2 * np.pi * f * t_axis(0.18)) * np.exp(-t_axis(0.18) * 22) for f in (1568, 2093)) * 0.12
    return mix(thump, air, at(sparkle, 0.03, d))


write("deploy", deploy(), rev=0.12)

for i, (f, lp) in enumerate([(130, 2200), (115, 1800), (150, 2600)]):
    d = 0.14
    body = sine_sweep(f * 1.6, f, d) * env(int(SR * d), 0.002, curve=5)
    smack = lowpass(noise(d), lp) * env(int(SR * d), 0.001, curve=9) * 0.9
    write(f"hit_{i}", mix(body, smack), peak=0.8)

for i, base in enumerate([1500, 1760]):
    d = 0.32
    t = t_axis(d)
    metal = sum(a * np.sin(2 * np.pi * base * r * t) * np.exp(-t * k) for r, a, k in [(1, 1, 16), (1.62, 0.6, 20), (2.41, 0.45, 26), (3.3, 0.25, 30)])
    tr = highpass(noise(0.03), 2000) * env(int(SR * 0.03), 0.0005, curve=6)
    thud = sine_sweep(220, 120, 0.08) * env(int(SR * 0.08), 0.001, curve=6) * 0.6
    write(f"sword_{i}", mix(metal * 0.55, tr * 0.8, thud), peak=0.75, rev=0.15)

for i, sweep in enumerate([(5000, 900), (4200, 800)]):
    d = 0.26
    whoosh = bandpass(noise(d), 600, 7000)
    whoosh = sweep_lowpass(whoosh, *sweep) * env(int(SR * d), 0.01, curve=2.2)
    pluck_t = t_axis(0.12)
    pluck = signal.sawtooth(2 * np.pi * (260 + i * 30) * pluck_t) * np.exp(-pluck_t * 40) * 0.35
    write(f"arrow_shoot_{i}", mix(whoosh, lowpass(pluck, 1800)), peak=0.7)

d = 0.12
write("arrow_hit", mix(lowpass(noise(d), 1500) * env(int(SR * d), 0.001, curve=7), sine_sweep(260, 140, d) * env(int(SR * d), 0.001, curve=6) * 0.7), peak=0.75)

d = 0.34
throw = bandpass(noise(d), 400, 3000) * np.sin(np.linspace(0, np.pi, int(SR * d))) ** 2
write("throw", sweep_lowpass(throw, 1200, 3500), peak=0.6)


def boom(d, low=48, bright=4000, tail=3.0):
    n = int(SR * d)
    rumble = lowpass(sweep_lowpass(noise(d), bright, 120), 1600, order=3) * env(n, 0.003, curve=tail)
    sub = sine_sweep(low * 2.2, low, d) * env(n, 0.002, curve=tail * 0.8)
    crack = bandpass(noise(0.05), 900, 3500) * env(int(SR * 0.05), 0.0005, curve=5) * 0.5
    return mix(rumble * 1.1, sub * 0.9, crack)


write("explosion_0", boom(0.9), peak=0.92, rev=0.2, rev_len=0.5)
write("explosion_1", boom(0.8, low=55, bright=3200), peak=0.92, rev=0.2, rev_len=0.5)

d = 0.2
tshot = mix(sine_sweep(320, 160, d) * env(int(SR * d), 0.002, curve=5) * 0.7, bandpass(noise(d), 800, 5000) * env(int(SR * d), 0.002, curve=4) * 0.5)
write("tower_shot", tshot, peak=0.55)


def crumble():
    d = 1.8
    out = boom(1.3, low=40, bright=3000, tail=2.2)
    out = at(out, 0.0, d)
    for k in range(26):
        s = 0.12 + rng.uniform(0, 1.3)
        f = rng.uniform(500, 2500)
        c = bandpass(noise(0.04), f * 0.7, f * 1.4) * env(int(SR * 0.04), 0.0005, curve=6) * rng.uniform(0.1, 0.3)
        out += at(c, s, d)
    return out


write("tower_destroyed", crumble(), peak=0.95, rev=0.25, rev_len=0.6)

d = 0.36
poof = lowpass(sweep_lowpass(noise(d), 2500, 250), 1800, order=3) * env(int(SR * d), 0.01, curve=2.6)
poof += sine_sweep(420, 180, d) * env(int(SR * d), 0.005, curve=4) * 0.25
write("unit_death", poof, peak=0.6)

# ------------------------------------------------------------------ UI

d = 0.06
click = np.sin(2 * np.pi * 1250 * t_axis(d)) * np.exp(-t_axis(d) * 70) + highpass(noise(d), 3000) * np.exp(-t_axis(d) * 160) * 0.2
write("click", click, peak=0.5)

d = 0.1
sel = sine_sweep(900, 1500, d) * env(int(SR * d), 0.002, curve=4)
write("card_select", sel, peak=0.45)

d = 0.22
buzz = (tone(155, d, "square") + tone(162, d, "square")) * env(int(SR * d), 0.003, curve=1.5)
write("invalid", lowpass(buzz, 1800), peak=0.45)

coin = mix(bell(1976, 0.5) * 0.7, at(bell(2637, 0.6), 0.07, 0.67))
write("coin", coin, peak=0.6, rev=0.15)

arp = np.zeros(int(SR * 0.9))
for k, f in enumerate([1047, 1319, 1568, 2093]):
    arp += at(bell(f, 0.5) * 0.6, k * 0.07, 0.9)
write("purchase", mix(arp, at(coin * 0.6, 0.0, 0.9)), peak=0.7, rev=0.25)

crown = np.zeros(int(SR * 0.9))
for k, f in enumerate([523, 659, 784]):
    crown += at(brass(f, 0.35 if k < 2 else 0.6, 3000) * 0.7, k * 0.11, 0.9)
write("crown", crown, peak=0.7, rev=0.25)

d = 0.24
bloop = sine_sweep(380, 980, d) * env(int(SR * d), 0.005, curve=2.0)
bloop += 0.3 * sine_sweep(760, 1960, d) * env(int(SR * d), 0.005, curve=3.0)
write("emote", bloop, peak=0.55)

d = 0.07
tick = bandpass(noise(d), 1500, 4000) * env(int(SR * d), 0.0005, curve=8) + np.sin(2 * np.pi * 1700 * t_axis(d)) * np.exp(-t_axis(d) * 80) * 0.6
write("countdown", tick, peak=0.5)

horn = np.zeros(int(SR * 1.2))
horn += at(mix(brass(392, 0.22, 2600), brass(494, 0.22, 2600) * 0.6, brass(587, 0.22, 2600) * 0.5), 0.0, 1.2)
horn += at(mix(brass(523, 0.75, 3200), brass(659, 0.75, 3200) * 0.6, brass(784, 0.75, 3200) * 0.5), 0.26, 1.2)
write("battle_start", horn, peak=0.8, rev=0.3, rev_len=0.6)

dbl = np.zeros(int(SR * 1.1))
for k, f in enumerate([784, 988, 1175, 1568, 1976]):
    dbl += at(mix(bell(f, 0.6) * 0.5, tone(f, 0.25, "tri") * np.exp(-t_axis(0.25) * 10) * 0.25), k * 0.08, 1.1)
write("double_elixir", dbl, peak=0.7, rev=0.3, rev_len=0.6)

ot = np.zeros(int(SR * 1.0))
for k in range(3):
    ot += at(lowpass(tone(880 if k % 2 == 0 else 660, 0.2, "square"), 2500) * env(int(SR * 0.2), 0.005, curve=1.2), k * 0.24, 1.0)
write("overtime", ot, peak=0.55, rev=0.2)

# ------------------------------------------------------------------ results

vic = np.zeros(int(SR * 2.6))
for k, (f, s, dur) in enumerate([(523, 0.0, 0.18), (659, 0.16, 0.18), (784, 0.32, 0.18), (1047, 0.48, 1.2)]):
    vic += at(mix(brass(f, dur, 3400), brass(f * 2, dur, 3400) * 0.25), s, 2.6)
for f in (523, 659, 784):
    vic += at(brass(f, 1.4, 2800) * 0.35, 0.5, 2.6)
vic += at(bell(2093, 1.2) * 0.25, 0.5, 2.6)
write("victory", vic, peak=0.85, rev=0.35, rev_len=0.8)

dfe = np.zeros(int(SR * 2.4))
for k, (f, s, dur) in enumerate([(392, 0.0, 0.45), (311, 0.42, 0.45), (262, 0.84, 1.3)]):
    dfe += at(lowpass(tone(f, dur, "tri") * env(int(SR * dur), 0.02, curve=1.6), 1500), s, 2.4)
    dfe += at(lowpass(tone(f / 2, dur, "saw"), 600) * env(int(SR * dur), 0.02, curve=1.6) * 0.35, s, 2.4)
write("defeat", dfe, peak=0.75, rev=0.3, rev_len=0.8)

drw = np.zeros(int(SR * 1.6))
for k, (f, s) in enumerate([(523, 0.0), (523, 0.3), (587, 0.6)]):
    drw += at(mix(bell(f, 0.9) * 0.7, brass(f, 0.4, 2000) * 0.3), s, 1.6)
write("draw", drw, peak=0.7, rev=0.3)

print("wrote", len(os.listdir(OUT)), "sounds")
