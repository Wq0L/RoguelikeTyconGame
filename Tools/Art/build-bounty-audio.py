"""Original airy seed chimes, Python stdlib synthesis; no samples or new dependencies."""
import math,random,struct,wave
from pathlib import Path
out=Path(__file__).resolve().parents[2]/'Assets/Art/LateGame/WanderingBounty/Audio'
out.mkdir(parents=True,exist_ok=True)
rate=22050
for name,duration,notes in [('Arrive',.62,[523.25,659.25]),('Progress',.19,[784]),('Bloom',.84,[523.25,659.25,1046.5])]:
 rng=random.Random(78);values=[];low=0
 for i in range(round(duration*rate)):
  t=i/rate;u=t/duration;low+=.08*(rng.uniform(-1,1)-low);v=0
  for j,f in enumerate(notes):
   tau=t-j*.085
   if tau>=0:v+=math.sin(2*math.pi*f*tau)*math.exp(-tau*7)*min(1,tau*100)*.25
  values.append((v+low*.045)*min(1,(duration-t)*30))
 peak=max(map(abs,values))
 with wave.open(str(out/(name+'.wav')),'wb') as w:
  w.setnchannels(1);w.setsampwidth(2);w.setframerate(rate);w.writeframes(b''.join(struct.pack('<h',round(x/peak*.45*32767)) for x in values))
 print(name,len(values),'.45 peak')
