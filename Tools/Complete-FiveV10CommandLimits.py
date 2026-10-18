"""Publish the v10 command-limit result only after actual test/match receipts pass."""
from datetime import datetime, timezone
import json
from pathlib import Path
import runpy

ROOT = Path(__file__).resolve().parent.parent
EVIDENCE = ROOT / 'Logs/Manager5v5/all-command-limits-v10-20261016'
DOCNAME = '53_전체명령_연속제한_T7예외_검증.md'
COMMANDS = ['T1 속공 전개', 'T2 전방 롱패스', 'T3 와이드 탈압박', 'T4 측면 컷백',
            'T5 진로 차단 수비', 'T6 수신자 봉쇄', 'T7 전체 공 회수', 'T8 골문 앞 집결']
LEARNING = 'all-command-limits-learning-v10-20261016-r002'
DISPLAY = 'all-command-limits-display-v10-20261016-r002'


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def main():
    tests = read(EVIDENCE / 'play-all-limits-04.json')
    assert tests['testResult'] == 'Passed' and tests['passed'] == 21 and tests['failed'] == 0
    assert read(EVIDENCE / f'build-{LEARNING}/build-result.json')['exitCode'] == 0
    assert read(EVIDENCE / 'display-build-r002/build-result.json')['exitCode'] == 0
    matches = []
    for folder, opponent in [('model-pair-r002', '160만 모델'), ('rule-pair-r002', '규칙형 strong-v11')]:
        summary = read(EVIDENCE / folder / 'summary.json')
        assert summary['passed'] and summary['games'] == 2
        for path in sorted((EVIDENCE / folder).glob('match-*/result.json')):
            row = read(path)
            assert row['passed'] and row['exitCode'] == 0 and row['commandLimitsVerified']
            assert row['limitsByAction'] == [3, 3, 3, 3, 3, 3, 2, 3]
            assert row['playerRuntime'] == 'tactics-v10-all-command-limits'
            matches.append(dict(opponentLabel=opponent, evidence=path.relative_to(ROOT).as_posix(), **row))
    assert len(matches) == 4
    table = '\n'.join(f'| {name} | {2 if i == 6 else 3}회 | {3 if i == 6 else 4}번째부터 | 다른 명령 2회 선택 |'
                      for i, name in enumerate(COMMANDS))
    game_rows = []
    for row in matches:
        score = row['score']
        outcome = '승' if score['own'] > score['other'] else '패' if score['own'] < score['other'] else '무'
        game_rows.append(f"| {row['opponentLabel']} | {'Red' if row['candidateTeam'] == 0 else 'Navy'} | {score['own']}–{score['other']} ({outcome}) | 통과 | 0 |")
    max_runs = [max(row['maxObservedConsecutiveBothTeams'][side][a] for row in matches for side in (0, 1)) for a in range(8)]
    command_count = sum(sum(row['stateCounts']) + sum(row['opponentActionCounts']) for row in matches)
    text = f'''# 전체 명령 연속 제한과 T7 전체 공 회수 예외 검증

2026-10-16 사용자 확정: 모든 감독 명령은 최대 3회 연속 허용하며, **T7 전체 공 회수만 최대 2회 연속** 허용한다. 제한에 도달하면 다른 명령을 두 차례 선택해야 다시 사용할 수 있다. 두 차례의 다른 명령은 서로 같아도 된다.

| 명령 | 연속 허용 | 차단 시점 | 재사용 조건 |
|---|---:|---|---|
{table}

예: `T7 전체 공 회수 → T7 전체 공 회수 → T1 속공 전개 → T1 속공 전개 → T7 전체 공 회수`는 허용된다. 앞의 두 번 직후 T7 전체 공 회수를 다시 선택하는 것은 거부한다. 다른 명령을 한 차례 선택한 뒤에도 아직 거부한다.

양 팀·각 명령별 독립 계산이다. 규칙형 감독, 강화학습 명령 마스크, 실제 명령 적용, 수동 선택에 같은 규칙을 적용했다. 선택 실패·시간 경과·일시정지·T9 수비 대형·득점 후 재개는 재사용 대기 횟수를 소비하지 않는다. 새 경기에서만 전체 상태를 초기화한다.

## 구현과 기존 모델

- 런타임: `tactics-v10-all-command-limits`.
- 계약: `MNG5-OBS676-A8-R1-M2-v6-ALLMAX3-T7MAX2-COOLDOWN2-GK6`, behavior `MNG5_Manager_v6`.
- 모든 8개 명령의 연속 횟수와 재사용까지 남은 선택 수를 추적·마스킹하고 trace에 배열로 저장한다. 기존 T2 전방 롱패스·T7 전체 공 회수 trace 필드도 유지한다.
- 관측676·행동8·득실점 보상만 유지한다. 골키퍼 기본 가속도는 일반 선수의1.2배, 최고속도도1.2배다.
- 기존 v8 모델을 추론에 사용하고 새 환경의 유효한 명령 마스크를 적용했다. 기존 동결 학습 Run을 새 계약으로 자동 재개하지 않는다.

## 검증 결과

- 관련 PlayMode **21/21 통과**: 모든 명령·양 팀의 연속 한도, 거부된 선택, 두 번의 다른 선택 후 복구, 규칙형 선택, RL 마스크/적용, 시간·일시정지·T9 수비 대형·득점 재개·새 경기 초기화, 관측·보상·전체 경기 경계, 평가 종료 처리.
- 기존240만 모델과160만 모델 양 진영2경기, 기존240만 모델과규칙형 양 진영2경기. seed77000, 각300초,100배속, 총4경기 정상 종료.
- 총 **{command_count}개 감독 선택**을 순서대로 독립 재계산해 trace의 연속 횟수·재사용 대기와 대조했다. 제한 위반0, 모든 명령이 동시에 마스킹된 경우0, 경계 강제 보정0, Player 종료 코드 모두0, Player 오류 로그0이다.
- 아래 점수는 기능 검증 중 관측값이다. 이4경기로 모델의 순위를 확정하지 않는다.

| 240만 모델의 상대 | 240만 진영 | 240만 기준 점수 | 명령 제한 | 종료 코드 |
|---|---|---|---|---:|
{chr(10).join(game_rows)}

명령 순서({', '.join(COMMANDS)})에 따른 실제 경기의 최대 연속 횟수: `{max_runs}`. 특정 명령의 실제 선택이 상한에 도달하지 않았더라도 별도 PlayMode 검사에서 모든 명령의 상한을 강제로 확인했다.

## 실패 기록과 해결

최초 PlayMode 두 배치에서 테스트 보조 코드가 과거 두 명령만을 전제하거나 물리 진행 중 관측 이력을 기록하지 않아 실패했다. 보조 코드를 새 검사 범위에 맞게 수정했으며 실패01·02와 통과03(20개)을 보존했다. 종료 처리 회귀 검사 추가 후 최종04(21개)가 통과했다.

첫 실제 경기4개는 300초 경기 자체의 선택 제한 검사를 충족했으나, 평가 통신 종료 중 자동으로 다음 경기가 시작되어 기본값인 T1 속공 전개가 반복되고 제한 오류가 발생했다. 이4개는 통과로 처리하지 않았다. 실패 빌드·로그·독립 진단(`first-integration-failure-diagnosis.json`)을 보존했다.

평가용 `--five-evaluate-once`를 추가하여 최종 terminal을 전송한 뒤 새 경기를 요청하지 않고 정지한다. 기본 연속 학습 동작은 유지한다. 새 r002 빌드와 새 폴더에서4경기를 다시 실행했고, 오류 로그 검사와 다음 경기 미발생 검사까지 통과했다. 오류를 무시하거나 명령 제한을 완화하지 않았다.

## 실행 파일과 보존

- 최신 검증 빌드: `Builds/Manager5v5/{LEARNING}/Soccer5v5Learning.exe`.
- 최신 관전 빌드: `Builds/Manager5v5/{DISPLAY}/Soccer5v5.exe`. 빌드 성공만 확인했으며 이번 요청에서는 관전 화면을 새로 실행하지 않았다.
- 실행 증거: `Logs/Manager5v5/all-command-limits-v10-20261016`. 테스트04, `model-pair-r002`, `rule-pair-r002`, `preservation-verification.json`, `completion.json`이 최종 근거다.
- 기존 v8의0~240만13개 체크포인트(ONNX·PT·상대 풀 총39개), 동결 v8 빌드753개 파일, v9 두 빌드 등록부의 주요8개 파일 해시, 기존 완료 기록과 모델 등록부의 일치를 확인했다. 변경 전 소스360개 항목과 실패 자료를 별도 ZIP으로 보존했다. Unity가 변경한 ProjectSettings는 작업 전 사본으로 복원하고 해시 일치를 확인했다.
- 새 학습·모델 변경·champion 승격은 없다. [기존1,120경기 평가](52_골키퍼가속도12배_8모델_전수평가.md)는 **v9의 이전 명령 제한**에서 나온 결과이며 v10의 순위 근거로 바꾸어 표기하지 않는다.
'''
    document = ROOT / 'docs/soccer/rebuild-5v5' / DOCNAME
    document.write_text(text, encoding='utf-8')
    entry_paths = {
        'AGENTS.md': 'docs/soccer/rebuild-5v5/',
        'docs/README.md': 'soccer/rebuild-5v5/',
        'docs/soccer/current-status.md': 'rebuild-5v5/',
        'docs/soccer/rebuild-5v5/AGENTS.md': '',
        'docs/soccer/rebuild-5v5/README.md': '',
        'docs/soccer/rebuild-5v5/04_결정사항과_변경기록.md': '',
    }
    marker = '> 2026-10-16 최신 명령 제한(v10)'
    for relative, prefix in entry_paths.items():
        path = ROOT / relative
        contents = path.read_text(encoding='utf-8-sig')
        if marker in contents:
            continue
        heading, rest = contents.split('\n', 1)
        update = (f'{marker}: **T7 전체 공 회수는 최대2연속, 나머지7개 명령은 최대3연속**. '
                  f'제한 뒤 다른 명령2회 선택 후 재허용. 양 팀·수동·규칙형·RL 공통 적용. PlayMode21/21 및300초4경기 통과. '
                  f'평가 종료 후 다음 경기 시작 오류를 수정하고 실패 자료도 보존했다. 기존240만 모델·학습 기록·v8/v9 빌드 보존, 추가 학습 없음. '
                  f'[53번 검증]({prefix}{DOCNAME}). 아래1,120경기 순위는 이전v9 결과다.')
        path.write_text(heading + '\n\n' + update + '\n' + rest, encoding='utf-8')
    runpy.run_path(str(ROOT / 'Tools/Verify-FiveV10Preservation.py'), run_name='__main__')
    preservation = read(EVIDENCE / 'preservation-verification.json')
    assert preservation['passed']
    completion = dict(passed=True, completedUtc=datetime.now(timezone.utc).isoformat(),
                      playerRuntime='tactics-v10-all-command-limits', limits=[3,3,3,3,3,3,2,3],
                      commandNames=COMMANDS, alternativeChoicesBeforeReuse=2, testsPassed=21,
                      integrationMatchesPassed=4, decisionsAudited=command_count, matches=matches,
                      failedIntegrationMatchesPreserved=4, maxObservedConsecutive=max_runs,
                      learningBuild=LEARNING, displayBuild=DISPLAY, viewerLaunched=False,
                      trainingStarted=False, checkpointsPreserved=13, champion=None,
                      report=document.relative_to(ROOT).as_posix())
    (EVIDENCE / 'completion.json').write_text(json.dumps(completion, ensure_ascii=False, indent=2), encoding='utf-8')
    print(json.dumps(dict(passed=True, tests=21, matches=4, decisions=command_count, report=completion['report']), ensure_ascii=False))


if __name__ == '__main__':
    main()
