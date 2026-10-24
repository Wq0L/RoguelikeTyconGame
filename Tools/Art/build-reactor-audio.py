"""Original quiet ascending energy charge, no external samples."""
import math, struct, wave
from pathlib import Path
p=Path('Assets/Art/LateGame/RareReactor');p.mkdir(parents=True,exist_ok=True)
rate=22050;duration=.24;values=[]
for i in range(round(rate*duration)):
 t=i/rate;env=math.sin(math.pi*t/duration)**1.6
 v=(math.sin(2*math.pi*(330*t+550*t*t))+.18*math.sin(2*math.pi*660*t))*.28*env
 values.append(struct.pack('<h',round(v*32767)))
with wave.open(str(p/'ReactorCharge.wav'),'wb') as w:
 w.setparams((1,2,rate,0,'NONE','not compressed'));w.writeframes(b''.join(values))
