"""Publish the locally verified completed evaluation to project entry points."""
import hashlib
import json
from pathlib import Path

ROOT=Path(__file__).resolve().parent.parent
OUT=ROOT/'outputs/v9-round-robin-20261016'
DOC=ROOT/'docs/soccer/rebuild-5v5/52_골키퍼가속도12배_8모델_전수평가.md'
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def percent(x):return '해당 없음' if x is None else f'{x*100:.2f}%'
def table(headers,rows):return '\n'.join(['| '+' | '.join(headers)+' |','| '+' | '.join(['---']*len(headers))+' |']+['| '+' | '.join(map(str,r))+' |' for r in rows])

def main():
    d=read(OUT/'evaluation-data.json');assert d['validation']['passed'] and d['validation']['games']==1120
    assert read(OUT/'workbook-verification.json')['passed']
    artifact=OUT/'골키퍼가속도12배_8모델_1120경기.xlsx';assert artifact.stat().st_size>10000
    rank=d['ranking'];top=rank[0];second=rank[1];u=d['uncertainty']
    duel=next(r for r in d['pairSummaries'] if r['id']==top['id'] and r['opponentId']==second['id'])
    section='\n\n## 완료 결과\n\n'
    section+='28개 조합 × 40경기 = **1,120경기**를 모두 완료했다. 모델당 280경기, 양 진영 각 140경기다. 모든 Player 종료 코드 0, v9 런타임, 300초 종료, 득실점 보상 일치, 경계 보정 0 및 종료 episode의 양 팀 명령 집계를 확인했다. T2·T7의 최대 2회 연속 사용 및 이후 2회 재사용 제한도 전체 trace에서 재검사했다.\n\n'
    section+=table(['순위','모델','승','무','패','승률','승점','득점','실점','득실차'],[[r['rank'],r['model'],r['wins'],r['draws'],r['losses'],percent(r['winRate']),r['points'],r['goalsFor'],r['goalsAgainst'],r['goalDifference']] for r in rank])
    section+=f"\n\n이번 표본의 종합 1위는 **{top['model']}**, 2위는 **{second['model']}**다. 두 모델의 직접 대결에서 {top['model']}는 **{duel['wins']}승 {duel['draws']}무 {duel['losses']}패**, 득실 **{duel['goalsFor']}:{duel['goalsAgainst']}**를 기록했다.\n\n"
    lo,hi=u['pointGap95CI']
    section+=f"1·2위 승점차는 {u['observedPointGap']}점이다. 동일 시드의 모든 대진·양 진영을 묶은 10,000회 부트스트랩의 승점차 95% 구간은 **{lo:.1f}~{hi:.1f}점**, 1위 모델이 승점 단독 1위를 유지한 재표집 비율은 **{percent(u['strictFirstPlaceFraction'])}**다. 이는 사후 선택된 상위 모델의 참고 진단이며 절대 우위의 확률이나 다중 비교 보정 검정이 아니다. "
    section+=('이 구간에는 0이 포함되므로 종합 1위의 재현 가능한 우위를 확정하지 않는다.' if lo<=0<=hi else '이 표준 킥오프·확률적 정책 선택 조건 안에서의 결과로 해석한다.')
    section+=' 다른 초기 배치나 새 환경에서 재학습한 모델은 평가하지 않았다. champion 승격은 하지 않았다.\n\n'
    section+='### 슈팅·패스·감독 명령 합계\n\n'
    section+=table(['모델','슈팅 수','슛 득점','슈팅 성공률','패스 수','성공 패스','패스 성공률','명령 수','명령/분'],[[r['model'],r['shots'],r['shotGoals'],percent(r['shotSuccessRate']),r['passAttempts'],r['passSuccesses'],percent(r['passSuccessRate']),r['decisions'],f"{r['decisions']/(r['games']*5):.2f}"] for r in rank])
    section+='\n\n각 성공률은 전체 성공 수/전체 시도 수이며 경기별 비율을 단순 평균하지 않았다. 총 득점과 슛 득점은 다를 수 있다. 슛 뒤 소유권 획득·패스·GK 클리어런스가 끼지 않은 득점만 해당 슛에 귀속한다.\n\n'
    section+='### 감독 명령별 선택 횟수와 빈도\n\n'
    section+=table(['모델']+d['commandNames'],[[r['model']]+[f"{n:,} ({percent(n/r['decisions'])})" for n in r['commands']] for r in rank])
    section+='\n\n### 직접 대결 전체표\n\n행 모델 기준 승–무–패다. 각 칸은 40경기이며 반대 방향 칸은 같은 경기들의 반대 관점이다.\n\n'
    ordered=d['protocol']['models'];lookup={(r['id'],r['opponentId']):r for r in d['pairSummaries']}
    section+=table(['행 모델 / 상대']+[x['name'] for x in ordered],[[m['name']]+['—' if m['id']==o['id'] else '{wins}–{draws}–{losses}'.format(**lookup[m['id'],o['id']]) for o in ordered] for m in ordered])
    section+='\n\n### 전체 경기별 표와 검사 증거\n\n'
    section+='- [Excel 전체표](../../../outputs/v9-round-robin-20261016/골키퍼가속도12배_8모델_1120경기.xlsx): 종합 순위, 대진별 결과 56행, 경기별 지표 2,240행, 명령별 횟수·빈도 2,240행. 한 경기의 양 팀을 각각 한 행으로 기록했다.\n'
    section+='- [CSV 전체표](../../../outputs/v9-round-robin-20261016/경기별_전체지표.csv): 경기 ID로 승패·득실점·슈팅·패스·8개 명령을 함께 조회할 수 있다.\n'
    section+='- 원본 경기·trace: `Logs/Manager5v5/keeper-accel12-v9-20261016/round-robin`; 최종 집계 검사 `final-verification.json`, 원본 보존 검사 `preservation-verification.json`.\n'
    section+='- 소스 ZIP 351개 파일, 기존 빌드 753개 파일, 13개 체크포인트의 PT·ONNX·상대 풀 39개 파일 보존을 확인했다. 이번 작업 중 학습은 실행하지 않았다.\n'
    text=DOC.read_text(encoding='utf-8-sig').split('\n\n## 완료 결과')[0]
    text=text.replace('현재 평가 진행 중이며 완료 후 결과를 추가한다.','전체 평가와 집계를 완료했으며 실제 결과는 아래에 기록한다.')
    DOC.write_text(text.rstrip()+section+'\n',encoding='utf-8')
    entry=f"> 2026-10-16 최신 승인 변경·평가 완료: GK 기본 가속도 일반 선수의 1.2배(36m/s²), 최고속도 1.2배 유지. v9 물리 검증 14/14 및 8개 모델 28조합 × 40경기 = 1,120경기 통과. 종합 1위 {top['model']}({top['wins']}승 {top['draws']}무 {top['losses']}패, {top['points']}점), 2위 {second['model']}. 순위의 불확실성과 직접 대결은 `52_골키퍼가속도12배_8모델_전수평가.md` 참조. 기존 v8 소스·빌드·13개 체크포인트 보존, 재학습·champion 승격 없음."
    paths=['AGENTS.md','docs/README.md','docs/soccer/current-status.md','docs/soccer/rebuild-5v5/AGENTS.md','docs/soccer/rebuild-5v5/README.md','docs/soccer/rebuild-5v5/04_결정사항과_변경기록.md']
    for name in paths:
        p=ROOT/name;lines=p.read_text(encoding='utf-8-sig').splitlines();indices=[i for i,s in enumerate(lines) if s.startswith('> 2026-10-16 최신 승인 변경·평가')];assert len(indices)==1,name
        lines[indices[0]]=entry;p.write_text('\n'.join(lines)+'\n',encoding='utf-8')
    deliverables=[artifact,OUT/'경기별_전체지표.csv',OUT/'evaluation-data.json',DOC]
    receipt=dict(passed=True,games=1120,files=[dict(path=str(p.relative_to(ROOT)),bytes=p.stat().st_size,sha256=hashlib.sha256(p.read_bytes()).hexdigest()) for p in deliverables])
    (OUT/'delivery-manifest.json').write_text(json.dumps(receipt,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps(dict(passed=True,top=top['model'],runnerUp=second['model'],documentsUpdated=7),ensure_ascii=False))

if __name__=='__main__':main()
