# Soccer 현재 상태

마지막 검토:2026-09-26. 세부 계약은 [MS v3 운영 기준](training/ms-v3-current.md),최신 결과는 [0k~2M 토너먼트 및 챔피언 선발](../archive/manager/ms-v3/ms3-v3-r002-tournament-20260926.md)을 따른다. [2M 학습 완료 이력](../archive/manager/ms-v3/ms3-v3-r002-2m-training-20260925.md)은 보존한다.

## 현재 단계

|구분|상태|
|---|---|
|Run|MNG_MS3V3-20260925-r002|
|학습 진행|2,001,834 step 저장·최종 검증 완료. 학습·평가·자원 모니터 종료|
|R6|완료:400719 step 및 독립80경기 확인 이력 유지|
|champion|2M / 실제2,001,834 step. 사용자 지정 토너먼트로 확정·registry 등록|
|향후 교체 기준|새 모델이 현 챔피언과 직접 대결에서 이기면 교체. 기본40경기·접전20경기 추가·이후 완전 동률만40경기 추가|
|향후 지표 평가|400k 간격으로 다양한 기존 상대와 평가 유지. 다른 상대 성적은 별도 지표로 보고하며 교체 조건에 추가하지 않음|
|평가|정규960경기 완료 후 별도0k~2M 토너먼트220경기 완료(기본200+추가20).100k는 저장·점검만|
|제외 상대|무작위 상대는 평가하지 않음|
|중단 조건|주간 잔여4% 이하 또는 치명적 문제 시 저장·중단. 학습 종료 당시 잔여54%; 토너먼트 확인은 별도 usage-checks.json|
|격리 모델|r001 및 오류주입 r900 재개/승격 금지|

최신 승인 목표2M과 예정 평가를 완료했다. 같은 optimizer·상대 풀·32환경·runtime·보상·PPO 상수 schedule을 이어받았다. 생성 config 변경은 명목 최대 step1M→2M과 모델 보존 수21→64뿐이다. 최종 양 진영 학습 전이는1012692/989142,optimizer update22278이다. checkpoint·pool·scalar 유한값,32환경 행동 기록,평가 전후 artifact hash 검사를 통과했다.2M 이후 학습은 시작하지 않았다. 이후 별도 사용자 승인으로0k~2M 순차 토너먼트를 수행하고 승자를 챔피언으로 등록했다.

원본600k manifest·1M 확장 이력·실패 시도·모델/풀 증거를 보존했다.2M 증거는 `Logs/MNG-Rebuild/MS3-v3-r002-to2m-20260925/completion-2m.json`과 구간별 checkpoint/inspection/monitor/comparison 파일이다. 번호별 모델·풀126개를 확장 증거 폴더에 보존했다.

1.7M 첫 시작에서는 GoogleDriveFS의 포트6507 점유와 충돌해 학습 결정0에서 종료했다.1.6M 저장본 무변경을 확인한 뒤 포트 해제를 기다렸고,원래32개 포트 bind 검사와 같은 조건의 재개를 검증했다. 다른 프로그램 종료나 포트·배치 난수 변경은 없었다. 실패 시도와 복구 근거는 `startup-failure-1700000.json`, `port-resolution-1700000.json`에 남겼다.

## 확인된 성능과 한계

고정5상대 평균 승점률은1M70%,1.2M68%,1.6M73.25%,2M74.25%다.2M 상대별 성적은 MS2 62.5%,Full88.75%,Recover72.5%,carry-shot56.25%,Balanced91.25%다.1.6M 대비 Full·Balanced는 상승했지만 MS2·carry-shot은 각각5%p 하락했다. 공통 상대별 변화95% 구간은 모두0을 포함한다.

2M은400k champion에16승9무15패(51.25%),직전1.6M에19승6무15패(55%)였다. 이는 당시 정규 평가 결과이며 당시에는 champion400719를 유지했다. 이후 사용자가 별도로 지정한 토너먼트220경기를 완료해 현 champion을2M(2,001,834 step)로 확정했다. 별도 training seed 일반화 검증은 수행하지 않았다.

2M320경기에서 Pass 선택2회 중 실제 타격 연결1회,경기 상태 변경 취소1회다. 패스 활용 부족은 미해결이다. 승인 MS2 상대 Red77.5%/Navy47.5%로30%p 차이가 있었고 seed 묶음95% 구간은7.5~52.5%p다. 진영 편차와 수비 위험 지표는 추가 관찰 대상이다. 빈도 강제나 보상·행동 마스크 변경은 하지 않았다.

완료 학습 구간과 평가 Player 오류0,메모리 최대80%,최소 가용6.11GiB였다. 최종 ONNX는 `results/MNG_MS3V3-20260925-r002/MNG_ManagerV2/MNG_ManagerV2-2001834.onnx`다. 최종 entropy0.334559,Approx KL0.000893,policy loss0.060288,value loss0.077847이다.

R6의400k champion은 독립80경기에서 승인 MS2에45승11무24패,승점률63.125%,95% 구간54.375~71.875%로 첫 승격 기준을 통과했다. [1M 이전 이력](../archive/manager/ms-v3/ms3-v3-r002-long-training-20260925.md)과 [1.6M 평가·포트 복구](../archive/manager/ms-v3/ms3-v3-r002-1600k-evaluation-20260925.md)는 보존한다. 이전200k 재개 한정 사용량 예외는 종료됐다.

## 최신 토너먼트 판정

0k→400k→800k→1.2M→1.6M→2M 순서로 직전 승자와 대결했다. 기본40경기·경기당300초·최대16개 병렬, 진영 교대 seed 쌍을 사용했다. 기본 승률 차이5%p 이하는20경기를 추가하고 누적 승수로 판정했다. 완전 동률인 경우만40경기를 추가하는 규칙이다. 최종 챔피언은2M(2,001,834 step)이며 총220경기를 수행했다.

원본 증거는 `Logs/MNG-Rebuild/MS3-v3-r002-tournament-20260926/completion.json`이다. 모든 후보·최종 checkpoint·상대 풀 해시를 유지했고 Player 오류0이다. 이번 선발은 사용자 지정 토너먼트 승수 기준이며 기존 통계적 승격 문턱을 추가 적용하지 않았다. 전수 리그나 모든 상대에 대한 통계적 우월성으로 해석하지 않는다.

## 현재 자산과 검증

- [IO 수정·검증 보고서](training/ms3-v3-io-fix-20260925.md),[기존 r001 중단 보고서](training/ms3-v3-r6-stop-20260925.md).
- Player: `Builds/MNG_V2/MS3-v3-WorkerIOFix-20260924T182752604Z/{MS3V2,EvaluationV2}`.
- runtime DLL SHA: `8c7c54abba8e36a1ced4d5ec144bf836c36bee484f8f9b67827d776a466b3d9d`. 환경 revision/관측244/행동6/경기 규칙은 유지했다.
- 준비물: `Logs/MNG-Rebuild/MS3-v3-IO-ready-20260925`. 실행 증거: `Logs/MNG-Rebuild/MNG_MS3V3-20260925-r002`.
- Python41/41,EditMode367/367,Soccer/Manager PlayMode54통과·0실패·수동진단2제외. Legacy Escape3실패는 해당 씬 미등록으로 별도 기록했다.
-32환경 공용 evidence 폴더,총153472결정·121경기 종료·반복 Reset·Player 예외0. 동결80경기는 기존 D4와 경기별 데이터 전체 동일.
- 별도 r900 오류 주입으로7552 step에서 모델·optimizer·pool 저장 및 재개 차단을 검증했다.
- 원래 준비물·빌드·r001·r900·구 MS3-v2 모델과 증거는 보존한다. 기존 구400k는 현행 초기값/상대/champion에서 제외한다.

## 현재 경기 규칙과 미해결 항목

정체2초/전진0.1m에 의한 강제 패스는 폐지했다. PassBuild는 감독 선택 뒤 열린 전방 경로에서 준비하고 자기 골문 앞 걷어내기는 모든 자동 선수 유형에 유지한다. 중앙 편향 드리블과 준비/수신 각2초 예산은 [경기 규칙 보고서](training/ms2-forward-pass-center-report-20260923.md)를 따른다.

후속 평가에서 패스 선택→준비/취소→실제 타격→수신, 진영별 성적, carry-shot 열세와 장시간 학습 성능을 추적한다. 패스 빈도는 향후 학습 경과로 판단하며 새 최소 횟수 gate를 만들지 않는다.

기존 선수 Curriculum은 L2-Find r017 step500248에서 미승인 종료했다. 최근5요약은99% gate 미달이고 독립 고정300 승급평가를 하지 않았다. L2-Score/L3를 자동 재개하지 않는다.

## 문서·에셋 정리

2026-09-24 정리 내역·검증·보존 판단은 [정리 보고서](../project/cleanup-20260924.md)에 기록한다. 기본 검색은 [문서 색인](../README.md)과 [도구 색인](../../Tools/README.md)에서 범위를 선택한다. 과거 누적 기록은 [보관 색인](../archive/README.md)에서 필요한 보고서만 찾아본다.

2026-09-25 구 Player129개를 삭제해 약17.28GiB를 확보했다. 현행 MNG 학습·평가2개와 `Builds/SoccerTraining`1개를 유지했다. 생성·사용 기록과 후속 정리 기준은 [빌드 관리](../project/build-lifecycle.md), 삭제 명세는 [보고서](../project/build-cleanup-20260925.md)다.
