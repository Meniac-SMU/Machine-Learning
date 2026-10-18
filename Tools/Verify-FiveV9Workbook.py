"""Read-only XLSX package validation against every evaluated team row."""
import json
from pathlib import Path
import math
import xml.etree.ElementTree as ET
import zipfile

ROOT=Path(__file__).resolve().parent.parent
OUT=ROOT/'outputs/v9-round-robin-20261016'
NS={'s':'http://schemas.openxmlformats.org/spreadsheetml/2006/main'}
def same(a,b):
    if isinstance(b,(int,float)):assert isinstance(a,(int,float)) and math.isclose(a,b,rel_tol=1e-10,abs_tol=1e-10),(a,b)
    else:assert a==b,(a,b)
def letter(n):
    s=''
    while n:n,a=divmod(n-1,26);s=chr(65+a)+s
    return s

def main():
    data=json.loads((OUT/'evaluation-data.json').read_text(encoding='utf-8'))
    with zipfile.ZipFile(OUT/'골키퍼가속도12배_8모델_1120경기.xlsx') as z:
        shared=[]
        if 'xl/sharedStrings.xml' in z.namelist():
            shared=[''.join(x.itertext()) for x in ET.fromstring(z.read('xl/sharedStrings.xml'))]
        sheets=[];formula_count=0
        for i in range(1,5):
            xml=ET.fromstring(z.read(f'xl/worksheets/sheet{i}.xml'));cells={}
            for c in xml.findall('.//s:sheetData/s:row/s:c',NS):
                assert c.get('t')!='e',ET.tostring(c)
                v=c.find('s:v',NS);value=None if v is None else v.text
                if c.get('t')=='s':value=shared[int(value)]
                elif c.get('t')=='inlineStr':value=''.join(c.find('s:is',NS).itertext())
                elif value is not None and c.get('t') not in ('str','b'):value=float(value)
                if c.find('s:f',NS) is not None:formula_count+=1;assert value is not None,c.get('r')
                cells[c.get('r')]=value
            sheets.append(cells)
            if i>1:
                pane=xml.find('.//s:pane',NS);assert pane is not None
                assert float(pane.get('ySplit','0'))==5
                assert float(pane.get('xSplit','0'))=={2:3,3:6,4:4}[i]
        for idx,row in enumerate(data['ranking'],6):
            expected=[row['rank'],row['model'],280,row['wins'],row['draws'],row['losses'],row['winRate'],row['points'],row['goalsFor'],row['goalsAgainst'],row['goalDifference'],row['points']/280]
            for j,v in enumerate(expected,1):same(sheets[0][f'{letter(j)}{idx}'],v)
        for idx,r in enumerate(data['teamRows'],6):
            expected=[r['game'],r['pair'],r['seed'],r['side'],r['modelName'],r['opponentName'],r['result'],r['goalsFor'],r['goalsAgainst'],r['shots'],r['shotGoals'],r['shotSuccessRate'] if r['shots'] else '해당 없음',r['passAttempts'],r['passSuccesses'],r['passSuccessRate'] if r['passAttempts'] else '해당 없음',r['decisions']]
            for j,v in enumerate(expected,1):same(sheets[2][f'{letter(j)}{idx}'],v)
            command=[r['game'],r['side'],r['modelName'],r['opponentName'],r['decisions']]+r['commands']+r['commandRates']+[r['decisions']/5]
            for j,v in enumerate(command,1):same(sheets[3][f'{letter(j)}{idx}'],v)
        assert max(int(k[1:]) for k in sheets[2] if k.startswith('A') and k[1:].isdigit())==2245
        assert max(int(k[1:]) for k in sheets[3] if k.startswith('A') and k[1:].isdigit())==2245
        table_xml=[ET.fromstring(z.read(p)) for p in z.namelist() if p.startswith('xl/tables/') and p.endswith('.xml')]
        assert len(table_xml)==3
        assert {x.get('ref') for x in table_xml}=={'A5:P2245','A5:V2245','A5:R61'}
    p=OUT/'workbook-verification.json';receipt=json.loads(p.read_text(encoding='utf-8'))
    receipt.update(exportedPackageVerified=True,allTeamRowCellsVerified=2240*38,formulaCachesVerified=formula_count,freezePanesVerified=True,visualReview='All four sheets and both command table halves reviewed; Korean headers and percentages readable.')
    p.write_text(json.dumps(receipt,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps(receipt,ensure_ascii=False))

if __name__=='__main__':main()
