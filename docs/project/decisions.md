# 현재 결정 요약

- 대상: 모든 담당자
- 상태: 활성 결정의 단일 기준
- 마지막 검토: 2026-09-24 (문서 구조·세대 경계)

현재 구현에 영향을 주는 결정만 요약한다. 배경과 대체 관계를 포함한 원문은 [전체 결정 기록](../archive/history/decision-log-full.md)에 보관한다. Archive 원문보다 `AGENTS.md`와 이 문서의 현행 해석을 우선한다.

## 현행 우선 기준

MNG는 MS3-v3 학습 전 D5 준비 완료 상태이며 [MS v3 운영 기준](../soccer/training/ms-v3-current.md)과 v3 registry를 우선한다. 기존 Core 정책과 별개다. 문서·진단 자산 이동과 보존 판단은 [정리 보고서](cleanup-20260924.md)를 따른다.

## 유지하는 기존 결정

| ID | 결정 | 실무 영향 |
| --- | --- | --- |
| D-002 | Turtle은 역사 자료로 보존 | 신규 개발 금지, 구조 이동으로 깨진 참조만 최소 복구 |
| D-004 | 학습 환경을 게임 기능보다 먼저 검증 | Observation·Action·Reward·종료·지표를 먼저 정의 |
| D-014 | Escape 개발 종료 | `Assets/_Legacy/Escape`에 실행 보존, 현행 Build·학습 제외 |
| D-015 | Pass를 폐기하고 Rule FSM을 비교군으로 사용 | Rule에 Trainer·ONNX 연결 금지 |
| D-016 | 최종 게임은 경기 중 Neural 전술 교체 | Attack·Defense·Press만 교체, 경기 상태 유지 |
| D-017 | 역할과 Action 계약 v2 확정 | 1-2-1 역할, 계약이 다른 ONNX 사용 금지 |
| D-018 | Goal·Match를 필수 목표로 두고 연계 shaping 사용 | 고정 Formation을 강요하지 않고 cap 적용 |
| D-020 | Model 이름과 최종 평가 척도 확정 | `Type-YYYYMMDD-vNNN`, 최종 시연은 Base RewardProfile |
| D-021 | AI 기본 시작과 경기 HUD | Human 전환 유지, Model·경기 Reward 표시 |
| D-022 | 독립 Model Slot과 공통 경기장 동기화 | Template과 다섯 Prefab parity 필수 |
| D-023 | Goal 전용 Material과 조건부 transparency | Renderer만 fade, Collider·Goal Tag 유지 |
| D-024 | DefenderKeeper 공통 half-line shield와 Recovery | D-018의 넓은 Keeper 전진 허용을 대체 |
| D-025 | 학습형과 Rule 담당자의 수정 경계 분리 | Reward 실험과 FSM 수정 경로를 분리 |
| D-026 | 활성 Soccer와 Legacy Asset을 물리 분리 | `_Soccer`, `_Legacy`, 담당자 접미사와 논리명 분리 |
| D-027 | 문서를 주제별 단일 기준과 Archive로 재구성 | 숫자 파일명 제거, 상세 수치 중복 금지, Archive 기본 로드 금지 |
| D-028 | 프로젝트 작업 경계를 Root로 단일화 | 다른 Project를 조회·수정하지 않고 모든 산출물을 Root 안에 둠 |
| D-029 | 프로젝트 Markdown을 Git 추적 대상으로 전환 | 팀원과 coding agent가 같은 문서를 공유하도록 `*.md` 제외 해제 |

경기 수치의 단일 기준은 [경기 계약](../soccer/gameplay-contract.md), Reward는 [보상 기준표](../soccer/rewards.md)다. 이 표에 수치를 다시 복제하지 않는다.

## D-027 — 문서 정보 구조

- 날짜: 2026-08-10
- 상태: `확정`
- 결정:
  - 활성 문서를 `project`, `soccer`, `soccer/training`으로 구분한다.
  - 파일명에서 순번을 제거하고 작업 목적을 이름으로 표시한다.
  - 한 주제의 값은 한 문서만 소유하고 다른 문서는 링크한다.
  - 완료·대체·Legacy 내용은 `archive`로 이동하고 기본 Agent Context에서 제외한다.
  - 해당 문서 정리 작업에서는 `.gitignore`와 공개 상태를 변경하지 않는다. 이 결정은 이후 D-029로 변경됐다.
- 이유: 신규 팀원 탐색 비용, 중복 수정과 agent token 사용량을 줄이기 위해서다.
- 영향: 이전 `docs/01...17` 링크는 새 주제 경로로 교체한다.

## D-028 — 프로젝트 경계 단일화

- 날짜: 2026-08-10
- 상태: `확정`
- 결정: 프로젝트 Source 조사와 모든 생성·수정·삭제 대상을 `C:\GitHub\Machine-Learning` 내부로 제한한다. 설치된 Unity·Python 도구는 이 프로젝트 검증에 호출할 수 있으나 설치 파일과 다른 Project는 수정하지 않는다.
- 이유: 다른 프로젝트와 사용자 자료를 보호하고 전달 가능한 자체 완결 문서를 유지하기 위해서다.
- 영향: 과거 D-001의 외부 reference 조회 허용 부분은 더 이상 사용하지 않는다. Archive에 남은 외부 경로는 당시 기록일 뿐 현재 지침이 아니다.

## D-029 — Markdown Git 추적 허용

- 날짜: 2026-08-10
- 상태: `확정`
- 결정: 프로젝트가 관리하는 모든 Markdown을 `.gitignore`에서 Git 추적 대상으로 허용한다. Library·Temp·Logs 등 생성 폴더의 기존 제외 규칙은 유지한다.
- 이유: 팀원과 coding agent가 같은 현행 명세·가이드·Archive를 GitHub에서 참고할 수 있어야 한다.
- 영향: 문서는 Git에 추가할 수 있는 상태가 되며, 실제 원격 공개에는 별도의 `git add`·commit·push가 필요하다. D-005의 비공개 원칙과 D-027의 공개 보류를 대체한다.

## 새 결정 기록 방법

되돌리기 어렵거나 여러 팀에 영향을 주는 변경만 새 ID로 기록한다.

```text
ID / 제목 / 날짜 / 상태
배경
결정
이유
영향
대체한 결정과 관련 기준 문서
```

단순 구현 과정과 Test 결과는 결정으로 만들지 않고 [현재 상태](../soccer/current-status.md) 또는 실험 기록에 남긴다.
