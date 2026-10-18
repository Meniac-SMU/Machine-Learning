"""Append final P7 measurements to the preserved development/failure narrative."""
import json
from pathlib import Path

ROOT=Path(__file__).resolve().parent.parent
BASE=ROOT/'Logs/Manager5v5/p7-20261012'
DOC=ROOT/'docs/soccer/rebuild-5v5/16_P7완료_결과와_인계.md'
def read(name): return json.loads((BASE/name).read_text(encoding='utf-8-sig'))

def main():
    role=read('roles-qualified-assessment-01.json');matrix=read('matrix-final-assessment-01.json')
    natural=read('natural-final-assessment-01.json');period=read('period-final-assessment-01.json');repeat=read('repeat-final-assessment-01.json')
    run=read('p7-final-validation-01.json');player=read('player-final/player-result.json');visual=read('player-final/visual-review.json')
    assert all(r['passed'] and r['complete'] for r in [role,matrix,natural,period,repeat])
    assert run['passed']==48 and run['failed']==0 and run['exitCode']==0 and visual['passed']
    text=DOC.read_text(encoding='utf-8-sig')
    assert '## 최종 P0~P7 판정' not in text
    text=text.replace('**현재 실행 중이며 P7 최종 합격 전이다.**','**P0~P7의 이번 단계별 기준을 통과해 개발·검증·검토본 전달을 완료했다.**')
    text=text.replace('후속 확인 결과·최종 게이트·실행 파일·원본 보존은 실행 완료 후 이 문서에 추가한다. 진행 중 자료를 P7 완료나 학습 준비 합격으로 사용하지 않는다.',
                      '위는 개발 중의 선행 이력이다. 아래 최종 판정이 현재 소스의 근거이며,과거 실패를 소급하여 합격으로 바꾸지 않는다.')
    lines=['','## 최종 P0~P7 판정','',
      '| 단계 | 완료한 내용 | 최종 근거 |','|---|---|---|',
      '| P0 | 요구·계획·단계별 기준·변경/실패 기록 | 실행 전 계획과 이 문서,원문 보존 |',
      '| P1 | 같은 Unity 프로젝트 안의 에셋 사본·참조 분리 | 기존693파일 SHA-256 일치,기존 Build Settings 유지 |',
      '| P2 | 팀당 공격2·수비2·GK1,선수·공0.9배 | 기반 계산·자산 검사와 실제 물리 회귀 |',
      '| P3 | 안정 소유·회전/드리블·패스 신호/정확 수신 | 무방해32패스,유효 방해·탈취·유예/감속 회귀 |',
      '| P4 | 독립 GK·동료 패스 예외·긴급 걷어내기 | 전술 독립 위치·실제 배급 수신·위험 대응 회귀 |',
      '| P5 | 이동 소모·정지/저속 회복·70/40% 감속·드리블 추가5%p·체력UI | 계산11개,실제 속도/회복 검사,Player 체력 막대8개 |',
      '| P6 | 8명령 행동·정상 판단 주기·규칙형 감독·팀당 하나 실행 | 기능 회귀43개와300초2경기 물리 단계별 단일 실행 검사 |',
      '| P7 | 역할·조건부 상성·활용·반복 상대·꼼수·최종 전달 | 아래 최종 배치,Windows 빌드·Player·소스 및 보존 검증 |','',
      'P8 관측/행동/보상/학습 계보 계약과 P9 강화학습은 시작하지 않았다. 검토용 HUD와 실행 파일이 있다는 사실을 P10 전시 제품 완료로 세지 않는다.',
      '', '## 최종 역할 확인과 조건부 상성','',
      '최종 소스11111~11114의1,408건에서는14/16비교가 합격했고,T5 측면 봉쇄 압박의 먼 롱패스 상대 소유 우위와그 반대 약점 가설2개는 실패했다. 해당 가설을 철회한 뒤,새 기준을 실행 전에 기록하고 미사용11121~11128의8seed·두 기존 장면·8명령·2진영·2후속 방식으로512건을 추가 확인했다. 실제 측면 연결을 압박하는 장면에서 T5 측면 봉쇄 압박의 이점과T7 전체 공 회수의 약점이 평균 방향·6/8seed 기준을 통과했다. 기존14개와새2개로 수정된 역할 정의의16개를 충족했으며 원래 가설의 실패는 유지한다. 아래 차이는 같은 상황의 우리 명령 대체 결과이고,직접 대결 승률이 아니다. 양 진영을 같은seed의 한 쌍으로 묶는다.','',
      '| 명령 | 고유 이점: 유리 방향 평균 차이 / 재현seed | 약점: 불리 방향 평균 차이 / 재현seed |','|---|---|---|']
    metric={'earlyMaxOwnAdvance':'최초3초 제어 공 위치(m)','ownGoals':'득점(골)','safe3':'3초 안전 탈출 비율',
            'earlyPossession':'최초3초 소유(초)','ownPossession':'소유(초)','enemyReceptions':'상대 안정 수신(회)','enemyGoals':'실점(골)'}
    for name in role['commandNames']:
        cells=[]
        for kind in ['advantage','weakness']:
            c=next(c for c in role['checks'] if c['command']==name and c['kind']==kind)
            cells.append(f"{c['alternative']} 대비 {metric[c['metric']]} {c['meanDifference']:+.3f} / {c['positiveSeedPairs']}/{c['seedPairCount']}")
        lines.append('| '+name+' | '+' | '.join(cells)+' |')
    lines += ['','약점 열의 양수는 불리한 방향으로 환산한 차이다. 실제 원시 득점/수신/시간과 부호는 분석JSON의 `direction`과 `samples`에 보존한다. 원래 T5 측면 봉쇄 압박의 먼 수신자 약점 가설은 필수 비교와 분리한 실패 진단으로 남겼다.','',
      f"교차 비교는4상황×2체력×8우리 명령×8상대 명령×2진영×2seed의{matrix['trials']:,}건이다. 고정12초의64맥락을 분리했으며 관측된 만능 명령은{len(matrix['universalDominators'])}개,한 명령이 다른 명령을 모든 맥락에서 지배한 쌍도{len(matrix['pairwiseDominators'])}개다. 이는 해당 표본의 주지표 평균 결과이며 모든 축구 상황의 균형을 증명하지 않는다. 상대가 애초에 패스를 시도하지 않아 상대 수신0인 칸도 있으므로 이를 모두 차단 성공으로 읽지 않는다.",
      '', '[조건별8×8 비교 그림](../../../Logs/Manager5v5/p7-20261012/p7-conditional-matrix-final.png)',
      '', '## 최종 자연 경기·활용·판단 주기','',
      '자연 경기64개와주기 비교48개는 이전에 수집한 시작seed를 수정 후 소스에서 재검증한 결과다. 새로운 독립seed로 다시 세지 않는다. 전술의 새 역할 확인4seed와반복 상대 확인2seed는 수정에 쓰지 않은 별도 배치다.','']
    n=natural['groups'][0];total=sum(n['selections'])
    lines += [f"자연64경기 모두300초 종료·경기별 패스/슛 발생·복수 소유 회수·유한 물리·정체30초 미만·사후 경계보정0을 통과했다. 합계{n['goals']}골,{n['passes']}회 패스 발사/{n['receptions']}회 안정 수신,{n['shots']}회 슛,최대 정체{n['maximumStall']:.2f}초다.",'',
      '| 명령 | 실제 선택(재선택 포함) | 전체 선택 비중 | 관측된 상황 기회 중 해당 명령 선택 |','|---|---:|---:|---:|']
    for i,name in enumerate(natural['commandNames']):
        lines.append(f"| {name} | {n['selections'][i]:,} | {n['selections'][i]/total:.2%} | {n['selectedWithOpportunity'][i]:,}/{n['opportunities'][i]:,} |")
    lines += ['', '기회는 실제 판단 시점의 공개 위치/공 소유/체력으로 계산한 중첩 가능한 장면 지표다. 정답 라벨이나 행동 마스크가 아니며 선택 규칙과 완전히 같은 정의도 아니다. 비율을 정답률·전체 성공률로 해석하지 않는다.8개 모두 실제 경기에서 선택되었지만 균등 사용을 요구하거나 보상하지 않았다. 패스 성공의 분모는 발사이며,무방해32패스 회귀와 상대 방해가 있는 실전 수신률은 구별한다.','',
      '| 판단 주기 | 합격 경기 | 골 | 발사/안정 수신 | 슛 | 실제 명령 교체 | 준비 시간초과 | 최대 정체 |','|---|---:|---:|---:|---:|---:|---:|---:|']
    for g in period['groups']:
        lines.append(f"| {g['period']:g}초 | {g['healthy']}/{g['games']} | {g['goals']} | {g['passes']}/{g['receptions']} | {g['shots']} | {g['actualChanges']:,} | {g['preparationTimeouts']} | {g['maximumStall']:.2f}초 |")
    lines += ['', '기본3초를 유지한다. 목표 위치/공격 패턴을 전개할 시간을 두되 상황 변화에 대응하는 초기 기준이다.2초의 득점이 많다는 이유로 즉시 바꾸지 않는다. 패스 신호→발사 준비 시간,발사→안정 수신,명령 유지 구간과 첫 후속 물리 사건은 분석JSON에 따로 남겼다. 후속 사건이 있었다는 이유만으로 그 명령의 인과적 성공으로 계산하지 않는다.','',
      '## 반복 상대·꼼수 확인','',
      '미사용13501~13502×양 진영×8고정 상대의32경기를 각각120초 실행했다. 모든 경기 종료·정체30초 미만·사후 경계보정0이며 상대는 경기 내내 지정한 명령 하나를 유지한다. 아래 승패는 규칙형 감독 쪽 기준이며 승리 자체를 기능 합격 조건으로 삼지 않았다.','',
      '| 고정 상대 명령 | 경기 품질 | 규칙형 승/무/패 | 최대 정체 |','|---|---:|---:|---:|']
    raw=[]
    for path in repeat['files']:raw.extend(json.loads((ROOT/path).read_text(encoding='utf-8-sig'))['matches'])
    for i,name in enumerate(repeat['commandNames']):
        games=[g for g in raw if g['fixedOpponent']==i]
        deltas=[(g['redScore']-g['navyScore'])*(1 if g['candidateTeam']==0 else -1) for g in games]
        lines.append(f"| {name} | 4/4 | {sum(d>0 for d in deltas)}/{sum(d==0 for d in deltas)}/{sum(d<0 for d in deltas)} | {max(g['maximumStall'] for g in games):.2f}초 |")
    lines += ['', '대응은 공개 관측에 따른 규칙형 선택이다. 상대 전술 ID를 읽거나 결과에 전술별 보너스를 더하지 않는다. 골문 보호·측면 압박·전원 회수·수신자 재배치를 정상 판단 시점에 교체하며 동시에 실행하지 않는다. 원시 로그에는 선택 이유와 그 시점의 양 팀 위치,공 소유,속도,체력,점수차와남은 시간이 남는다. 소유 유예 감속,유예 만료,실제 속도 기반 회복,명령 연타,탈취/방해와GK 독립은 기능 회귀로 확인했다. 보상 계약을 만들지 않았으므로 미래 강화학습의 보상 악용이 모두 해결됐다고 주장하지 않는다.','',
      '## 전달한 실행 파일과 증거','',
      f"최신 Windows 검토본은 [Soccer5v5.exe](../../../Builds/Manager5v5/p7-review-final/Soccer5v5.exe)다. 실제 Player300초 경기 {player['redScore']}:{player['navyScore']},패스{player['passes']}/안정 수신{player['receptions']},슛{player['shots']},사후 보정{player['boundaryCorrections']}건,체력 막대{player['headBars']}개,종료 코드0을 확인했다. 실제 카메라와UI 렌더 프레임을 열어 표시를 검토했다. 사람의 마우스 조작/체감 검토와는 구분한다.",'',
      '- [실행·재현 안내](09_실행과_재현안내.md)',
      '- [최종 기능/실험 실행 영수증](../../../Logs/Manager5v5/p7-20261012/p7-final-validation-01.json)',
      '- [수정된 역할 정의의 최종 판정](../../../Logs/Manager5v5/p7-20261012/roles-qualified-assessment-01.json)',
      '- [기존 역할 가설의 최종 실패 판정](../../../Logs/Manager5v5/p7-20261012/roles-final-assessment-01.json)',
      '- [최종 조건부 교차 판정](../../../Logs/Manager5v5/p7-20261012/matrix-final-assessment-01.json)',
      '- [최종 자연 경기 판정](../../../Logs/Manager5v5/p7-20261012/natural-final-assessment-01.json)',
      '- [최종 판단 주기 판정](../../../Logs/Manager5v5/p7-20261012/period-final-assessment-01.json)',
      '- [최종 반복 상대 판정](../../../Logs/Manager5v5/p7-20261012/repeat-final-assessment-01.json)',
      '- [최종 완료·원본/소스 일치 영수증](../../../Logs/Manager5v5/p7-20261012/completion.json)',
      '- [실제 경기 렌더](../../../Logs/Manager5v5/p7-20261012/player-final/match.png)',
      '- [최종 소스·문서 보존본](../../../Logs/Manager5v5/p7-20261012/p7-source-final.zip)','',
      '다음 사람 피드백의 초점은 세 가지다. 첫째,3초 유지가 전술을 읽기에 충분하고 답답하지 않은지. 둘째,T3 와이드 탈압박과 T4 측면 컷백,T5 측면 봉쇄 압박과 T7 전체 공 회수가 화면에서 구분되는지. 셋째,패스/드리블 보정과체력 감속의 체감이 목표에 맞는지. 이는 P8 계약을 정하기 전의 제품 판단이며,P7의 실제 검사 결과를 대신하지 않는다.','']
    DOC.write_text(text+'\n'.join(lines),encoding='utf-8')
    print(DOC)

if __name__=='__main__':main()
