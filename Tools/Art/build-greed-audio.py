"""Original short preparation and soft harvesting sweep; no samples."""
import math, random, struct, wave
from pathlib import Path
p=Path('Assets/Art/LateGame/GreedyHarvest');p.mkdir(parents=True,exist_ok=True)
r=random.Random(37);rate=22050
for name,duration in [('GreedReady',.16),('GreedSpent',.13)]:
 values=[]
 for i in range(round(rate*duration)):
  t=i/rate;u=t/duration;env=math.sin(math.pi*u)**1.5
  v=(math.sin(2*math.pi*(380*t+400*t*t))*.26 if name=='GreedReady' else (math.sin(2*math.pi*(180*t-200*t*t))*.22+r.uniform(-1,1)*.12))*env
  values.append(struct.pack('<h',round(v*32767)))
 with wave.open(str(p/(name+'.wav')),'wb') as w:
  w.setparams((1,2,rate,0,'NONE','not compressed'));w.writeframes(b''.join(values))
