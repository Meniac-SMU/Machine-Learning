# SAICCER

> 2026-10-18 최신 실행기: `Builds/Manager5v5/saiccer-pause-colors-20261018-r001/Play-SAICCER.cmd`. 재개 흰남색·종료 기존남색, 감독UI와 버튼 간격28 통일, 로고 테두리2px, 근접 체력바3cm 하향.16:10 1384·Player785·HUD3480 통과. 최근3빌드 보존. [85번 기록](docs/soccer/rebuild-5v5/85_일시정지색상_간격통일_체력바미세조정.md). 아래 최신 표기는 이전 이력이다.

> 2026-10-18 최신 실행기: `Builds/Manager5v5/saiccer-pause-20261018-r001/Play-SAICCER.cmd`. 양 화면비 일시정지 버튼/HUD 겹침 해소·제목 하향·남색 버튼·로고 테두리1.5px.16:10 1368·Player785·HUD3464 통과. 사용자 지시로 최근3개 빌드만 보존, 이전 빌드 경로는 실행 불가. [84번 기록](docs/soccer/rebuild-5v5/84_일시정지겹침수정_남색버튼_최근3빌드정리.md). 아래 최신 표기는 이전 이력이다.

> 2026-10-18 최신 실행기: `Builds/Manager5v5/saiccer-overlay-20261018-r001/Play-SAICCER.cmd`.16:10 모델선택·일시정지 전체 반투명 배경, 경기 HUD 위아래 모서리 고정, 모든 카메라 흰색SAICCER·얇은남색테두리.16:10 1348·Player785·HUD3440 통과. [83번 기록](docs/soccer/rebuild-5v5/83_16대10_전체반투명배경_모서리HUD_흰색로고.md). 아래 최신 표기는 이전 이력이다.

> 2026-10-18 최신 실행기: `Builds/Manager5v5/saiccer-laptop-20261018-r005/Play-SAICCER.cmd`. 원본512px 언어 아이콘·근접 체력바 상향·커서38px·벽 투명90%. 시작 화면16:10 전체 높이 대응 및 레터박스 제거.16:10 1308·Player785·HUD3348 검사 통과. [82번 기록](docs/soccer/rebuild-5v5/82_언어아이콘_체력바_커서38_16대10검증.md). 아래 최신 표기는 이전 이력이다.

> 2026-10-18 최신 실행기: `Builds/Manager5v5/saiccer-surface-20261018-r003/Play-SAICCER.cmd`. 모든 카메라 연두SAICCER, 벽 투명도85%, 네 벤치 원높이 복원, 주변 바닥·대기 선수 발Y=-0.01m. 골대 바닥은 불투명한 연한 RED/NAVY. 양골대48시점의 겹침 표본8,089→0. Player785/785·HUD3348/3348 통과. [81번 기록](docs/soccer/rebuild-5v5/81_연두로고_벽투명85_벤치복원_골대바닥겹침해소.md). 아래 최신 표기는 이전 이력이다.

> 2026-10-18 최신 실행기: `Builds/Manager5v5/saiccer-ground-20261018-r004/Play-SAICCER.cmd`. 시작/득점 재개 시 일반 선수 기준점 반경2.5m 무작위·GK고정. 전체 관전에서만SAICCER흰색, 다른시점연하늘색. 주변바닥Y=0 및네벤치·대기6명발높이정렬. Player780/780·HUD3348/3348·기존PlayMode15/15 통과. [80번 기록](docs/soccer/rebuild-5v5/80_재배치반경25m_관전흰로고_주변바닥높이.md). 아래 바닥 검토만 수행 및 최신 표기는 이전 이력이다.

> 2026-10-18 최신 실행기: `Builds/Manager5v5/saiccer-independent-20261018-r002/Play-SAICCER.cmd`. 벤치 선수 개별 이동·대기·목표 위치, 영역30m·속도0.5m/s·중심 최소3m. 득점 후 선수만 고정하고 공 물리 유지, Esc와 킥오프 복원 확인. 주변 바닥 약0.63m 높이 차이는 검토만 수행·미변경. Player768/768·기존 조작/학습 PlayMode15/15 통과. [79번 기록](docs/soccer/rebuild-5v5/79_벤치개별움직임_득점후공유지_바닥높이검토.md). 아래 최신 표기는 이전 이력이다.

> 2026-10-18 최신 실행기: `Builds/Manager5v5/saiccer-bench-range-20261018-r002/Play-SAICCER.cmd`. 커서36px, 팀별 벤치 영역 폭30m·이동속도0.5m/s. 간격을 유지하며 기준자리에서 약±11.8m 이동, 팀 영역/물리/감독관측 분리 유지. 실제Player749/749 및 화면 검증 통과. [78번 기록](docs/soccer/rebuild-5v5/78_커서36_벤치영역30m_속도05.md). 앞선10m·30%감속 요청은 최신30m·0.5 지시로 대체됐으며 아래 최신 표기는 이전 이력이다.

> 2026-10-18 최신 실행기: `Builds/Manager5v5/saiccer-benches-20261018-r003/Play-SAICCER.cmd`. 관전 위쪽 두 벤치 앞에 팀별 일반2·GK1, 총6명 추가. 팀별독립GK추첨·공추적·영역/간격 제한, 메시전용으로 물리/감독관측 분리. 경기브랜드 연하늘색·커서10%확대·시작로고/설정글자 확대·Your Team우측정렬·VS중앙화. Player743/743·HUD3340/3340 및 실제 화면 확인 통과. [77번 기록](docs/soccer/rebuild-5v5/77_벤치교체선수와_로고_설정가독성.md). 아래 최신 표기는 이전 이력이다.

> 2026-10-18 최신 실행기: `Builds/Manager5v5/saiccer-cursor-hq-20261018-r003/Play-SAICCER.cmd`. 512px 원본을 32px로 선축소하던 커서를 128px 텍스처/28.8px 표시·반시계17.5도 UI 커서로 개선했다. 신경망 영역은 유지하면서 버튼150·글자 확대, 플레이 모드 RED에 Your Team, 선택 버튼 밝은 초록 적용. Player687/687·HUD3336/3336 및 실제 화면 검증 통과. [76번 기록](docs/soccer/rebuild-5v5/76_커서해상도_175도_설정가독성_YourTeam.md). 아래 최신 표기는 이전 이력이다.

> 2026-10-18 최신 실행기: `Builds/Manager5v5/saiccer-cursor-20261018-r002/Play-SAICCER.cmd`. 팀 로고/경기 중 커서 숨김, 메뉴/설정/정지/결과에 19도·32px 콘 커서 적용. 설정 그라데이션 중앙 흰색70%·팀 색30%, 카드/로봇 크기를 유지하며 신경망 선택 높이129→181. Player658/658·HUD3336/3336 및 실제 캡처 검증 통과. [75번 기록](docs/soccer/rebuild-5v5/75_콘커서_화면별표시_설정카드확장.md). 아래 최신 표기는 이전 이력이다.

> 2026-10-18 설정 로봇 미리보기를 측면으로 5도 더 돌려 바퀴 노출을 늘렸다. 최신 실행기: `Builds/Manager5v5/saiccer-preview-20261018-r001/Play-SAICCER.cmd`. 실제 Player611/611 및 화면 확인 통과. [74번 기록](docs/soccer/rebuild-5v5/74_설정로봇_측면5도_미리보기.md). 아래 최신 표기는 이전 이력이다.

> 2026-10-18 후속 UI 완료. 최신 실행기: `Builds/Manager5v5/saiccer-ui-20261018-r001/Play-SAICCER.cmd`. 영어 모델명은 `400K steps`/`3.6M steps`, 점수판은 `400K`/`3.6M`, 0회는 `0 steps`다. 시작 하단은 제작팀 **MENIAC**, 큰 게임 로고는 **SAICCER**. 설정 화면 상하 여백을 같게 맞추고 RED/NAVY 카드에 기존 색→흰색85% 혼합→기존 색의 대각선 그라데이션을 적용했다. 실제 Player611/611·HUD3254/3254, 한·영720p/1080p 캡처와 픽셀 검사 통과. [73번 기록](docs/soccer/rebuild-5v5/73_영문모델명_MENIAC_설정여백_그라데이션.md). 아래 최신 표기는 이전 이력이다.

게임 이름은 **SAICCER**다. 최신 Windows 실행기는 [Play-SAICCER.cmd](Builds/Manager5v5/saiccer-20261018-r003/Play-SAICCER.cmd). 시작 로고 AI 연두색, 카메라 ±40°, 방향키·1/0 추가, RED CUBE/NAVY CUBE 선택 영역, 종료 통계 설명 한 줄 삭제를 반영했다. 실제 Player 585개·HUD 3,147개 검사와 한·영 720p/1080p 화면 확인을 통과했다. [2026-10-18 변경·검증](docs/soccer/rebuild-5v5/72_SAICCER_게임명_조작키_캐릭터선택영역.md). 아래 최신 표기는 이전 이력이다.

> 2026-10-18 최신 시연: `Builds/Manager5v5/demo-pitch-20261018-r004/Play-MENIAC.cmd`. 초록색 바닥·흰선 메시와 재질 분리, 기존 선 중심을 기준으로 폭50%, 레드 선수 카메라 최초 거리 대비25% 접근. 경기장 bounds·치수·충돌 보존 및 실제 Player455/455 검사·화면 확인. 아래 최신 표기는 이전 이력이다. [71번 기록](docs/soccer/rebuild-5v5/71_잔디라인분리_선폭절반_카메라25.md).

> 2026-10-17 최신 시연: **로봇 4종 및 빨강·분홍 눈 재질 밝기 50% 적용**. 실행기는 `Builds/Manager5v5/demo-robots-20261017-r001/Play-MENIAC.cmd`. 실제 Player 114/114 검사, 4종 각각 드리블·패스·슈팅, 게임 시간 300초 경기 통과. 선수당 BoxCollider 4개·몸체 0.9m 유지. 아래 이전 최신 표기는 이력이다. [69번 실행과 검증](docs/soccer/rebuild-5v5/69_로봇4종_시연빌드와_눈밝기50.md).

Unity `6000.3.16f1`, ML-Agents `4.0.3` 기반 Soccer 4v4 프로젝트다. 활성 게임과 과거 실험을 분리했으며, 팀별 강화학습과 Rule FSM을 같은 경기 계약에서 비교한다.

현재 개발 중심은 **MNG 감독 RL: MS2-v3 출발 기준 / MS3-v3 학습 전 D5 당시 준비 완료**다. 실제 v3 학습은 미시작이며, 2026-09-25 확인된 기존 준비 소스 SHA 불일치는 실행 전에 재검증해야 한다. 기존 선수 학습은 별도 보존 경로다.

기본 탐색은 [문서 색인](docs/README.md), [현재 상태](docs/soccer/current-status.md), [Tools 색인](Tools/README.md)으로 한정한다. 과거 자료는 [Archive](docs/archive/README.md)에서 필요한 파일만 선택한다.

## 빠른 시작

1. 저장소 루트를 Unity `6000.3.16f1`로 연다.
2. [문서 색인](docs/README.md)에서 역할에 맞는 문서를 선택한다.
3. 현재 구현과 남은 작업은 [Soccer 현재 상태](docs/soccer/current-status.md)를 확인한다.
4. Unity batch 실행이 라이선스에서 멈추면 [설정 및 검증](docs/project/setup-and-validation.md)의 Hub 충돌 우회를 따른다.

## 주요 폴더

```text
Assets/
├─ _Soccer/                  활성 Soccer 코드·Scene·Prefab·팀 작업공간
│  ├─ Manager/              현행 감독 RL
│  ├─ Curriculum/           기존 선수 커리큘럼 보존
│  ├─ Core/
│  └─ Teams/
│     ├─ Attack_KMW/
│     ├─ Defense_PJH/
│     ├─ Press_KMG/
│     └─ Rule_PHC/
├─ _Legacy/                  Turtle·Escape와 폐기 실험 보관
├─ Settings/                 공통 URP 설정
└─ InputSystem_Actions.inputactions
docs/                        주제별 개발 문서와 Archive
```

폴더 접미사는 담당자 구분용이다. `BehaviorName`, 모델명, C# 타입과 asmdef에서는 논리명 `Attack`, `Defense`, `Press`, `Rule`을 유지한다. 상세 구조는 [Asset 구조와 소유권](docs/project/asset-layout.md)을 참고한다.

Turtle과 Escape의 소스·에셋은 Legacy 보존 대상이며 신규 개발 대상은 아니다. 과거 Escape Player는 2026-09-25 삭제했다. 기타 Template·Diagnostics·과거 Soccer 자료는 기록 보존용이다. 빌드 생성 시 단계·시점·목적을 기록하고 [빌드 관리 기준](docs/project/build-lifecycle.md)에 따라 사용 종료된 생성물을 검토한다.
