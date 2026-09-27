# UI 1차 최종본 구현 현황

**2026-09-27 일시정지 제목 배경·Unity 시작 로고 제거 및 최신 Windows 빌드 검증 완료.**

- 최신 실행 파일: `Builds/MNG_Exhibition/UI1-20260927T074211233Z/MANAGER.exe`
- [실행 안내](player-guide.md) / [이번 수정 기록](pause-splash-20260927.md) / [완료 명세](implementation-completion.json)
- 실행 파일과 MANAGER_Data, DLL 등 빌드 폴더 전체를 함께 사용한다.

## 이번 변경

- 한영 가이드 5페이지: RED 소개 → 감독 선택 6가지 → 선수 행동 12가지 → 조작키 → AI 시뮬레이션.
- 일시정지 제목: 밝은 하늘색 #EAF7FF, 테두리 없음. 제목 전용 남색 #101C38 사각형 배경(투명도5%). Unity 시작 화면·로고를 비활성화하고 MENIAC 로고는 유지.
- GOAL 배경 투명도 8%(alpha 0.92), 양쪽 골대 투명도 40%(alpha 0.60).
- 두 골키퍼 킥 플레이트: 동일한 형광 노랑 #E6FF00.
- 종료 버튼: 테두리 제거, 연한 회색 글씨, 폭 축소 및 경기 준비 버튼 오른쪽 끝에 정렬.
- 한국어·영어 동일 반영. 기존 카메라·사람 조작·선방 집계·학습 계약 유지.

## 최신 검증

- 독립 Player 한영 UI 검사 **36개 통과, 오류 0**. 두 언어의 가이드 Enter/Esc 동작과 변경된 스타일·재질 확인.
- 종료 버튼 실제 마우스 클릭 및 프로세스 **exit code 0** 확인.
- [한국어 감독 안내](qa-guides-final-20260927/guide-ko-2.png), [한국어 선수 안내](qa-guides-final-20260927/guide-ko-3.png), [영어 선수 안내](qa-guides-final-20260927/guide-en-3.png), [일시정지](qa-pause-splash-20260927/pause-ko.png) 등 실제 캡처 19개 보존.
- 증거: `Logs/Exhibition/pause-splash-20260927/player`.
- MNG.Runtime.dll은 직전 빌드와 SHA256 동일. 기존 92항목 게임플레이·5분 경기·Manager 회귀 검증은 [이전 기록](polish-20260927.md)에 보존한다. 이번에는 UI·재질 범위를 검증했다.
- 프로젝트/빌드 설정은 작업 시작 전과 바이트 동일하게 복구했다.
- 최신 빌드만 유지한다. 이전12개 정리 기록은 guides-final-20260927에, 이번 교체 삭제 기록은 `Logs/Exhibition/pause-splash-20260927/retired-builds/cleanup.json`에 보존한다.
- 마지막 확인 주간 잔여량 **88%**, 4% 이하 저장·중단 기준 유지.

다음 수정은 이 문서와 `pause-splash-20260927.md`에서 이어간다. 재빌드는 `Tools/Build-Exhibition.ps1`을 사용한다.