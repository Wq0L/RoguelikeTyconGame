"""Original Thorn Siege foley synthesis. Python standard library only; deterministic mono PCM."""
import math
from pathlib import Path
import random
import struct
import wave

OUT = Path(__file__).resolve().parents[2] / 'Assets/Art/LateGame/ThornSiege/Audio'
RATE = 22050

def render(name, duration, kind):
    rng = random.Random(3807 + kind)
    samples = []
    smooth = 0.0
    phase = 0.0
    for i in range(int(duration * RATE)):
        t = i / RATE
        u = t / duration
        noise = rng.uniform(-1, 1)
        smooth += .13 * (noise - smooth)
        if kind == 0:  # shifting soil, three soft pulses
            env = math.sin(math.pi * u) ** 2 * (.4 + .6 * math.sin(t * 19) ** 8)
            signal = smooth * 1.5 + math.sin(2 * math.pi * 73 * t) * .12
        elif kind == 1:  # rising root, papery woody creak
            env = math.sin(math.pi * u) ** .6
            phase += 2 * math.pi * (95 + 100 * u) / RATE
            signal = smooth + math.sin(phase) * .2 + noise * .10
        elif kind == 2:  # short dry hit, no metallic ringing
            env = min(t * 1000, 1) * math.exp(-t * 38)
            signal = math.sin(2 * math.pi * (160 * t - 280 * t*t)) * .45 + noise * .25 + smooth
        elif kind == 3:  # staggered snap and loose grit
            env = min(t * 700, 1) * math.exp(-t * 8)
            pops = sum(math.exp(-max(0,t-at)*75) if t >= at else 0 for at in [0,.045,.12])
            signal = smooth + noise * pops * .22 + math.sin(2*math.pi*91*t)*.13
        else:  # soft leaf release, rising breath without a musical reward jingle
            env = math.sin(math.pi * u) ** 1.3
            signal = smooth * (.8 - .5*u) + noise * .09
        samples.append(signal * env)
    peak = max(abs(v) for v in samples)
    data = b''.join(struct.pack('<h', round(v / peak * .65 * 32767)) for v in samples)
    with wave.open(str(OUT / (name + '.wav')), 'wb') as output:
        output.setnchannels(1); output.setsampwidth(2); output.setframerate(RATE); output.writeframes(data)
    return f'{name}: {duration}s, {len(data)} PCM bytes, peak 0.65'

if __name__ == '__main__':
    OUT.mkdir(parents=True, exist_ok=True)
    for k,(name,duration) in enumerate([('Prepare',.8),('Emerge',.52),('Hit',.16),('Break',.46),('Clear',.64)]):
        print(render(name,duration,k))
