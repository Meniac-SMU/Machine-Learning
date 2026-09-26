# MS3-v3 0k~2M 토너먼트 평가 및 챔피언 선발

실행일: 2026-09-26. 사용자가 지정한 순차 승자 대결과 추가 경기 규칙으로 완료했다.

최종 챔피언: **2M / 실제 2,001,834 step**. `currentChampion`과 `championEvidence`에 등록했다.

## 조건과 판정

- 동일 runtime·관측244·명령6·공통 경기 규칙·ONNX 확률 추론. 매 경기300초, 최대16개 병렬 실행.
- 각 대결 기본40경기=20 seed×진영2. 상대가 바뀌는 다음 라운드는 앞 라운드 승자 확정 뒤 시작했다.
- 승률은 승/전체 경기, 차이는 |A승−B승|/전체 경기×100%p. 기본40경기 뒤5%p 이하이면20경기를 한 차례 추가했다.
- 이후 누적 승수가 다르면 차이가5%p 이하라도 승자를 확정한다. 누적 완전 동률만 기본 경기 수100%인40경기를 추가하며, 동률이 계속되면 반복한다.
- 추가 경기는 새 seed와 양 진영 쌍을 사용한다. 무승부는 양측에 동일하므로 승점률로 비교해도 승자와 격차는 같다.
- 기존 통계적 승격 문턱을 추가 적용하지 않았다. 이번 사용자 지정 규칙이 챔피언 판정 기준이다.

## 모델

|표기|실제 MS3 step|ONNX SHA-256|
|---|---:|---|
|0k|0|`3e8cd87b21ce50d1dd45384ce9f28145442123dbfb41aed6ee0ae6ce12ec5fd7`|
|400k|400,719|`ba9c6599106f7373d5e522bcb95d5bef0bfc16dcbf2481a8facfb0b569db21c3`|
|800k|801,184|`5eb1d0e151f7cdc8f40324d3235eb7e52da80f0e885b49f80b4576bd4105a7ca`|
|1.2M|1,200,569|`6c51f7f1a6464b6be8fa523a09ceaff779d25def7cac5d00b4ddb5e2059f3442`|
|1.6M|1,600,333|`57be6b02bf08e83f058725552c2c0a9521685579680b3733acfb2d2bc1c1ceb9`|
|2M|2,001,834|`31b042143e5a01be5e5c2717126e6a967b1c16400d8a4167e72580acde4dc2a1`|

0k는 r002의 실제 저장된0-step ONNX다. 승인 MS2 초기 정책과 파일 SHA가 동일하며 무작위 정책이 아니다.

## 라운드 결과

|순서|대결 A : B|A 승·무·패|기본→추가 경기|승률 차이|승자|
|---|---|---|---|---:|---|
|1|0k : 400k|12승 7무 21패|40|22.50%p|400k|
|2|400k : 800k|24승 5무 11패|40|32.50%p|400k|
|3|400k : 1.2M|14승 9무 17패|40|7.50%p|1.2M|
|4|1.2M : 1.6M|16승 5무 19패|40|7.50%p|1.6M|
|5|1.6M : 2M|21승 9무 30패|40 + 20|15.00%p|2M|

총 **220경기**: 기본200경기 + 추가20경기.

### 경기 묶음별 근거

- 라운드1 묶음0: 12승 7무 21패(A 기준), seed 936001~936020. `Logs\MNG-Rebuild\MS3-v3-r002-tournament-20260926\round-1-batch-0\report.json`
- 라운드2 묶음0: 24승 5무 11패(A 기준), seed 946001~946020. `Logs\MNG-Rebuild\MS3-v3-r002-tournament-20260926\round-2-batch-0\report.json`
- 라운드3 묶음0: 14승 9무 17패(A 기준), seed 956001~956020. `Logs\MNG-Rebuild\MS3-v3-r002-tournament-20260926\round-3-batch-0\report.json`
- 라운드4 묶음0: 16승 5무 19패(A 기준), seed 966001~966020. `Logs\MNG-Rebuild\MS3-v3-r002-tournament-20260926\round-4-batch-0\report.json`
- 라운드5 묶음0: 17승 6무 17패(A 기준), seed 976001~976020. `Logs\MNG-Rebuild\MS3-v3-r002-tournament-20260926\round-5-batch-0\report.json`
- 라운드5 묶음1: 4승 3무 13패(A 기준), seed 977001~977010. `Logs\MNG-Rebuild\MS3-v3-r002-tournament-20260926\round-5-batch-1\report.json`

## 검증과 해석 범위

- 모든 경기의300초 완료·진영/seed·명령 무결성·공통 규칙을 검사했다. Player 오류0. 메모리 관측 최대49%, 최소 가용15.94GiB.
- 후보6개 ONNX, 최종 checkpoint·optimizer 저장본, 상대 풀, 평가 코드의 평가 전후 해시가 유지됐다.
- 이전 registry·문서·모델·훈련 및 평가 이력은 보존했다. 학습은 수행하지 않았다.
- 이 결과는 지정된 순차 토너먼트의 챔피언이다. 모든 모델 간 전수 리그나 별도 학습 seed에 대한 일반적 우월성을 증명하지 않는다.
- 누적 승수 차이가 작은 경우도 요청된 판정대로 승자로 인정했다. 통계적 불확실성은 원본 report의 paired95에 별도로 남아 있다.

## 원본 증거

- `Logs/MNG-Rebuild/MS3-v3-r002-tournament-20260926/manifest.json`
- `Logs/MNG-Rebuild/MS3-v3-r002-tournament-20260926/completion.json`
- 챔피언 모델: `results/MNG_MS3V3-20260925-r002/MNG_ManagerV2/MNG_ManagerV2-2001834.onnx`
- 판정 전후 registry: 동일 증거 폴더의 `registry-before.json`, `registry-after.json`.
