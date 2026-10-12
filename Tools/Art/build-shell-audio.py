"""Original bark creaks and comic selection cues; no samples or dependencies."""
import math,random,struct,wave
from pathlib import Path
out=Path(__file__).resolve().parents[2]/'Assets/Art/LateGame/GraftedShell/Audio'
out.mkdir(parents=True,exist_ok=True)
rate=22050
for k,(name,duration) in enumerate([('Prepare',.8),('Choice',.38),('Close',.52),('Open',.72)]):
 rng=random.Random(490+k); values=[];low=0;phase=0
 for i in range(int(rate*duration)):
  t=i/rate;u=t/duration;n=rng.uniform(-1,1);low+=.13*(n-low)
  freq=95+35*k+(160*u if k==3 else -45*u)
  phase+=2*math.pi*freq/rate
  env=math.sin(math.pi*u)**.6
  if k==1: env=math.exp(-t*9)*min(1,t*500)
  snap=math.exp(-((u-(.65 if k==2 else .25))/.028)**2)
  values.append(env*(low*.65+math.sin(phase)*(.2 if k==1 else .06))+snap*n*.2)
 peak=max(map(abs,values))
 with wave.open(str(out/(name+'.wav')),'wb') as w:
  w.setnchannels(1);w.setsampwidth(2);w.setframerate(rate)
  w.writeframes(b''.join(struct.pack('<h',round(v/peak*.55*32767)) for v in values))
 print(name,duration)
