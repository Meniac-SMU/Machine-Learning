# Soccer 4v4 개요

- 대상: 신규 팀원, 기획·개발 담당자
- 상태: 활성 제품 방향의 단일 기준
- 마지막 검토: 2026-08-10

## 제품 목표

Unity ML-Agents로 팀당 4명이 경기하는 Soccer 게임을 만든다. 플레이어는 한 선수를 직접 조작할 수 있고, 감독으로서 경기 중 자기 팀의 `Attack`, `Defense`, `Press` ONNX 전술을 교체한다.

세 학습 폴더는 서로 다른 구단이 아니라 같은 팀에 사용할 전술을 따로 학습하는 작업공간이다. `Rule`은 강화학습 없이 FSM과 Raycast로 움직이는 비교 기준이며 감독 전술 교체 대상이 아니다.

## 플레이 구조

```text
사람의 직접 조작 ─┐
                   ├─ 공통 이동·킥·골키퍼 보호 ─ 경기 진행
선택된 ONNX 전술 ─┤
Rule FSM ─────────┘
```

- Human 모드에서는 Red 선수 한 명만 직접 조작하고 나머지 선수는 자율 행동을 계속한다.
- AI 모드에서는 팀 전체가 현재 Controller를 사용한다.
- 최종 감독 기능은 점수, 시간, 공과 선수 상태를 유지한 채 세 Neural 전술만 교체한다.
- 모든 Controller는 같은 경기·관측·행동·물리 계약을 통과한다.

수치와 입력 의미는 [경기 계약](gameplay-contract.md), 코드 흐름은 [아키텍처](architecture.md)를 단일 기준으로 사용한다.

## 개발 공간

| 공간 | 목적 | 최종 산출물 |
| --- | --- | --- |
| Base | 공통 학습 파이프라인과 기준 상대 | 승인된 Base v2 ONNX |
| Attack | 공격 성향 shaping 실험 | Attack v2 ONNX |
| Defense | 수비 성향 shaping 실험 | Defense v2 ONNX |
| Press | 전방 압박 성향 shaping 실험 | Press v2 ONNX |
| Rule | 비학습 FSM 비교 | Rule 경기 지표 |

담당 폴더와 논리 이름은 다르다. 예를 들어 물리 폴더는 `Attack_KMW`지만 `BehaviorName`, 모델 Type과 C# 논리명은 `Attack`이다.

## 성공 기준

- 같은 정책 계약으로 학습된 세 ONNX를 안전하게 교체한다.
- Human 전환과 AI 복귀가 나머지 선수의 행동을 끊지 않는다.
- Rule이 Trainer나 모델 없이 완전한 경기를 수행한다.
- 필수 득점·승패 목표가 shaping보다 우선한다.
- 보상과 전술 변화가 같은 조건의 경기 지표와 영상으로 재현된다.
- 팀 담당자의 개별 실험이 공통 경기장·관측·행동 계약을 바꾸지 않는다.

## 현재 범위

활성 범위:

- Soccer 4v4 공통 경기와 품질 개선
- Base·Attack·Defense·Press 학습과 평가
- Rule FSM 개선과 비교
- ONNX 등록과 감독 전술 교체 구현
- 사람 조작, HUD, 빌드와 회귀 검증

제외 범위:

- Turtle·Escape 신규 개발
- 폐기된 Pass 전술 재활성화
- 온라인 Multiplayer와 최종 출시 Art·Platform 확정

Turtle과 Escape 자료는 [Archive 안내](../archive/README.md)에 따라 보존한다.

## 다음에 읽을 문서

- 현재 구현과 우선순위: [현재 상태](current-status.md)
- 공통 계약을 바꿀 때: [경기 계약](gameplay-contract.md), [아키텍처](architecture.md)
- 학습 담당자: [학습 운용](training/overview.md), [학습형 팀 가이드](training/learning-teams.md)
- Rule 담당자: [Rule 팀 가이드](training/rule-team.md)
- 보상 담당자: [보상 기준표](rewards.md)
