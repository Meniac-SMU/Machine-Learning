# UI 1차 최종본 구현 현황

**2026-09-27 카메라·이동 보간·골키퍼 통계 후속 수정 완료.**

- 최신 실행 파일: `Builds/MNG_Exhibition/UI1-20260926T185254685Z/MANAGER.exe`
- [실행 안내](player-guide.md) / [이번 수정·검증 기록](polish-20260927.md) / [완료 명세](implementation-completion.json)
- 실행 파일과 `MANAGER_Data`, 런타임 DLL 등 빌드 폴더 전체를 함께 보존한다. README와 Pretendard 라이선스를 포함했다.

## 이번 수정

- H 카메라 전환: 위치·회전·화각을 약 0.8초 동안 이동, 반대 방향 전환도 동일 적용.
- RED 공격수 머리 표식: 몸통 상단에 밀착.
- 전시 선수 8명과 공의 렌더 보간: 사람·감독·시뮬레이션 모두 적용. 학습용 프리팹에는 추가하지 않음.
- 하이드레이션 브레이크: 진한 녹색. 2배속 배경: 매우 연한 빨강. 한국어·영어 공통 적용.
- 골대: 고정 반투명 재질(alpha 0.25), 콜라이더 유지. 골키퍼 몸통: RED 분홍색, NAVY 하늘색.
- 선방: 골키퍼의 실제 몸통·킥 플레이트 공 접촉 횟수. 지속 접촉은 1회, 떨어진 뒤 재접촉은 추가. 골 이후 유지, 새 경기 초기화, 상황판에 양 팀 표시.
- 사람 패스: AI와 같은 거리별 세기 계산으로 통일. 기존 전방 30m 조준에서 20.67 → 34.67m/s 요청. 슈팅은 기존부터 양쪽 동일. E 패스는 슈팅으로 집계하지 않음.
- 이동 능력: 사람/AI 요청 상한 9m/s, 모터 가속도 30m/s²로 동일. 동일 바닥·감쇠 조건의 실제 정상 속도도 **8.46m/s로 일치**.

## 검증 결과

|검증|결과|증거 (`Logs/Exhibition/polish-20260927` 아래)|
|---|---|---|
|최종 독립 Player|92개 통과, 수집 오류 0|`player-2/result.json`|
|Manager PlayMode|35개 최종 통과, 진단 2개 제외. 최초 34통과·카메라 검사 1실패 후 검사 대기 보완 및 해당 항목 1통과|`playmode.xml`, `camera-playmode.xml`|
|사람/AI 실제 속도|양쪽 8.46m/s|`player-2/physical-speed-parity.txt`|
|실제 패스·슈팅|물리 접촉, 요청 속도, 슈팅 구분, 쿨다운, 원격 타격 없음|`player-2/physical-E.txt`, `physical-Space.txt`|
|이동 보간|물리 위치 사이 렌더 위치 확인. 24프레임 표본 평균 16.67ms; 전용 CPU 벤치마크 아님|`player-2/interpolation-samples.csv`|
|선방|양 팀 실제 몸통 2회·플레이트 1회, 지속 접촉 중복 방지·재접촉·득점 후 유지·초기화|`player-2/result.json`, `18-keeper-counts-ko.png`|
|전체 경기|2배속 실제 300 게임초 완주, 결과 유지·홈 복귀|`player-2/result.json`|
|모델·설정|원본/사본 모델 6종 해시 일치. 프로젝트/빌드 설정 실행 전 상태로 복구|`model-hash-audit.json`, `settings-audit.json`|

[실제 Player 캡처 18개](qa-unity-polish-20260927): [사람 경기](qa-unity-polish-20260927/09-match-ko.png), [일시정지](qa-unity-polish-20260927/10-pause-ko.png), [영어 시뮬레이션](qa-unity-polish-20260927/12-match-en.png), [접촉 집계 결과](qa-unity-polish-20260927/18-keeper-counts-ko.png). 마지막 이미지는 실제 충돌을 격리해 시험한 상황판이며 일반 경기 성적을 뜻하지 않는다.

## 보존 및 다음 작업

- 신경망 관측244/명령6, 보상, 원본 모델, 학습/평가 자격과 기존 미커밋 변경을 보존했다. 학습·모방학습·모델 승격은 실행하지 않았다.
- `ProjectSettings.asset`와 `EditorBuildSettings.asset`는 이번 작업 시작 전과 바이트 단위로 일치한다.
- Build-Lifecycle Register/Use/Review 완료. 이전 빌드와 실패 증거도 보존했다.
- 주간 잔여량 **92%** 확인. 사용자 지정 4% 저장·중단 기준을 유지한다.
- 재빌드: `Tools/Build-Exhibition.ps1`. 다음 수정은 이 문서와 `polish-20260927.md`에서 이어간다.
- 이전 이력: [HUD·사람 조작 수정](revision-20260927.md), [이번 작업 전 전체 현황](../../archive/exhibition/implementation-status-before-polish-20260927.md), [최초 시안 동결](../../archive/exhibition/ui-v1-final-20260927).