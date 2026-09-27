# UI 1차 최종본 구현 현황

**2026-09-27 시작 화면 종료 버튼·색상·투명도 수정 완료.**

- 최신 실행 파일: `Builds/MNG_Exhibition/UI1-20260927T070233440Z/MANAGER.exe`
- [실행 안내](player-guide.md) / [이번 수정 기록](menu-colors-20260927.md) / [완료 명세](implementation-completion.json)
- 실행 파일과 MANAGER_Data, DLL 등 빌드 폴더 전체를 함께 사용한다.

## 이번 변경

- 하단 영어 문구 대신 **종료하기 / Quit game** 버튼. 클릭 시 게임 프로그램을 종료한다.
- 하이드레이션 브레이크 제목: 밝은 초록색 #76D943.
- GOAL 배경: 투명도 15%(alpha 0.85).
- 양쪽 골대: 투명도 75%에서 60%로 15%p 감소(alpha 0.40).
- 골키퍼 몸통: RED는 선명한 분홍색 #F05278, NAVY는 선명한 하늘색 #429CF2.
- 한국어·영어 동일 반영. 기존 카메라 전환, 이동 보간, 사람 패스·슈팅, 선방 접촉 집계 유지.

## 최신 검증

- 독립 Player 한영 UI 검사 **14개 통과, 수집 오류 0**.
- 종료 버튼의 실제 마우스 클릭, 종료 요청, **프로세스 exit code 0** 확인.
- [시작 화면](qa-menu-colors-20260927/home-ko.png), [영어 시작](qa-menu-colors-20260927/home-en.png), [일시정지](qa-menu-colors-20260927/pause-ko.png), [득점](qa-menu-colors-20260927/goal-ko.png) 등 실제 캡처 9개.
- 증거: `Logs/Exhibition/menu-colors-20260927/player-3`.
- MNG.Runtime.dll은 직전 빌드와 동일한 SHA256. 이번 변경 범위는 전시 UI·재질·종료 기능이다. 직전의 92항목 게임플레이 검사, 실제 5분 경기 및 Manager 회귀 결과는 [이전 수정 기록](polish-20260927.md)에 보존한다.
- 프로젝트/빌드 설정은 작업 시작 전으로 복구. 기존 모델·학습 계약 및 미커밋 변경 유지.
- Build-Lifecycle Register/Use/Review 완료. 이전 빌드·실패 기록 보존.
- 마지막 확인 주간 잔여량 **90%**, 사용자 지정 4% 저장·중단 기준 유지.

다음 수정은 이 문서와 `menu-colors-20260927.md`에서 이어간다. 재빌드는 `Tools/Build-Exhibition.ps1`을 사용한다. [이번 작업 전 현황](../../archive/exhibition/implementation-status-before-menu-colors-20260927.md)은 별도로 보존했다.