# Soccer 현재 상태

- 대상: 모든 담당자
- 마지막 검토: 2026-08-31
- 상태: Stadium 공통 계약과 6-Profile Windows 훈련 Build 자동 검증 완료

이 문서는 현재 사실과 다음 작업만 유지한다. 완료 과정의 상세 기록은 [Archive](../archive/README.md)로 옮긴다.

## 한눈에 보기

| 영역 | 현재 상태 |
| --- | --- |
| 활성 경기장 | 다섯 팀 모두 `Stadium4v4_*` Scene과 `StadiumEnvironment_*` Prefab 사용 |
| Build Settings | Base, Attack, Defense, Press, Rule Stadium Scene만 포함 |
| 프로젝트 기본 Scene | `Assets/_Soccer/Core/Scenes/Stadium4v4_Base.unity` |
| 공통 경기 규칙 | 5분 경기, Goal Pause·Reset, HUD, Human/AI 전환 유지 |
| Stadium 크기 | 긴 축 124m, Half Length 62m, Half Width 약 42.327m |
| Stadium 물리 | 공 scale `0.012705`, 굴림 각속도, 반지름 포함 경계 제한, 모서리 완화 벽, Goal Floor 유지 |
| Kick | Controlled `2000`, Strong `5000` |
| 정책 계약 | 전체 입력 379, Action Branch `[3,3,3,3]`, 기존 Behavior 이름 유지 |
| 팀 표시 | TeamId 0은 Red, TeamId 1은 Navy. 선수·골대·킥 플레이트·HUD에 공통 적용 |
| 팀 전술 | Attack·Defense·Press·Rule의 TeamDefinition·RewardProfile·Rule FSM 연결 유지 |
| 공유 훈련 Build | `Builds/SoccerTraining/SoccerTraining.exe`, 학습 5종 + Rule 평가 1종 |
| 병렬 학습 | `Tools/Train-Soccer.ps1`에서 Worker `8` 또는 `16`, Profile별 독립 Port 지원 |
| 전술 상대 안전장치 | Attack·Defense·Press는 현재 Build에 Base v2 Navy Model이 없어 기본 시작 차단 |
| 기존 경기장 | 자산은 이력 보존용으로 남아 있지만 Build Settings와 기본 실행 경로에서는 사용하지 않음 |
| v2 ONNX | 없음. 변경된 Stadium 동역학에 맞춘 재학습·재평가 필요 |

## 활성 Stadium 자산

| 유형 | Scene | Environment Prefab | 학습 제어 |
| --- | --- | --- | --- |
| Base fallback | `Assets/_Soccer/Core/Scenes/Stadium4v4_Base_FallbackTraining.unity` | Base Prefab instance | Red 학습, Navy 규칙 fallback 강제 |
| Base | `Assets/_Soccer/Core/Scenes/Stadium4v4_Base.unity` | `Assets/_Soccer/Core/Prefabs/StadiumEnvironment_Base.prefab` | Red·Navy |
| Attack | `Assets/_Soccer/Teams/Attack_KMW/Scenes/Stadium4v4_Attack.unity` | `Assets/_Soccer/Teams/Attack_KMW/Prefabs/StadiumEnvironment_Attack.prefab` | Red |
| Defense | `Assets/_Soccer/Teams/Defense_PJH/Scenes/Stadium4v4_Defense.unity` | `Assets/_Soccer/Teams/Defense_PJH/Prefabs/StadiumEnvironment_Defense.prefab` | Red |
| Press | `Assets/_Soccer/Teams/Press_KMG/Scenes/Stadium4v4_Press.unity` | `Assets/_Soccer/Teams/Press_KMG/Prefabs/StadiumEnvironment_Press.prefab` | Red |
| Rule | `Assets/_Soccer/Teams/Rule_PHC/Scenes/Stadium4v4_Rule.unity` | `Assets/_Soccer/Teams/Rule_PHC/Prefabs/StadiumEnvironment_Rule.prefab` | 학습 없음, Red 4명 Rule FSM |

각 팀 Scene과 Prefab은 고유 GUID를 사용한다. 공통 Stadium 지형·골대·벽·센서·HUD·공·카메라 계약은 Core Base Stadium에서 생성하며 팀별 전술, RewardProfile, TeamDefinition, 학습 플래그만 각 작업공간에 연결한다. Scene 임시 Model Override는 기존과 같이 비어 있고 영구 Model 참조는 TeamDefinition이 소유한다.

## Stadium 기본 계약

- 관찰 위치 정규화, 목표 위치 제한, Rule 목표, 수비 걷어내기 판단은 활성 `SoccerArenaGeometry`를 사용한다.
- Geometry가 없을 때도 기존 직사각형 경기장 수치가 아니라 Stadium 상수로 fallback한다.
- Demo 골대는 Red `#D33F4E`, Navy `#2F5C9C`로 표시한다. 선수가 해당 골문 안에 있고 실제 골대 Collider가 카메라와 선수를 가로막을 때만 RGB를 유지한 채 반투명화한다. 연회색 Goal Floor도 다섯 환경에서 공통 사용한다.
- 선수 몸통은 추가 HSV 조정으로 Red 채도를 약 10% 높인 `#A00218`, Navy 채도·밝기를 각각 약 15% 높인 `#002770`이다. Navy 채도는 계산 결과 100%에서 제한되며 킥 플레이트와 HUD는 기존 팀 색 계열을 유지한다.
- 하늘색 반투명 벽은 직선부와 모서리부 두께가 같고 Ray Sensor의 `wall` 대상으로 인식된다.
- 네 모서리는 높이가 외곽 벽과 같은 1.5m 판 3개씩, 총 12개로 안쪽을 완화한다.
- 공은 Stadium 경계 안에서만 움직이며 평면 이동량에 맞춰 굴러가는 각속도를 적용한다.
- 보상 패널은 Stadium에서 우측 하단에 배치된다.
- 다섯 Stadium은 도로·주차장 27개, 관목·꽃 104개, 외부 도시 소품 42개를 모두 제외한다. 공급자 원본 파일과 Terrain은 보존한다.

세부 수치는 [경기 계약](gameplay-contract.md), 생성·검증 절차는 [설정과 검증](../project/setup-and-validation.md)을 따른다.

## 2026-08-31 검증 결과

| 단계 | 결과 | 증거 |
| --- | --- | --- |
| 선명한 Red·Navy 선수와 외부 장식 제거를 포함한 Stadium 전체 재생성 | 통과 | `Logs/Stadium-Clutter-Color-Generate.log`, `STADIUM EXTERIOR OPTIMIZATION REMOVAL objects=173`, `ACTIVE STADIUM VALIDATION PASS` |
| 전체 EditMode | `56/56` 통과 | `Logs/Stadium-Clutter-Color-EditMode-All.xml` |
| Soccer PlayMode | `18/18` 통과 | `Logs/Stadium-Clutter-Color-Soccer-PlayMode.xml` |
| 기준선 보존 감사 | 기준선 806개 중 동일 776, 의도된 변경 30, 누락 0, 새 팀 Stadium 파일 16 | `Logs/Stadium4v4-AllTeams-Final-Audit.json`, `Logs/Stadium4v4-AllTeams-BaselineChanges.csv` |
| Red·Navy 자산 회귀 검사 | 다섯 Prefab의 선수·킥 플레이트·골대 Material 경로·정확한 RGB, HUD Label·Class, Tag, 센서 순서 통과 | `RedAndNavyVisualIdentityIsConsistentAcrossEveryActiveStadiumAndHud` |
| 훈련 Profile Validate | 6개 Profile, 학습 가능 5개 통과. Base fallback Navy의 Trainer·ONNX 강제 차단 확인 | `Logs/Soccer-Training-Validate.log` |
| 훈련 변경 포함 전체 EditMode | `64/64` 통과 | `Logs/Soccer-Training-EditMode.xml` |
| 훈련 변경 후 Soccer PlayMode | `18/18` 통과 | `Logs/Soccer-Training-PlayMode.xml` |
| 공유 Windows 훈련 Build | 성공, 7개 Scene, 143,098,493 bytes, 오류 0 | `Logs/Soccer-Training-Build.log`, `Builds/SoccerTraining/build-info.json` |
| Build Profile headless routing | `base-fallback`, `base-selfplay`, `attack`, `defense`, `press`, `rule-eval` 모두 통과 | `Logs/Soccer-Profile-Smoke/*.log` |
| Trainer communicator smoke | 최종 Build에서 Unity package `4.0.3`, communication `1.5.0`, `Soccer4v4_Base?team=0` 연결 후 7,680 step에서 수동 중단 | `Logs/Soccer-Training-Smoke-Results/BuildSmokeFinal-20260831-1646` |

공유 Build의 실행 파일 SHA-256은 `d3db57b9b88a2d8fedd1621461ddd7b29ed18001efd29a93ef9f31d4e8a483cd`이며 `build-info.json`과 실제 파일이 일치한다. Build warning 485개는 Unity AI Inference(Sentis) package의 D3D11 Shader compile warning이고 C# 또는 Player Build 오류는 0개다. Communicator smoke가 만든 ONNX는 연결·Step 증가 확인을 위한 중단 산출물이므로 성능 평가나 TeamDefinition 등록에 사용하지 않는다.

EditMode는 다섯 Stadium Prefab 모두에서 승인된 도로·조경·도시 소품이 남지 않고 새 Red·Navy 몸통 재질이 공통 연결되는지 검사한다. PlayMode는 다섯 Scene을 차례로 열어 팀별 TeamDefinition, 학습 플래그, 선수 8명, 전후방 센서, HUD, Goal Occlusion Fader, Stadium Geometry와 Kick 값을 검증한다. 골대 가림 검사는 골문 밖 선수와 열린 입구를 통해 보이는 골문 안 선수에게는 불투명을 유지하고, 골문 안 선수가 실제 골대 Collider에 가려질 때만 Red·Navy RGB를 유지하며 Alpha를 `0.25`로 바꾼 뒤 원래 Material로 복구하는 것까지 확인한다.

프로젝트 전체 PlayMode를 필터 없이 실행한 결과는 Soccer 18개가 모두 통과하고 별도 `MachineLearning.Escape` 테스트 3개가 실패했다. Stadium 검증 결과와 혼동하지 않도록 Soccer 어셈블리 범위를 다시 실행해 위의 `18/18`을 확정했다. Escape 실패는 `Logs/Stadium-AllTeams-PlayMode-AfterRebuild.xml`에 남겨 두었으며 이번 Soccer 범위에서는 수정하지 않았다.

기준선 감사에서 공급자 원본 `Assets/Hayq Art/GrantStadium` 파일 누락이나 변경은 없었다. Unity가 검증 중 `Scripting Define` 순서를 바꾼 것은 기준선 순서로 복구했으며, `SENTIS_ANALYTICS_ENABLED`와 `APP_UI_EDITOR_ONLY`를 모두 유지했다. `ProjectSettings.asset`의 의도된 차이는 Stadium 기본 Scene 지정이다. `EditorBuildSettings.asset`의 의도된 차이는 다섯 Stadium Scene으로의 교체다.

## 현재 알려진 제약

- 입력 shape는 379로 유지했지만 경기장 크기, 골대 폭, 공 크기, Kick과 경계 물리가 달라졌다. 기존 ONNX의 텐서 호환 가능성만으로 플레이 품질을 보장할 수 없으므로 새 Stadium에서 재학습·재평가해야 한다.
- Windows 실행 파일, 6개 Profile routing, Trainer 연결과 Step 증가는 확인했지만 `--num-envs 8/16`의 실제 처리량·메모리 Benchmark와 500,000-step 장기 학습은 아직 수행하지 않았다.
- Attack·Defense·Press는 승인 Base v2 Model을 `BaseTeamDefinition`에 등록하고 Windows Build를 다시 만들기 전까지 Launcher가 시작을 차단한다. 의도적인 fallback 상대 실험만 `-AllowFallbackOpponent`를 사용한다.
- 자동 테스트는 장시간 Human 조작감, 프레임 성능, 실제 Controller, 학습 수렴과 경기 재미를 대신하지 않는다.
- 기존 `Soccer4v4_*` Scene과 `SoccerEnvironment_*` Prefab은 삭제하지 않았다. 활성 경로에서 제외된 이력 자산이며 별도 정리 승인이 있기 전까지 보존한다.
- `SoccerFieldTwos.prefab`의 과거 시연 기록 Component는 활성 Stadium 경로와 무관하며 삭제하지 않았다.

## 다음 작업 순서

1. 한 Profile에서 Worker 8과 16의 Step/s, CPU, GPU, memory를 같은 Seed로 짧게 비교해 기본 Worker 수를 정한다.
2. Base fallback과 Base Self-Play를 서로 다른 Run ID로 500,000 step 학습하고 승률, Goal 빈도, Pass·Shot·Strong Kick 비율, 공 정체·모서리 탈출을 비교한다.
3. 선정한 Base 정책을 공통 상대 정책으로 고정한 뒤 Attack, Defense, Press를 같은 조건에서 각각 재학습한다.
4. Rule 환경과 학습형 팀을 장시간 수동 경기로 비교하고 Goal Occlusion, Human 조작감, UI, 프레임 성능을 확인한다.
5. 새 Stadium 기준 모델을 승인한 뒤 TeamDefinition에 등록하고 다섯 Scene의 실제 Tensor·Model 일치를 다시 검증한다.

학습 절차는 [학습 운용](training/overview.md), 담당 경계는 [학습형 팀 가이드](training/learning-teams.md)를 사용한다. 이번 작업에서는 commit·push와 500,000-step 장기 학습은 수행하지 않았고, Windows Build와 final communicator smoke까지만 수행했다.
