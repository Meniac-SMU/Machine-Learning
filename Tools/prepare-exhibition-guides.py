"""Generate localized exhibition guides only; preserve archived design originals."""
from pathlib import Path
import html
import re
import xml.etree.ElementTree as ET
from fontTools.ttLib import TTFont
from fontTools.pens.svgPathPen import SVGPathPen

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'docs/soccer/exhibition/guide-art'
OUT.mkdir(exist_ok=True)
FONTS = {w: TTFont(ROOT / 'Assets/_Soccer/Manager/Exhibition/Fonts' / f'Pretendard-{n}.otf')
         for w, n in [(500, 'Medium'), (700, 'Bold'), (900, 'Black')]}

def text(x, y, content, size=26, weight=500, color='#4b6340'):
    return f'<text x="{x}" y="{y}" font-size="{size}" font-weight="{weight}" fill="{color}">{html.escape(content)}</text>'

def outline(match):
    node = ET.fromstring(match.group(0))
    a = node.attrib
    font = FONTS[int(a.get('font-weight', '500'))]
    glyphs, cmap = font.getGlyphSet(), font.getBestCmap()
    scale = float(a['font-size']) / font['head'].unitsPerEm
    x, y = float(a['x']), float(a['y'])
    content = ''.join(node.itertext())
    paths = []
    for c in content:
        glyph = glyphs[cmap.get(ord(c), '.notdef')]
        pen = SVGPathPen(glyphs)
        glyph.draw(pen)
        paths.append(f'<path transform="translate({x:.3f} {y}) scale({scale:.6f} {-scale:.6f})" d="{pen.getCommands()}"/>')
        x += glyph.width * scale
    return f'<g fill="{a["fill"]}" aria-label="{html.escape(content, quote=True)}">{"".join(paths)}</g>'

def outlined(content):
    return re.sub(r'<text\b[^>]*>.*?</text>', outline, content)

def page(lang, index, title, subtitle, rows, columns):
    content = '<rect width="1600" height="800" fill="#ecf0e5"/>'
    content += text(100, 91, f'HOW TO PLAY / 0{index}', 26, color='#637554')
    content += text(96, 185, title, 62, 900, '#182a17')
    content += text(100, 244, subtitle, 28)
    width = 1400 / columns
    height = 137 if columns == 2 else 106
    for i, (name, description) in enumerate(rows):
        x, y = 100 + (i % columns) * width, 289 + (i // columns) * height
        content += f'<rect x="{x}" y="{y}" width="{width-20}" height="{height-15}" fill="#dce6cf"/>'
        content += text(x+23, y+43, name, 32 if columns == 2 else 28, 700, '#203b21')
        content += text(x+23, y+83, description, 25 if columns == 2 else 23)
    if index == 3:
        content += text(100, 755, '할 일이 없으면 대기합니다.' if lang == 'ko' else 'Players wait when no action is needed.', 24)
    colors = ['#d53a47', '#ec8e49', '#c6e54b', '#429653', '#4369c2', '#9a78b9']
    content += ''.join(f'<rect x="{i*267}" y="784" width="267" height="16" fill="{c}"/>' for i, c in enumerate(colors))
    svg = '<svg xmlns="http://www.w3.org/2000/svg" width="1600" height="800" viewBox="0 0 1600 800">' + outlined(content) + '</svg>'
    (OUT / f'guide-{lang}-{index}.svg').write_text(svg, encoding='utf-8')

manager = {
 'ko': [('전진 운반','공을 몰고 앞으로 갑니다.'),('패스 전개','동료에게 패스하며 공격을 이어갑니다.'),('슈팅 시도','골문을 향해 슈팅합니다.'),('적극 회수','상대나 빈 공에 접근해 공을 되찾습니다.'),('균형 유지','공격과 수비의 균형을 유지합니다.'),('후방 보호','뒤쪽 공간과 우리 골문을 지킵니다.')],
 'en': [('Advance carry','Carry the ball forward.'),('Build with passes','Pass to teammates to build an attack.'),('Attempt shot','Try a shot at goal.'),('Recover ball','Close in and win the ball back.'),('Stay balanced','Balance attack and defence.'),('Protect defence','Protect space behind the team.')]
}
players = {
 'ko': [('이동','지정된 위치로 이동합니다.'),('공 운반','공을 몰고 이동합니다.'),('패스 받기','패스를 받을 위치로 갑니다.'),('패스','동료에게 공을 보냅니다.'),('슈팅','골문을 향해 공을 찹니다.'),('압박','공을 가진 상대에게 접근합니다.'),('공간 수비','위험한 공간을 지킵니다.'),('대인 수비','상대 선수를 따라붙습니다.'),('지원 이동','동료를 도울 위치로 갑니다.'),('골문 복귀','골키퍼가 골문으로 돌아갑니다.'),('골키퍼 공 회수','골키퍼가 공을 잡으러 나옵니다.'),('골키퍼 슈팅 차단','골키퍼가 슈팅 경로를 막습니다.')],
 'en': [('Move','Move to a target position.'),('Carry the ball','Move forward with the ball.'),('Receive pass','Move to receive a pass.'),('Pass','Send the ball to a teammate.'),('Shoot','Kick the ball towards goal.'),('Press','Close down the ball carrier.'),('Cover space','Defend a dangerous area.'),('Mark opponent','Stay close to an opponent.'),('Support run','Move to help a teammate.'),('Return to goal','Keeper returns to goal.'),('Claim ball','Keeper moves out for the ball.'),('Block shot','Keeper blocks the shot path.')]
}
for lang in ('ko', 'en'):
    ko = lang == 'ko'
    page(lang, 2, 'AI 감독의 6가지 선택' if ko else 'SIX MANAGER CHOICES',
         '감독이 전략을 고르면 선수들이 실행합니다.' if ko else 'The manager chooses a strategy. The players carry it out.', manager[lang], 2)
    page(lang, 3, '선수들은 이렇게 움직입니다' if ko else 'PLAYER ACTIONS',
         '선수들은 감독의 전략과 경기 상황에 맞춰 행동합니다.' if ko else 'Players act according to the strategy and match situation.', players[lang], 3)
    for old, new in ((2, 4), (3, 5)):
        source = ROOT / 'docs/archive/exhibition/ui-v1-final-20260927/assets' / f'guide-{lang}-{old}.svg'
        svg = source.read_text(encoding='utf-8')
        svg, count = re.subn(r'<g[^>]*aria-label="HOW TO PLAY / 0'+str(old)+r'"[^>]*>.*?</g>',
                            outlined(text(100, 110, f'HOW TO PLAY / 0{new}', 29.9, color='#637554')), svg)
        assert count == 1, f'Missing page header: {source}'
        (OUT / f'guide-{lang}-{new}.svg').write_text(svg, encoding='utf-8')
print(OUT)
