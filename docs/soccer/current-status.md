# Soccer 현재 상태

- 대상: 모든 담당자
- 마지막 검토: 2026-08-10
- 상태: 공통 구현 완료, v2 학습과 통합 재검증 대기

이 문서는 현재 사실과 다음 작업만 유지한다. 완료 과정의 상세 기록은 [Archive](../archive/README.md)로 옮긴다.

## 한눈에 보기

| 영역 | 상태 | 비고 |
| --- | --- | --- |
| 4v4 공통 경기 | 구현 완료 | 5분 경기, Goal Pause, Reset, HUD |
| 정책 계약 v2 | 구현 완료 | Vector 43, 전체 입력 379, Action `[3,3,3,3]` |
| Human 이동 | 구현 완료 | analog 입력, target velocity, Rigidbody interpolation |
| Keeper 보호 | 구현 완료 | Neural·fallback·Rule 공통 shield와 Recovery |
| 동료 crowding | 구현 완료 | 공통 penalty와 Rule separation |
| 팀 작업공간 | 구조 완료 | Base, Attack, Defense, Press, Rule 독립 Prefab |
| 문서 구조 | 정리 완료 | 주제별 활성 문서와 Archive 분리 |
| v2 ONNX | 없음 | `SourceModels/SoccerTwos.onnx`는 이전 계약 모델 |
| 감독 전술 교체 | 미구현 | Model 계약 검사와 같은 Frame 교체 필요 |
| Windows 학습 Build | 미검증 | Trainer communicator smoke test 필요 |

## 최신 검증

2026-08-10에 Unity Hub를 종료하고 Editor `6000.3.16f1`을 직접 batch 실행해 Asset import와 C# compile이 성공했다.

- 성공 Log: `Logs/Licensing-Isolated-Compile.log`
- compile error: `0`
- batch 종료: 정상
- 원인: Hub Licensing Client `1.17.4`와 Editor 내장 Client `1.18.1`의 protocol 충돌
- Student entitlement의 `com.unity.editor.headless` 권한은 정상 확인

폴더 이동 전 마지막 회귀 기준:

- EditMode `23/23`: `Logs/Soccer-Workspace-EditMode.xml`
- Soccer PlayMode `3/3`: `Logs/Soccer-Workspace-PlayMode.xml`
- Builder 생성·검증: `Logs/Soccer-Generate.log`

폴더 이동 후에는 정적 GUID·Scene·Prefab 검사와 전체 C# compile까지 통과했다. 새 경로에서 `ValidateBatch`, EditMode와 PlayMode 전체 실행은 아직 완료 기록이 없으므로 다음 통합 검증에서 다시 실행한다. 실행 절차는 [설정 및 검증](../project/setup-and-validation.md)을 따른다.

## 현재 알려진 제약

- Base·Attack·Defense·Press의 `Models` 폴더에 정책 계약 v2 ONNX가 없다.
- 승인된 Base v2 Model이 없으면 Attack·Defense·Press Scene의 Purple은 학습된 Base 상대가 아니라 heuristic fallback일 수 있다.
- Model 파일명과 `PolicyContractVersion = 2`만으로 실제 Tensor 호환성을 증명할 수 없다.
- 경기 중 감독 전술 관리자가 아직 없어 Scene 시작 시 Model만 적용한다.
- 실제 Windows 화면에서 HUD·Goal transparency·Human game feel을 최종 확인하지 않았다.
- `SoccerFieldTwos.prefab`에는 현재 Package에 없는 과거 시연 기록 Component가 남아 있다. 활성 Scene은 직접 사용하지 않으며 삭제는 별도 승인 대상이다.
- Unity Hub가 실행 중이면 licensing IPC 충돌이 재발할 수 있다.

## 다음 작업 순서

1. Hub를 닫은 상태에서 새 경로 `ValidateBatch`, EditMode, Soccer PlayMode를 실행한다.
2. 짧은 Base smoke training으로 Trainer·Behavior 연결을 확인한다.
3. Base Self-Play 대표 v2 ONNX를 선정하고 `BaseTeamDefinition`에 등록한다.
4. Attack·Defense·Press를 같은 Base 상대 조건으로 각각 학습·평가한다.
5. 세 ONNX의 실제 Input/Output Tensor와 네 선수 Model 일치를 검증한다.
6. 경기 상태를 유지하는 감독 전술 Manager와 HUD 입력을 구현한다.
7. Windows Build, 실제 Controller, 장시간 경기와 성능을 검증한다.

학습 절차는 [학습 운용](training/overview.md), 담당 경계는 [학습형 팀 가이드](training/learning-teams.md)를 사용한다.

## 이번 문서 정리의 영향

| 항목 | 변경 |
| --- | ---: |
| C#·Scene·Prefab·Profile | `0` |
| Trainer 설정 | `0` |
| 보상 수치·판정·상한 | `0` |
| `.gitignore` | Markdown Git 추적 허용 규칙 추가 |

Markdown의 이름·폴더·링크와 설명을 정리했고, 이후 팀 공유를 위해 Git 추적 대상으로 전환했다. 실제 commit과 push는 아직 수행하지 않았다.
