# 전시 UI 디자인 v3 검증 기록

검토일: 2026-09-26. HTML·CSS·JavaScript·문구표·SVG 디자인 패키지만 수정했다. Unity 개발, 실제 선방 관측, 학습·평가·빌드는 수행하지 않았다.

이전 자료: [v2 검증 기록](verification-v2-20260926.md). `manager-v2-*`와 `mng-design-*` 이미지는 이전 버전이며 최신 캡처는 `manager-v3-*`다.

## 정적 확인

- [정적 검사](static-checks-v3.json): HTML id 73개 중복 없음, 로컬 참조 11개 유효, 번역 70개 키 모두 KO/EN 값 존재, SVG 11개 XML 파싱 통과.
- JavaScript의 직접 id 참조 누락 없음. `node --check preview.js` 통과.
- 삭제한 modeLabel은 HTML과 JavaScript에 남아 있지 않음.
- 디자인 자산 생성기 실행 성공. 강화학습 6개 모델 출처·SHA 검증 및 규칙형 5개 목록 유지. 원본 모델은 수정하거나 실행하지 않음.

## 브라우저 확인

인앱 브라우저, 로컬 서버 `http://127.0.0.1:8769/`. 1600×1020 viewport에서 가로 16:9 게임 영역과 한국어·영어 화면을 확인했다. 검토 후 임시 viewport를 해제한다.

| 사용자 변경 요청 | 확인한 결과 |
|---|---|
| 규칙형 영문 우선·한글 병기 | 두 언어 모두 Recover (회수) → Balanced (균형) → Carry & Shoot (운반슈팅) → Full rules (전체 규칙형) → Random (랜덤) |
| 상황판 순서 | 최종 누적 보상 → 전체 슈팅 → AI 슈팅 → 사람 슈팅 → 공 회수 → 선방 횟수 |
| 슈팅 구분 보존 | RED 8 = 6 + 2, NAVY 6 = 6 + 0의 예시 표시 유지 |
| 선방 UI | 양 팀 `—` 표시. 측정되지 않은 값을 0으로 취급하지 않음. 화면 밖 시안 안내에서 관측 연결 예정임을 설명 |
| 경기 우상단 | MENIAC 텍스트 요소 하나만 존재. 전시 경기 문구 제거 |
| 경기 좌하단 | RED 직접 플레이 문구와 배경 제거. 키 안내만 표시 |
| HUD 축소 | 좌상단 82% 배율, 키 안내 84% 및 내용에 맞춘 배경 폭, 우하단 폭 45→32cqw와 행·여백·폰트 축소 |
| 축소 후 가독성 | 한국어·영어 감독 이름·현재 판단·보상과 키 안내에 검사 대상 가로 넘침 없음 |
| 득점 띠 | RED/NAVY 득점 표시. 띠 좌우 좌표 16/1584, 폭 1568로 게임 영역과 일치 |
| 영어 시작 문구 | Human Centered AI Major · EXHIBITION 2026. 우측 상단 배치. 한국어 배치 유지 |
| 결과 유지·복귀 | Esc 이후 유지. 종료 버튼으로 시작 복귀 |
| 브라우저 로그 | 확인 시 error/warn 0개 |

보상 +2.450 / +1.870과 공 회수 12 / 9는 시안 예시다. 실제 보상·공 회수·슈팅 집계와 연결하지 않았다. 선방은 UI만 추가했으며 관측 카운터 구현을 시작하지 않았다.

## 최신 캡처

- [영문·한글 감독 이름](../../soccer/exhibition/qa/manager-v3-rules-ko.png)
- [작아진 한국어 경기 HUD](../../soccer/exhibition/qa/manager-v3-match-ko.png) / [영어 HUD](../../soccer/exhibition/qa/manager-v3-match-en.png)
- [화면 전체 폭 RED 득점](../../soccer/exhibition/qa/manager-v3-goal-red-ko.png) / [NAVY GOAL](../../soccer/exhibition/qa/manager-v3-goal-navy-en.png)
- [한국어 상황판](../../soccer/exhibition/qa/manager-v3-result-ko.png) / [영어 상황판](../../soccer/exhibition/qa/manager-v3-result-en.png)
- [영어 시작 화면의 우측 상단 문구](../../soccer/exhibition/qa/manager-v3-home-en.png)

## 범위

작업 전의 AGENTS.md, docs/README.md, current-status.md, ms-v3-current.md, 패스 모방학습 계획 변경을 보존했다. 수정 범위는 전시 디자인 폴더와 이전 검증 기록 보관을 위한 docs/archive/exhibition이다. Assets/Packages/ProjectSettings 변경, Unity 실행, 모델 변경, 학습, 평가 실행, commit/push는 없다. 현재 디자인 계약은 [설계 문서](../../soccer/exhibition/design-spec.md)에 반영했다.
