# 프로젝트 문서 색인

마지막 검토: 2026-08-10

이 폴더는 사람과 coding agent가 함께 사용하는 문서의 진입점이다. 모든 문서를 한 번에 읽지 말고 아래 작업별 경로에서 필요한 문서만 선택한다. 프로젝트가 관리하는 Markdown은 `.gitignore`에서 Git 추적 대상으로 허용한다. 실제 GitHub 반영은 검토 후 `git add`·commit·push 단계에서 진행한다.

## 처음 참여할 때

1. [Soccer 개요](soccer/overview.md) — 제품 목표와 현재 범위
2. [현재 상태](soccer/current-status.md) — 완료, 미검증, 다음 작업
3. [Asset 구조](project/asset-layout.md) — 폴더 소유권과 이동 규칙
4. 자신의 역할에 맞는 [학습형 가이드](soccer/training/learning-teams.md) 또는 [Rule 가이드](soccer/training/rule-team.md)

## 작업별 최소 문서

| 작업 | 먼저 읽을 문서 | 추가 기준 |
| --- | --- | --- |
| 경기 규칙·입력·물리 | [경기 계약](soccer/gameplay-contract.md) | [아키텍처](soccer/architecture.md) |
| 공통 Core·Prefab·Builder | [아키텍처](soccer/architecture.md) | [검증 절차](project/setup-and-validation.md) |
| 보상 수치·판정 변경 | [보상 기준표](soccer/rewards.md) | 해당 팀 가이드와 README |
| Base·Attack·Defense·Press 학습 | [학습 운용](soccer/training/overview.md) | [학습형 팀 가이드](soccer/training/learning-teams.md) |
| Rule FSM 수정 | [Rule 팀 가이드](soccer/training/rule-team.md) | [경기 계약](soccer/gameplay-contract.md) |
| ONNX 등록·전술 교체 | [학습 운용](soccer/training/overview.md) | [현재 상태](soccer/current-status.md) |
| Unity 실행·테스트·라이선스 | [설정 및 검증](project/setup-and-validation.md) | [현재 상태](soccer/current-status.md) |
| 파일 이동·이름 변경 | [Asset 구조](project/asset-layout.md) | [결정 요약](project/decisions.md) |
| 과거 Turtle·Escape 조사 | [Archive 안내](archive/README.md) | 필요한 보관 문서만 선택 |

## 단일 기준 문서

같은 숫자와 규칙을 여러 문서에 복사하지 않는다.

| 정보 | 단일 기준 |
| --- | --- |
| 제품 목표와 범위 | [Soccer 개요](soccer/overview.md) |
| 경기 시간·역할·관측·행동·입력·골키퍼 규칙 | [경기 계약](soccer/gameplay-contract.md) |
| 코드 책임·데이터 흐름·공통/개별 경계 | [아키텍처](soccer/architecture.md) |
| 보상 값·판정·상한 | [보상 기준표](soccer/rewards.md) |
| 학습·모델·평가 절차 | [학습 운용](soccer/training/overview.md) |
| 최신 검증과 남은 작업 | [현재 상태](soccer/current-status.md) |
| 폴더·GUID·Legacy 위치 | [Asset 구조](project/asset-layout.md) |
| 확정 결정 | [결정 요약](project/decisions.md) |

## 작성 원칙

- 한국어를 기본으로 쓰되 `RewardProfile`, `BehaviorName`, `Prefab`, `ONNX`, `FSM`, `Raycast`처럼 실무에서 통용되는 이름은 원문을 유지한다.
- 문서에는 목적, 대상, 마지막 검토일과 단일 기준 여부를 짧게 표시한다.
- 세부 수치를 복제하지 말고 기준 문서의 제목 링크를 사용한다.
- 한 문서가 대략 180줄을 넘기거나 서로 다른 두 작업 흐름을 다루면 분리한다.
- 현재 지침에는 현재 경로만 쓴다. 당시 사실이 중요한 작업 기록은 Archive로 이동한다.
- 완료된 작업을 현재 상태에 계속 누적하지 않는다. 결과만 남기고 상세 이력은 Archive로 옮긴다.
- 보상 변경 시 [보상 기준표](soccer/rewards.md), Profile, 팀 README, 테스트와 `기존 값 / 변경 값 / 변경량` 표를 같은 작업에서 갱신한다.

## Archive

`archive`는 기본 Context가 아니다. 현행 작업에 직접 필요한 경우에만 파일 하나를 선택해 읽는다.

- [Archive 안내](archive/README.md)
- `archive/history`: 과거 결정 원문과 날짜별 작업 기록
- `archive/completed`: 완료된 시각·복구 작업
- `archive/legacy`: Turtle·Escape 기획과 기술 기록
