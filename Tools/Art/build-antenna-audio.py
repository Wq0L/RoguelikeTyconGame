"""Original mono electrical/mechanical foley. Python stdlib; no external samples."""
import math, random, struct, wave
from pathlib import Path
out=Path(__file__).resolve().parents[2]/'Assets/Art/LateGame/AntennaFamily/Audio'
out.mkdir(parents=True,exist_ok=True)
rate=22050
for name,duration in [('Settle',.32),('Snap',.18),('Line',.32),('Circuit',.46)]:
    rng=random.Random(314); values=[]
    for i in range(round(rate*duration)):
        t=i/rate
        if name=='Settle':
            v=math.sin(2*math.pi*140*t)*math.exp(-t*20)*.3+rng.uniform(-1,1)*math.exp(-t*38)*.3
        else:
            pulses=[0,.19] if name=='Circuit' else [0]
            v=0
            for start in pulses:
                x=t-start
                if x<0: continue
                freq=900 if name=='Snap' else 540
                v+=(math.sin(2*math.pi*(freq*x-420*x*x))*.28+rng.uniform(-1,1)*.17)*math.exp(-x*22)*min(1,x*800)
        values.append(v*min(1,(duration-t)*70))
    peak=max(map(abs,values))
    with wave.open(str(out/(name+'.wav')),'wb') as w:
        w.setnchannels(1);w.setsampwidth(2);w.setframerate(rate)
        w.writeframes(b''.join(struct.pack('<h',round(v/peak*.48*32767)) for v in values))
    print(name, len(values), 'samples')
