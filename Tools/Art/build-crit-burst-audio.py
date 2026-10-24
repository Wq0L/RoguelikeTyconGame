"""Original compact comic thump, no external samples."""
import math,struct,wave,random
from pathlib import Path
p=Path('Assets/Art/LateGame/CritBurst');p.mkdir(parents=True,exist_ok=True)
r=random.Random(83);rate=22050;duration=.19;values=[]
for i in range(round(rate*duration)):
 t=i/rate;u=t/duration;env=min(t/.004,1)*(1-u)**2
 tone=math.sin(2*math.pi*(190*t-270*t*t))
 v=(tone*.42+r.uniform(-1,1)*.18*math.exp(-t*35))*env
 values.append(struct.pack('<h',round(v*32767)))
with wave.open(str(p/'CritBurstThump.wav'),'wb') as w:
 w.setparams((1,2,rate,0,'NONE','not compressed'));w.writeframes(b''.join(values))
