# 시작 화면 종료 버튼과 색상·투명도 수정

2026-09-27 사용자 요청을 한국어·영어에 반영하고 Windows 빌드·실행 검증을 완료했다.

최신 실행 파일: `Builds/MNG_Exhibition/UI1-20260927T070233440Z/MANAGER.exe`.

|항목|변경|
|---|---|
|시작 화면 하단|REINFORCEMENT LEARNING FOOTBALL 문구 제거, 같은 위치에 종료하기 / Quit game 버튼 추가|
|게임 종료|버튼 클릭 시 Application.Quit(0) 호출. 경기 중 경기 종료/End match는 기존처럼 홈 복귀|
|일시정지 제목|진한 녹색 #14532D → 밝은 초록색 #76D943|
|GOAL 배경|투명도 30% → 15%, alpha 0.70 → 0.85|
|양쪽 골대|투명도 75% → 60%, alpha 0.25 → 0.40. 15%만큼 낮추라는 요청을 15%p 감소로 적용|
|RED 골키퍼 몸통|#F18FAE → #F05278, 빨강에 가까운 선명한 분홍색|
|NAVY 골키퍼 몸통|#86CFFA → #429CF2, 파랑에 가까운 선명한 하늘색|

종료 버튼은 기존 하단 문구의 중심 위치를 유지하고 340×66의 고정 영역을 사용한다. 한국어·영어 문자열은 Unity Localization 테이블에서 관리한다. 기존 이동·카메라·패스·슈팅·선방 집계는 변경하지 않았다.

## 검증

- 최종 독립 Player에서 한/영 14개 UI 검사 통과, 수집 오류 0.
- 마우스 입력으로 실제 종료 버튼 클릭 → 종료 요청 로그 및 wantsToQuit 확인 → 프로세스 exit code 0으로 종료 확인.
- 시작·경기·일시정지·RED/NAVY 득점 화면 캡처 9개: `qa-menu-colors-20260927`.
- 증거: `Logs/Exhibition/menu-colors-20260927/player-3/result.json`, `quit-observed.json`, `process-exit.json`.
- 첫 검사에서는 Localization 비동기 완료 전 검사했고, 다음 검사에서는 마지막 홈 화면의 레이아웃 완료 전 클릭했다. 검증 대기 시점을 보완해 최종 실행에서 모두 통과했다. 실패 기록도 보존한다.
- 기존 MNG.Runtime.dll SHA256이 직전 검증 빌드와 동일하다. 이번에는 전시 UI·표시 재질·종료 기능만 변경하여 해당 범위의 실제 Player 검증을 수행했다. 이전 92항목 게임플레이 및 Manager 회귀 결과는 `polish-20260927.md`에 별도 보존한다.
- ProjectSettings/EditorBuildSettings는 작업 시작 전 상태로 복구. 새 빌드 Register/Use/Review 기록 완료.
- 마지막 확인 주간 잔여량 90%. 4% 이하 저장·중단 기준 유지.
