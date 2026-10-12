"""Original soft electrical click; no sampled or third-party audio."""
import math, random, struct, wave
from pathlib import Path
p=Path('Assets/Art/LateGame/CritArc');p.mkdir(parents=True,exist_ok=True)
r=random.Random(12026);rate=22050;duration=.12
with wave.open(str(p/'CritArcClick.wav'),'wb') as w:
 w.setparams((1,2,rate,0,'NONE','not compressed'))
 samples=[]
 for i in range(round(rate*duration)):
  t=i/rate
  v=(math.sin(2*math.pi*(1100*t-1800*t*t))*.23+r.uniform(-1,1)*.12)*math.exp(-t*48)*min(1,t*1200)*min(1,(duration-t)*120)
  samples.append(struct.pack('<h',round(v*32767)))
 w.writeframes(b''.join(samples))
