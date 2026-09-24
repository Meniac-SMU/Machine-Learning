# 프로젝트 도구 색인

마지막 검토: 2026-09-24. 먼저 용도별 진입점만 읽는다. 스크립트 목록 전체를 매번 열지 않는다.

## 현행 MS v3

| 용도 | 진입점 | 의존성과 실행 경계 |
|---|---|---|
| 파일·본문 검색 | `Find-ProjectContext.ps1` | 기본은 현행 문서. Archive/Legacy는 명시 선택 |
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
- `MNG_MS*`, `MNG_Build.ps1`, `MNG_Train.ps1`, `MNG_Evaluate*.ps1`은 구 M/MS 단계 재현용이다. 현행 v3 시작 명령으로 사용하지 않는다. 파일의 `$PSScriptRoot` 및 보존된 스냅샷·스크립트 참조를 유지한다.
- `MNG_V2_League.ps1`, `MNG_V2_ModelGuard.ps1`, `mng_v2_pin_champion.py` 등의 직접 실행은 v2 이력 경로다. 현행 자격은 v3 registry를 우선한다.
- `Diagnose-MNGPostR6.ps1`, `diagnose_mng_post_r6.py`, `inspect_mng_post_r6.py`, `compare_mng_common_rules.py`는 원인 조사 재현용이다.
- `Train-Soccer.ps1`, `Build-SoccerTraining.ps1`, `evaluate_soccer_policy.py`, `inspect_soccer_training.py`, `assess_soccer_l3_entry.py`, `soccer_evaluation_seeding.py`, `test_soccer_*.py`는 기존 선수 Curriculum 경로다. r017 중단 상태를 존중한다.
- `MNG_*Snapshot.ps1`은 증거 보존용이다. `__pycache__`는 생성 캐시이며 기본 검색에서 제외한다.

도구 물리 경로는 의존성·재현성과 동결된 source hash를 위해 유지했다. 사용되지 않음이 확정되지 않은 실행 도구는 삭제하지 않았다.

## 최소 검색 예

```powershell
./Tools/Find-ProjectContext.ps1
./Tools/Find-ProjectContext.ps1 -Scope Manager -Pattern SetTask -Literal
./Tools/Find-ProjectContext.ps1 -Scope Tools -Pattern mng_v3
./Tools/Find-ProjectContext.ps1 -Scope Archive -Pattern r017 -Literal
```
