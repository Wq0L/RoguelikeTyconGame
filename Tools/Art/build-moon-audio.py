"""Original brief air/metal sweeps, synthesized with Python stdlib; no external samples."""
import math,random,struct,wave
from pathlib import Path
out=Path(__file__).resolve().parents[2]/'Assets/Art/LateGame/CrescentFamily/Audio'
out.mkdir(parents=True,exist_ok=True)
for name,duration,start,end in [('Launch',.25,780,330),('Grow',.19,620,1080),('Return',.4,420,240)]:
    rng=random.Random(671);rate=22050;values=[];noise=0
    for i in range(round(rate*duration)):
        t=i/rate;u=t/duration;noise+=.3*(rng.uniform(-1,1)-noise)
        env=math.sin(math.pi*u)**1.5
        tone=math.sin(2*math.pi*(start*t+(end-start)*t*t/(2*duration)))
        values.append(env*(noise*.62+tone*.13))
    peak=max(map(abs,values))
    with wave.open(str(out/(name+'.wav')),'wb') as f:
        f.setnchannels(1);f.setsampwidth(2);f.setframerate(rate);f.writeframes(b''.join(struct.pack('<h',round(v/peak*.42*32767)) for v in values))
