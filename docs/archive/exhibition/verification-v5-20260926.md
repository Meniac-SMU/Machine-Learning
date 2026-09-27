# 전시 UI 디자인 v5 검증 기록

검토일: 2026-09-26. 클릭형 디자인 시안과 문구·이미지·설계 문서만 수정했다. Unity 실제 개발, 모델 변경, 학습·평가·빌드는 수행하지 않았다.

이전 자료: [v4 검증 기록](verification-v4-20260926.md). 과거 캡처는 보존하며 최신 파일은 `manager-v5-*`다.

## 확인 결과

- 초기 모델: 선택 목록은 한국어 `초기 모델: 0회`, 영어 `Initial model: 0 steps`. 경기 설정·HUD·결과는 `초기 모델` / `Initial model`.
- 저장 횟수: 수량이 1일 때만 step, 0과 그 외 수량은 steps. 현재 모든 모델은 `0 steps`, `400,719 steps` 등의 복수형 표시.
- 규칙형 목록: Recover → Balanced → Carry & Shoot → Random 4개, 2×2 배치. Full rules는 목록·시안 샘플 판단에서 제거. 실제 소스와 과거 기록 보존.
- 경기 조작 안내: E 패스 / Pass, SPACE 슈팅 / Shoot. 가이드 이미지와 대체 텍스트도 같은 문구로 갱신.
- 우하단 패널: 폭 32→28cqw. 높이 10cqw 고정. 영어 초기 모델·Carry & Shoot의 두 줄 이름을 포함한 최종 HUD의 가로·세로 넘침 없음.
- 네 모서리 HUD: 게임 영역 기준 1.5cqw 여백. 화면 해상도에 비례하되 문자열에 따라 이동하지 않음.
- 득점: RED GOAL / NAVY GOAL. 전체 폭 띠 및 팀별 색 유지.
- 시작 버튼: 경기 준비 / PREPARE MATCH.

## 고정 크기 검증

1600×1020 viewport에서 DOM의 실제 크기를 비교했다. [비교 수치](../../soccer/exhibition/qa/manager-v5-geometry.json)에 9개 비교 결과를 저장했다. 다음 항목은 글자 변경 전후 폭·높이가 동일했다.

1. 한국어·영어 시작 메뉴 버튼.
2. 경기 설정의 언어·선택 모델 이름 변경.
3. 신경망 선택 카드와 모달의 한국어·영어 변경.
4. 규칙형 선택 카드와 모달의 한국어·영어 변경.
5. 경기 HUD의 한국어·영어 변경.
6. RED/NAVY 및 한국어·영어 GOAL 배너.
7. 한국어·영어 상황판 행과 종료 버튼.
8. 가이드 Next page → Finish guide 버튼과 모달.
9. 점수 0→100 변경 시 점수판·조작 안내·감독 패널.

점수에는 세 자리 고정 공간과 tabular-nums, 모델 이름·판단에는 줄 수를 확보했다. 이는 시안의 문구와 예시 데이터에서 검증한 결과다. Unity 렌더링·실제 데이터의 무제한 수치 범위를 검증했다는 의미는 아니다.

## 정적 검사

[정적 검사 결과](static-checks-v5.json): HTML id 72개 중복 없음, 로컬 참조 11개 유효, KO/EN 번역 68개 모두 존재, SVG 11개 파싱 성공, 직접 JavaScript id 참조 유효. `node --check preview.js` 통과. 자산 생성 시 신경망 6개 원본 SHA 대조 성공. 브라우저 error/warn 0개.

## 최신 캡처

- [한국어 시작](../../soccer/exhibition/qa/manager-v5-home-ko.png) / [영어 시작](../../soccer/exhibition/qa/manager-v5-home-en.png)
- [한국어 신경망 목록](../../soccer/exhibition/qa/manager-v5-models-ko.png) / [영어 신경망 목록](../../soccer/exhibition/qa/manager-v5-models-en.png)
- [한국어 4개 규칙형](../../soccer/exhibition/qa/manager-v5-rules-ko.png) / [영어 4개 규칙형](../../soccer/exhibition/qa/manager-v5-rules-en.png)
- [한국어 경기 HUD](../../soccer/exhibition/qa/manager-v5-match-ko.png) / [영어 경기 HUD](../../soccer/exhibition/qa/manager-v5-match-en.png)
- [RED GOAL](../../soccer/exhibition/qa/manager-v5-goal-ko.png) / [NAVY GOAL](../../soccer/exhibition/qa/manager-v5-goal-en.png)
- [한국어 상황판](../../soccer/exhibition/qa/manager-v5-result-ko.png) / [영어 상황판](../../soccer/exhibition/qa/manager-v5-result-en.png)

## 작업 범위

수정 범위는 전시 디자인 폴더와 이전 검증 기록을 보관한 docs/archive/exhibition이다. 다른 미커밋 변경은 보존했다. Unity Assets/Packages/ProjectSettings, 실제 모델·경기·통계·입력 처리를 수정하지 않았다. 선방은 계속 미측정 UI이며 사전 학습 100만 회는 사용자 지정 시안 가정이다. [최신 설계 문서](../../soccer/exhibition/design-spec.md).
