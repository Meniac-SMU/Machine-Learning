"""Summarize the fixed-model runtime comparison and audit every retained trace."""
import json
import argparse
from pathlib import Path
import re
import numpy as np
from five_tactic_evaluate import COMMANDS

ROOT=Path(__file__).resolve().parent.parent
BASE=ROOT/'Logs/Manager5v5/defensive-state-20261014'
EVAL=BASE/'evaluation'
DOC=ROOT/'docs/soccer/rebuild-5v5/43_T9수비대형_T7연속제한_골키퍼가속_검증.md'

def read(path):return json.loads(path.read_text(encoding='utf-8-sig'))
def pct(x):return '해당 없음' if x is None else f'{100*x:.2f}%'
def ci(values):
    values=np.asarray(values)
    rng=np.random.RandomState(20261014)
    boot=values[rng.randint(0,len(values),(20000,len(values)))].mean(axis=1)
    return [float(np.mean(values)),*np.quantile(boot,[.025,.975]).tolist()]

def main():
    global EVAL
    parser=argparse.ArgumentParser();parser.add_argument('--revision2',action='store_true');args=parser.parse_args()
    if args.revision2:EVAL=BASE/'evaluation-r2'
    assert read(EVAL/'completion.json')['games']==96
    summaries={};games={};audits={}
    for v in ('before','after'):
        for opponent in ('rule','self'):
            key=f'{v}/{opponent}';folder=EVAL/v/opponent
            summaries[key]=read(folder/'summary.json')
            games[key]=sorted([read(p) for p in folder.glob('match-*/result.json')],key=lambda r:(r['seed'],r['side']))
            assert len(games[key])==24 and all(r['passed'] for r in games[key])
            counters=dict(games=24,t9Entries=0,t9Exits=0,keeperPasses=0,keeperClearances=0,keeperPossessions=0,
                          decisionDuringT9=0,maxT7Streak=0,boundaryCorrections=0,maxKeeperHoldSeconds=0,keeperHoldsOver10Seconds=0)
            for result in games[key]:
                match=folder/f'match-{(result["seed"]-65000)*2+result["side"]:03d}'
                trace,=match.glob('trace-*.jsonl')
                rows=[json.loads(s) for s in trace.read_text(encoding='utf-8-sig').splitlines()]
                terminals=[r for r in rows if r['kind']=='terminal'];assert len(terminals)==2
                assert all(r['elapsed']>=300 and r['boundaryCorrections']==0 for r in terminals)
                assert sum(r['reward'] for r in terminals)==0
                streak=[0,0]
                for r in rows:
                    if r['kind']!='decision':continue
                    t,a=r['team'],r['action'];assert 0<=a<8
                    streak[t]=streak[t]+1 if a==6 else 0
                    counters['maxT7Streak']=max(counters['maxT7Streak'],streak[t])
                    if r['observation'][10]>.5:counters['decisionDuringT9']+=1
                primary=next(r for r in terminals if r['team']==0)
                owner=-1
                keeper_start=None
                for e in primary['matchEvents']:
                    if keeper_start is not None and e['kind'] in ('possession','release','goal'):
                        duration=e['time']-keeper_start
                        counters['maxKeeperHoldSeconds']=max(counters['maxKeeperHoldSeconds'],duration)
                        counters['keeperHoldsOver10Seconds']+=int(duration>10)
                        keeper_start=None
                    if e['kind']=='possession':
                        owner=e['actor']
                        if owner%5==0:
                            counters['keeperPossessions']+=1
                            keeper_start=e['time']
                    elif e['kind']=='release':owner=-1
                    elif e['kind']=='defensive-formation-enter':
                        assert owner in (0,5) and owner//5!=e['actor']
                        counters['t9Entries']+=1
                    elif e['kind']=='defensive-formation-exit':
                        assert owner not in (0,5) or owner//5==e['actor']
                        counters['t9Exits']+=1
                    elif e['kind']=='pass-kicked' and e['actor']%5==0:counters['keeperPasses']+=1
                    elif e['kind']=='keeper-clearance':counters['keeperClearances']+=1
                if keeper_start is not None:
                    duration=primary['elapsed']-keeper_start
                    counters['maxKeeperHoldSeconds']=max(counters['maxKeeperHoldSeconds'],duration)
                    counters['keeperHoldsOver10Seconds']+=int(duration>10)
                for log in match.glob('Player*.log'):
                    assert not re.search(r'^(?:[\w.]*Exception:|Assertion failed|\[MNG FATAL\])',log.read_text(errors='replace'),re.M)
            if v=='after':assert counters['maxT7Streak']<=3 and counters['decisionDuringT9']==0
            audits[key]=counters
    before=games['before/rule'];after=games['after/rule']
    delta=[];points=[]
    for i in range(0,24,2):
        diff=[];pd=[]
        for b,a in zip(before[i:i+2],after[i:i+2]):
            assert (b['seed'],b['side'])==(a['seed'],a['side'])
            bg=b['score']['own']-b['score']['other'];ag=a['score']['own']-a['score']['other']
            diff.append(ag-bg);pd.append((1 if ag>0 else .5 if ag==0 else 0)-(1 if bg>0 else .5 if bg==0 else 0))
        delta.append(np.mean(diff));points.append(np.mean(pd))
    comparison=dict(goalDifferenceChange=ci(delta),matchPointsChange=ci(points),seedBlocks=12,bootstrapResamples=20000)
    (EVAL/'analysis.json').write_text(json.dumps(dict(audits=audits,comparison=comparison),ensure_ascii=False,indent=2),encoding='utf-8')
    lines=['## 결과','', '고정 모델 505,386회로 변경 전 48경기와 변경 후 48경기, 총 96경기를 정상 종료했다. 추가 학습은 하지 않았다.','',
        '| 조건 | 상대 | 승/무/패 | 득점:실점 | 슈팅/슈팅 득점 | 슛 성공률 | 패스 시도/성공 | 패스 성공률 |',
        '|---|---|---:|---:|---:|---:|---:|---:|']
    for v in ('before','after'):
        for o in ('rule','self'):
            s=summaries[f'{v}/{o}'];m=s['metrics']
            lines.append(f'| {"변경 전" if v=="before" else "변경 후"} | {"규칙형" if o=="rule" else "동일 모델"} | {s["wins"]}/{s["draws"]}/{s["losses"]} | {s["goalsFor"]}:{s["goalsAgainst"]} | {m["shots"]}/{m["shotGoals"]} | {pct(m["shotConversionRate"])} | {m["passAttempts"]}/{m["passSuccesses"]} | {pct(m["passSuccessRate"])} |')
    lines+=['','자가 대전의 승무패는 독립 난수열을 부여한 평가측 기준이다. 양 팀 가중치는 같으므로 어느 모델의 우열로 해석하지 않는다.','',
        '| 명령 | 규칙형 상대 전 | 규칙형 상대 후 | 자가 대전 전 | 자가 대전 후 |','|---|---:|---:|---:|---:|']
    for i,name in enumerate(COMMANDS):
        lines.append('| '+name+' | '+' | '.join(pct(summaries[k]['ratios'][i]) for k in ('before/rule','after/rule','before/self','after/self'))+' |')
    lines+=['','| 변경 후 상태 지표 | 규칙형 상대 | 자가 대전 |','|---|---:|---:|']
    for label,key in [('평가측 T9 발동 수','t9Entries'),('평가측 T9 총 시간(초)','t9Seconds'),('평가측 T7 마스크 횟수','masked'),('양 팀 최대 T7 연속 선택','maxT7Streak')]:
        vals=[summaries[f'after/{o}'][key] for o in ('rule','self')]
        lines.append(f'| {label} | {vals[0]:.2f} | {vals[1]:.2f} |')
    lines+=['','| 골키퍼 지표(양 팀 합계) | 규칙형 상대 전 | 규칙형 상대 후 | 자가 대전 전 | 자가 대전 후 |',
            '|---|---:|---:|---:|---:|']
    for label,key in [('패스','keeperPasses'),('긴급 클리어런스','keeperClearances'),('최장 연속 소유(초)','maxKeeperHoldSeconds'),('10초 초과 소유 건수','keeperHoldsOver10Seconds')]:
        lines.append('| '+label+' | '+' | '.join(f'{audits[k][key]:.2f}' for k in ('before/rule','after/rule','before/self','after/self'))+' |')
    gd=comparison['goalDifferenceChange'];pt=comparison['matchPointsChange']
    lines+=['',f'규칙형 상대 경기당 득실차의 변경 전후 차이는 {gd[0]:+.3f}, 시드 블록 부트스트랩 95% 구간은 [{gd[1]:+.3f}, {gd[2]:+.3f}]다. 승점률(승 1·무 0.5)의 차이는 {100*pt[0]:+.2f}%p, 95% 구간은 [{100*pt[1]:+.2f}, {100*pt[2]:+.2f}]%p다.',
        '', '세 변경을 함께 적용했으므로 개별 변경의 인과 효과는 이 비교만으로 분리할 수 없다. 과거 모델의 추가 학습 없이 적용한 결과이며 새 학습 성공을 뜻하지 않는다.',
        '', '전체 경기에서 정상 종료·득실점 보상 일치·경계 보정 0을 확인했다. 변경 후 두 팀 모두 T7 연속 4회 선택이 없고, 상대 골키퍼 소유 중 감독 판단 요청도 없었다. T9 진입·해제 이벤트는 실제 소유자 변화와 대조했다.',
        '', f'자세한 원자료: `{EVAL.relative_to(ROOT)}/{{before,after}}/{{rule,self}}`. 검사 집계와 시드 블록 구간은 같은 폴더의 `analysis.json`이다.']
    if args.revision2:
        lines+=['','## 검증과 보존한 최초 실패','',
            'PlayMode는 최초 전체 검사 106/111 통과 후 실패 5건을 수정·재검증했다. T9를 별도 상태로 세는 계약으로 검사 조건을 갱신했고, 무압박 패스 검사의 정지 상대를 고정했다. 성공 기준을 낮추지 않았으며 패스 32/32 수신을 확인했다. 테스트의 물리 모드 복원 순서를 고쳤고, 시간 초과 검사는 동일 180초 제한으로 단독 통과했다.',
            '', '첫 변경본의 48경기는 기술적으로 종료됐지만, 상세 분석에서 GK 소유가 최장 184.12초·247.59초 계속된 접촉 정체를 발견했다. 이 빌드를 최종 채택하지 않았으며 `evaluation/review-verdict.json`, 원시 경기, `hold-repro-05.xml`의 재현 실패를 보존했다.',
            '', 'r2는 T9 후퇴 시 밀착 킥 플레이트를 먼저 회전 없이 빼낸다. 기록 장면 두 개 모두 10초 안에 소유가 끝나는 회귀 검사를 추가했고 핵심 4건 및 접촉 관련 31건을 통과했다. 검사명별 최신 PlayMode 결과는 112/112, EditMode는 27/27이다. 전체 112개를 한 번에 다시 수행했다는 뜻은 아니다.',
            '', '최종 비교는 기존 기준 48경기와 r2 48경기로 이루어진 96경기다. 미채택 첫 변경본 48경기를 포함하면 실제 수행한 평가는 총 144경기이며, 기준 48경기는 파일을 복사해 재사용했다. 모델 학습과 가중치 갱신은 없었다.']
    old=DOC.read_text(encoding='utf-8').split('## 결과')[0]
    DOC.write_text(old+'\n'.join(lines)+'\n',encoding='utf-8')
    print(json.dumps(comparison,ensure_ascii=False,indent=2))

if __name__=='__main__':main()
