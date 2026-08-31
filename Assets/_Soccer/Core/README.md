# Soccer 공통 Core

`Assets/_Soccer/Core`는 모든 Controller가 공유하는 정책·경기 계약과 Base 학습 공간이다. 팀 전용 실험을 위해 `Core/Scripts`에 분기를 추가하지 않는다.

## 진입점

| 폴더·파일 | 책임 |
| --- | --- |
| `Scripts/AgentSoccer.cs` | Observation, Action, Human 입력, 이동·Kick |
| `Scripts/SoccerEnvController.cs` | 경기 상태, Team, Reset, model 없는 공통 carry/pass/shot 선택 |
| `Scripts/Core/SoccerRewardEngine.cs` | 공통 Reward 사건과 cap |
| `Scripts/Core/SoccerDefenderKeeperRules.cs` | 공통 Keeper Engage/Recovery shield |
| `Scripts/Core/SoccerDefensiveClearanceRules.cs` | 자책골 궤적 예측, 중앙 걷어내기와 Strong Kick 분류 |
| `Profiles` | Base RewardProfile과 TeamDefinition |
| `Scenes/Stadium4v4_Base.unity` | 기본 Base 학습·검증 Stadium Scene |
| `Prefabs/StadiumEnvironment_Base.prefab` | 다섯 환경의 데모 외형·Terrain·물리 기준본 |
| `Editor/SoccerStadiumBuilder.cs` | Core Stadium 생성 후 모든 팀 Stadium으로 전파·검사 |
| `Scripts/Core/SoccerArenaGeometry.cs` | Scene별 필드·골문 치수, 목표·공 최종 위치 제한과 골문 판정 |
| `Training` | 짧은 Base Trainer 연결 설정 |
| `Models` | 승인된 Base v2 ONNX |

## 수정 경계

- Base 개인 실험은 Base Profile, Trainer, Model과 TeamDefinition의 Model 참조에서 한다.
- Observation·Action·Physics·사건 판정 변경은 전체 정책 계약 변경이다.
- 공통 경기장 변경은 `SoccerProjectBuilder`를 통해 다섯 환경과 함께 검증한다.
- Goal·공 70% 크기, 선수당 후방 Ray 정확히 1개, 적극 Keeper, Human/AI 가속 parity, Pass·운반·안전 Kick은 모든 환경이 공유하는 기본기 계약이다.
- Base Model 변경은 Attack·Defense·Press의 Navy 기준 상대를 바꾸므로 담당자에게 공유한다.

## 활성 Stadium 기준본

`Stadium4v4_Base`와 `StadiumEnvironment_Base`는 Base·Attack·Defense·Press·Rule의 유일한 활성 경기장 기준본이다. 원본 `Hayq Art/GrantStadium`은 보존하고, 예전 `SoccerEnvironment_*`와 `Soccer4v4_*`는 Build Settings와 학습에서 제외한 이력 자산으로 남긴다.

`Tools/Soccer/Stadium/Build all team Stadiums`가 Demo를 복제하고 현재 Core Stadium Prefab의 선수·MatchSetup·규칙을 유지하며 공유 UI 자산에서 HUD·카메라를 다시 구성한 뒤 네 팀 폴더로 전파한다. 이 경로는 이력용 사각 경기장 Scene·Prefab에 의존하지 않는다. 후속 조정에는 `Tools/Soccer/Stadium/Apply current Stadium tuning (preserves terrain and UI)`를 사용한다. 기존 자산을 백업한 뒤 공·골대 폭·벽/모서리·Kick 힘·HUD 위치를 갱신하고 모든 팀 Prefab·Scene을 다시 동기화한다. 반복 적용해도 배율이 누적되지 않는다. 비파괴 검사는 `Tools/Soccer/Stadium/Validate all team Stadiums`를 사용한다. `SoccerProjectBuilder.BuildAllBatch`도 Core Stadium 기준본에서 다섯 작업공간을 생성한다.

현재 증분 조정에는 승인된 팀 색 골대, 골문 바닥 표시와 나무 인스턴스 제거도 포함된다. 원본 노란색 공유 재질을 바꾸지 않고 `Materials/StadiumRedGoal.mat`·`StadiumNavyGoal.mat`를 사용하며 기존 골대 fader를 그대로 적용한다. 골문 아래의 기존 Floor Collider는 `Materials/StadiumGoalFloor.mat`로 연회색 표시만 추가해 중복 물리 바닥을 만들지 않는다. Stadium의 나무만 제거하고 원본 나무 에셋, Terrain 표면, 관목·꽃과 나머지 디자인·UI는 보존한다.

정책 shape는 `379`와 `[3,3,3,3]`으로 유지한다. 자기 위치 정규화, 골문 크기·모서리를 포함한 필드 경계와 목표 Clamp는 Scene Geometry에서 읽으며, 공통 fallback 수치도 Stadium 기준이다. 모든 활성 공은 바닥 틈을 없애고 평면 속도에 맞춘 굴림 회전과 반지름 포함 최종 경계 제한을 사용한다. Controlled/Strong Kick 기본값과 Prefab 값은 `2000 / 5000`이다. 전술·누적 보상 패널은 우측 하단에 둔다. 상세 수치는 [활성 Stadium 경기 계약](../../../docs/soccer/gameplay-contract.md#활성-stadium-공통-scene), 최신 검증과 제약은 [현재 상태](../../../docs/soccer/current-status.md)를 따른다.

수치는 이 README에 복제하지 않는다. [경기 계약](../../../docs/soccer/gameplay-contract.md), [Reward 기준표](../../../docs/soccer/rewards.md), [학습 운용](../../../docs/soccer/training/overview.md)을 사용한다.
