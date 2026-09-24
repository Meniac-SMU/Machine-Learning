# Soccer 현재 상태

마지막 검토: 2026-09-24. 이 파일에는 현재 상태와 다음 작업만 둔다. 세부 계약은 [MS v3 운영 기준](training/ms-v3-current.md), 상세 증거는 [D4·D5 보고서](training/ms3-v3-d4-d5-report-20260923.md)를 따른다.

## 현재 단계

| 구분 | 상태 |
|---|---|
| MS2-v3 출발 기준 | 기존 승인 MS2 step200120 actor + 최신 공통 경기 규칙 채택, 사용자 경기 확인 완료 |
| 공통 개편 R1~R4 / 준비 정책 R5 | 기존 구현·MS2 계보 재사용 완료 |
| MS3-v3 학습 전 안정화 D1~D5 | 완료, 실행 준비만 완료 |
| 새 v3 초기 자기대전 R6 | **미시작: 학습0 / optimizer update0** |
| R7·R8 확장 | 미진입 |
| 현행 champion | 없음 |

준비된 다음 흐름은 첫100k 저장·정지 → 점검 → 최대200k 평가다. 현재 정리 요청으로 학습을 시작하지 않는다. 400k·R7/R8·수백만 step으로 자동 연장하지 않는다.

## 현행 자산과 증거

- [v3 모델 registry](training/ms-v3-model-registry.json)와 [v3 schema](training/ms-v3-schema.json)가 선택 자격과 ABI의 기준이다.
- runtime: `MNG-MS2-forward-pass-center-20260923`. DLL SHA: `30aa9b0c9ce224de2d9e9b66457f258fc1bdaef2f3a8ef6069bb6f2194ac775a`.
- Player: `Builds/MNG_V2/MS2-forward-pass-center-20260923/{MS3V2,EvaluationV2}`.
- 준비 패키지: `Logs/MNG-Rebuild/MS3-v3-D5-prepared-20260923`. 예약 Run: `MNG_MS3V3-20260923-r001`. manifest의 readyToStart는 학습 완료나 무조건 실행 승인이 아니다.
- v3 D5 검증: `Logs/MNG-Rebuild/MS3-v3-D5-validation-20260923`. 당시 Python37/37, 32환경512결정·학습0, dry-run 및 잘못된 입력4종 거부 통과.
- 게임 코드 당시 검증: Edit357/357, Play54통과·실패0·수동진단2제외. 이 값들은 이전 runtime 검증 이력이며 이번 문서 정리에서 다시 실행한 결과가 아니다.
- 동결80경기 무결성 통과. 동일 정책 지정 후보 Red35%/Navy55%, 차이95%구간[-55,+17.5]%p. 진영 동등성은 미입증이다. Full 대비 승점률68.75%, carry-shot 대비25%는 출발 기준선이다.
- 기존 MS3-v2 r005 400k는 초기값·상대·평가 기준·champion·기본 시연에서 제외하고 원본 증거를 유지한다.

## 현재 경기 규칙과 미해결 항목

정체2초/전진0.1m에 의한 강제 패스는 폐지했다. PassBuild는 감독 선택 뒤 열린 전방 경로에서 준비하고 자기 골문 앞 걷어내기는 모든 자동 선수 유형에 유지한다. 중앙 편향 드리블과 준비/수신 각2초 예산은 [경기 규칙 보고서](training/ms2-forward-pass-center-report-20260923.md)를 따른다.

후속 평가에서 패스 선택→준비/취소→실제 타격→수신, 진영별 성적, carry-shot 열세와 장시간 학습 성능을 추적한다. 패스 빈도는 향후 학습 경과로 판단하며 새 최소 횟수 gate를 만들지 않는다.

기존 선수 Curriculum은 L2-Find r017 step500248에서 미승인 종료했다. 최근5요약은99% gate 미달이고 독립 고정300 승급평가를 하지 않았다. L2-Score/L3를 자동 재개하지 않는다.

## 문서·에셋 정리

2026-09-24 정리 내역·검증·보존 판단은 [정리 보고서](../project/cleanup-20260924.md)에 기록한다. 기본 검색은 [문서 색인](../README.md)과 [도구 색인](../../Tools/README.md)에서 범위를 선택한다. 과거 누적 기록은 [보관 색인](../archive/README.md)에서 필요한 보고서만 찾아본다.
