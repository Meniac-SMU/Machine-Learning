> 보관일: 2026-09-24. 작성 당시의 계획·판정·승인 이력이며 현재 실행 지시가 아닙니다. 현행 기준: [현재 상태](../../../soccer/current-status.md).

# MS0 사용자 확인 체크리스트

> 2026-09-20 사용자가 MS0 확인 완료를 선언했다. 아래 항목은 완료 당시의 사람 확인 기준으로 보존한다.

MS0 자동 gate는 완료되었다. 사용자는 강한 정책의 경기력을 평가할 필요가 없고, 다음 MS1 학습 전에 **씬 구성과 화면상 동작이 의도한 형태인지**만 확인하면 된다. smoke ONNX는 연결 검사용이므로 승패·전술 수준을 합격 기준으로 삼지 않는다.

## 필수 확인 4개

- [ ] **R0 상대 구성이 맞다.** Unity에서 `Assets/_Soccer/Manager/Curriculum/MS_ManagerSimple/Scenes/MNG_MS_Train.unity`를 열었을 때 Red에는 PPO 감독 하나, Navy에는 규칙형 감독 하나만 활성화되어 있다. `MNG_MSController`는 `R0Opponent`, 상대 Profile은 `Full`, episode는 20초, time scale은 10이다.
- [ ] **평가 씬이 정상 속도다.** `MNG_MS_Evaluation.unity`의 상대는 Full R0이고 경기 길이는 300초, time scale은 1이다. 빠른 학습 씬의 10배속이 사람용 평가 씬에 남아 있지 않아야 한다.
- [ ] **self-play 제어권이 겹치지 않는다.** `MNG_MS_SelfPlay.unity`에는 같은 `MNG_Manager` BehaviorName의 PPO 감독 두 개가 TeamId 0과 1로 활성화되어 있고 Rule/Fallback/Human 감독은 비활성화되어 있다.
- [ ] **화면상 축구 기술이 깨지지 않았다.** trainer 또는 추론 모델을 연결해 잠깐 보았을 때 선수들이 순간이동하거나 멈춘 채 쌓이지 않고, 공 추격·회전·드리블·패스/슛 접촉이 기존 코드 기술층을 통해 일어난다. Unity Console에 새 exception이 없어야 한다.

## 판단할 필요가 없는 것

- smoke 정책이 R0를 이기는지
- 여섯 명령을 균등하게 쓰는지
- self-play에서 무승부가 많은지
- 공격·수비 상황 성공률이 높은지

이 항목들은 MS0의 목적이 아니다. 공격·수비 판단은 MS1에서, Full R0 상대 강도는 MS2에서, 정책 간 강도 향상은 MS3에서 각각 고정 평가한다.

## 확인 결과를 남길 때

다음 세션에 아래 네 줄만 전달하면 충분하다.

```text
MS0 사용자 확인
- R0 학습 씬 구성: 합격/문제 있음
- 1배속 평가 씬: 합격/문제 있음
- self-play 양 팀 제어권: 합격/문제 있음
- 화면상 이동·공 기술·Console: 합격/문제 있음
```

문제가 있으면 Scene 이름, 어느 팀, 대략적인 경기 시각, Console의 첫 exception만 기록한다. 기존 Run을 삭제하거나 새 학습을 시작하지 않고 해당 연결만 재현한다.
