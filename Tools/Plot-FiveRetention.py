"""Plot recorded PhysX trajectories; this is a diagnostic plot, not a game screenshot."""
import argparse, json
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

p=argparse.ArgumentParser();p.add_argument('input');p.add_argument('output');p.add_argument('--scenario',default='wide-carrier-switch')
a=p.parse_args();rows=json.loads(Path(a.input).read_text(encoding='utf-8-sig'))['trials']
rows=[r for r in rows if r['scenario']==a.scenario and r['mirror']==0 and r['continuation']=='fixed-twelve-seconds']
if len(rows)!=8 or any(not r['frames'] for r in rows):raise ValueError('Expected eight complete recorded trajectories')
canvas=Image.new('RGB',(1800,1040),'#f7f8fa');draw=ImageDraw.Draw(canvas)
font=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',16)
small=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',13)
title=ImageFont.truetype('C:/Windows/Fonts/arialbd.ttf',24)
draw.text((25,15),f'Recorded physical trajectories: {a.scenario}',fill='#20212c',font=title)
draw.text((25,50),f'Same start, seed {rows[0]["seed"]}; first 6 seconds. Dark line: ball. Red/blue: team movement. D/A: defender/attacker.',fill='#20212c',font=font)
for index,r in enumerate(sorted(rows,key=lambda r:r['candidate'])):
    left=20+(index%4)*445;top=95+(index//4)*465
    def project(p):return (left+15+(p['x']+65)/130*410,top+75+(38-p['y'])/76*345)
    box=(left+15,top+75,left+425,top+420)
    draw.rectangle(box,fill='#e5efde',outline='#b3c3ad')
    draw.line([project({'x':-65,'y':0}),project({'x':65,'y':0})],fill='white',width=1)
    draw.line([project({'x':0,'y':-38}),project({'x':0,'y':38})],fill='white',width=1)
    frames=[f for f in r['frames'] if f['time']<=r['frames'][0]['time']+6]
    draw.line([project(f['ball']) for f in frames],fill='#20212c',width=3)
    first,last=frames[0],frames[-1]
    for i in range(10):
        if i%5==0:continue
        c='#d84343' if i<5 else '#285ca6'
        trail=[f['players'][i] for f in frames]
        draw.line([project(v) for v in trail],fill=c,width=1)
        start,end=first['players'][i],last['players'][i]
        x,y=project(start);draw.ellipse((x-3,y-3,x+3,y+3),outline=c,width=1)
        x,y=project(end);draw.ellipse((x-5,y-5,x+5,y+5),fill=c,outline='white')
        draw.text((x+4,y+4),('D' if i%5<3 else 'A')+str((i%5-1)%2+1),fill=c,font=small)
    safe=r.get('safeExitAt',-1);text=f'{safe:.2f}s' if safe>=0 else 'none in 12s'
    draw.text((left+15,top+8),f"T{r['candidate']+1} | safe exit: {text}",fill='#20212c',font=font)
    draw.text((left+15,top+32),f"12s receptions {r['ownReceptions']} | conceded {r['enemyGoals']}",fill='#20212c',font=small)
    draw.text((left+15,top+425),'x: -65 to 65 m; z: -38 to 38 m',fill='#505663',font=small)
out=Path(a.output)
if out.exists():raise ValueError('Preserve prior plot')
canvas.save(out)
print(out)
