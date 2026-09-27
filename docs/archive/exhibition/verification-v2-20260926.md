# 전시 UI 디자인 v2 검증 기록

검토일: 2026-09-26. 범위는 `docs/soccer/exhibition`의 디자인 파일과 브라우저 미리보기다. Unity 실행·개발·빌드는 수행하지 않았다. [1차 시안 검증 기록](../../soccer/exhibition/qa/verification-v1.md)과 `mng-design-*.png`는 이전 자료다. 현재 화면은 `manager-v2-*.png`를 참고한다.

## 정적 확인

- [정적 검사 결과](static-checks-v2.json): HTML id 68개 중복 없음, 로컬 참조 11개 유효, SVG 11개 XML 파싱 통과.
- 한국어·영어 67개 키 / 134개 값. 누락·빈 문자열 없음. HTML 번역 키와 JavaScript 직접 id 참조 누락 없음.
- `node --check preview.js` 통과.
- 강화학습 6개 파일의 SHA-256이 토너먼트 기록과 일치. 0K / 400K / 800K / 1.2M / 1.6M / 2M 확인. 모델 복사·수정·실행 없음.
- 규칙형 감독: 회수 / 균형 / 운반슈팅 / 전체 규칙형 / 랜덤. 모두 rule 종류. MS2 독립 항목 없음.
- 언어 선택 DOM은 ko / en 두 항목뿐이다.
- Pretendard OTF 400–900 여섯 파일이 로컬 원본과 SHA-256 일치. 라이선스 동봉. SVG 로고·가이드·제작자는 같은 폰트의 윤곽으로 생성.

## 브라우저 확인

Codex 인앱 브라우저의 `http://127.0.0.1:8769/`에서 확인했다. 일반 좁은 패널과 전시용 가로 레이아웃 검토를 위한 1600×1020 viewport에서 검토했다. 게임 영역은 16:9다. 임시 viewport는 검토 후 해제한다.

| 동작·표현 | 확인 결과 |
|---|---|
| 로고 단독 페이지 | 게임 영역에는 팀 로고 이미지만 존재. 별도 문구 없음 |
| 페이드 → 시작 | 자동 전환 확인, 새로 고침 시 한국어 기본 |
| 시작 문구 | MANAGER 임시 로고, 요청한 한글 전시 문구·설명, 영어 전시 문구 일치 |
| 언어 목록 | Language는 KO/EN 동일. 한국어·English 두 항목만 표시 |
| 확대된 설명 | 시작·설정·모델·가이드·경기·결과 배치 확인 |
| Pretendard | 시각 확인 및 결과 팀명·점수·기록·설명 computed font family 확인. 게임 영역 전체에 공통 폰트 규칙 적용 |
| 설정 | 경기 준비 문구 없음. 플레이어 팀·상대 팀 강조, 기본 5분 |
| 시간 | 10분 선택 후 HUD 10:00 시작 |
| 강화학습 모델 | 여섯 단계 표시. RED 400K 선택 시 NAVY 2M 유지 |
| 규칙형 감독 | KO/EN 다섯 항목 순서, 랜덤의 유효 행동 한정 설명 표시 |
| 모델 반영 | 선택한 모델·종류가 각 팀 카드, 경기 HUD, 결과 모델명에 반영 |
| 경기 HUD | 좌상단 M 삭제, 우상단 MENIAC, 좌하단 키 안내, 우하단 양 팀 감독 현황 |
| 예시 감독 현황 | 시간 경과 후 판단·누적 보상 변경. 실제 추론으로 표시하지 않음 |
| 득점 배너 | RED 득점 / NAVY 득점 / NAVY GOAL 확인. 팀색 변경 |
| 직접 플레이 | RED 조작 안내, H → AI 표시 → H 복귀. HTML 상태만 검증 |
| 시뮬레이션 | H 이후에도 AI vs AI 유지, 키 안내 숨김 |
| 사람 포함 결과 | RED 8 = AI 6 + 사람 2 / NAVY 6 = AI 6 + 사람 0 |
| 시뮬레이션 결과 | RED 8 = AI 8 + 사람 0 / NAVY 6 = AI 6 + 사람 0 |
| 결과 유지 | 작업 중 결과 유지, Esc로 닫히지 않음. 종료 버튼으로 시작 복귀 |
| 한글 가이드 | 이미지 표시, Enter 1→2, Esc 종료 |
| 영어 가이드 | 이미지 교체, Enter 1→2→3→닫기 |
| 제작자 | 한국어 단일 이미지, Esc 복귀 |
| 브라우저 오류 | 최종 확인 시 error/warn 0개. 영어 설정 및 경기 패널의 검사 대상 텍스트 가로 넘침 없음 |

슈팅 합계와 감독 정보는 디자인의 표현·상태 전환 확인이다. 실제 경기 데이터나 인간 킥 집계를 검증한 결과가 아니다. 예시 경기 종료 버튼은 고정된 2:1 / 8:6 기록을 보여주는 검토 도구다.

## 최신 화면

- [한국어 시작](../../soccer/exhibition/qa/manager-v2-home-ko.png) / [영어 시작](../../soccer/exhibition/qa/manager-v2-home-en.png)
- [팀 로고](../../soccer/exhibition/qa/manager-v2-splash.png) / [두 언어 목록](../../soccer/exhibition/qa/manager-v2-language.png)
- [한국어 설정](../../soccer/exhibition/qa/manager-v2-setup-ko.png) / [영어 설정](../../soccer/exhibition/qa/manager-v2-setup-en.png)
- [강화학습 모델](../../soccer/exhibition/qa/manager-v2-models-ko.png) / [규칙형 감독](../../soccer/exhibition/qa/manager-v2-rules-ko.png)
- [한글 가이드](../../soccer/exhibition/qa/manager-v2-guide-ko.png) / [영어 가이드](../../soccer/exhibition/qa/manager-v2-guide-en.png) / [제작자](../../soccer/exhibition/qa/manager-v2-credits-ko.png)
- [한국어 HUD](../../soccer/exhibition/qa/manager-v2-match-ko.png) / [영어 HUD](../../soccer/exhibition/qa/manager-v2-match-en.png)
- [RED 득점](../../soccer/exhibition/qa/manager-v2-goal-red-ko.png) / [NAVY GOAL](../../soccer/exhibition/qa/manager-v2-goal-navy-en.png)
- [사람 슈팅 포함 결과](../../soccer/exhibition/qa/manager-v2-result-ko.png) / [시뮬레이션 결과](../../soccer/exhibition/qa/manager-v2-simulation-result-ko.png)

## 별도 구현·검증 대상

Unity Localization, Scene·UXML/USS·FontDefinition, 실제 모델 추론, 실제 경기 시간, 인간 킥의 슈팅 분류, 실제 보상·판단 연결, 카메라 근접 조정, Windows Player 빌드는 이번 범위에 포함하지 않는다. 경기장은 디자인 도식이다. JTBC 실제 2026 경기 프레임과 픽셀·모션 일치는 미확인 상태를 유지한다.

추가 지표는 [설계 문서](../../soccer/exhibition/design-spec.md)의 현재 수집 정보 표에 제안했다. 보상 조건에 종속되는 `GetCompletedPassCount`와 `GetValidShotCount`를 일반 패스 성공·유효 슈팅 카운터로 오인하지 않도록 구분했다.

## 변경 범위

이번 수정은 `docs/soccer/exhibition/` 안의 HTML·CSS·JavaScript·SVG·폰트·문구표·모델 매핑·검토 문서다. 기존 docs/README 색인 한 행과 작업 전부터 있던 AGENTS/current-status/ms-v3-current/패스 모방학습 계획 변경을 보존했다. Assets, Packages, ProjectSettings, 모델 원본·학습 결과는 수정하지 않았다. 학습·평가, commit/push는 수행하지 않았다.
