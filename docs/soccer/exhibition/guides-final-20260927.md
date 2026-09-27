# 가이드 확장 및 UI 최종 정리 · 2026-09-27

최신 Windows Player: `Builds/MNG_Exhibition/UI1-20260927T072438004Z/MANAGER.exe`.

## 반영 내용
- Unity Localization Asset Table의 한영 가이드를 3→5장으로 확장했다. 2장은 MNG_Command의 감독 명령 6가지, 3장은 MNG_PlayerSkill의 동작 12가지와 대기 안내다. 기존 조작법·시뮬레이션 안내는 4·5장으로 이동했다.
- 감독 용어는 현재 한영 HUD 번역과 동일하다. 선수 안내는 코드 식별자를 그대로 노출하지 않고 각 언어의 간단한 표현을 사용한다. PNG의 텍스트는 Pretendard 윤곽선으로 생성했다.
- 일시정지 제목 #14532D, 순백색 #FFFFFF 테두리 3px. 기존 제목·버튼 위치 및 배경 설정 유지.
- GOAL 배경 투명도 8% = alpha 0.92, 양쪽 골대 투명도 40% = alpha 0.60.
- 두 골키퍼의 킥 플레이트는 동일한 #E6FF00 재질이다. 몸통 색상·물리 및 학습용 프리팹 유지.
- 종료 버튼은 폭 340→220, x 655→695로 변경해 오른쪽 끝 915를 경기 준비 버튼과 맞췄다. 테두리 0, 글씨 #BFC3BC. 한영 공통 고정 크기다.

## 검증
- Unity Windows 빌드 성공: `Logs/Exhibition/build-20260927T072438004Z/build.log`.
- 실제 독립 Player에서 **34개 검사 통과, 오류 0**: `Logs/Exhibition/guides-final-20260927/player/result.json`.
- 두 언어에서 가이드 1~5장 순서대로 Enter 이동·마지막 장 닫기, Esc 닫기를 확인했다. 시작 화면·가이드·경기·일시정지·득점 캡처 19개를 보존했다.
- 실제 마우스로 종료 버튼을 눌러 종료 요청과 프로세스 exit code 0 확인. `quit-observed.json`, `process-exit.json`.
- MNG.Runtime.dll SHA256은 이전 게임플레이 검증 빌드와 동일: `85E5940A71605E66D1A42E325047FC69DB4896F9496614B108C41A268E40B0FC`. 이번에는 UI·재질 범위를 검증했으며 5분 경기 및 전체 게임플레이 검사는 재실행하지 않았다. 기존 92항목 검증은 `polish-20260927.md`에 남아 있다.
- ProjectSettings.asset / EditorBuildSettings.asset은 작업 전 파일로 복구하고 해시 일치를 확인했다.

## 이전 빌드 정리
사용자가 이번 요청에서 이전 UI 관련 빌드 삭제를 명시적으로 승인했다. 최신 검증 빌드를 보존 목록에 등록하고 `Builds/MNG_Exhibition/UI1-*`의 이전 빌드 12개(논리 용량 2,061,193,075 bytes, 약 2.06GB)를 삭제 완료했다. 해당 전시 경로에는 최신 빌드 1개만 남았다. 실제 삭제 결과와 원래 파일 목록·주요 해시·메타데이터는 `Logs/Exhibition/guides-final-20260927/retired-builds/cleanup.json` 및 하위 폴더에서 확인한다. 학습/평가 빌드, 원본 모델, 로그, 스크린샷, 과거 보고서는 보존한다. 과거 보고서에 기재된 UI 빌드 경로는 역사적 실행 기록이며 삭제된 바이너리의 백업을 의미하지 않는다.

## 재생성
- `Tools/prepare-exhibition-guides.py`: fontTools를 사용해 새 가이드 2·3장과 페이지 번호가 변경된 4·5장 SVG 생성.
- `Tools/render-exhibition-guides.cjs`: Sharp로 Unity용 PNG 생성. NODE_PATH는 설치된 Sharp 경로를 사용한다.
- `Tools/Build-Exhibition.ps1`: Windows 빌드. 가이드 원본은 `guide-art`, 디자인 이전 원본은 `docs/archive/exhibition/ui-v1-final-20260927/assets`에 보존한다.

주간 사용량 확인: 사용 11%, 잔여 89%. 4% 이하 저장·중단 조건 유지.
