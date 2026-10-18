"""Write the P9 handoff only from the completed, audited experiment result."""
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
BASE = ROOT/"Logs/Manager5v5/p9-20261013"
DOC = ROOT/"docs/soccer/rebuild-5v5"


def main():
    result = json.loads((BASE/"result.json").read_text(encoding="utf-8"))
    assert result["completed"]
    growth = result["growthDemonstratedOnFixedProtocol"]
    verdict = "사전 지정 고정 평가에서 초기 대비 성장 확인" if growth else "이번 실행 예산에서 초기 대비 성장 입증 실패"
    lines = ["# P9 완료 결과와 인계", "", f"2026-10-13 P9 학습·저장·고정 평가 개발과 실행을 완료했다. **품질 판정: {verdict}.**",
        "", "P9 실행 완료와 학습 품질의 판정을 구분한다. 기존 4대4 모델과 검증용 Run은 본 실험의 초기값·상대·후보에 포함하지 않았다.",
        "", "## 실제 실행", "", f"- 본 Run: `{result['runId']}`, 새 seed 19013 초기화, CPU PPO, 4개 Windows Player.",
        f"- 승인 예산: 감독 결정 200,000개. 실제 저장: **{result['actualStep']:,}개**, PPO minibatch 업데이트 **{result['optimizerUpdates']:,}회**.",
        "- R1 득점 +1·실점 −1·그 외 0, M2 676관측·8행동·3초 결정·300초 경기를 유지했다.",
        f"- 학습 팀별 실제 경험: Red {result['teamLearningExposure']['0']:,}개 / Navy {result['teamLearningExposure']['1']:,}개.",
        "- 초기·중간·최종 각 72경기, 합계 216경기. 모든 경기는 300초·진영 교대·고정 seed로 진행했다.",
        "- 이전 주간 잔여량 조건으로191,523개·2,208회 업데이트를 저장·중단했다. 사용자의 ‘잔여량에 관계없이 20만까지 학습을 진행하고 평가하라’ 지시에 따라 사용량 중단 조건을 해제하고, 모델·optimizer·상대 풀4개 파일 해시를 확인한 같은 Run의 `resume-to-200k` 구간에서 완료했다. 이전 STOP·중단 로그·저장본은 보존했다.",
        "- 미니배치/trajectory 단위 처리로 명목 중단 지점과 실제 저장 step이 다를 수 있으며 위 수치는 실제 값이다.",
        "", "## 고정 상대 결과", "", "각 칸은 승/무/패, 괄호는 총 득점:실점이다.", "",
        "| 시점·실제 step | P7 규칙형 감독 | T7 전체 공 회수 고정 | 본 Run 초기 정책 |", "|---|---:|---:|---:|"]
    labels = {"initial":"초기", "middle":"중간", "final":"최종"}
    for model in result["models"]:
        cells = []
        for opponent in ("rule", "recover", "initial"):
            row = result["stages"][model["label"]][opponent]
            cells.append(f"{row['wins']}/{row['draws']}/{row['losses']} ({row['goalsFor']}:{row['goalsAgainst']})")
        lines.append(f"| {labels[model['label']]} {model['step']:,} | " + " | ".join(cells) + " |")
    lines += ["", "같은 seed와 진영의 초기-후속 경기 득실차를 대응 비교했다. 두 진영과 3종 상대를 seed별로 묶은 12개 블록을 10,000회 bootstrap했다. 이 작은 고정 평가의 통계적 추정이며 모든 상대·대형에서의 우월성은 아니다.", ""]
    for label in ("middle", "final"):
        row = result["pairedComparison"][label]; low, high = row["pairedSeedBootstrap95"]
        lines.append(f"- {labels[label]}-초기: 경기당 득실차 변화 {row['meanGoalDifferenceGain']:+.3f}, 95% 구간 [{low:+.3f}, {high:+.3f}].")
    lines += ["", "## 선택 분포 해석", "", "최종 정책의 실제 선택을 우리 공/상대 공/자유 공으로 나눴다. 모든 전술이 선택 가능했고, 균등 사용 보상이나 정답 마스크는 없었다. 선택이 치우쳤다는 사실만으로 전술이 불필요하거나 모든 상황에서 최적이라고 판정하지 않는다.", "",
              "| 상태 | 결정 수 | 가장 많이 선택한 명령 | 실제 선택 비율 |", "|---|---:|---|---:|"]
    names = ["T1 속공 전개","T2 전방 롱패스","T3 와이드 탈압박","T4 측면 컷백","T5 진로 차단 수비","T6 수신자 봉쇄","T7 전체 공 회수","T8 골문 앞 집결"]
    for state, name in enumerate(("우리 공", "상대 공", "자유 공")):
        counts = [sum(result["stages"]["final"][opp]["actionCounts"][state][a] for opp in ("rule","recover","initial")) for a in range(8)]
        total = sum(counts); top = max(range(8),key=lambda a:counts[a])
        lines.append(f"| {name} | {total:,} | {names[top]} | {100*counts[top]/total:.1f}% |")
    lines += ["", "전체 선택 수와 확률 합은 `Logs/Manager5v5/p9-20261013/result.json` 및 각 평가의 `summary.json`에 보존했다. 위 표는 선택 빈도이며 전술 실행 성공률이나 인과적 가치의 증명과 구분한다.",
        "", "## 검증과 보존", "",
        f"- Unity PlayMode 56개·EditMode 11개 통과. 실제 빌드와 현재 C# {result['compiledSourceFiles']}개를 대조했다.",
        "- 새 Player에서 P8 LLAPI 계약 재검증, 규칙형 상대 양 진영, 초기 ONNX 실제 추론, CPU 실제 optimizer 업데이트와 저장·재개를 통과했다.",
        "- 초기·중간·최종 PT와 ONNX 확률을 실제 경기 관측 384개씩 대조했다. 각 `parity-*.json`에 최대 오차와 가중치 SHA-256을 기록했다.",
        "- 학습 trace의 점수 보상, 676개 값의 유한성·범위, 이력 시각, 경기/재개 초기화, 물리 경계 보정을 검사했다. `formal-traces.json` 참조.",
        f"- 기존 원본 {result['originalsPreserved']}개와 5대5 경기 파일 {result['gameplayFilesPreserved']}개를 보존했다. ProjectSettings와 EditorBuildSettings는 시작 시점 바이트와 같다.",
        "- 정책·optimizer·자기 대전 풀·교대 상태·RNG·모델/설정/Player 해시를 보존했다. 중단 시 진행 중 경기 물리와 미완성 rollout까지 비트 단위로 복구하는 기능은 아니다.",
        "", "## 실패와 제한", "",
        "1. `rule-wire-01`: 경기는 종료됐으나 평가 도구가 SDK 프로세스 필드명을 잘못 읽어 실패했다. `_process`로 수정한 `rule-wire-02`가 통과했다.",
        "2. `P9-preflight01`: 설치 SDK가 지원하지 않는 로그 경로 CLI 인수로 시작 전 종료했다. 지원하는 environment factory의 로그 인수로 수정했다.",
        "3. 최초 재개 출처 검사: JSON 키 순서 차이를 내용 변경으로 오판했다. 모든 identity 필드와 파일별 해시의 의미 비교로 수정했다. 무결성 기준은 완화하지 않았다.",
        "4. `P9-preflight02`: GPU에서 4,547개·48회 업데이트를 저장한 뒤 재개 시 `nvcuda64.dll`에서 `0xC0000409`로 종료했다. Windows 이벤트·PT/ONNX·풀·로그를 보존하고 CUDA 실행을 격리했다. 저장 파일은 CPU strict load와 확률 대조를 통과했다. 드라이버 오류 자체는 해결되지 않았다.",
        "5. 최초 PT/ONNX 대조 도구: PyTorch와 NumPy의 OpenMP DLL 중복 초기화로 실패했다. 두 행렬 연산을 독립 프로세스로 나눠 해결했으며 `KMP_DUPLICATE_LIB_OK`로 오류를 숨기지 않았다.",
        "6. `P9-cpu-preflight01`: 새 CPU 계보에서 9,510개·96회 업데이트와 실제 재개를 통과했다. GPU/CPU 검증 경험 합계 14,057개는 본 20만 개 및 성장 평가와 별도로 보존한다.",
        "7. SDK 자기 대전 ELO는 마지막 transition 보상 부호를 사용하므로 R1의 실제 승패 지표로 쓰지 않았다. 평가의 점수판 기반 승/무/패가 기준이다.",
        "8. 동일 킥오프와 독립 행동 seed의 비교다. 새로운 대형·전시 화면·Unity 내장 Sentis 모델 실행·사용자 체감은 P9 검증에 포함되지 않는다.",
        "", "## 인계", "", "- 실행 계획: [22_P9학습과_평가_실행계획.md](22_P9학습과_평가_실행계획.md)",
        "- 새 모델 계보: [p9-model-registry.json](p9-model-registry.json). `currentChampion`은 비어 있으며 자동 추가 학습은 하지 않는다.",
        "- 학습: `Tools/Run-FiveTraining.ps1`, 고정 평가: `Tools/Run-FiveEvaluation.ps1`, 원시 확률 평가: `Tools/five_evaluate.py`.",
        "- 결과 증거: `Logs/Manager5v5/p9-20261013/result.json`, 완료 봉인: 같은 폴더의 `completion.json`과 최종 소스/파일 해시 manifest.",
        "- 다음 P10에서는 검증 모델을 실제 게임에서 선택·실행하는 경로와 표시를 구현하고 Unity 추론·화면·사용자 동작을 별도로 검증해야 한다. P9 완료를 P10 완료로 표시하지 않는다.", ""]
    (DOC/"23_P9완료_결과와인계.md").write_text("\n".join(lines),encoding="utf-8")


if __name__ == "__main__": main()
