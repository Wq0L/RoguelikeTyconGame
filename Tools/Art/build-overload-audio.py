"""Original quiet two-note charge and small discharge; no external samples."""
import math,random,struct,wave
from pathlib import Path
p=Path('Assets/Art/LateGame/Overload');p.mkdir(parents=True,exist_ok=True)
r=random.Random(99);rate=22050
for name,duration in [('OverloadCharge',.16),('OverloadRelease',.11)]:
 values=[]
 for i in range(round(rate*duration)):
  t=i/rate;u=t/duration;env=math.sin(math.pi*u)**2
  v=(math.sin(2*math.pi*(410*t+430*t*t))*.25 if name=='OverloadCharge' else (math.sin(2*math.pi*260*t)*.2+r.uniform(-1,1)*.1*math.exp(-t*40)))*env
  values.append(struct.pack('<h',round(v*32767)))
 with wave.open(str(p/(name+'.wav')),'wb') as w:
  w.setparams((1,2,rate,0,'NONE','not compressed'));w.writeframes(b''.join(values))
