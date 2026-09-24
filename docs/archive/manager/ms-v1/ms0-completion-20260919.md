> 보관일: 2026-09-24. 작성 당시의 계획·판정·승인 이력이며 현재 실행 지시가 아닙니다. 현행 기준: [현재 상태](../../../soccer/current-status.md).

# MS0 완료 보고서 — 2026-09-19

MS0의 단일 완료 기록이다. 구현 계약은 [MS 계획](ms-plan.md), 판정 기준은 [MS 평가 계약](ms-validation.md), 재현 가능한 원본 수치는 `Logs/MNG-MS/MS0-completion/ms0-completion.json`을 따른다. 이전 [중단 체크포인트](ms0-checkpoint-20260919.md)는 당시 상태를 보존하는 이력이며 이 문서로 대체된다.

2026-09-20 사용자가 MS0 확인 완료를 선언했다. 다음 단계는 [MS1 개발·학습 계획](ms1-development-plan-20260920.md)을 따른다.

## 판정

**MS0 준비 단계를 완료했다.** R0 상대 학습, 1·2·8개 병렬 환경, PPO update와 PT/ONNX 저장, 같은 Run resume, 양 팀 PPO self-play와 ghost snapshot·학습 팀 전환이 실제 Windows Player에서 연결되었다. 이 판정은 파이프라인 준비 완료를 뜻하며 강한 정책의 학습이나 경기력 통과를 뜻하지 않는다. 다음 단계는 MS1 혼합 상황 학습이다.

사용량 비율 중단 기준은 2026-09-23에 폐지했다. reset credit이나 자동 재개는 사용자 지시 없이 사용하지 않는다. 완료 직전 실제 잔여량은 각각 57%, 63%였으며 중단선에 도달하지 않았다.

## 구현된 최소 범위

- `MNG_MSController`가 R0 상대와 self-play 모드를 분리하고, worker 포트·process·episode·seed·양 팀 명령 수를 worker별 JSONL에 기록한다.
- `MNG_MSOpponentProfile`이 Rescue `0.20×/2.0초`, Easy `0.35×/1.5초`, Medium `0.60×/1.0초`, Full `1.00×/0.5초`를 고정한다. Full은 현재 승인된 R0와 동일하다.
- 공통 `MNG_PlayerMotor`, `MNG_RuleBasedManager`, `MNG_MatchController`, `MNG_RewardEngine`에 opt-in MS 연결만 추가했다. 기존 선수 이동·패스·슛·소유·골 판정과 R0 기본값은 재사용한다.
- `MNG_MS_Train`은 Red PPO 대 Navy Full R0, `MNG_MS_Evaluation`은 같은 상대의 300초·1배속 평가, `MNG_MS_SelfPlay`는 TeamId 0·1의 PPO 두 팀으로 구성된다. Rule과 PPO가 같은 팀 명령을 동시에 쓰지 않는다.
- R0 학습용과 self-play용 Windows Player, 세 smoke YAML, MS0 프로토콜, source snapshot·build·train 스크립트를 마련했다.
- self-play 마지막 transition은 승 `+0.5`, 무 `0`, 패 `-0.5`로 확정한다.

## 자동 검증 결과

| 항목 | 결과 | 핵심 증거 |
| --- | --- | --- |
| Unity 컴파일·Builder Validate | 통과 | `MNG MS0 VALIDATION PASS` |
| EditMode MS 계약 | `8/8` 통과 | Full R0 동등성, 네 강도, terminal 부호, worker seed, 이동 배율 |
| PlayMode Scene 연결 | `2/2` 통과 | R0: 정책 1+규칙 1, self-play: 정책 2+규칙 0 |
| YAML 파싱 | 3개 통과 | 설치 trainer `mlagents 1.2.0.dev0`로 파싱 |
| Windows Player | 2개 성공 | R0Smoke와 SelfPlaySmoke, build error 0, exe/level/source hash 일치 |
| 병렬 환경 | 1→2→8 통과 | 서로 다른 port·process·spawn seed, worker별 파일과 episode 생성 |
| R0 PPO | 통과 | 최종 checkpoint 1,284, PPO update 2회, PT 7개, ONNX 7개 |
| 같은 Run resume | 통과 | step 482에서 같은 Run을 resume하여 1,280 summary까지 증가 |
| self-play PPO | 통과 | 최종 checkpoint 2,050, PPO update 7회, PT 7개, ONNX 7개 |
| self-play 교체 | 통과 | TeamId 0·1 연결, snapshot 교체, 학습 팀 1→0→1 전환 DEBUG trace |
| ONNX 구조 | 통과 | `onnx.checker`, 입력 관측 `[0,133]`, action mask `[0,6]` |
| optimizer 저장 | 통과 | 두 `checkpoint.pt`에 Policy와 optimizer/global step key 존재 |

최종 테스트 원본은 `Logs/MNG-MS/MS0/final-editmode-status.json`과 `Logs/MNG-MS/MS0/final-playmode-status.json`에 있다. 전체 산출물의 크기와 SHA256은 `Logs/MNG-MS/MS0-completion/ms0-completion.json`에 고정했다.

## 합격 Run과 step 예산

| Run | 목적 | 환경 수 | 최종 checkpoint | PPO update | episode |
| --- | --- | ---: | ---: | ---: | ---: |
| `MNG_MS0Parallel1-20260919-r001` | 단일 환경 기준 | 1 | 281 | 0 | 7 |
| `MNG_MS0Parallel2-20260919-r001` | 최소 병렬 | 2 | 282 | 0 | 8 |
| `MNG_MS0Parallel8-20260919-r001` | 기본 병렬 후보 | 8 | 280 | 0 | 8 |
| `MNG_MS0R0-20260919-r001` | R0 학습·중단·resume | 2 | 1,284 | 2 | 34 |
| `MNG_MS0SelfPlay-20260919-r001` | 양 팀 PPO 학습 | 2 | 2,050 | 7 | 54 |

합격 Run의 최종 checkpoint 합계는 **4,177 / 5,000 step**이다. 병렬 세 Run은 짧은 연결·격리 검사라 optimizer update가 없고, update 증거는 R0와 self-play Run에서 확보했다. 1·2·8 환경의 전체 실행시간은 각각 약 21.07초, 12.09초, 14.30초였다. 이 결과는 MS0 연결 증거다. 2026-09-20 사용자 결정으로 MS1 본학습은 16개 환경 고정이며, 이 과거 수치를 근거로 8/2개로 낮추지 않는다.

### 진단 Run 예산 편차

일반 INFO 로그만으로 ghost snapshot 대상과 학습 팀 전환을 직접 입증할 수 없어 `MNG_MS0SelfPlay-20260919-r002`를 DEBUG 진단으로 추가 실행했다. 이 Run은 summary 1,280, 마지막 checkpoint 1,246에서 끝났고 합격 모델·seed·예산 합계에는 포함하지 않았다. 실제 실행된 모든 manager step을 summary 기준으로 합치면 **5,457 step**이므로, 5,000 상한을 457 step 초과한 과정 편차가 있다. 모델 선택이나 성능을 유리하게 만들기 위한 재시도가 아니며, `trainer.log`의 snapshot/team switch 증거만 사용한다. 이 편차를 숨기거나 r번호를 바꾸어 예산을 초기화하지 않는다.

## 중단·resume 해석

R0 Run은 첫 checkpoint 이후 정상 `Ctrl+C`로 PT/ONNX를 저장한 뒤 같은 run ID와 config·seed로 `-Resume`했다. trainer가 상태 파일 부재 경고를 출력했지만 `Resuming training from step 482`를 명시했고 최종 checkpoint 1,284까지 증가했다. 최종 `checkpoint.pt`에 optimizer 상태가 있으므로 MS0의 저장·재개 연결은 통과로 판정한다. 이 짧은 smoke를 장시간 학습의 완전한 장애 복구 보장으로 확대 해석하지 않는다.

## 재현 산출물

| 산출물 | 경로 / 식별값 |
| --- | --- |
| 최종 source manifest | `Logs/MNG-MS/MS0-final-source-v2/source-sha256.json` — SHA256 `7d3c43cd8cdf48a91f5166113443611f6db8cd151c5db9661331a8dff29c3c54` |
| R0 Player | `Builds/MNG_MS/MS0_R0/MNG_MS0_R0.exe` — SHA256 `d3db57b9b88a2d8fedd1621461ddd7b29ed18001efd29a93ef9f31d4e8a483cd` |
| self-play Player | `Builds/MNG_MS/MS0_SelfPlay/MNG_MS0_SelfPlay.exe` — 같은 exe SHA, 서로 다른 `level0` SHA |
| R0 optimizer checkpoint | `results/MNG_MS0R0-20260919-r001/MNG_Manager/checkpoint.pt` |
| R0 ONNX | `results/MNG_MS0R0-20260919-r001/MNG_Manager.onnx` |
| self-play optimizer checkpoint | `results/MNG_MS0SelfPlay-20260919-r001/MNG_Manager/checkpoint.pt` |
| self-play ONNX | `results/MNG_MS0SelfPlay-20260919-r001/MNG_Manager.onnx` |
| 완료 manifest | `Logs/MNG-MS/MS0-completion/ms0-completion.json` |

최초 `Logs/MNG-MS/MS0` snapshot과 중간 `MS0-final-source` snapshot은 이력으로 보존한다. Player의 기준 source는 `MS0-final-source-v2`뿐이다. 기존 R0/M Run과 사용자의 다른 dirty 변경은 원복하거나 덮어쓰지 않았고 commit·push도 수행하지 않았다.

## 남은 범위와 다음 단계

- MS0 모델은 파이프라인 smoke 산출물이다. 54개 self-play episode 중 Red 승 1, Navy 승 0, 무승부 53이므로 강한 정책으로 평가하거나 시연 모델로 승격하지 않는다.
- 독립 공격/수비 성공률, 무작위 명령 baseline 대비 개선, Full R0 40경기 성능은 MS1·MS2의 gate다.
- MS1을 시작하기 전 사용자는 [MS0 사용자 확인 체크리스트](ms0-user-checklist.md)의 짧은 육안 항목만 확인하면 된다. 확인 없이도 자동 MS0 증거는 보존되지만, 실제 화면·조작 감각에 관한 사람의 판단은 자동 테스트가 대신할 수 없다.
- MS1의 코드·학습·Run은 아직 시작하지 않았다.
