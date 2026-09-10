# MNG Manager 개발 인계 — 2026-09-10 13:57 KST

## 13:57 골키퍼 시선·경합 정체 해소·관전 카메라 복구 — 아래 모든 과거 기록보다 우선

- 골키퍼 이동은 목표 위치와 바라보는 위치를 분리했다. `MNG_PlayerMotor`의 새 facing-target 이동 경로를 사용해 Keeper가 전진·후진하는 동안 항상 현재 공을 향한다. 실제 PlayMode에서 공 반대 방향으로 후퇴하면서 공 방향 dot>0.85를 확인했다.
- 정면 경합 정체는 양 팀의 가장 가까운 경합자1명씩을 찾고, 공이0.55m 안에0.60초 머물면 `MNG_ContestedBallEscape`를 발동한다. 첫 대칭 측면 이동안은300초 동안168회 발동했는데도 공 최대 이동0.72m라 폐기했다. 최종안은1.40초 주기마다 Red/Navy 중 한 팀을 교대로 공을 빼는 주체로 정하고 상대는 공에서 물러나게 한다. 직접 순간이동·강제소유·직접 공 impulse는 없으며 실제 킥 플레이트와 motor만 사용한다.
- 최종 경합안의300초 Fallback 재현 결과는 `1:1`, 소유 전환81회, 탈출3회, 공 최대 이동61.71m다(`Logs/MNG-Contest-WinnerYield-PlayMode.xml`). 기존 중앙 교착 재발을 막기 위해 전체 경기 PlayMode에 공 최대 이동5m 초과 gate를 추가했다. 정면 배치에서 서로 반대 측면 명령이 나오는 단일 검사는 `Logs/MNG-Contest-Target-Retest-PlayMode.xml` 1/1 통과다.
- 기존 비활성 Main Camera/AgentCamera9개를 생성기에서 제거하고, 활성 `MainCamera` 하나와 `MNG_SpectatorCamera`를 MNG 프리팹에 생성한다. 기본은 `(0,100,-100)` 중계 시점, H로 Red Striker를 Human 전환하면 선수 뒤를 추적하고 AI 복귀 시 중계 시점으로 즉시 돌아간다. 단일 전환 검사는 `Logs/MNG-Camera-Retest-PlayMode.xml` 1/1 통과다.
- Builder 생성/검증은 `Logs/MNG-Camera-Contest-Generate.log`에서 통과했고, 전체 EditMode는188/188 통과했다(`Logs/MNG-Camera-Contest-EditMode.xml`). 최종 제품 코드가 포함된 전체 PlayMode r3/r4는 각각19/20이며 실패1건은 제품 assertion이 아니라 Trainer 없는 학습 Scene의 첫 FixedUpdate에서 의도된 `MNG diagnostic heuristic is disabled`가 테스트보다 먼저 실행된 순서 경합이다. 제품 대상 골키퍼·카메라·경합·300초 경기 검사는 모두 통과했다.
- 중단 직전 모든 정책 Scene 검사에서 ML-Agents Academy 자동 스텝을 장면 로드 전에 끄고 Manager 비활성화 뒤 복구하도록 테스트 격리를 공통 수정했다. 이 마지막 테스트 전용 수정은 사용자의 중단 요청에 따라 아직 Unity 재컴파일/전체20개 재실행을 하지 않았다. 다음 재개 첫 작업은 새 log/XML로 MNG 전체 PlayMode20/20을 확인하는 것이다.
- 테스트가 제거한 Standalone define은 `APP_UI_EDITOR_ONLY;SENTIS_ANALYTICS_ENABLED`로 복구됐다. 새 학습, 기존 Run 재개/덮어쓰기, commit, push, reset credit 사용은 하지 않았다. 기존 M1/M2/M1B 승인 모델은 보존되며, 실행 의미가 바뀌었으므로 다음 단계 전에 frozen 재평가가 필요하다.

### 정확한 다음 재개 순서

1. 5시간 잔여가8%를 초과하는지 확인한다.
2. 컴파일 후 MNG 전체 PlayMode를 새 r5 XML/log로 실행해20/20과300초 공 최대 이동5m 초과를 확인한다. Academy 순서 경합 예외가0인지 확인한다.
3. ProjectSettings/EditorBuildSettings/Core Prefab SHA와 Standalone define을 다시 확인한다.
4. 승인 frozen M1, M2, M1B를 변경된 저수준 실행 계약으로 새 evidence revision에 재평가한다. 기존 승인 근거와 모델은 덮어쓰지 않는다.
5. 세 frozen gate가 유지될 때만 아직 미승격인 M3 r001의300초×40경기 정식 평가를 실행한다. 실패 시 gate를 완화하지 않고 새 r002 원인을 분리한다.

## 12:20 역할별 이동·킥오프 스폰·M1B 이동 수비 승인 — 아래 모든 과거 기록보다 우선

- 포메이션 스폰 오차는 최초 Scene 배치와 득점 뒤 중앙 킥오프 `ResetRound`에서만 적용한다. 경기 중 `FixedUpdate`에는 Transform 보정이 없으며 `SpawnPlacementRevision` 회귀가 일반 플레이30 tick 동안 불변, 득점 뒤 정확히1회 증가, 이후 다시 불변임을 확인한다. 상황형 M1/M2/M3는 각 episode 시작 배치에만 오차를 적용한다.
- Manager 관측133/행동[6] 계약은 유지하고 `MNG_TeamPlanner`를 v2로 올렸다. Keeper/MidLeft/MidRight/Striker의 formation anchor·공 추종 비율·lane/depth를 분리했고, 수비 시 가장 가까운 비골키퍼 한 명만 primary presser로 정한다. 담당은1.25초 유지하고 다른 선수가3m 이상 유리할 때만 교체한다. Move/Cover/Receive 목표는 최소3m 분리한다.
- 이 실행 의미 변경에 따라 M1/M2/M3 protocol v3를 새로 만들고 v1/v2는 보존했다. 새 random-valid 기준선은 M1 `66득점/자책0/timeout34`, M2 `빠른회수52/전체57/실점45/timeout55`다. v3 gate는 M1 요구76·자책0, M2 빠른회수60·최대실점44다.
- 기존 frozen M1은 v3에서 `99득점/자책0/timeout1`로 통과했다(`Logs/MNG_M1Attack-20260910-r001-eval-r005`). 기존 frozen M2는 `빠른회수62/전체64/실점35/timeout65`로 통과했다(`Logs/MNG_M2Defense-20260910-r001-eval-r003`). 각 ONNX SHA는 기존 승인 모델과 동일하다. M1 결과는 여전히 정지 Navy 범위다.
- 기존 M1A를 덮어쓰지 않고 `M1_AttackMoving` Scene/Player/YAML/평가 protocol을 추가했다. Navy `MNG_FallbackManager`와 네 실행기를 활성화하며 PlayMode에서 Navy 선수의 실제 위치 이동을 확인한다. moving-defense random-valid 기준선은 `20득점/자책0/timeout80`, 승격 gate는 `30득점/자책0`이다.
- `MNG_M1Moving-20260910-r001`은 승인 M1 r001에서 initialize-from해 `20,006` step을 정상 완료했다. 최종 ONNX SHA-256은 `FAF9EAE392C604042C14787C0548E300594271BF8BF5BC85CE44E87FC3A6C8D5`; checker 통과, 최종 최근5 summary 평균 보상은 약0.294이며 학습 완료 자체와 품질 판단을 분리했다.
- M1B frozen validation seed21001의100개 미러 상황은 `33득점/자책0/timeout67`로 gate를 통과했다(`Logs/MNG_M1Moving-20260910-r001-eval-r001`). build manifest는 model/protocol/exe/level0 SHA와 `movingNavyDefense=true`를 고정한다. Unity 눈검사용 승인 Scene은 `Curriculum/M1_AttackMoving/Scenes/MNG_M1_AttackMoving_Evaluation.unity`, 모델은 `Models/MNG_M1Moving-20260910-r001.onnx`다.
- 최종 회귀는 EditMode `40/40`(`Logs/MNG-M1Moving-EditMode.xml`)와 PlayMode `17/17`(`Logs/MNG-M1Moving-PlayMode-r2.xml`)이다. 첫 전체 PlayMode는 새 배선 검사에서 Red Agent를 정리하지 않아 다음 테스트에 Heuristic 예외가 누적되어 `7/17`이었고 실패 로그를 보존했다. 테스트 격리를 수정한 r2만 최종 근거다.
- MNG 학습 진행도 검사를 위해 `Tools/inspect_soccer_training.py`가 단일 TensorBoard behavior 폴더를 자동 인식하도록 확장했다. MNG에는 Soccer L1/L2 gate를 적용하지 않고 `unconfigured-behavior/promotion_decision=false`로 표시한다. 시작·종료 증거는 `Logs/MNG_M1Moving-20260910-r001-startup.json`, `...-final.json`이다.
- 최종 5시간 사용량은 `91% 사용 / 9% 잔여`로 사용자 중단선(잔여8% 이하) 바로 위다. 요청 범위가 완료되어 새 장기 작업은 시작하지 않았고 reset credit은 사용하지 않았다.

## 11:05 M1 Navy·스폰·개별 이동 감사와 v2 재평가 — 아래 모든 과거 기록보다 우선

- 사용자 검토를 위해 임시 변경됐던 Catalog의 `m1TimeScale`과 `m2TimeScale`을 모두 `20`으로 복구했다.
- M1 Navy 정지는 로컬 실행 오류가 아니다. `MNG_CurriculumController.Start()`는 M1에서 Navy Fallback을 끄고 Navy 4명의 `MNG_PlayerSkillExecutor`를 모두 비활성화한다. 따라서 승인 M1 r001은 정지 수비수를 상대로 학습·평가됐다. 계획의 “정지/느린 수비” 중 느린 수비 변형은 구현되지 않았고, M1 결과는 움직이는 상대에 대한 공격력을 증명하지 않는다. M2에서는 Navy Fallback이 활성화된다.
- 포메이션 기준 위치에 축별 최대 `±0.75m`의 결정적 스폰 오프셋을 추가했다. 일반 MNG 새 경기와 M1/M2/M3 상황 생성에 적용되며, 같은 seed/상황은 재현되고 좌우 평가쌍은 X가 같고 Y가 반전된다. 확정 소유자는 킥 플레이트 드리블 앵커를 유지한다. 생성기 버전은 모두 v2이며 v1 protocol/evidence는 삭제하지 않았다.
- v2 독립 무작위 기준선은 M1 `58득점/자책0/timeout42`, M2 `빠른회수66/전체회수78/실점50/timeout50`이다. v2 gate는 M1 요구득점70, M2 빠른회수60·최대실점49로 고정했다.
- 기존 frozen M1 ONNX를 v2에서 재평가한 결과 `87득점/자책0/timeout13`, 통과했다(`Logs/MNG_M1Attack-20260910-r001-eval-r004`). 이 결과도 정지 Navy 조건이다. 기존 frozen M2 ONNX는 `빠른회수71/전체회수73/실점40/Red득점2/timeout58`, 통과했다(`Logs/MNG_M2Defense-20260910-r001-eval-r002`). 모델 SHA는 v1 승인 때와 동일하다.
- 회귀는 EditMode `37/37`(`Logs/MNG-SpawnJitter-V2-EditMode.xml`)와 PlayMode `14/14`(`Logs/MNG-SpawnJitter-PlayMode.xml`)가 통과했다. Player build 오류0, 기존 Sentis warning485다.
- 개별 움직임의 실제 주체는 신경망이 아니라 `MNG_TeamPlanner`다. `MNG_ManagerAgent`는6개 팀 명령 중 하나만 고르고, `MNG_MatchController.DispatchTeamPlan()`이 네 슬롯 task를 만든 뒤 `MNG_PlayerSkillExecutor`가 목표점으로 이동한다. 비소유 선수 목표가 대부분 `ball + 동일 depth + slot별 고정 lane`이라 공이 움직이면 여러 목표가 같은 벡터로 이동하고, 동일 motor가 같은 방식으로 추종해 평행·동기 이동처럼 보인다. 현재는 역할별 home zone, 동적 역할 배정 비용, 임무 유지 hysteresis, 동료 분리, 경로 회피가 없다.

### 권장 개별 이동 개선 순서

1. Manager 관측133/행동[6]은 유지하고 저수준 플래너만 개선한다. 신경망이 선수별 조이스틱8개를 직접 출력하도록 계약을 확대하지 않는다.
2. 각 팀에 역할별 formation anchor를 두고 공 위치만 따르는 비율을 역할·소유 상태별로 다르게 한다. Keeper는 골문, 두 Midfielder는 서로 다른 half-space, Striker는 전방/후방 연계 영역을 기준으로 한다.
3. 매 결정마다 carrier, primary presser, cover, outlet/receiver를 별도 배정한다. 현재 위치에서 후보 목표까지의 거리·역할 적합도·상대 위협을 비용으로 쓰고, 작은 차이로 담당자가 계속 바뀌지 않도록 hysteresis를 둔다.
4. 비담당 선수는 공으로 직접 평행 이동하지 않고 자신의 anchor와 공 기반 support target을 혼합한다. 동료가 가까우면 target을 분리하고, 도착 반경·속도 계수를 역할별로 달리한다.
5. task/target/slot trace와 최소 동료 거리, 동일방향 동시이동 비율을 추가한 뒤 정적 단위검사→MNG PlayMode→frozen M1/M2/M3 재평가 순서로 검증한다. 실행 의미가 바뀌므로 기존 ONNX 호환만으로 성능 유지라고 간주하지 않는다.
6. M1의 움직이는 공격 상대는 기존 M1A를 덮어쓰지 않고 별도 M1B curriculum/Run으로 추가한다. 정지→감속 Fallback→정상 Fallback 순으로 난도를 올린 뒤 새 moving-defense 평가를 통과해야 “움직이는 수비 상대 공격”으로 승인한다.

## 05:15 M3 r001 학습 완료·정식 평가 준비 — 아래 모든 과거 기록보다 우선

- 사용자 확인으로 M0 사람 검토를 합격 처리했고, 이미 승격된 M1/M2 뒤의 M3 개발을 진행했다.
- M3는 동일 관측133/행동[6]/결정0.5초 계약으로 Red PPO 1개와 Navy `MNG_Fallback` 1개만 활성화한다. 고정 seed의 kickoff/Red 소유/Navy 소유/중립 경합을 좌우 대칭으로 섞는다. 60초 수집은 `EpisodeInterrupted`, 300초 경기는 승패 terminal이며, 득점은 경기 종료가 아니라3초 GoalPause 후 계속된다.
- `MNG_M3Fallback-20260910-r001`은 승인 M2 PT에서 initialize-from하여 `100,080` step을 정상 완료했다. 최종 frozen ONNX SHA-256은 `FE69ECD1893994D57C8D008C5AEF7B087A0FC6531682660F099504729C3A4B1E`, 최종 PT SHA-256은 `962DC8BF8BC29E8307DA579A481589351B86D5E8716EA40B049C7FAD5C2B298E`이다. 중간 PT/ONNX 39,960/59,880/79,920/99,960 및 최종100,080을 기존 Run 안에 보존했다.
- 학습 로그의 최근 구간 평균 보상은 대부분0이고 간헐적으로 `-0.125`였다. 정상 완료와 정책 품질은 별개이므로 M3는 아직 승격하지 않는다. 이 r001은 삭제하거나 덮어쓰지 않고 frozen 평가 대상으로만 사용한다.
- 정식 M3 평가 코드를 추가했다. validation seed23001, 300초×40경기, frozen Red `InferenceOnly` 대 Navy Fallback이며, gate는 scoreRate>=0.50, 무득점경기<=8/40(20%), 양 팀 실제 득점>=1이다. 모델/protocol/exe/level0 SHA를 manifest에 고정하고 새 evidence revision만 허용한다.
- 평가 계약 EditMode는 `36/36` 통과했다(`Logs/MNG-M3-Eval-EditMode.xml`). 정식 평가 Player 빌드와40경기 실행은 아직 시작하지 않았다.
- 05:15 KST에 5시간 사용량 `94% 사용 / 6% 잔여`를 확인해 사용자 중단선(잔여8% 이하)에 도달했다. 학습은 이미 완료·저장됐고 새 장기 평가를 시작하지 않았다. 마무리 후 최종 확인은 `99% 사용 / 1% 잔여`다. reset credit 1개는 사용하지 않았다.

### 정확한 다음 재개 순서

1. 5시간 잔여가8%를 초과하는지 확인한다. 이하이면 새 Unity 실행이나 평가를 시작하지 않는다.
2. 설정 기준 SHA와 잔류 프로세스를 재확인한다. ProjectSettings/EditorBuildSettings/Core Prefab 기준은 각각 `54FE0BA0...D49D85` / `AE8E2777...68A104` / `A88CA3D0...322B46`, Standalone define은 `APP_UI_EDITOR_ONLY;SENTIS_ANALYTICS_ENABLED`다.
3. 다음 명령으로 새 평가 증거를 만든다: `& '.\Tools\MNG_EvaluateM3.ps1' -RunId 'MNG_M3Fallback-20260910-r001' -EvidenceId 'MNG_M3Fallback-20260910-r001-eval-r001'`.
4. 정확히40경기 결과와 Player 로그 오류0, frozen ONNX/build manifest SHA 일치를 확인한다. scoreRate·무득점·양팀득점 gate를 모두 통과할 때만 M3를 승격한다.
5. 실패하면 r001을 보존하고 보상 원장을 먼저 감사한다. 특히 계획서의 유효슛/실제패스수신/전진5m/실제회수 shaping이 M3 60초 수집에 실제 지급되었는지 확인한 뒤, 단일 원인 수정으로 `MNG_M3Fallback-20260910-r002`를 새로 학습한다. gate를 사후 완화하지 않는다.
6. 정식40경기와 별도로 D2 최소 시연(양 팀 실제 감독 ONNX 10 seed쌍×60초, 각 팀 득점>=1, 득점 경기>=8/10, 사건 trace) 및 최소1개300초 시연은 M3 최종 승격 근거에 추가한다.

## 04:35 M1/M2 승격 완료 — 아래 모든 과거 기록보다 우선

- 사용자가 M0의 실제 화면·킥 플레이트·조작감·HUD/H키 검토를 모두 합격으로 확인했으므로 M0를 마감했다.
- M1 `MNG_M1Attack-20260910-r001`은 단일 Red PPO로20,007 step을 완료했다. frozen ONNX SHA는 `3B1D69BBE79E6E458D4F2117B0D51E20D6D38FFCC2C519C0314E70580685DCCB`이며, validation seed21001의 고정100상황에서92득점·자책0·timeout8로 요구75득점을 통과했다. 근거는 `Logs/MNG_M1Attack-20260910-r001-eval-r003/`이다.
- M2 무작위 유효명령 사전 기준선은 seed22001에서10초 내 회수66·전체회수77·실점55다. `MNG_M2Defense-20260910-r001`은 승인 M1 PT에서 initialize-from하여20,004 step을 완료했고, frozen ONNX SHA `B60306FE77C060BD770B1D2512CC5782A169571B62C9D2015093D7310CB73000`으로10초 내 회수63·전체회수68·실점29·timeout71을 기록했다. 최소회수60과 무작위보다 적은 실점(최대54)을 모두 통과했다. 근거는 `Logs/MNG_M2Defense-20260910-r001-eval-r001/`이다.
- 평가 Player는 Trainer 없이 `InferenceOnly`로 실행했고 모델·protocol·exe·level0 SHA를 build manifest에 고정했다. M1 평가 r001은 sandbox 초기화 중단, r002는 잘못된 MonoScript 직렬화로 level0 역직렬화 실패였으며 둘 다 보존했다. MonoBehaviour를 동일 이름 파일로 분리한 r003만 M1 합격 근거다.
- 최신 전체 회귀는 EditMode `34/34`(`Logs/MNG-M2-Final-EditMode.xml`)와 PlayMode `12/12`(`Logs/MNG-M2-Final-PlayMode.xml`)다. PlayMode에서 드리블 직선/좌/우 각20/20, 패스5/10/20m 각20/20, 슛10/20m 각20/20, 양 팀 탈취20/20을 재확인했다.
- 전체 묶음 회귀의 물리 기준선은 독립 실행과 소폭 달랐다(M1 득점64 대65, M2 빠른회수67 대66·실점52 대55). 사전 고정한 독립 기준선을 사후 변경하지 않았고 두 모델의 승격 여유는 이 변동보다 크다. 이는 Unity 물리의 실행 간 완전 비트 결정성을 증명하지 못한다는 제한으로 남긴다.
- ProjectSettings/EditorBuildSettings/Core Prefab은 테스트 후 Standalone define을 복구하면 기준 SHA와 일치한다. commit·push·force·기존 Run 덮어쓰기는 하지 않았다.

### 정확한 다음 순서

1. 5시간 잔여가8%를 초과하는지 확인한다.
2. M3_FallbackMatch를60초 혼합 시작 상태와 Navy Fallback 상대, 동일133/[6]/0.5초 계약으로 구현한다. 득점은 episode 종료가 아니라3초 GoalPause이고60초 수집 제한만 interrupted로 끝낸다.
3. 승인 M2 PT에서 새 M3 Run을 initialize-from하고100k 또는3시간 상한을 적용한다.
4. 고정10 seed쌍×60초 D2 평가와 최소1개300초 경기를 frozen ONNX로 실행한다. 정식40경기 scoreRate>=0.50은 별도 승격 gate다.

## 00:12 MNG 킥 플레이트 완료 상태 — 아래 모든 과거 중단 기록보다 우선

- 사용자 결정에 따라 기존 킥 플레이트 기믹을 제거하지 않고 MNG 전용 `MNG_KickPlate`로 분리했다. Builder는 기존 center+wing 3개 Collider 형상을 보존하고 legacy `SoccerKickPlate` 컴포넌트만 제거한다. 최종 MNG Prefab에는 `MNG_KickPlate` 8개, legacy plate controller 0개가 있다.
- 패스·슛 요청은 더 이상 공 Rigidbody에 직접 impulse를 주지 않는다. `TryKick`은 소유자 plate를 무장하고, plate가0.08초 전진해 자기 Collider로 공에 실제 접촉한 한 번만 token을 소비해 목표 출구속도 impulse를 준다. plate는0.50초에 걸쳐 수축하며 OnCollisionEnter/Stay 반복으로 중복 힘이 발생하지 않는다.
- plate 수축 위치 전방1.32m를 드리블 위치로 정의했다. 공이 반경0.72m에 놓이면 실제 충돌이 없어도1 fixed tick(0.02초) 후 소유가 된다. 전진 명령속도0.40m/s 이상·전방 정렬내적0.70 이상일 때만 제한된 가속/감쇠로 공이 plate 앞을 따르며, parenting·teleport는 없다.
- 정지·후진·옆걸음에서는 보정을 즉시 끄고0.08초 뒤 소유를 해제한다. 정지한 선수가 제자리에서 즉시 재획득하지 못하게 다시 전진하거나 공이1.02m release 범위를 벗어날 때까지 차단한다. 상대 plate의 드리블 zone은 유효 탈취 후보이며0.02초 확인, 이전 소유자 잠금0.12초로 쉽게 탈취되도록 했다.
- `MNG_PlayerAvatar`는 전용 plate와 cooldown reset을 소유하고, `MNG_PlayerMotor`는 마지막 명령속도를 기록한다. `MNG_BallControl`은 plate 접촉만 세고, 실제 strike·드리블 보정·정지 해제·탈취·킥 후 재흡착 차단을 단일 writer로 처리한다. 경기/round reset은 plate, drive, possession, cooldown을 함께 초기화한다.

### 최신 자동 검증

| 계층 | 최신 결과 | 근거 |
|---|---:|---|
| EditMode | `29/29` | `Logs/MNG-KickPlate-EditMode-r3.xml` |
| PlayMode 전체 | `8/8` | `Logs/MNG-KickPlate-PlayMode-r2.xml` |
| 실제 plate 패스 | 5m·10m·20m 각각 `20/20` | 전체 PlayMode의 `UnopposedPassesAtFiveTenAndTwentyMetersAreActuallyReceived` |
| 실제 plate 슛 | 10m·20m 각각 `20/20` | 전체 PlayMode의 `OpenGoalShotsFromTenAndTwentyMetersReachNinetyPercent` |
| plate 드리블 | 직선·좌회전·우회전 각각 `20/20`, 정지·후진 소유 해제 | `Logs/MNG-KickPlate-Dribble-PlayMode-r5.xml`, 전체 PlayMode 재확인 |
| 상대 탈취 | 전체 `20/20`, Red/Navy 각각 `10/10`, 모두0.2초 이내 | `Logs/MNG-KickPlate-Steal-PlayMode-r3.xml`, 전체 PlayMode 재확인 |
| Fallback 300초 | `15,001` fixed tick 완주, NaN·공 소실·중복득점0 | `Logs/MNG-Fallback300-PlayMode-r1.xml`, 전체 PlayMode 재확인 |
| Builder | 연속2회 validation/build/batch 통과 | `Logs/MNG-KickPlate-Generate-r3.log`, `Logs/MNG-KickPlate-Generate-r4.log` |
| Windows Player | Succeeded, 오류0, 기존 Sentis warning485, exe667,648 bytes | `Logs/MNG-KickPlate-Windows-Build-r1.log`, `Builds/MNG_Training/build-info.json` |

- 300초 Fallback 경기는 `0:0`, possession transition593회였다. 장기 물리/상태 안정성은 통과했지만 득점 능력·전술 품질·RL 성능은 증명하지 않는다.
- 최종 exe SHA는 `D3DB57B9B88A2D8FEDD1621461DDD7B29ED18001EFD29A93EF9F31D4E8A483CD`, level0 SHA는 `8692F853D61E65C77F204DEB76C736CC09E0A95D52FC3ABE4C9A868BEBF4656D`, trainer config SHA는 `8D8F7AD80D83AAB1B87F22E9F868B01D48193C656E34C9B6231CEEBDF6765C68`이다.
- 테스트가 바꾼 Standalone define은 `APP_UI_EDITOR_ONLY;SENTIS_ANALYTICS_ENABLED`로 복구했다. ProjectSettings/EditorBuildSettings/Core Prefab SHA는 각각 기준 `54FE0B...D49D85` / `AE8E27...68A104` / `A88CA3...22B46`과 일치한다.
- 진단 실패 XML/log를 삭제하거나 덮어쓰지 않았고 commit·push·force·기존 training Run 변경을 하지 않았다. r006/r007은 계속 PPO 연결 smoke일 뿐 성능 모델이 아니다.
- 00:14 KST 최종 점검에서 5시간 사용량은 `89% 사용 / 11% 잔여`로 사용자 중단선(잔여8% 이하) 전이다. reset credit 2개는 사용하지 않았으며 Unity/Trainer/MNG Player 잔류 프로세스는0이다. 다음 필수 gate가 사람의 실제 화면/입력 검토이므로 자동 작업은 이 인계 상태에서 멈춘다.

### 남은 M0 사람 검토와 다음 순서

1. Unity Editor 또는 최신 Windows Player에서 plate가 패스/슛 때 눈에 보이게 전진·수축하고, 공이 실제 plate에서 출발하며 중복 발사가 없는지 확인한다.
2. Red Striker를 H키로 AI→HUMAN→AI 각20회 전환한다. 전환 중 위치·점수·시간·공 상태가 보존되고, HUMAN 동안 나머지3명과 Navy는 계속 동작하는지 확인한다.
3. W/A/S/D 전진·좌우 회전으로 공이 plate 앞에서 자연스럽게 따라오는지, 손을 놓거나 S로 후진하면 공이 즉시 보정에서 풀리고 쉽게 빼앗기는지 확인한다. 정지 때 튀김·진동·흡착처럼 보이는 현상이 없어야 한다.
4. 소유 중·패스 비행 중·GoalPause 중 각각 H 전환을 확인하고, HUD의 `AI/HUMAN`, 점수, 시간, 양 팀 명령이 실제 상태와 일치하는지 확인한다. 키보드를 우선 확인하고 게임패드는 입력 경로가 마련된 뒤 별도 확인한다.
5. 위 사람 검토에 결함이 없으면 M0를 마감한다. M1 장기 성능 훈련/M2 self-play는 별도 gate이며 자동 plate 성공률이나 r007 연결 smoke를 RL 성능으로 승격하지 않는다.

재생성 명령의 실제 진입점은 `MachineLearning.Soccer.Manager.Editor.MNG_ProjectBuilder.BuildM0StadiumBatch`다. 과거 기록의 `BuildAllBatch` 표기는 잘못됐으므로 사용하지 않는다.

## 21:49 재개 후 최신 상태 — 아래 모든 과거 중단 기록보다 우선

- M0 열린 골문 gate의 원인을 분리했다. 선수 자식 Collider를 모두 비활성화하고 공 초기화 시간을 늘렸으며, 실제 damping에서20m에 못 미치던 강킥을24→28m/s로 확정했다. `Logs/MNG-speed28-PlayMode.xml`은10m `20/20`, 20m `20/20`, 전체 PlayMode `4/4` 통과다.
- 24방향×5/20/40m의72개 motor 접근 fixture를 추가했다. 도착 오차0.5m 이하·정지속도0.5m/s 이하·NaN0·필드이탈0 조건으로 최신 전체 EditMode `26/26`을 통과했다(`Logs/MNG-pass-fix-EditMode.xml`). 이는 순수 motor 수치 gate이며 실제 Rigidbody 충돌/화면 품질 증거는 아니다.
- 실제 Rigidbody 패스 수신 fixture를5/10/20m×20방향으로 추가했다. 다른 선수 Collider를 끄고 지정 수신자와 실제 충돌한 뒤 `MNG_BallControl.Carrier`가 그 선수가 된 경우만 성공으로 센다.
- 복합 선수 Collider는 실제 접촉해도 rigidbody 중심거리가 기본 bounds 획득 반경보다 컸다. 수신 가능 속도18m/s 이하의 **실제 충돌**이 먼저 있었을 때만0.24초 동안 그 충돌의 실측 중심거리를 인정하고 공 상대속도를 감쇠하도록 수정했다. 비접촉 원거리 소유권은 허용하지 않는다.
- 자동 패스는 약킥14m/s를 고정 사용하지 않고 거리별로10m 이하14m/s, 10~20m 14→28m/s 선형 보간, 20m 이상28m/s를 사용한다. 최종 단일 gate `Logs/MNG-pass-distance-speed-PlayMode.xml`은5m·10m·20m 모두 `20/20`, `1/1` 통과다.
- `ResetMatch()`가 점수/위치뿐 아니라 실제 possession ledger와 접촉 진단도 초기화하도록 연결했다. 반복 fixture 사이의 소유 상태 누수를 막는다.
- 최신 패스 코드까지 포함한 전체 EditMode는 `26/26` 통과했다. 다만 최신 전체 PlayMode(예상5개), Builder 재생성, Windows Player 재빌드는 아직 실행하지 않았다. 직전 전체 PlayMode `4/4`와 패스 단일 `1/1`은 서로 분리된 증거다.
- 현재 남은 M0 자동 gate는 실제8m 운반(직선/좌/우), 접촉 탈취/비접촉 강탈0/양팀 대칭, Fallback 대 Fallback300초 완주다. HUD/H 양방향 전환과 조작감은 실제 화면 검증으로 별도 남아 있다.
- 이번 재개 중 진단 실패 로그/XML을 삭제하거나 덮어쓰지 않았고 commit·push·force·기존 Run 변경을 하지 않았다. r006/r007은 계속 연결 smoke일 뿐 정책 성능 모델이 아니다.
- 21:50 KST 기준 5시간 사용량은 `93% 사용 / 7% 잔여`로 사용자 지정 중단선(잔여8% 이하)에 도달했다. 그래서 아래 재개 순서를 저장하고 추가 Unity 실행·빌드·훈련을 중단했다. reset credit 2개는 사용하지 않았다.
- 종료 점검에서 Unity/Trainer/MNG Player 잔류 프로세스는0이다. Standalone define은 `APP_UI_EDITOR_ONLY;SENTIS_ANALYTICS_ENABLED`로 복구했고, ProjectSettings/EditorBuildSettings/Core Prefab SHA는 각각 기존 기준 `54FE0B...D49D85` / `AE8E27...68A104` / `A88CA3...22B46`과 일치한다.

### 정확한 다음 재개 순서

1. 5시간 잔여가8%를 초과하는지 먼저 확인한다. 이하이면 실행하지 않는다.
2. 최신 전체 PlayMode를 새 XML/log로 실행해 `5/5`와 열린 골문10/20m·패스5/10/20m 각각 `20/20`을 확인한다.
3. `MNG_ProjectBuilder.BuildAllBatch`를 실행하고 M0/M1 Scene 계약 및 Builder 재실행 배율 누적0을 확인한다.
4. Windows training Player를 재빌드하고 오류0, exe/level0 SHA, ProjectSettings/EditorBuildSettings/Core Prefab 불변성을 다시 기록한다. Unity 테스트가 제거한 Standalone define은 `APP_UI_EDITOR_ONLY;SENTIS_ANALYTICS_ENABLED`로 복구한다.
5. 8m 운반 fixture와 접촉 탈취 fixture를 구현·통과시킨 뒤 Fallback 대 Fallback300초 자동 완주를 실행한다.
6. 실제 화면에서 HUD, H 양방향 전환, 소유중/패스비행/GoalPause, AI→Human writer0을 확인한다. 자동 수치와 실제 영상 판정을 섞지 않는다.
7. 위 M0 gate가 모두 통과하기 전에는 M1 성능 훈련이나 M2/self-play를 시작하지 않는다. 필요 시 r008은 최신 Player의 연결 회귀용 새 Run으로만 실행하고 기존 Run을 재개·덮어쓰지 않는다.

## 18:27 재개 후 최신 상태 — 아래 과거 중단 기록보다 우선

- 현재 티켓은 **M0 런타임 완성 및 M1-A PPO 연결**이다. M1-A 연결 gate는 `MNG_M1Smoke-20260909-r006`과 최신 `r007`로 통과했지만, 이는 정책 성능 승인이 아니다.
- 전술 목표 누락을 고쳐 `MNG_TacticalTargetResolver`가 신경망/규칙형에 동일한 예측 수신자와 실제 goal-width 기반 슛 대상을 제공한다. 정책 명령 자체는 선택하지 않는다.
- M0-B 누락이던 `MNG_HudPresenter`를 구현했다. 공통 Soccer UXML/PanelSettings를 재사용하고 점수·시간·HUMAN 상태·양 팀 현재 명령·누적 보상을 표시하며 legacy `SoccerHudController`는 남기지 않는다.
- 안정 통과 근거는 Builder 통과(`Logs/MNG-hud-Generate-r2.log`), EditMode `25/25`, 기본 PlayMode `3/3`, Windows Player 오류0/기존 Sentis warning485/exe667,648 bytes다. 기본 PlayMode는 양쪽 골로 실제 공을 발사해 득점1회·GoalPause·중앙 reset도 확인한다(`Logs/MNG-physical-goal-PlayMode-r2.xml`). 이후 추가한 열린 골문 성공률 gate는 아래와 같이 실패했으므로 최신 전체 PlayMode는 `3/4`이며 M0 기술 gate는 미통과다.
- r007은 단일 `MNG_Manager?team=0`, Bootstrap activeManagers1, 1,112 environment step, policy update2, optimizer update33, checkpoint2로 정상 종료했다. 관찰 누락·예외·reward profile 누락·missing script·kinematic·masked command 오류는 모두0이다.
- r007의 1,100 decision 집계에서 pass mask가22회 열리고 `PassBuild`가5회 선택됐다. shot mask는0회였다. 짧은 무학습 smoke가 슛 지역 도달이나 전술 품질을 증명하지 않으며, 실제 goal width 판정은 EditMode 회귀로만 확인했다.
- r007 root ONNX SHA `A6779CDA3D495839A1551CFF8FF0DEE361BF47B523BABA9EE01E3FD31A21760B`, checkpoint SHA `A232445432703F345FDC7F95C1ECAB116939F9D23F5CDF76A2C139782C1D2533`이다. 연결 smoke 모델이므로 성능 모델로 등록하지 않는다.
- 최종 Player exe SHA `D3DB57B9B88A2D8FEDD1621461DDD7B29ED18001EFD29A93EF9F31D4E8A483CD`, level0 SHA `15C144539EAA6D980B896ECA6CD20ACCB30824E84DBA75128B8C6816261D33EA`이다.
- `ProjectSettings.asset` SHA `54FE0BA053A6860458DBF2BDEBE716F5F7989E0DF55556519FB9237444D49D85`, `EditorBuildSettings.asset` SHA `AE8E277743DB8A6270B39BCDB61A7369BDD54D342E56FC9AD02D0DFA1368A104`, Core Prefab SHA `A88CA3D038348B0BE9B84E01FA8FFC007D2C9325366CB14212DA52C787322B46`로 기준과 같다.
- 현재 Trainer/MNG Player/Unity 잔류 프로세스는0이다. commit·push·force·기존 Run 덮어쓰기는 수행하지 않았다. reset credit도 사용하지 않았다.
- 마지막 사용량은 5시간96% 사용/4% 잔여였다. 사용자 지정 잔여8% 중단선에 도달해 추가 실행을 중단했다. reset credit은 사용하지 않았다.

### 마지막 M0 열린 골문 실패

- 신규 PlayMode `OpenGoalShotsFromTenAndTwentyMetersReachNinetyPercent`는 실제 Rigidbody 공을 좌우 골 방향과5개 횡 위치로 10m/20m 각각20회 발사한다.
- 첫 실행 `Logs/MNG-open-goal-PlayMode.xml`은 10m `16/20`으로90% gate 미달이었다.
- 선수 방해를 제거하려 모든 선수 Collider를 비활성화했지만, `Logs/MNG-open-goal-PlayMode-r2.xml`도 10m `16/20`, 전체 `3/4` 실패였다. 따라서 수비 방해가 원인은 아니다.
- 재개 시 gate를 완화하지 말고 실패 sign/lane/최종 위치·속도를 attempt별 trace로 기록해 goal surface/frame geometry 또는 ResetMatch/ball state 원인을 고친다. 그 전에는 20m 결과와 열린 골문 gate를 통과로 기록하지 않는다.

### 다음 작업 순서

1. 열린 골문 실패 attempt의 sign/lane/최종 위치·속도 trace를 추가하고 동일40샷을 새 XML/log로 재실행해 16/20 원인을 고친다.
2. 그 뒤 24방향×3거리 접근, 8m 운반, 접촉 탈취, 5/10/20m 패스 fixture를 구현한다. 자동 수치와 실제 영상 판정을 분리한다.
2. Fallback 대 Fallback 300초 완주, 양 팀 득점/회수 사례, 공소실·NaN·중복득점0을 확인한다.
3. 실제 화면에서 HUD, H 양방향 전환, 소유중/패스비행/GoalPause, AI→Human writer0을 확인한다.
4. 위 M0 gate 뒤에만 `MNG_CurriculumController`/Catalog와 `MNG_M1_AttackChoice` 20초 Scene/YAML/고정100 평가 도구를 구현한다.
5. M1은 새 Run·20k decision 또는1시간 상한이며 r006/r007을 초기 성능 모델로 채택하지 않는다. M2/self-play는 M1 승급 전 시작하지 않는다.

아래 내용은 94% 사용 시점의 과거 중단 기록으로 보존한다. 당시 r003 예정은 r003–r007 진단을 거치며 이미 대체되었다.

## 중단 이유와 현재 판정

- 2026-09-09 KST에 5시간 사용량 `94% 사용 / 6% 잔여`를 확인했다.
- 사용자 지정 중단선은 잔여 `8% 이하`이므로 새 구현·Unity 실행·Player Build·훈련을 중단했다.
- 무료 사용량 reset credit은 사용하지 않았다.
- 마지막 변경은 `MNG_TrainingBootstrap`의 활성 Manager 판정을 `manager.enabled`에서 `manager.isActiveAndEnabled`로 고친 것이다. 이 변경 이후의 컴파일·테스트·Player Build·Trainer 스모크는 아직 실행하지 않았다.
- 현재 Trainer, `MNG_Training` Player, Unity 프로세스는 남아 있지 않다.
- commit·push는 수행하지 않았다. 작업 전부터 존재한 다른 Soccer 변경과 과거 Run을 정리하거나 원복하지 않았다.

## 구현된 범위

MNG는 기존 379/[3,3,3,3] 선수 정책과 분리된 감독 정책이다.

- 관측: 팀 기준으로 정규화·미러링한 정확히 `133` floats.
- 행동: `Balanced`, `ProtectLead`, `HighPress`, `WideAttack`, `CentralAttack`, `CounterAttack`의 discrete `[6]`.
- 제어권: 팀당 Manager 1개, 선수 4명은 `MNG_PlayerMotor`/`MNG_PlayerSkillExecutor`; H키는 Red Striker만 Human으로 전환하고 Manager는 나머지 3명을 계속 지휘한다.
- 물리: Core 복사본에서 공 scale `×1.10`, mass `4.5`, bounce `0.05`; 공 parenting/teleport 없이 force/impulse를 사용한다.
- 경기: 300초, 득점 1회 집계, 3초 Goal Pause, reset, possession ledger, command/task revision, 보상 dedupe와 60초 cap.
- 상대: M0에서는 양 팀 explicit `MNG_FallbackManager`; M1 연결 씬에서는 Red PPO endpoint 1개와 Navy fallback.
- 생성: `MNG_ProjectBuilder`가 Core Stadium으로부터 MNG Prefab/M0 Scene/M1 Scene/Profile을 다시 만들고 legacy Agent/DecisionRequester/환경 writer가 남지 않는지 검사한다.
- 도구: `Tools/MNG_Build.ps1`, `Tools/MNG_Train.ps1`; Run 중복 방지와 config/exe/level0 SHA 기록을 포함한다.

주요 시작점:

- `Runtime/MNG_ManagerAgent.cs`
- `Runtime/MNG_MatchController.cs`
- `Runtime/MNG_ObservationWriter.cs`
- `Runtime/MNG_TeamPlanner.cs`
- `Runtime/MNG_PlayerMotor.cs`
- `Runtime/MNG_BallControl.cs`
- `Runtime/MNG_HumanInput.cs`
- `Runtime/MNG_RewardEngine.cs`
- `Editor/MNG_ProjectBuilder.cs`
- `Editor/MNG_TrainingBuildBuilder.cs`
- `Tests/EditMode/MNG_RuntimeContractTests.cs`
- `Tests/PlayMode/MNG_RuntimePlayModeTests.cs`
- `Training/MNG_M1_ConnectionSmoke.yaml`

## 검증 근거와 경계

마지막 Bootstrap 수정 전에 다음이 통과했다.

| 근거 | 결과 | 파일 |
|---|---:|---|
| MNG Builder validation | 통과 | `Logs/MNG-M1-SingleTeam-Scene-Build.log` |
| MNG EditMode | `23/23` | `Logs/MNG-M1-PreBuild-EditMode.xml` |
| MNG PlayMode | `2/2` | `Logs/MNG-M0-PlayMode.xml` |
| Windows training Player | 오류0, 기존 Sentis shader warning487, exe 667,648 bytes | `Logs/MNG-M1-SingleTeam-Windows-Build-Manifest.log` |
| YAML parser | `MNG_Manager`, max_steps1000 확인 | 설치된 ML-Agents `1.2.0.dev0` parser |

자동 통과는 수동 H키 조작감, 실제 Controller, 물리 체감, 장기 경기 안정성, 학습 수렴, 전술 품질을 증명하지 않는다. 특히 아래 r001/r002는 승인 모델이 아니다.

## 보존 Run 판정

### `MNG_M1Smoke-20260909-r001`

- 종료와 artifact는 보존한다.
- Red/Navy 두 Behavior가 모두 Trainer에 연결됐다.
- 의도한 단일 Red 학습 계약을 위반했으므로 실패 진단이며 resume·승급 금지다.

### `MNG_M1Smoke-20260909-r002`

- Trainer에서는 `MNG_Manager?team=0` 하나만 연결됐고 1,024 environment steps를 처리했다.
- `_update_policy` 2회, `TorchPPOOptimizer.update` 36회, 최종 1,024-step ONNX/PT export까지 수행됐다.
- 그러나 Player 로그에는 다음 오류가 있다.
  - `MNG connection smoke requires exactly one active manager, found 2` 1회.
  - `Fewer observations (0) made than vector observation size (133)` 1,025회.
  - `MNG reward profile for Navy is missing` 3회.
- 따라서 r002는 communicator/export 경로 진단일 뿐, 유효한 관측·행동·보상 스모크나 학습 성능 근거가 아니다. resume·force·모델 등록·승급 금지다.
- Bootstrap 예외의 직접 원인은 inactive Navy GameObject에 붙은 enabled Component까지 센 것이며 마지막 코드에서 `isActiveAndEnabled`로 수정했다. 0-observation과 Navy reward 예외가 이 초기화 예외의 연쇄 결과인지 여부는 r003 전 로그 검증으로 확인해야 한다.

## 불변 SHA-256 근거

| 대상 | SHA-256 |
|---|---|
| `Training/MNG_M1_ConnectionSmoke.yaml` | `8D8F7AD80D83AAB1B87F22E9F868B01D48193C656E34C9B6231CEEBDF6765C68` |
| `Builds/MNG_Training/MNG_Training.exe` | `D3DB57B9B88A2D8FEDD1621461DDD7B29ED18001EFD29ED02D0DFA1368A104` |
| `Builds/MNG_Training/MNG_Training_Data/level0` | `02B4BC4E35118AC3194D75536CA73742627C83885A36CB82D2739D173EA8F5B0` |
| r002 root ONNX | `6E6F9287FE01E495571B44EA85F460486941BEA1BE21B540C413603B6B5EDCA5` |
| r002 1,024-step PT | `1DFF12A85421F5EB9F8589F91BD2BDD5CF84FDC0A5009771E9F9F17A4230942E` |

주의: exe SHA는 이전 Soccer build와 같을 수 있어 단독으로 씬 변경을 식별하지 못한다. 실제 serialized scene을 포함한 `level0` SHA와 `Builds/MNG_Training/build-info.json`을 함께 사용한다.

## 정확한 재개 순서

1. 작업을 시작하기 전에 실제 5시간 사용량을 확인한다. 잔여8% 이하라면 아래 명령을 실행하지 않는다.
2. `MNG_TrainingBootstrap.cs`의 `isActiveAndEnabled` 변경을 포함해 MNG EditMode와 PlayMode를 다시 실행하고 XML의 total/passed/failed/result를 직접 확인한다.
3. `MNG_ProjectBuilder.BuildAllBatch`를 다시 실행하고 M0/M1 Scene 계약을 검증한다.
4. `Tools/MNG_Build.ps1`로 Windows Player를 새로 만들고 `ProjectSettings.asset`, `EditorBuildSettings.asset`, Standalone define을 기준값과 비교한다.
5. 새 Player를 Trainer 없이 한 번 실행해 Player 로그에 아래가 모두 맞는지 확인한다.
   - active Manager 정확히 1개.
   - `MNG_Manager?team=0`만 등록.
   - 0/133 observation padding 0회.
   - reward profile 누락 예외 0회.
   - missing script 경고 0회.
6. 위 네 조건이 모두 통과할 때만 새 Run ID `MNG_M1Smoke-20260909-r003`으로 max_steps1000 연결 스모크를 실행한다. r001/r002를 재개하거나 덮어쓰지 않는다.
7. r003에서 단일 brain, 133 관측, 유효 action mask, PPO update, 체크포인트/ONNX, Player/Trainer 오류0을 별개로 확인한다. 이것은 연결 검증이며 정책 품질 승인이 아니다.
8. r003 통과 뒤에만 M0 실제 경기/HUMAN 조작 검증과 문서의 다음 M1 gate를 진행한다. 장기 훈련과 M2 self-play는 그보다 앞서 시작하지 않는다.

## 설정 보존

- 마지막 확인 시 `ProjectSettings/ProjectSettings.asset`과 `ProjectSettings/EditorBuildSettings.asset`은 이번 작업 diff가 없다.
- MNG 테스트 중 Standalone define이 바뀌면 기록된 기준 `APP_UI_EDITOR_ONLY;SENTIS_ANALYTICS_ENABLED`로 복구하고 파일 diff/hash를 다시 확인한다.
- 기존 L0/L1/L2/L3 모델, 결과, 로그, 승인/미승인 판정은 MNG 결과와 섞지 않는다.
