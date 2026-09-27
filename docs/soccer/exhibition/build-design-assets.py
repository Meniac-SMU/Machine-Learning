"""Generate only this design package's SVG placeholders, copy table and verified model catalog."""
from pathlib import Path
import csv
import hashlib
import html
import json
import re
import xml.etree.ElementTree as ET
from fontTools.ttLib import TTFont
from fontTools.pens.svgPathPen import SVGPathPen

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[2]
ASSETS = HERE / 'assets'
ASSETS.mkdir(exist_ok=True)
FONTS = {weight: TTFont(ASSETS / 'fonts' / f'Pretendard-{name}.otf') for weight, name in [(400,'Regular'),(500,'Medium'),(600,'SemiBold'),(700,'Bold'),(800,'ExtraBold'),(900,'Black')]}

def write(name, text):
    (HERE / name).write_text(text, encoding='utf-8')

def svg(name, contents, width=1600, height=800):
    # Outline placeholder artwork using Pretendard. SVG-as-image cannot load external fonts.
    # The editable text remains in this generator; the shipped OTF files remain unmodified.
    def outline(match):
        node=ET.fromstring(match.group(0))
        attrs=node.attrib
        size=float(attrs.get('font-size','16'))
        if size <= 36 and name != 'pitch.svg': size=round(size*1.15,2)
        font=FONTS.get(int(attrs.get('font-weight','500')),FONTS[500])
        glyphs=font.getGlyphSet(); cmap=font.getBestCmap(); scale=size/font['head'].unitsPerEm
        content=''.join(node.itertext()); spacing=float(attrs.get('letter-spacing','0'))
        advances=[glyphs[cmap.get(ord(c),'.notdef')].width*scale+spacing for c in content]
        width_=sum(advances)-spacing if advances else 0
        x=float(attrs.get('x','0')); y=float(attrs.get('y','0'))
        anchor=attrs.get('text-anchor','start')
        x-=width_/2 if anchor=='middle' else width_ if anchor=='end' else 0
        paths=[]
        for char,advance in zip(content,advances):
            pen=SVGPathPen(glyphs); glyphs[cmap.get(ord(char),'.notdef')].draw(pen)
            paths.append(f'<path transform="translate({x:.3f} {y}) scale({scale:.6f} {-scale:.6f})" d="{pen.getCommands()}"/>')
            x+=advance
        return f'<g fill="{attrs.get("fill","#000")}" aria-label="{html.escape(content,quote=True)}"><title>{html.escape(content)}</title>{"".join(paths)}</g>'
    contents=re.sub(r'<text\b[^>]*>.*?</text>',outline,contents)
    write('assets/' + name, f'<svg xmlns="http://www.w3.org/2000/svg" width="{width}" height="{height}" viewBox="0 0 {width} {height}">{contents}</svg>')

svg('team-logo.svg', '<path d="M120 240V70h55l65 95 65-95h55v170h-58V160l-62 90-62-90v80z" fill="#c2f16c"/><text x="240" y="322" font-size="66" fill="#f3f4eb" font-weight="900" text-anchor="middle" letter-spacing="8">MENIAC</text>', 480, 360)
svg('game-logo.svg', '<text x="0" y="135" fill="#f3f5eb" font-size="155" font-weight="900" letter-spacing="-6">MANAGER</text>', 800, 160)

def spectrum():
    return ''.join(f'<rect x="{i*267}" y="784" width="267" height="16" fill="{color}"/>' for i,color in enumerate(['#d53a47','#ec8e49','#c6e54b','#429653','#4369c2','#9a78b9']))

for lang in ['ko','en']:
    ko = lang == 'ko'
    svg(f'credits-{lang}.svg', '<rect width="1600" height="800" fill="#152218"/>'
        '<path d="M1260-100C820 180 1230 590 1730 580" fill="none" stroke="#a9d848" stroke-width="155"/>'
        '<path d="M1400-100C960 180 1370 590 1870 580" fill="none" stroke="#705cbd" stroke-width="105"/>'
        '<text x="120" y="160" fill="#d2f18b" font-size="25" letter-spacing="6">MADE BY</text>'
        '<text x="110" y="340" fill="#f1f5e8" font-size="155" font-weight="900">MENIAC</text>'
        f'<text x="120" y="432" fill="#ecf3e0" font-size="36">{"다섯 명의 개발자, 하나의 경기장." if ko else "Five developers. One pitch."}</text>'
        f'<text x="120" y="596" fill="#b9c7ad" font-size="28">{"제작자 이미지가 들어갈 자리입니다." if ko else "Your team credits image goes here."}</text>'
        f'<text x="120" y="645" fill="#b9c7ad" font-size="23">{"최종 팀 소개 이미지로 교체 예정" if ko else "Placeholder artwork"}</text>' + spectrum())
    pages = [
        ('당신의 팀은 RED', 'YOUR TEAM IS RED', '직접 플레이에서는 RED 공격수를 조작합니다.', 'Take control of the RED striker in Play mode.'),
        ('움직이고, 슛하세요', 'MOVE. TURN. STRIKE.', '키보드로 선수의 이동과 킥을 조작합니다.', 'Move your player and kick with the keyboard.'),
        ('AI의 경기를 지켜보세요', 'WATCH AI PLAY', '양 팀 모델을 선택하고 AI 시뮬레이션을 시작하세요.', 'Choose a model for each side and start an AI simulation.')]
    for index, (titleko,titleen,subko,suben) in enumerate(pages,1):
        title=titleko if ko else titleen
        subtitle=subko if ko else suben
        content=f'<rect width="1600" height="800" fill="#ecf0e5"/><text x="100" y="110" fill="#637554" font-size="26" letter-spacing="4">HOW TO PLAY / 0{index}</text><text x="96" y="232" fill="#182a17" font-size="72" font-weight="900">{title}</text><text x="100" y="302" fill="#4b6340" font-size="29">{subtitle}</text>'
        if index == 1:
            content += '<rect x="100" y="382" width="610" height="262" fill="#ca2a3e"/><rect x="890" y="382" width="610" height="262" fill="#25377d"/><text x="405" y="547" fill="#fffaf0" font-size="120" font-weight="900" text-anchor="middle">RED</text><text x="1195" y="547" fill="#fffaf0" font-size="120" font-weight="900" text-anchor="middle">NAVY</text><text x="800" y="532" fill="#263b21" font-size="52" font-weight="900" text-anchor="middle">VS</text>'
            content += f'<text x="405" y="604" fill="#fffaf0" font-size="26" text-anchor="middle">{"플레이어 + AI" if ko else "PLAYER + AI"}</text><text x="1195" y="604" fill="#fffaf0" font-size="26" text-anchor="middle">AI</text>'
        if index == 2:
            for i,(key,captionko,captionen) in enumerate([('W / S','전진 / 후진','Forward / Back'),('A / D','좌 / 우 회전','Turn left / right'),('E','패스','Pass'),('SPACE','슈팅','Shoot')]):
                x=100+i*355
                content+=f'<rect x="{x}" y="390" width="325" height="165" fill="#203822"/><text x="{x+162}" y="497" text-anchor="middle" fill="#d6f38f" font-size="51" font-weight="900">{key}</text><text x="{x+162}" y="613" text-anchor="middle" fill="#2a4223" font-size="28">{captionko if ko else captionen}</text>'
            content+=f'<text x="100" y="719" fill="#516846" font-size="25">{"H : 직접 조작과 AI 조작 전환" if ko else "H : Switch between player and AI control"}</text>'
        if index == 3:
            labels=['모델 선택','5분 / 10분','경기 기록'] if ko else ['CHOOSE MODELS','5 / 10 MINUTES','MATCH STATS']
            values=['RED / NAVY','05:00','2 : 1']
            for i,(caption,value) in enumerate(zip(labels,values)):
                x=100+i*480
                content+=f'<rect x="{x}" y="390" width="440" height="230" fill="#d8e3c8"/><text x="{x+220}" y="502" text-anchor="middle" fill="#203b21" font-size="57" font-weight="900">{value}</text><text x="{x+220}" y="566" text-anchor="middle" fill="#3b5630" font-size="28">{caption}</text>'
            content+=f'<text x="100" y="719" fill="#516846" font-size="25">{"경기 후 종료 버튼을 누르면 시작 화면으로 돌아갑니다." if ko else "After the match, select Exit to return to the title screen."}</text>'
        svg(f'guide-{lang}-{index}.svg',content+spectrum())

# Schematic broadcast view. It deliberately does not imply a Unity runtime capture.
pitch='<defs><linearGradient id="grass" x2="0" y2="1"><stop stop-color="#548c40"/><stop offset="1" stop-color="#36672e"/></linearGradient><pattern id="crowd" width="20" height="15" patternUnits="userSpaceOnUse"><rect width="20" height="15" fill="#182c28"/><circle cx="4" cy="5" r="2.2" fill="#909b7a"/><circle cx="14" cy="10" r="2.4" fill="#8b664d"/></pattern><filter id="shadow"><feDropShadow dx="3" dy="7" stdDeviation="3" flood-opacity=".32"/></filter></defs><rect width="1600" height="900" fill="#193b2b"/><rect width="1600" height="155" fill="url(#crowd)"/><path d="M0 160H1600" stroke="#a2ba8b" stroke-width="24"/><path d="M0 160H1600" stroke="#182e2a" stroke-width="10"/><polygon points="124,186 1476,186 1730,900 -130,900" fill="url(#grass)"/>'
for i in range(10):
    if i%2==0:
        x1=124+i*135.2;x2=x1+135.2;b1=-130+i*186;b2=b1+186
        pitch+=f'<polygon points="{x1},186 {x2},186 {b2},900 {b1},900" fill="#bce780" opacity=".07"/>'
pitch+='<g fill="none" stroke="#dbe5c0" stroke-width="3.2" opacity=".86"><path d="M162 221H1438L1643 842H-43Z"/><path d="M800 221V842"/><ellipse cx="800" cy="514" rx="133" ry="101"/><path d="M116 360H330L270 674H-12M1484 360H1270L1330 674H1612"/><path d="M88 430H196L167 606H29M1512 430H1404L1433 606H1571"/><path d="M-1 463H83L52 571H-46M1601 463H1517L1548 571H1646"/><path d="M304 447Q378 508 286 586M1296 447Q1222 508 1314 586"/></g><g fill="#e4ebd4"><circle cx="800" cy="514" r="5"/><circle cx="251" cy="514" r="4"/><circle cx="1349" cy="514" r="4"/></g>'
for x,y,number,color in [(202,511,1,'#cf2942'),(506,383,2,'#cf2942'),(544,675,3,'#cf2942'),(840,502,4,'#cf2942'),(1400,507,1,'#263e90'),(1120,375,2,'#263e90'),(1080,651,3,'#263e90'),(754,445,4,'#263e90')]:
    pitch+=f'<g transform="translate({x} {y})" filter="url(#shadow)"><ellipse cy="15" rx="20" ry="9" fill="#162817" opacity=".4"/><path d="M-11-9L-22-2-16 13-10 8-10 29H10V8L16 13 22-2 11-9Z" fill="{color}" stroke="#f2eaca" stroke-width="2"/><circle cy="-18" r="10" fill="#e3c9a1"/><text y="14" text-anchor="middle" fill="#fff" font-size="15" font-weight="700">{number}</text></g>'
pitch+='<path d="M839 464l-10-16h20z" fill="#baff40"/><circle cx="867" cy="528" r="9" fill="#f5f4df" stroke="#203724" stroke-width="3"/><rect y="865" width="1600" height="35" fill="#122d1a" opacity=".3"/>'
pitch=pitch.replace('<polygon points="124,186', '<g transform="translate(135 25) scale(.83 .93)"><polygon points="124,186', 1)+'</g>'
svg('pitch.svg',pitch,1600,900)

# Read canonical tournament provenance, never rename a nearby checkpoint as a requested step.
tournament=json.loads((ROOT/'Logs/MNG-Rebuild/MS3-v3-r002-tournament-20260926/completion.json').read_text(encoding='utf-8-sig'))
selected={}
for round_ in tournament['rounds']:
    for key in ['incumbent','challenger']:
        item=round_[key]
        selected[item['label'].upper()]=item
models=[]
for nominal,label_ in [(0,'0K'),(400000,'400K'),(800000,'800K'),(1200000,'1.2M'),(1600000,'1.6M'),(2000000,'2M')]:
    item=selected[label_]
    digest=hashlib.sha256((ROOT/item['path']).read_bytes()).hexdigest()
    assert digest == item['sha256'], f"Model hash mismatch: {item['path']}"
    models.append(dict(id=f'stage-{nominal}',group='steps',label={'ko':'초기 모델' if nominal==0 else f'{nominal//10000}만','en':'Initial model' if nominal==0 else label_},kind='neural',nominalStep=nominal,actualStep=item['step'],source=item['path'],sha256=digest))
    if nominal==0:
        models[-1]['selectionLabel']={'ko':'초기 모델: 0회','en':'Initial model: 0 steps'}
for source,ko,en,desc_ko,desc_en in [
    ('recover','회수 우선','Recover','공을 되찾는 선택을 우선합니다.','Prioritizes recovering the ball.'),
    ('balanced','균형 유지','Balanced','균형 잡힌 팀 배치를 선택합니다.','Chooses a balanced team shape.'),
    ('carry-shot','전진-슈팅','Carry & Shoot','소유와 슈팅 가능성을 보고 선택합니다.','Chooses by possession and shot availability.'),
    ('uniform-valid','무작위 선택','Random','불가능한 행동을 제외하고 무작위로 선택합니다.','Randomly chooses among valid actions only.')]:
    models.append(dict(id=f'reference-{source}',group='reference',label={'ko':ko,'en':en},description={'ko':desc_ko,'en':desc_en},kind='rule',source=source,implementationSource='Tools/mng_v2_evaluate.py'))
catalog=dict(version=6,checkedDate='2026-09-26',scope='Exhibition design mapping only. Korean rule picker uses bilingual names; English uses English only. No models loaded or evaluation eligibility changed.',pretrainingAssumption=dict(steps=1000000,scope='User-requested exhibition copy assumption, not verified model provenance.'),intervalSteps=400000,sourceEvidence='Logs/MNG-Rebuild/MS3-v3-r002-tournament-20260926/completion.json',models=models)
encoded=json.dumps(catalog,ensure_ascii=False,indent=2)
write('model-catalog.json',encoded+'\n')
write('model-catalog.js','window.MNG_CATALOG = '+encoded+';\n')

rows=[
('speedBadge','2배속','2× SPEED'),
('pauseTitle','하이드레이션 브레이크!','Hydration Break!'),('resumeMatch','경기 재개','Resume match'),('quitMatch','경기 종료','End match'),
('eyebrow','휴먼AI공학전공 · 2026 졸업 전시회','Human Centered AI Major · EXHIBITION 2026'),
('tagline','강화학습 기반 4vs4 축구 게임 개발','Developing a 4vs4 football game with reinforcement learning'),
('start','경기 준비 <span>↗</span>','PREPARE MATCH <span>↗</span>'),('language','Language','Language'),('credits','제작자','Credits'),('guide','가이드','Guide'),
('matchSetup','경기 설정','MATCH SETUP'),('back','← 시작 화면','← Title screen'),('yourTeam','플레이어 팀','YOUR TEAM'),('opponent','상대 팀','OPPONENT'),('teamModel','팀 모델','TEAM MODEL'),
('redCaption','직접 플레이 시 RED 공격수를 조작합니다.','In Play mode, you control the RED striker.'),('navyCaption','선택한 AI가 NAVY 팀을 이끕니다.','Your chosen AI manages the NAVY team.'),
('playMode','경기 모드','MATCH MODE'),('humanMode','직접 플레이','Play'),('simMode','AI 시뮬레이션','AI simulation'),('duration','경기 시간','MATCH DURATION'),('five','5분','5 min'),('ten','10분','10 min'),('kickoff','경기 시작 ↗','KICK OFF ↗'),
('exhibition','전시 경기','EXHIBITION'),('move','이동','Move'),('turn','회전','Turn'),('softKick','패스','Pass'),('strongKick','슈팅','Shoot'),('aiSwitch','AI 전환','Switch AI'),('timeRemaining','남은 경기 시간','TIME REMAINING'),
('fullTime','경기 종료','FULL TIME'),('matchStats','경기 기록','MATCH STATISTICS'),('shots','전체 슈팅','Total shots'),('aiShots','AI 슈팅','AI shots'),('humanShots','사람 슈팅','Human shots'),('shotSum','전체 슈팅 = AI 슈팅 + 사람 슈팅','Total shots = AI shots + human shots'),('resultHelp','종료 버튼을 누르면 시작 화면으로 돌아갑니다.','Select Exit to return to the title screen.'),('exit','종료 ↗','EXIT ↗'),
('finalReward','최종 누적 보상','Final cumulative reward'),('recoveries','공 회수','Ball recoveries'),('saves','선방','Saves'),
('closeGuide','가이드 종료','Close guide'),('next','다음 장 →','Next page →'),('finishGuide','가이드 종료 ✓','Finish guide ✓'),('close','닫기','Close'),
('stepTab','강화학습 모델','RL MODELS'),('referenceTab','규칙형 감독','RULE-BASED MANAGERS'),
('neural','강화학습','RL model'),('rule','규칙형','Rule-based'),('aiTeam','AI 팀','AI TEAM'),('simulationLabel','AI vs AI · 시뮬레이션','AI vs AI · SIMULATION'),('draw','무승부','DRAW'),('wins','승리','WINS'),('selectModel','모델 선택','SELECT MODEL'),
('stepsNotice','모든 강화학습 모델은 100만 회의 사전 학습을 거쳤습니다.','All reinforcement learning models have completed 1 million pre-training steps.'),
('referenceNotice','각 감독은 같은 선수 기술을 사용하며, 서로 다른 규칙으로 전략을 선택합니다.','Each manager uses the same player skills and a different set of tactical rules.'),
('managerLive','감독 현황','MANAGERS'),('currentDecision','현재 판단','Current decision'),('cumulativeReward','누적 보상','Total reward'),('goal','GOAL','GOAL'),
('command0','전진 운반','Advance carry'),('command1','패스 전개','Build with passes'),('command2','슈팅 시도','Attempt shot'),('command3','적극 회수','Recover ball'),('command4','균형 유지','Stay balanced'),('command5','후방 보호','Protect defence'),
('creditsAlt','제작자 소개 임시 이미지','Temporary team credits image'),('pitchAlt','경기장 구도 예시 이미지. 실제 게임 렌더링이 아닙니다.','Schematic pitch view, not a Unity runtime capture.'),
('guideAlt1','직접 플레이에서는 RED 공격수를 조작합니다.','Control the RED striker in Play mode.'),('guideAlt2','W/S 이동, A/D 회전, E 패스, Space 슈팅, H AI 전환.','W/S move, A/D turn, E pass, Space shoot, H switch AI.'),('guideAlt3','양 팀 모델과 5분 또는 10분을 선택하며, 경기 후 종료 버튼으로 시작 화면으로 돌아갑니다.','Choose both models and 5 or 10 minutes. Exit returns to the title screen after the match.')]
translations={key:dict(ko=ko,en=en) for key,ko,en in rows}
write('locales.js','window.MNG_I18N = '+json.dumps(translations,ensure_ascii=False,indent=2)+';\n')
import re
with (HERE/'ui-strings.csv').open('w',encoding='utf-8-sig',newline='') as file:
    writer=csv.writer(file)
    writer.writerow(['Key','Korean(ko)','English(en)'])
    for key,ko,en in rows:
        writer.writerow(['MNG.Exhibition.'+key,re.sub('<[^>]+>','',ko),re.sub('<[^>]+>','',en)])
print(json.dumps(dict(verifiedNeuralFiles=6,trainingChoices=6,ruleChoices=4,localizationKeys=len(rows),localeCount=2,output=str(HERE)),ensure_ascii=False))
