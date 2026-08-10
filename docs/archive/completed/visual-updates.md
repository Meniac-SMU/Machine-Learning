# Soccer Camera·외형·Material 완료 기록

> 상태: 보관 — 구현 완료  
> 현행 계약: [경기 계약의 HUD와 Camera](../../soccer/gameplay-contract.md#hud와-camera)  
> 이 문서의 값은 변경 당시 기록이며 현재 구현은 활성 계약과 코드를 우선한다.

## 2026-08-09 — 골대 가림 조건부 반투명

- Main Camera에 `SoccerGoalOcclusionFader`를 추가했다.
- 화면 안 선수 중심까지의 선분보다 앞에서 골대 Renderer Bounds가 교차할 때만 해당 골대 그룹을 반투명 처리한다.
- 골대는 선수와 공유하지 않는 `GoalBlue`, `GoalPurple`, `GoalNetBlack`, `GoalNetWhite` 전용 머티리얼을 사용한다.
- 가림 중 Alpha는 `0.25`, 전환 속도는 `8`이며 가림이 끝나면 원본 불투명 머티리얼과 Shadow 설정을 복구한다.
- 선수 머티리얼, 골대 Collider, `blueGoal`·`purpleGoal` Tag와 득점 판정은 변경하지 않는다.
- 일반·Base·Attack·Defense·Press·Rule 여섯 활성 4v4 씬에 같은 설정을 적용했다.
- Builder와 폴더 이동 전 PlayMode 기준은 통과했다. 2026-08-10에 Hub를 닫고 direct Editor batch compile도 성공했으며, 새 경로의 전체 PlayMode 재실행과 실제 화면 확인은 남아 있다.

## 2026-08-09 — AI 관전 카메라 거리 조정

- AI Overview 위치를 `(0,94,-90)`에서 `(0,82,-78)`로 변경했다.
- Field of View를 `52`에서 `50`으로 변경했다.
- Human 추적 거리 `7.5`와 추적 높이 `3.4`는 유지했다.
- AI 기본 시작 후 가까운 Overview 위치, Human 전환 후 선수 추적, AI 복귀를 PlayMode 테스트로 확인했다.

## 2026-07-18 — 초기 카메라와 외형

- 기준일: 2026-07-18
- `SoccerField4v4` 생성 과정에서 모든 `Headband` 자식 오브젝트를 제거한다. 생성기 검증도 헤어밴드가 하나라도 남으면 실패한다.
- 기본 Human 모드에서는 Blue 1번 선수의 진행 방향 뒤쪽과 위쪽을 따라가는 3인칭 카메라를 사용한다.
- H 키 또는 Xbox Y로 AI 모드로 전환하면 기존의 경기 전체 조망 카메라 위치, 회전, FOV로 부드럽게 복귀한다.
- 다시 Human 모드로 전환하면 같은 메인 카메라가 3인칭 추적으로 돌아간다. 별도 카메라를 활성화/비활성화하지 않아 오디오 리스너와 HUD가 중복되지 않는다.
- Unity 생성기 검증 통과, PlayMode 2/2 통과.

## 2026-07-18 — Material 복구

- 당시 원본 Soccer Prefab이 외부 Example의 `AgentBlue`, `AgentPurple`, `Eye`, `GrayMiddle`, `Black`, `Net` GUID를 참조했지만 초기 복제에 Asset이 빠져 있었다.
- Material 여섯 개와 `.meta`를 함께 복제해 GUID를 보존했다.
- 현재 위치는 `Assets/_Soccer/Materials`다.
- Builder가 URP Lit 설정과 선수, 벽, Goal frame, Net, 유리벽 연결을 명시적으로 복구한다.
- Renderer에 유효한 Material이 하나 이상 있는지 Builder가 검증한다.
- 당시 PlayMode 2/2에서 Missing·빈 Material이 없음을 확인했다.
