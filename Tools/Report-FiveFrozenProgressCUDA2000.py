"""Save a stage report without scheduling extra evaluation matches."""
import json
from pathlib import Path

COMMANDS = ['T1 속공 전개','T2 전방 롱패스','T3 와이드 탈압박','T4 측면 컷백',
            'T5 진로 차단 수비','T6 수신자 봉쇄','T7 전체 공 회수','T8 골문 앞 집결']
def read(p): return json.loads(p.read_text(encoding='utf-8-sig'))
def report(base):
    base = Path(base)
    stages = read(base/'stage-results.json')
    text = ['# 동결 v8 셀프 플레이 진행 현황', '', '후속 승인 상한 200만 감독 결정. 30,167회부터 16환경으로 재개; 168,948회부터 CUDA. 초기32환경 및 CPU 구간 포함. CUDA 검증 사본의12,000회는 본 Run에 포함하지 않음. 20만 저장·현황, 40만 평가. 비율 할당 보상 없음.', '']
    for stage in stages:
        step = stage['step']; segment = f'to-{step:07d}-16env-cuda' if step else 'to-0000000'
        result = read(base/segment/'process-result.json')
        text += [f'## {step:,}회', '', f"누적 optimizer 갱신: {result['optimizerUpdates']:,}. 검사 오류: {len(result['failures'])}.", '']
        counts = result['traceAudit']['bothTeamsDecisionCounts']
        total = [sum(counts[t][s][a] for t in range(2) for s in range(3)) for a in range(8)]
        if step == 200000:
            previous = read(base/'to-0200000/process-result.json')['traceAudit']['bothTeamsDecisionCounts']
            total = [total[a]+sum(previous[t][s][a] for t in range(2) for s in range(3)) for a in range(8)]
            cpu16 = read(base/'to-0200000-16env/process-result.json')['traceAudit']['bothTeamsDecisionCounts']
            total = [total[a]+sum(cpu16[t][s][a] for t in range(2) for s in range(3)) for a in range(8)]
        denominator = sum(total)
        text += ['해당 구간 셀프 플레이 양 팀 선택 현황(정식 평가와 구분):', '', '| 명령 | 횟수 | 비율 |', '|---|---:|---:|']
        text += [f'| {name} | {count:,} | {count/max(1,denominator):.2%} |' for name,count in zip(COMMANDS,total)]
        text += ['', '평가: '+('실시' if stage['evaluations'] else '해당 단계 미실시'), '']
        for opponent, data in stage['evaluations'].items():
            m=data['candidateMetrics']; text += [f"- {opponent}: {data['wins']}승 {data['draws']}무 {data['losses']}패, 득실 {data['goalsFor']}:{data['goalsAgainst']}; 슈팅 {m['shots']}회·성공률 {m['shotConversionRate']:.2%}, 패스 {m['passAttempts']}회·성공률 {m['passSuccessRate']:.2%}."]
            c=[sum(row[a] for row in data['actionCounts']) for a in range(8)]; n=sum(c)
            text += ['  '+', '.join(f'{name} {v}회({v/max(1,n):.2%})' for name,v in zip(COMMANDS,c))]
        text += ['']
    (base/'stage-report.md').write_text('\n'.join(text),encoding='utf-8')

if __name__ == '__main__':
    import argparse
    parser=argparse.ArgumentParser(); parser.add_argument('directory',type=Path)
    report(parser.parse_args().directory)
