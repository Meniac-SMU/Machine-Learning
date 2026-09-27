# 전시 UI 디자인 v4 검증 기록

검토일: 2026-09-26. HTML·CSS·JavaScript·문구표·SVG 디자인 패키지만 수정했다. Unity 개발, 실제 선방 관측, 학습·평가·빌드는 수행하지 않았다.

이전 자료: [v3 검증 기록](verification-v3-20260926.md). 최신 캡처는 `manager-v4-*`이며 이전 버전 이미지는 보존한다.

## 정적 확인

- [정적 검사](static-checks-v4.json): HTML id 72개 중복 없음, 로컬 참조 11개 유효, 번역 68개 키 모두 KO/EN 값 존재, SVG 11개 XML 파싱 통과.
- JavaScript의 직접 id 참조 누락 없음. `node --check preview.js` 통과.
- 모델 모달 하단의 도움말·선택지 개수와 modelCount 참조 제거 확인.
- 한국어·영어 단계 이름, 규칙형 목록의 순서·이름, Goal·선방·사전 학습 문구 확인.
- 디자인 자산 생성기 실행 성공. 강화학습 6개 모델 출처·SHA 검증 및 규칙형 5개 목록 유지. 원본 모델은 수정하거나 실행하지 않음.

## 브라우저 확인

인앱 브라우저, 로컬 서버 `http://127.0.0.1:8769/`. 1600×1020 viewport에서 가로 16:9 게임 영역과 한국어·영어 화면을 확인했다. 검토 후 임시 viewport를 해제하고 영어 시작 화면을 열어 두었다.

| 사용자 변경 요청 | 확인한 결과 |
|---|---|
| 영어 시작 문구 위치 | 한국어와 같은 좌측 상단으로 복귀. Human Centered AI Major · EXHIBITION 2026. 우측 THE GAME IS LEARNING. 표시 유지 |
| 영어 규칙형 목록 | Recover / Balanced / Carry & Shoot / Full rules / Random. 한글 병기 없음 |
| 한국어 규칙형 목록 | Recover: 회수 우선 / Balanced: 균형 유지 / Carry & Shoot: 전진-슈팅 / Full rules: 전체 규칙형 / Random: 무작위 선택 |
| 병기 크기 | 다섯 이름 모두 영문·한글 computed font-size 27.44px로 동일. 가로 넘침 없음 |
| 한국어 강화학습 단계 | 0 / 40만 / 80만 / 120만 / 160만 / 200만. 영어 기존 단위 유지 |
| 사전 학습 안내 | 한국어 100만 회, 영어 1 million pre-training steps 문구 표시 |
| 모델 모달 하단 | 양쪽 문구 삭제. model-foot 요소 0개 |
| 경기 좌하단 폭 | 2열·3행 배치. 한국어 약 290→198px, 영어 약 278px. 확인한 조작 안내·감독 표시 요소의 가로 넘침 없음 |
| 득점 표현 | 한국어 RED Goal, 영어 NAVY Goal 확인. 팀 이름과 전체 폭 배경 띠 유지 |
| 상황판 | — / 선방 / — 표시. 누적 보상 → 전체·AI·사람 슈팅 → 공 회수 → 선방 순서 유지 |
| 브라우저 로그 | 확인 시 error/warn 0개 |

사전 학습 100만 회는 사용자가 지정한 전시 문구의 가정이다. 실제 모델의 학습 이력을 검증한 수치가 아니며 모델 원본·단계·출처는 유지했다. 경기의 보상·공 회수·슈팅도 시각 검토용 예시다. 선방은 UI만 유지하며 관측 카운터를 구현하지 않았다.

## 최신 캡처

- [영어 시작 화면의 좌측 상단 문구](../../soccer/exhibition/qa/manager-v4-home-en.png)
- [한국어 규칙형 목록](../../soccer/exhibition/qa/manager-v4-rules-ko.png) / [영어 규칙형 목록](../../soccer/exhibition/qa/manager-v4-rules-en.png)
- [한국어 학습 단계·사전 학습 안내](../../soccer/exhibition/qa/manager-v4-models-ko.png) / [영어 안내](../../soccer/exhibition/qa/manager-v4-models-en.png)
- [한국어 경기 HUD](../../soccer/exhibition/qa/manager-v4-match-ko.png) / [영어 HUD](../../soccer/exhibition/qa/manager-v4-match-en.png)
- [화면 전체 폭 RED Goal](../../soccer/exhibition/qa/manager-v4-goal-ko.png)
- [선방 표기를 적용한 상황판](../../soccer/exhibition/qa/manager-v4-result-ko.png)

## 범위

작업 전의 AGENTS.md, docs/README.md, current-status.md, ms-v3-current.md, 패스 모방학습 계획 변경을 보존했다. 수정 범위는 전시 디자인 폴더와 이전 검증 기록 보관을 위한 docs/archive/exhibition이다. Assets/Packages/ProjectSettings 변경, Unity 실행, 모델 변경, 학습, 평가 실행, commit/push는 없다. 현재 디자인 계약은 [설계 문서](../../soccer/exhibition/design-spec.md)에 반영했다.
