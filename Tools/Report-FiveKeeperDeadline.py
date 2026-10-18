"""Publish fixed-model evaluation and verified rule diagnostics without training."""
import json
import hashlib
import zipfile
from pathlib import Path
import numpy as np
import xml.etree.ElementTree as ET
from five_tactic_evaluate import COMMANDS

ROOT=Path(__file__).resolve().parent.parent
BASE=ROOT/'Logs/Manager5v5/defensive-state-v5-20261014'
OUT=BASE/'evaluation'
DOC=ROOT/'docs/soccer/rebuild-5v5/44_T7두차례제한_T9충돌회피_GK6초배급_검증.md'
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def pct(v):return '해당 없음' if v is None else f'{v*100:.2f}%'
def interval(values):
    data=np.asarray(values);rng=np.random.RandomState(20261014)
    estimates=data[rng.randint(0,len(data),(20000,len(data)))].mean(axis=1)
    return [float(data.mean()),*np.quantile(estimates,[.025,.975]).tolist()]

def main():
    assert read(OUT/'completion.json')['passed']
    summaries={f'{v}/{o}':read(OUT/v/o/('summary-episode-only.json' if v=='before' else 'summary.json')) for v in ('before','after') for o in ('rule','self')}
    before=sorted([read(p) for p in (OUT/'before/rule').glob('match-*/result.json')],key=lambda r:(r['seed'],r['side']))
    after=sorted([read(p) for p in (OUT/'after/rule').glob('match-*/result.json')],key=lambda r:(r['seed'],r['side']))
    delta=[]
    for i in range(0,24,2):
        changes=[]
        for b,a in zip(before[i:i+2],after[i:i+2]):
            assert (a['seed'],a['side'])==(b['seed'],b['side'])
            changes.append(a['score']['own']-a['score']['other']-b['score']['own']+b['score']['other'])
        delta.append(np.mean(changes))
    gd=interval(delta)
    coverage={}
    for path in sorted(BASE.glob('*.xml'),key=lambda p:p.stat().st_mtime):
        for t in ET.parse(path).iter('test-case'):coverage[t.attrib['fullname']]=dict(result=t.attrib['result'],source=path.name)
    failed={k:v for k,v in coverage.items() if v['result']!='Passed'}
    assert not failed, failed
    model=ROOT/'results/Manager5v5/MNG5_V2-20261014-r002/MNG5_Manager_v2/MNG5_Manager_v2-505386.onnx'
    assert sha(model)=='5d5077dd51edef29b401e97a8c8d88b7c1a0375fb9ffb4ce36ad39fd5dd7d4b2'
    assert sha(model.with_suffix('.pt'))=='651be46419f9003d48740967196426f37e9937b71e8da89b5ba509ece9d9e995'
    for name in ('ProjectSettings','EditorBuildSettings'):
        assert sha(ROOT/f'ProjectSettings/{name}.asset')==sha(BASE/f'{name}.before.asset')
    with zipfile.ZipFile(BASE/'before-source.zip') as archive:
        beforeReward=archive.read('Assets/_Soccer/Manager5v5/Runtime/FiveLearningContract.cs').decode('utf-8-sig').replace('\r\n','\n').split('public sealed class FiveScoreReward')[1]
        assert beforeReward==(ROOT/'Assets/_Soccer/Manager5v5/Runtime/FiveLearningContract.cs').read_text(encoding='utf-8-sig').split('public sealed class FiveScoreReward')[1]
    build=ROOT/'Builds/Manager5v5/keeper-six-second-20261014'
    assert read(BASE/'build-keeper-six-second-20261014/build-result.json')['exitCode']==0
    result=dict(passed=True,implementationPassed=True,evaluationCompleted=True,trainingStarted=False,modelStep=505386,
        modelSha256=sha(model),checkpointSha256=sha(model.with_suffix('.pt')),rewardUnchanged=True,settingsRestored=True,
        playerRuntime='tactics-v5-keeper-six-second',contract='MNG5-OBS676-A8-R1-M2-v4-T7COOLDOWN2-GK6',
        latestTests=len(coverage),latestPassed=len(coverage),coverage=coverage,
        regression=dict(fullBatch=read(BASE/'regression-03.json'),independentContact=read(BASE/'contact-04.json'),
            caveat='One historical wall-scrum acquisition failed in the full batch and passed independently without changing its assertions. Exact cause not established.'),
        evaluation=read(OUT/'completion.json'),summaries=summaries,goalDifferenceChange95=gd,
        qualityImprovementConfirmed=False,pairedGoalDifferenceIncreaseSupported=gd[1]>0,
        qualityLimit='Frozen policy, paired regression seeds, combined changes, no independent holdout or training.',
        sources=[dict(path=str(p.relative_to(ROOT)),sha256=sha(p)) for p in sorted((ROOT/'Assets/_Soccer/Manager5v5').rglob('*')) if p.is_file()],
        buildPayload=[dict(path=str(p.relative_to(build)),sha256=sha(p)) for p in sorted(build.rglob('*')) if p.is_file()])
    assert not (BASE/'completion.json').exists()
    (BASE/'completion.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
    lines=['# T7 두 차례 제한·T9 충돌 회피·골키퍼 6초 배급 검증','',
        '2026-10-14 사용자 요청 구현. 최신 505,386회 고정 모델을 사용했으며 추가 학습·가중치 갱신·보상 변경은 없다. 런타임 `tactics-v5-keeper-six-second`, 계약 `MNG5-OBS676-A8-R1-M2-v4-T7COOLDOWN2-GK6`다. 관측676/행동8을 유지하지만 제한 의미가 달라져 새 계약으로 구분한다.','',
        '## 구현','',
        '- T7 전체 공 회수 3연속 후 승인된 4·5번째 감독 선택에서 제외한다. 6번째 선택부터 허용한다. 같은 다른 명령을 두 번 선택해도 차감하며, 거절·T9 진입/해제·득점 재개는 차감하지 않는다. 새 경기만 전체 초기화한다.','- T9 수비 대형은 상대 골키퍼 소유 중만 작동한다. 자기 팀 GK 소유는 자기 팀 T9를 발동하지 않는다. 정상 전술과 패스 수신 동작은 계속된다. 근거리 8m 경로에서 상대 선수·GK의 현재/0.3초 예측 위치와 여유 폭을 확인해 우회한다. 안전한 경로가 없으면 감속·대기하며, 이미 밀착한 GK에서는 기존 회전 없는 이탈을 먼저 한다. 순간이동·충돌 무효화는 없다.','- GK는 전술별 수신자를 우선하고, 없으면 같은 팀 필드 선수 중 통로·압박·거리 기준의 대안을 찾는다. 3초 이후 일반 패스의 거리/재시도 대기 때문에 막힐 경우 전용 배급 준비를 시작한다. 5.9초부터 수신 준비를 더 기다리지 않고 패스해 6초 상한을 지킨다. 소유 중 무목적 긴급 클리어런스 대신 동료를 지정한 배급을 사용한다. 실제 수신 성공이나 가로채기는 물리 결과이며 강제 성공으로 만들지 않는다.','',
        '## 검증 범위','',
        f'검사명별 최신 결과 {len(coverage)}/{len(coverage)} 통과(PlayMode115·EditMode27). 최초 실패 로그는 보존한다. PlayMode 전체 배치에서는114/115였고, 기존 RecordedEndWallScrumCanRegainFrontPossessionWithoutChangingOrders 1건이 13초 안의 소유 획득을 확인하지 못했다. 해당 장면은 keeper 기능이 꺼져 있고 GK 소유/T9 발동도 없었다. 검사 조건·시간·경합 코드를 바꾸지 않은 별도 접촉/T9 15개 배치에서는 모두 통과했다. 전체 실행 실패의 정확한 원인은 확정하지 못했으므로 단일 전체 배치 완전 통과나 해당 원인 수정으로 표현하지 않는다.','',
        '최초 새 시나리오 2건은 장애물이 원래 경로 밖에 있거나 FreezeRotation에도 MoveRotation이 작동해 의도한 상태를 만들지 못했다. 장애물을 경로 안에 두고 준비되지 않은 수신자의 방향을 각 스텝 복원해 해당 조건을 검증했다. 조건을 제거하거나 6초 기준을 늘리지 않았다. 전체 실행 배치별 결과는 같은 증거 폴더의 JSON/XML이다.','',
        '이전 v4-r2의 48경기를 해시 대조 후 기준으로 재사용했고 v5에서 48경기를 새로 수행했다. 12개 동일 시드65000~65011·진영 교대·규칙형/동일 모델 각24경기·300초·100배속 설정·평가12프로세스다. 최종 비교96경기이며 새 학습은 없다. 재사용 시드는 회귀 비교이고 독립 품질 검증 표본은 아니다.','',
        '최초 결과 감사는 종료 직후 자동 초기화된 다음 episode의 T7 카운터를 이전 경기와 비교해 실패했다. 경기 규칙 위반이 아니라 분석기의 경기 경계 오류였다. terminal과 동일한 episode만 대조하도록 수정하고 완료된24경기는 재실행 없이 재검사했다. 명령 비율 역시 양쪽 버전 모두 해당 경기의 episode만 집계했다. 이전 보고서는 평가 경기 밖 초기화 episode의 T1 기록도 포함했으므로 명령 비율을 정정한다. 기준 원본 파일은 변경하지 않고 summary-episode-only.json을 별도 저장했다.','',
        '| 조건 | 상대 | 승/무/패 | 득점:실점 | 슈팅/슛 득점 | 슛 성공률 | 패스 성공/시도 | 패스 성공률 |','|---|---|---:|---:|---:|---:|---:|---:|']
    for v in ('before','after'):
        for o in ('rule','self'):
            s=summaries[f'{v}/{o}'];m=s['metrics']
            lines.append(f'| {v} | {o} | {s["wins"]}/{s["draws"]}/{s["losses"]} | {s["goalsFor"]}:{s["goalsAgainst"]} | {m["shots"]}/{m["shotGoals"]} | {pct(m["shotConversionRate"])} | {m["passSuccesses"]}/{m["passAttempts"]} | {pct(m["passSuccessRate"])} |')
    lines+=['','동일 모델 승무패는 고정 평가측 기준이며 같은 가중치 간 우열을 뜻하지 않는다. 슛 득점은 슈팅 사건 기준이라 전체 득점과 다를 수 있다.','',
        '| 명령 | 규칙형 전 | 규칙형 후 | 동일 모델 전 | 동일 모델 후 |','|---|---:|---:|---:|---:|']
    for i,name in enumerate(COMMANDS):lines.append('| '+name+' | '+' | '.join(pct(summaries[k]['ratios'][i]) for k in ('before/rule','after/rule','before/self','after/self'))+' |')
    lines+=['','| 규칙 검사(양 팀 합계) | 규칙형 상대 | 동일 모델 상대 |','|---|---:|---:|']
    for label,key in [('T7 제한 발동','t7CooldownTriggers'),('GK 연속 소유 검사 수','keeperPossessions'),('최장 GK 소유(초)','maxKeeperHoldSeconds'),('6초 초과 소유','keeperHoldsOverSixSeconds'),('GK 패스','keeperPasses'),('마감 시점 패스','deadlinePasses'),('T9 우회 진입','t9AvoidanceEvents'),('T9 진입','t9Entries'),('T9 소유 해제','t9Exits'),('T9 득점으로 종료','t9EndedByGoal'),('T9 경기 종료','t9EndedByMatch')]:
        lines.append('| '+label+' | '+' | '.join(f'{summaries[f"after/{o}"]["rulesAudit"][key]:.3f}' for o in ('rule','self'))+' |')
    lines+=['',f'규칙형 상대 경기당 득실차 변화 {gd[0]:+.3f}, 시드 블록 부트스트랩95% 구간 [{gd[1]:+.3f}, {gd[2]:+.3f}]. 세 변경을 함께 적용했으므로 개별 인과 효과를 분리할 수 없다.','',
        'T9 우회 진입은 경로 조정 이벤트 수이며 접촉 건수나 접촉 완전 제거의 증거가 아니다. 양 진영의 GK/필드 장애물 배치에서 실제 최소 거리를 검사했다. 움직이는 상대가 들이받는 모든 상황까지 무충돌을 보장하지 않는다.','',
        '원자료와 완료 영수증: `Logs/Manager5v5/defensive-state-v5-20261014`. 기존 빌드·실패·505,386회 ONNX/PT는 보존한다. 이 빌드는 평가용 Learning Player이며 화면 시연 증거와 구분한다.']
    DOC.write_text('\n'.join(lines)+'\n',encoding='utf-8')
    print(json.dumps({k:v for k,v in result.items() if k not in ('sources','buildPayload','coverage','summaries')},ensure_ascii=False,indent=2))

if __name__=='__main__':main()
