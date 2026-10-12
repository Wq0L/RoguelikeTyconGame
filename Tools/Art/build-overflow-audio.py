"""Original soft energy fup. Standard library, no external samples."""
import math, random, struct, wave
from pathlib import Path
out=Path('Assets/Art/LateGame/Overflow');out.mkdir(parents=True,exist_ok=True)
r=random.Random(511);rate=22050;duration=.16;values=[];noise=0
for i in range(round(rate*duration)):
 t=i/rate;noise=.86*noise+.14*r.uniform(-1,1)
 env=math.sin(math.pi*min(1,t/duration))**.6*math.exp(-t*15)
 value=(math.sin(2*math.pi*(260*t-550*t*t))*.45+noise*.3)*env
 values.append(struct.pack('<h',round(value*32767)))
with wave.open(str(out/'OverflowFup.wav'),'wb') as w:
 w.setparams((1,2,rate,0,'NONE','not compressed'));w.writeframes(b''.join(values))
