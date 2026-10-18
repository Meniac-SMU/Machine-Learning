# 프로젝트 도구 색인

> 2026-10-16 최신 선수 v11: **골키퍼 제동 → 회전 → 정면 접근 수정·검증 완료**. 동일96개 재현 자책골8→0·획득88→96, 280만 대240만 각모드40경기(합계80)에서 저속 측면 접촉 실점4→0. 전체PlayMode122/122·추가대조3/3·EditMode27/27, 기존모델·학습기록239파일과v10빌드377파일 보존. 280만 승수22→16도 기록했으며 종합 경기력 향상·champion 승격은 판정하지 않는다. 현행 정식40경기 진입점은 `Tools/Evaluate-FiveV11ModelPair40.py`, 최신 관전 빌드는 `keeper-turn-first-display-v11-20261016`이며 `Tools/Show-FiveV11_2800vs2400.py`로 실행한다. [57번 검증](../docs/soccer/rebuild-5v5/57_골키퍼_회전후전진_자책골_검증.md).

최신 고정 모델 평가는 `Evaluate-FiveKeeperDeadline.py`, 재감사는 `--audit-only`, 분석·완료 기록은 `Report-FiveKeeperDeadline.py`다. 새48경기와 기존48경기를 비교하며 학습은 하지 않는다. 완료된 출력은 덮어쓰지 않는다. 명령 비율은 terminal과 동일한 episode만 집계한다. [44번 보고](../docs/soccer/rebuild-5v5/44_T7두차례제한_T9충돌회피_GK6초배급_검증.md). 이전 v4-r2 도구와 실패 증거는 보존한다.

마지막 검토: 2026-09-25. 먼저 용도별 진입점만 읽는다. 스크립트 목록 전체를 매번 열지 않는다.

## 5대5 개발과 검증

[현행 단계와 문서 색인](../docs/soccer/rebuild-5v5/README.md)과 [실행 안내](../docs/soccer/rebuild-5v5/09_실행과_재현안내.md)를 따른다. 같은 프로젝트의 독립 에셋을 사용한다. 검증·고정 평가 도구는 학습을 실행하지 않으며, P9 학습 도구는 아래 별도 절을 따른다.

| 용도 | 진입점 | 경계 |
|---|---|---|
| strong/legacy 규칙형 평가 | `Run-FiveRuleEvaluation.ps1`, `five_rule_evaluate.py` | 동결ONNX/고정 전술 대 native 규칙형,300초·진영 교대·676/8·payload/출처 검증, 새 출력 폴더 필수 |
| 강한 규칙형 확인 배치·판정 | `Run-FiveRuleHoldout.ps1`, `Assess-FiveRule.py` | Player176경기와 별도native32경기, 대응seed bootstrap·승수·정체·경계 기준. 기본값은v7의이미 사용한 확인seed이며 재실행은 회귀 |
| 강한 규칙형 완료 기록 | `Write-FiveRuleReport.py`, `Finalize-FiveRule.py` | 이번208경기·회귀·원본/P9/공통 실행 보존·소스와빌드 일치 후 등록. [완료 보고](../docs/soccer/rebuild-5v5/26_강한규칙형감독_완료검증과인계.md) |
| 에셋 생성·EditMode·PlayMode | `Run-FiveValidation.ps1` | 새 Run 이름, 소스 보존, LongRunning 시험은 명시 선택 |
| 소스 보존 | `Save-FiveSource.ps1` | 새5대5 에셋·소스·기획·도구와 SHA-256, 기존 공용 자산은 현재 프로젝트에서 참조 |
|300초 경기·판단 주기 판정| `Assess-FiveGames.ps1`, `Assess-FivePeriods.ps1` | 수집 종료와 개별 경기 품질을 구별 |
| 전술 대체 비교 | `Assess-FiveHoldout.ps1`, `Summarize-FiveMatchups.ps1`, `Review-FiveDominance.ps1` | score/mission 비교·탐색적 지배 검토를 구분, 사용한 seed 명시 |
| 전술 존속 검토 | `Review-FiveRetention.py`, `Review-FiveFocused.py` | Python 표준 라이브러리. 모든 대체 명령·원시 지표·진영 쌍을 비교. 균등 사용을 합격 기준으로 삼지 않음 |
| P7 역할·교차·경기 판정 | `Assess-FiveP7Containment.py` | 현행 역할 가설 v6. 역할별 이점/열세,상황·체력별8×8 비교,경기별 품질과 실제 선택/기회/명령 유지 구간. 표본 수를 명시하며 기존 결과를 덮어쓰지 않음 |
| P7 조건부 상성 도표 | `Plot-FiveP7Containment.py` | Python/Pillow.9상황·2체력·12초 물리 지표이며 승률이나 모든 상황의 보장으로 해석하지 않음 |
| P7 전달 검증 | `Finalize-FiveP7Resume.py` | 현행 최종 소스 일치·P7 전용 판정·45개 기능 회귀·11개 계산 검사·빌드/실제 Player·원본693개·소스 일치와 문서 링크를 확인해 완료 영수증 생성 |
| 물리 궤적 도표 | `Plot-FiveRetention.py` | Python/Pillow. 저장된 실제 PhysX 프레임을 그림으로 표시하며 게임 스크린샷과 구분 |
| Windows 검토 빌드·렌더 경기 | `Build-FiveReview.ps1`, `Run-FiveReview.ps1` | 새 빌드 경로, lifecycle 등록/사용 기록, 숨겨진 검토 창의 실제 카메라/UI 렌더 |

`Assess-FiveP7.py`, `Plot-FiveP7.py`, `Finalize-FiveP7.py`, `Write-FiveP7Report.py`는 이전 P7 실패 배치용 이력 도구다. 현행 완료 판정에 사용하지 않는다. 실제 수행한 배치·역할 가설 변경·재현 범위는 [P7 재개 인계](../docs/soccer/rebuild-5v5/17_P7재개_최종검증과인계.md)를 따른다.

## 현행 MS v3

| 용도 | 진입점 | 의존성과 실행 경계 |
|---|---|---|
| 파일·본문 검색 | `Find-ProjectContext.ps1` | 기본은 현행 문서. Archive/Legacy는 명시 선택 |
| 현행 Manager Player 빌드 | `Build-MNGCurrent.ps1` | 단계·목적 필수, UTC 이름의 새 경로, 기록 자동 등록. `-PlanOnly`는 실행 없음 |
| 졸업 전시 UI 1차 최종 Player | `Build-Exhibition.ps1` | 전시 전용 씬·Localization·6종 신경망/4종 규칙형. [구현·검증 현황](../docs/soccer/exhibition/implementation-status.md) |
| 빌드 생성·사용 기록 / 정리 검토 | `Build-Lifecycle.ps1` | Register/Use/Review. [관리 기준](../docs/project/build-lifecycle.md); 자동 삭제 없음 |
| 경기 시연 | `watch_mng_v3.py` | 내부 `watch_mng_v2.py` 재사용. MS2-v3 자기대전 기준선 |
| 동결 평가 | `mng_v3_evaluate.py` | 내부 `mng_v2_evaluate.py` 재사용. v3 계보·runtime 검사 |
| 정책 계보 | `mng_v3_policy_guard.py` | `docs/soccer/training/ms-v3-model-registry.json` |
| 준비 패키지 | `prepare_mng_ms3_experiment.py` | `--generation v3`; actor·receipt·preflight 필요 |
| 한정 학습 | `MNG_V2_Train.ps1` | v3 Run 명칭 지원. 준비된 launch 스크립트와 gate를 통해 사용 |
| 사전 연결 검사 | `check_mng_ms3_preflight.py` | 32환경 preflight, 학습 품질 검증과 구분 |
| 학습·평가 계측 | `inspect_mng_v2_training.py`, `inspect_mng_v2_evaluation.py`, `inspect_mng_pass_transitions.py` | v2 이름을 가진 공유 구현 |
| 회귀 | `test_mng_v3_lineage.py`, `test_mng_ms3_readiness.py`, `test_mng_v2_*.py`, `test_mng_common_rules.py` | v3와 공유 기반 검사 |

실제 준비 패키지는 `Logs/MNG-Rebuild/MS3-v3-D5-prepared-20260923`이다. 현재 단계와 실행 범위는 [현재 상태](../docs/soccer/current-status.md), 명령과 평가 seed는 [D4·D5 보고서](../docs/soccer/training/ms3-v3-d4-d5-report-20260923.md)를 따른다. 이 색인은 실행 승인이 아니다.

## 유지하는 도구

- `mng_v2_learn.py`, `mng_v2_formal_learn.py`, `mng_v2_assess.py` 등은 v3 실행기·검사기의 의존성이 있다. 이름만으로 Archive로 이동하지 않는다.
- `MNG_MS*`, `MNG_Build.ps1`, `MNG_Train.ps1`, `MNG_Evaluate*.ps1`은 구 M/MS 구현 이력이다. 현행 v3 시작 명령으로 사용하지 않는다. 대응 과거 바이너리는 2026-09-25 폐기했으므로 원래 명령 그대로의 재실행은 불가능하다. 동결 스냅샷·스크립트의 역사적 경로는 유지하며 자동 재빌드하지 않는다.
- `MNG_V2_League.ps1`, `MNG_V2_ModelGuard.ps1`, `mng_v2_pin_champion.py` 등의 직접 실행은 v2 이력 경로다. 현행 자격은 v3 registry를 우선한다.
- `Diagnose-MNGPostR6.ps1`, `diagnose_mng_post_r6.py`, `inspect_mng_post_r6.py`, `compare_mng_common_rules.py`는 원인 조사 재현용이다.
- `Train-Soccer.ps1`, `Build-SoccerTraining.ps1`, `evaluate_soccer_policy.py`, `inspect_soccer_training.py`, `assess_soccer_l3_entry.py`, `soccer_evaluation_seeding.py`, `test_soccer_*.py`는 기존 선수 Curriculum 경로다. r017 중단 상태를 존중한다.
- `MNG_*Snapshot.ps1`은 증거 보존용이다. `__pycache__`는 생성 캐시이며 기본 검색에서 제외한다.

도구 물리 경로는 의존성·재현성과 동결된 source hash를 위해 유지했다. 사용되지 않음이 확정되지 않은 실행 도구는 삭제하지 않았다.

구 v2 시연 기본값, `MNG_V2_Smoke.ps1`, `Diagnose-MNGPostR6.ps1` 등의 기본 경로에 있던 이전 Player도 삭제됐다. 이력 분석 코드는 보존했으나 바이너리가 필요한 구 명령은 그대로 실행할 수 없다. 현행 시연은 `watch_mng_v3.py`를 사용한다. 새 빌드로 오래된 실험을 실행해 동일 조건 재현으로 간주하지 않는다.

## 최소 검색 예

P8 R1+M2 계약은 `Build-FiveLearning.ps1`로 별도 빌드를 생성하고 `Smoke-FiveLearning.py`로 학습 없는676관측 LLAPI 통신을 검사한다. `Finalize-FiveP8.py`는 사용자 확정·실제 검사·빌드 payload·원본 보존을 확인한 뒤 완료 영수증을 쓴다. [확정 계약](../docs/soccer/rebuild-5v5/18_P8학습계약과_검증계획.md)을 따른다.

5대5 단일 명령 계약 검토의 재현은 `Run-FiveValidation.ps1`의 `Five(CommandContract|Tactic|CounterTiming|Controls)PlayTests` 필터를 사용한다. `Finalize-FiveCommandContract.py`는2026-10-12 해당 실행 결과·원본 해시·미변경 런타임·문서 링크를 확인하는 전용 완료 도구다. 경기 결과를 새로 생성하지 않는다. 자세한 명령·결과는 [단일 선택 검토](../docs/soccer/rebuild-5v5/14_상황별_명령구성과_단일선택_검토.md)에 있다.

```powershell
./Tools/Find-ProjectContext.ps1
./Tools/Find-ProjectContext.ps1 -Scope Manager -Pattern SetTask -Literal
./Tools/Find-ProjectContext.ps1 -Scope Tools -Pattern mng_v3
./Tools/Find-ProjectContext.ps1 -Scope Archive -Pattern r017 -Literal
```

## P9 새 5대5 감독 학습과 고정 평가

`Run-FiveTraining.ps1`은 고정된 CPU PPO 설정과 Player payload를 확인해 새 Run/같은 Run 재개를 실행한다. `Run-FiveEvaluation.ps1`은 checkpoint별3종 상대72경기와 PT/ONNX 확률 대조를 실행한다. `Audit-FiveTraces.py`와 `Combine-FiveP9Traces.py`는 완료한 실제 학습 trace를 검사·합산한다. `Summarize-FiveP9.py` → `Write-FiveP9Report.py` → 최종 `Save-FiveSource.ps1` → `Finalize-FiveP9.py` 순서로 결과·보고서·소스/증거 해시를 확정한다. 기존 완료 증거는 덮어쓰지 않는다. [P9 완료 결과](../docs/soccer/rebuild-5v5/23_P9완료_결과와인계.md) 및 [실행 계획](../docs/soccer/rebuild-5v5/22_P9학습과_평가_실행계획.md)을 따른다.
