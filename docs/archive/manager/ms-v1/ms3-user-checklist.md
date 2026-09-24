> 보관일: 2026-09-24. 작성 당시의 계획·판정·승인 이력이며 현재 실행 지시가 아닙니다. 현행 기준: [현재 상태](../../../soccer/current-status.md).

# MS3 사용자 확인 체크리스트

MS3 자동 완료 gate와 대규모 R0-Full 평가는 이미 통과했다. 아래 절차는 완성된 PPO 대 PPO 경기의 움직임을 사람이 눈으로 확인하는 선택적 시각 QA다. 이 확인을 기다리지 않고도 현재 MS3 산출물은 완료 상태다.

## 실행 방법

1. `C:\GitHub\Machine-Learning\Builds\MNG_MS\MS3-Final-Review\START_MS3_FINAL_REVIEW.cmd`를 더블클릭한다.
2. Windows 보안 창이 나타나면 프로젝트에서 직접 빌드한 실행 파일인지 경로를 확인한 뒤 실행한다.
3. 300초 동안 Red와 Navy의 PPO 감독이 1배속으로 경기한다. 두 팀 모두 같은 동결 모델이며 규칙형 감독은 없다.
4. 확인을 마치면 `Alt+F4`로 종료한다. 실행 결과는 `Builds\MNG_MS\MS3-Final-Review\ReviewLogs\player.log`에 남는다.

## 눈으로 확인할 항목

- [ ] Red와 Navy 선수들이 모두 킥오프 뒤 공에 반응하고 경기가 멈추지 않는다.
- [ ] 한 팀만 움직이거나 한쪽 골문만 공격하는 명백한 진영 오류가 없다.
- [ ] 공 소유와 위치에 따라 회수, 전진, 슛, 균형, 후방 보호 행동이 바뀐다.
- [ ] 같은 자리에서 무한 회전하거나 벽에 장시간 고정되는 심각한 반복이 없다.
- [ ] 득점 뒤 정상적으로 다음 킥오프로 재개된다.
- [ ] 프레임 정지, 예외 창, 강제 종료가 없다.

패스는 이번 MS3의 완료 근거가 아니다. 최종 R0 평가에서 PPO가 `PassBuild`를 선택한 횟수는 0이므로, 패스가 적게 보이는 현상 자체를 빌드 오류로 판단하지 않는다. 현재 정책의 입증된 강점은 R0-Full 상대 경기 결과, 양 진영 성능, 유효 슛과 명령 무결성이다.

## 로그 확인이 필요할 때

`ReviewLogs\player.log`에서 다음 문구를 찾는다.

- `mode=SelfPlay`
- `policies=2 rules=0`
- `policyAssistMode=None`
- `blockedPassOverrides=0`
- `explicitBlockedPassRewards=0`

육안으로 이상이 보이면 발생한 경기 시간과 화면 증상을 기록한다. 모델 재학습부터 시작하지 말고 같은 로그에서 예외와 양 팀 정책 telemetry를 먼저 확인한다.
