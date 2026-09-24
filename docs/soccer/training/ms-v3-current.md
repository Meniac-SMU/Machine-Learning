# MS v3 운영 기준

마지막 검토: 2026-09-24. 현행 코드·schema·registry·D5 보고서를 대조한 운영 진입점이다. 진행 상태는 [현재 상태](../current-status.md) 한 곳에서 관리한다.

## 정책·런타임 계약

- 팀당 감독 Agent 1개, PPO 숫자 관측. 감독은 전진·패스·슛·회수·균형·후방보호의 6명령을 선택하고 코드가 선수의 이동·목표·물리 킥을 실행한다.
- [schema](ms-v3-schema.json): Behavior `MNG_ManagerV2`, 관측244, action branch[6], task/event version2, protocol3. 기존133 + 선수4×27 + 전역3이다. 세대 v3와 ABI 이름 v2는 별개다.
- 현행 revision은 `MNG-MS2-forward-pass-center-20260923`. `Assets/_Soccer/Manager/Runtime/MNG_RuntimeV2.cs`와 schema가 일치해야 한다.
- MNG 정책형·규칙형·fallback은 같은 기술 경로를 사용한다. 사람 입력 소유권을 유지하고 행동 성공·소유를 강제 생성하지 않는다.
- 구 선수 정책379/[3,3,3,3]와 감독244/[6]는 별도 계약이다.

## 현재 경기 규칙

- 자기 골문 앞 걷어내기는 모든 자동 선수 유형에 유지한다. 정체2초/전진0.1m에 따른 강제 패스는 폐지했다.
- 감독 PassBuild 선택 뒤 전방 동료를 향해 준비하며 실제 경로가 막히면 취소한다. 상대 골문 중심24m 이내에서는 PassBuild를 선택하지 않는다.
- 패스 범위5~28m, 경로 여유1.25m, 예측 수신점 중앙 쪽1.5m 편향, 드리블 목표 중앙 쪽3m 편향(중앙선 초과 금지)을 재사용한다.
- 준비/수신 예산은 각2초. 실제 킥·수신과 준비 요청을 별도로 집계한다. 명령 선택만으로 보상하지 않는다.
- MNG 공·선수 solver48/16 및 Reset의 Transform/Rigidbody 동기화는 유지한다. 이 수치를 별도 Core 환경 전체에 확대하지 않는다.
- 세부 근거: [현행 경기 규칙 보고서](ms2-forward-pass-center-report-20260923.md). 과거 설계가 필요할 때만 [개편 런타임 명세 이력](../../archive/manager/ms-v2/ms-rebuild-runtime-contract-20260922.md)을 본다.

## 정책 계보·실행 경계

[v3 registry](ms-v3-model-registry.json)의 승인 MS2 step200120 actor만 출발 기준이다. 초기 actor 사본은 step0, 새 optimizer, 새 상대 풀로 시작한다. MS2를 새로 학습했다는 뜻이 아니다. 기존 MS3-v2 r005 400k는 모든 현행 선택에서 제외하고 보존한다.

첫100k 저장·정지, 모니터링 통과 후 같은 Run으로200k까지 재개한다. 32환경, 집계 step,100k 점검/200k 고정300초 상세 평가를 유지한다. 현재 준비 상한200k이며400k 이상은 후속 판단과 별도 준비가 필요하다. 진영 동등성·장시간 메모리/속도·정책 품질을 준비 검증만으로 승인하지 않는다.

상대 풀은 MS2 초기 snapshot 하나를 pinned로 시작하고 새 v3 Run의 snapshot을 쌓는다. 외부 history·구 pool·구400k를 넣지 않는다. [D4·D5 보고서](ms3-v3-d4-d5-report-20260923.md)에 준비물·평가 상대·seed·검증 경계를 고정했다.

## 도구 선택

[도구 색인](../../../Tools/README.md)에서 현행 진입점을 선택한다. 평가 `mng_v3_evaluate.py`, 시연 `watch_mng_v3.py`, 계보 검사 `mng_v3_policy_guard.py`를 사용한다. 이들이 호출하는 v2 구현 모듈과 `MNG_V2_Train.ps1`은 현행 의존성이므로 폐기 대상이 아니다.

준비 패키지는 `Logs/MNG-Rebuild/MS3-v3-D5-prepared-20260923`다. 실행 전에 manifest가 참조하는 코드·build·actor hash를 확인한다. 실제 학습은 해당 작업의 사용자 지시 범위에서만 실행하며 이번 파일 정리는 학습 실행 요청이 아니다.
