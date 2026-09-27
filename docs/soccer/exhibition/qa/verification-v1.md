# 전시 UI 디자인 검증 기록

검토일: 2026-09-26. 범위는 `docs/soccer/exhibition`의 디자인 파일과 브라우저 미리보기다.

## 정적 확인

- [정적 검사 결과](static-checks.json): HTML id 56개 중복 없음, 로컬 참조 10개 유효, SVG 11개 XML 파싱 통과.
- 한국어·영어 57개 키 / 114개 번역 값이 비어 있지 않음. HTML이 참조하는 번역 키와 JavaScript의 직접 id 참조 누락 없음.
- `node --check preview.js` 통과.
- 단계 모델 6개와 MS2 1개를 실제 ONNX 파일의 SHA-256으로 확인. 토너먼트·registry의 기록과 일치. 모델은 복사·수정·실행하지 않음.
- 400K 간격과 실제 저장 step의 연결은 [모델 명세](../model-catalog.json)에 기록.

## 브라우저 동작 확인

Codex 인앱 브라우저에서 로컬 서버로 실행했다. 시안 자체는 외부 라이브러리·폰트·fetch를 사용하지 않아 `index.html`을 로컬 파일로 여는 구조도 지원한다. 이 검토에서는 HTTP 경로로 동작을 확인했다.

| 확인한 동작 | 결과 |
|---|---|
| 실행 시 임시 팀 로고 → 자동 시작 화면 | 확인 |
| 새로 고침 후 기본 한국어 | 확인 |
| 언어 버튼에서 동일 화면의 목록 열림 | 확인 |
| 영어 선택 후 시작·설정·가이드 문구 전환 | 확인 |
| 일본어·중국어 준비 중 항목 비활성 | 확인 |
| 한국어 이미지 가이드 Enter 1→2, Esc 종료 | 확인 |
| 영어 이미지 가이드 Enter 1→2→3→닫기 | 확인 |
| KO/EN 제작자 단일 이미지 및 Esc 닫기 | 확인 |
| 기본 5분 / 10분 선택 후 10:00 시작 | 확인 |
| 400K 간격 정확히 6개 선택지 | 확인 |
| RED 400K 선택 시 NAVY 2M 유지 | 확인 |
| 기존 평가 상대 정확히 5개, NAVY Carry-shot 선택 | 확인 |
| AI 시뮬레이션 H 입력 후에도 AI 상태 유지 | 확인 |
| 직접 플레이 RED 표시, H로 AI 전환·복귀 | 확인. HTML 상태 표시만 검증 |
| 예시 경기 종료 → 점수 2:1 / 슈팅 8:6 / 설정 10분 표시 | 확인. 예시 수치 |
| 결과에서 Esc를 눌러도 화면 유지 | 확인 |
| 결과 화면 55초 경과 후 유지 | 확인 |
| 종료 버튼 → 시작 화면 | 확인 |
| 브라우저 오류 로그 | 확인 시 error 0개 |

## 시각 확인 자료

- [한국어 시작 화면](mng-design-home-ko.png)
- [언어 선택 팝업](mng-design-language.png)
- [한국어 경기 설정](mng-design-setup-ko.png)
- [영어 경기 설정](mng-design-setup-en.png)
- [400K 모델 선택](mng-design-models-ko.png)
- [한국어 가이드](mng-design-guide-ko.png)
- [영어 가이드](mng-design-guide-en.png)
- [제작자 임시 화면](mng-design-credits-ko.png)
- [경기 HUD 시안](mng-design-match-ko.png)
- [최종 상황판](mng-design-result-ko.png)

한국어·영어 주요 화면의 이미지 로드, 텍스트 배치와 버튼 가림 여부를 확인했다. 작은 보조 문구는 1차 캡처 후 크기를 키웠으며 설정·모델·경기·상황판을 다시 캡처했다. 경기 이미지는 도식이며 실제 카메라 이동이나 선수 동작을 검증하지 않는다.

## 확인하지 않은 범위

- Unity 패키지 설치, LocalizationSettings/String Table/Asset Table/Addressables 생성·빌드
- Unity UXML/USS import, compile, Scene 연결, PlayMode와 실제 신경망 추론
- 실제 300초·600초 경기 종료 및 반복 경기의 통계 초기화
- 인간 킥의 슈팅 집계 기준과 정확성
- 실제 Unity 카메라 근접 조정과 전시 모니터의 시야·가독성
- Windows Player 생성·실행, 오프라인 Player 검증
- 실제 JTBC 경기 중계 점수판과 픽셀·서체·모션 일치 여부

위 항목은 [상세 설계](../design-spec.md)의 Unity 구현 단계에서 검증한다. 브라우저 결과를 게임 기능 구현 완료나 빌드 성공으로 보고하지 않는다.

## 변경 범위

이번 작업은 `docs/soccer/exhibition/` 신규 파일과 `docs/README.md`의 색인 한 행이다. 작업 전부터 있던 AGENTS/current-status/ms-v3-current/패스 모방학습 계획 변경은 그대로 보존했다. Assets, Packages, ProjectSettings, 모델·학습 결과는 수정하지 않았다. commit/push, 학습·평가는 수행하지 않았다.
