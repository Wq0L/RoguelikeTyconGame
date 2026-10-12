"""Original sprout leaf/seed foley; standard library only, no recordings or external samples."""
import math, random, struct, wave
from pathlib import Path
out=Path(__file__).resolve().parents[2]/'Assets/Art/LateGame/InvasiveSprout/Audio'
out.mkdir(parents=True,exist_ok=True)
rate=22050
for kind,(name,duration) in enumerate([('Unfurl',.65),('Spread',.28),('LeafHit',.17),('Wilt',.72)]):
    rng=random.Random(3810+kind);values=[];filtered=0;phase=0
    for i in range(int(rate*duration)):
        t=i/rate;u=t/duration;n=rng.uniform(-1,1);filtered+=.2*(n-filtered)
        phase+=2*math.pi*(150+kind*35+100*math.sin(u*math.pi))/rate
        if kind==2: env=min(1,t*800)*math.exp(-t*28);sound=filtered*.6+n*.2+math.sin(phase)*.3
        elif kind==3: env=math.sin(math.pi*u)**1.5;sound=filtered*.65+n*.09+math.sin(phase)*.025
        else: env=math.sin(math.pi*u)**.9;sound=filtered*.45+n*.04+math.sin(phase)*.11
        values.append(sound*env)
    peak=max(abs(x) for x in values)
    with wave.open(str(out/(name+'.wav')),'wb') as w:
        w.setnchannels(1);w.setsampwidth(2);w.setframerate(rate)
        w.writeframes(b''.join(struct.pack('<h',round(v/peak*.55*32767)) for v in values))
    print(name,duration)
