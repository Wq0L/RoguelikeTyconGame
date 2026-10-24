"""Original soft tok with two quiet short echoes; no external samples."""
import math, random, struct, wave
from pathlib import Path
out=Path('Assets/Art/LateGame/HarvestEcho');out.mkdir(parents=True,exist_ok=True)
rate=22050;duration=.22;r=random.Random(312);samples=[]
for i in range(round(rate*duration)):
 t=i/rate;v=0
 for start,gain in [(0,1),(.045,.24),(.083,.09)]:
  x=t-start
  if x<0:continue
  v+=gain*(math.sin(2*math.pi*(360*x-650*x*x))*.42+r.uniform(-1,1)*.025)*math.exp(-x*70)*min(1,x*1500)
 samples.append(struct.pack('<h',round(v*min(1,(duration-t)*80)*32767)))
with wave.open(str(out/'EchoTok.wav'),'wb') as w:
 w.setparams((1,2,rate,0,'NONE','not compressed'));w.writeframes(b''.join(samples))
