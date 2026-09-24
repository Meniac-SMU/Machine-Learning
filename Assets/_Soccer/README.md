# Soccer 활성 Asset

`Assets/_Soccer`는 현재 Soccer 4v4의 유일한 활성 Root다. 공통 Runtime, 다섯 작업공간, Scene·Prefab·UI·Editor Builder와 Test를 함께 보관한다.

현행 개발 중심은 [MNG 감독](Manager/MNG_README.md)이며 기존 Core·팀·Curriculum도 보존한다. 최신 상태는 [현재 상태](../../docs/soccer/current-status.md)를 먼저 읽는다.

## 폴더 안내

| 폴더 | 책임 |
| --- | --- |
| `Manager` | 현행 MS v3 감독 RL·공유 선수 기술 |
| `Core` | 공통 Runtime과 Base 환경 |
| `Teams/Attack_KMW` | Attack 학습 |
| `Teams/Defense_PJH` | Defense 학습 |
| `Teams/Press_KMG` | Press 학습 |
| `Teams/Rule_PHC` | 비학습 Rule FSM |
| `Editor` | 생성·검증 Builder |
| `Prefabs`, `Scenes`, `UI` | 공용 경기와 표시 Asset |
| `Tests` | EditMode·PlayMode 계약 검증 |
| `Diagnostics` | Soccer 진단 기록 |

담당자 접미사는 물리 폴더명에만 쓴다. `BehaviorName`, Model Type, C#·asmdef 논리명은 `Attack`, `Defense`, `Press`, `Rule`을 유지한다.

## 변경 경계

- 학습 담당자는 자기 Profile, YAML, Versioned ONNX, TeamDefinition Model 참조와 제한된 RewardPolicy를 소유한다.
- Rule 담당자는 FSM을 소유하며 Trainer·ONNX를 추가하지 않는다.
- 경기·관측·Action·Physics·Sensor·Reward 사건·Keeper·Reset은 공통 계약이다.
- 공통 변경은 Builder로 Template과 다섯 환경 Prefab에 함께 적용한다.

필독: [문서 색인](../../docs/README.md), [Soccer 아키텍처](../../docs/soccer/architecture.md), [Asset 구조](../../docs/project/asset-layout.md).
