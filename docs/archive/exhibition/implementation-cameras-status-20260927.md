# UI 1차 최종본 구현 현황

**2026-09-27 마우스 시선·Cinemachine 추적·골키퍼 시점·카메라 HUD 여백 축소 완료.**

- 최신 실행 파일: `Builds/MNG_Exhibition/UI1-20260927T090744798Z/MANAGER.exe`
- [실행 안내](player-guide.md) / [이번 수정 기록](cameras-20260927.md) / [완료 명세](implementation-completion.json)
- MANAGER_Data와 DLL을 포함한 빌드 폴더 전체를 함께 사용한다.

## 현재 카메라

- 사람 모드 마우스: 시선만 좌우 최대60도, 입력이1.5초 없으면 정면으로 부드럽게 복귀. 선수 이동/회전 명령 유지.
- Cinemachine Follow 위치 감쇠0.2. H로 사람/AI 전환 시 기존0.8초 이동 효과 유지.
- AI 모드 화살표: RED 골키퍼 ↔ 중계 ↔ NAVY 골키퍼 순환. 초기 중계, 왼쪽 RED, 오른쪽 NAVY. AI 시점 전환은 즉시 Cut.
- 선수 시점 배지 RED: ST / RED: GK / NAVY: GK만 표시한다. 중계는 이름 표기 없음. 배경은 매우 어두운 빨강/남색, 투명도5%, 흰색 글씨다.
- 추가 피드백으로 배지 폭180→128. 카메라 안내 폭도 모드·언어별로 축소했고 각 상태에서는 고정 크기다.
- 기존 한영5장 가이드, 시작 로고, 일시정지 제목, 득점 배경, 골키퍼 색상/선방 집계 유지. Unity 시작 로고 비활성화 유지.

## 검증

- 여백 축소 최종 Player에서 **카메라·한영 UI122개 검사 통과, 오류0**, 실제 종료 버튼 클릭 후 exit code0.
- 동일 카메라 기능의 여백 축소 직전 빌드에서 **기존 게임플레이92개 검사 통과, 오류0**, 실제300초 경기를2배속으로 완료하고 상황판 유지 확인. 이동·패스·슈팅·선방·카메라 제어 전환·일시정지 포함.
- 증거: `Logs/Exhibition/cameras-20260927/compact-player-final` 및 `full-player-final`.
- [RED 골키퍼](qa-cameras-20260927/camera-red-gk-ko.png), [NAVY 골키퍼 영어](qa-cameras-20260927/camera-navy-gk-en.png), [사람 모드](qa-cameras-20260927/camera-human-front-ko.png), [중계](qa-cameras-20260927/camera-broadcast-en.png).
- 공통 MNG.Runtime.dll은 이전과 SHA256 동일. 학습용 prefab·모델·보상/관측 계약 유지. 전시 전용 카메라를 추가하고 Cinemachine3.1.7 및 의존 패키지2개를 설치했다.
- ProjectSettings/EditorBuildSettings는 작업 전 해시로 복구했다.
- 최신 UI 빌드1개만 보존한다. 정리 증거: `Logs/Exhibition/cameras-20260927/retired-builds/cleanup.json`.
- 주간 잔여량 마지막 확인85%,4% 이하 저장·중단 기준 유지.

다음 수정은 이 문서와 `cameras-20260927.md`에서 이어간다. 재빌드는 `Tools/Build-Exhibition.ps1`을 사용한다.