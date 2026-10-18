"""Render conditional 8x8 physical measurements, never a universal win-rate chart."""
import argparse
import json
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

p=argparse.ArgumentParser();p.add_argument('--input',required=True);p.add_argument('--output',required=True);a=p.parse_args()
report=json.loads(Path(a.input).read_text(encoding='utf-8-sig'))
out=Path(a.output)
if out.exists():raise ValueError('Preserve existing plots')
font_path='C:/Windows/Fonts/malgun.ttf'
def font(size):return ImageFont.truetype(font_path,size)
image=Image.new('RGB',(1780,3810),'#f5f6f8');d=ImageDraw.Draw(image)
d.text((32,20),'P7 · 상황과 체력을 분리한 8×8 실제 물리 비교',font=font(28),fill='#162536')
d.text((32,64),'각 칸은 동일 시작 조건 2seed × 2진영 평균. 색은 패널 안 주지표의 유불리이며 승률이 아닙니다.',font=font(19),fill='#415366')
scenarios=[('central-opening','열린 중앙 · 최초3초 제어 공 위치','m, 높을수록 전진'),
           ('deep-goal-block','골문 앞 블록 · 공격 측 득점','골, 높을수록 유리'),
           ('wide-trap-window','측면 압박 · 수비 측 공 소유','초, 높을수록 유리'),
           ('distant-receiver-denial','먼 수신자 · 상대 안정 수신','회, 낮을수록 유리'),
           ('central-running-threat','달리는 상대 · 최초 안정 회수','초, 낮을수록 빠름 / 미회수12초'),
           ('central-approaching-threat','중앙 슈팅 거리 접근 · 수비 측 실점','골, 낮을수록 유리'),
           ('rear-pressure-far-receiver','후방 압박 · 최초3초 제어 공 위치','m, 높을수록 전진'),
           ('wide-carrier-switch','넓은 탈압박 · 최초3초 안전 탈출','성공 비율0~1, 높을수록 유리'),
           ('nearby-loose-ball','가까운 자유 공 · 최초3초 공 소유','초, 높을수록 유리')]
names=report['commandNames']
for row,(scenario,title,unit) in enumerate(scenarios):
    for col,energy in enumerate(['100','35']):
        x=24+col*876;y=108+row*404
        d.rounded_rectangle((x,y,x+852,y+388),radius=12,fill='white',outline='#d5dde5')
        d.text((x+16,y+12),title+' / 시작 체력 '+energy+'%',font=font(19),fill='#162536')
        d.text((x+16,y+42),'행: 비교 명령 · 열: 상대 명령  |  '+unit,font=font(15),fill='#52697e')
        contexts=[c for c in report['contexts'] if c['scenario']==scenario and c['phase'].endswith('-'+energy)]
        contexts.sort(key=lambda c:names.index(c['opponent']))
        if len(contexts)!=8:raise ValueError('Incomplete matrix')
        metric=contexts[0]['metric'];direction=contexts[0]['direction']
        values=[c['values'][i][metric] for c in contexts for i in range(8)]
        lo=min(values);hi=max(values);gx=x+150;gy=y+108;cw=85;ch=32
        for j,name in enumerate(names):
            parts=name.split(' ');label=' '.join(parts[:2])+'\n'+' '.join(parts[2:])
            d.multiline_text((gx+j*cw+4,y+70),label,font=font(13),fill='#22384e',spacing=1)
        for i,name in enumerate(names):
            d.text((x+12,gy+i*ch+7),name,font=font(14),fill='#22384e')
            for j,c in enumerate(contexts):
                v=c['values'][i][metric];t=(v-lo)/(hi-lo) if hi>lo else .5
                if direction<0:t=1-t
                bad=(248,223,218);good=(195,229,218)
                color=tuple(round(bad[k]*(1-t)+good[k]*t) for k in range(3))
                bx=gx+j*cw;by=gy+i*ch
                d.rectangle((bx,by,bx+cw-2,by+ch-2),fill=color)
                label=f'{v:.2f}';d.text((bx+cw/2,by+ch/2),label,font=font(15),fill='#182f39',anchor='mm')
d.text((32,3750),'초기 상태·공 소유·체력·상대가 바뀌면 결과도 달라집니다. 고정12초 실험이며 자연 경기의 사용률과 구분합니다.',font=font(16),fill='#415366')
d.text((32,3780),'제어 공 위치 -62는 제어 표본 없음 표지입니다. 실제 후퇴 위치나 전진 거리로 해석하지 않습니다.',font=font(16),fill='#415366')
image.save(out)
print(out)
