# UI 1차 최종본 구현 현황

**2026-09-27 후속 수정 완료.** 최신 실행 파일은 `Builds/MNG_Exhibition/UI1-20260926T180338699Z/MANAGER.exe`다. [HUD·사람 조작 수정 기록](revision-20260927.md) / [완료 명세](implementation-completion.json) / [실행 안내](player-guide.md).

직접 플레이 AI 시작, H에 따른 조작 안내·추적/중계 카메라 전환, 실제 사람 패스·슈팅, GOAL 위치/투명도, 중앙 배속 표시, 확대 HUD와 초록색 일시정지 제목을 한국어·영어에 반영했다. 최종 Player **65개 통과·오류 0**, Manager PlayMode **35 통과·실패 0·명시적 진단 2 제외**, 실제 5분 시뮬레이션 완주. 주간 잔여 **95%**에서 완료했다. 다음 수정은 이 최신 빌드와 `revision-20260927.md`를 기준으로 이어간다.

## 최초 UI 1차 구현 기록 (보존)

2026-09-27 사용자 승인: 현재 시안을 UI 1차 최종본으로 확정하고 Unity 기능 구현, 검증, Windows 빌드까지 진행한다.

**완료: UI 1차 최종본의 Unity 구현 및 Windows x64 Player 빌드·검증을 마쳤다.**

- 최초 실행 파일 (보존): `Builds/MNG_Exhibition/UI1-20260926T165503666Z/MANAGER.exe`
- 실행 씬: `Assets/_Soccer/Manager/Exhibition/Exhibition.unity`
- [사용 안내](player-guide.md) / [완료 명세](implementation-completion.json)
- 배포할 때는 실행 파일과 `MANAGER_Data`, 런타임 DLL 등을 포함한 빌드 폴더를 함께 전달한다. Pretendard 라이선스와 README를 포함했다.

## 확정 기준
- 승인된 HTML/CSS/JS, 이미지, 72개 한/영 문자열, 6개 신경망과 4개 규칙형 감독 목록을 동결한다.
- 마지막 일시정지 배경은 **투명도 85% (흰색 alpha 0.15)**, 블러 없음, 흰색 제목 상단 7%, 버튼 하단 중앙 4%다.
- Unity Localization을 사용한다. 기본 한국어, 영어만 지원한다.
- 직접 플레이는 RED 공격수, AI 시뮬레이션은 2배속. 5분/10분, 양 팀 모델 선택, 실제 통계와 지속되는 결과 화면을 구현한다.
- 슈팅은 실제 공 타격 기준이다. 사람의 Space 강한 킥을 사람 슈팅으로, AI Shot 의도를 AI 슈팅으로 집계한다. 선방은 미구현 표시로 둔다.
- 사전 학습 100만 회 문구는 사용자 지정 전시 표현이며 모델 계보 수치를 재정의하지 않는다.
- 기존 학습/평가/보상/관측244/행동6 계약, 원본 모델 및 미커밋 작업을 보존한다. 추가 학습이나 모델 승격은 하지 않는다.

## 단계
1. [완료] 시안 동결·전용 에셋/패키지 준비 (`docs/archive/exhibition/ui-v1-final-20260927`, SHA256 목록 포함)
2. [완료] Unity 전시 화면·경기 연결·Localization (1.5.13, ko/en 문자열96개, Pretendard 6개 두께)
3. [완료] EditMode 220/220, Manager PlayMode 35 통과·실패0·명시적 진단2 제외. 최종 독립 Player 45개 검증 통과·오류0.
4. [완료] Windows x64 빌드, 기본 D3D12 그래픽 실행·1600×900 캡처, Build-Lifecycle Register/Use/Review 기록

## 사용량 및 재개
- 주간 잔여량을 31% → 30% → 29% → 28% → 26% → **25%**로 확인했다. 4% 중단 기준에 도달하지 않고 완료했다.
- 사용량 기록: `Logs/Exhibition/20260927/usage-checks.json`. 후속 작업도 사용자가 지정한 중단 조건을 따른다.
- 구현 시작 전 Unity CLI elevated status: 연결된 Editor 없음. 기존 시안 서버는 127.0.0.1:8769.
- 기존 변경: AGENTS.md, docs/README.md, current-status.md, ms-v3-current.md, exhibition 디자인 폴더 및 pass-imitation 계획. 되돌리지 않는다.

다음 작업은 이 문서와 `implementation-completion.json`에서 이어간다. 미완료 빌드 단계는 없다. 새로운 수정 후에는 `Tools/Build-Exhibition.ps1`로 새 폴더에 재빌드하고 독립 Player 검증을 수행한다.

## 구현 경로
- `Assets/_Soccer/Manager/Exhibition`: 전시 Runtime / Editor builder / UITK / 6종 Pretendard / 검증된 ONNX 사본 / Localization.
- `Tools/prepare-exhibition-assets.cjs`: 동결 시안에서 이미지와 모델을 원본 변경 없이 가져온다.
- `Tools/Build-Exhibition.ps1`: 새 출력 폴더에 Windows Player 생성 후 Build-Lifecycle Register/Review.
- `MNG_HumanInput.OnDisable`: 일시정지 때 잔여 입력을 지운다. 기존 보상/관측/선수 기술 계약은 수정하지 않았다.
- 검증 로그: `Logs/Exhibition/20260927`. 개발 중 발견한 폰트 API, 에셋 저장/재로드, 로컬라이징 이미지 Addressables 등록, NPOT 로고 비율, 아이콘 글리프 문제를 수정하고 최종 Player에서 재확인했다. 최초 실패 로그와 중간 빌드는 보존했다.
- `ProjectSettings.asset`는 실행 전 원본과 해시가 같다. 기존 Build Settings 씬 목록을 유지하고 Localization/Addressables 설정 참조만 추가했다.
- 신경망 원본6개와 전시 사본6개의 SHA-256을 다시 대조했다. 기존 학습/평가 자격·보상·관측244/행동6·원본 모델은 유지한다.

## 검증 범위와 증거

|검증|결과|증거|
|---|---|---|
|EditMode|220 통과, 실패0|`Logs/Exhibition/20260927/editmode.xml`|
|Manager PlayMode|35 통과, 실패0, 명시적 출력 인수가 필요한 진단2 제외|`Logs/Exhibition/20260927/playmode.xml`|
|최종 Player|45개 확인 통과, 수집된 오류0|`Logs/Exhibition/20260927/player-final/result.json`|
|모델 보존|6개 원본 무변경 및 사본 일치|`Logs/Exhibition/20260927/model-hash-audit.json`|
|최종 빌드|Windows x64 성공|`Logs/Exhibition/build-20260926T165503666Z/build.log`|

Player 검증에는 한국어 기본 시작, 마우스로 경기 준비 진입, Enter/Esc 가이드 조작, 6개 신경망의 양 팀 실제 추론, 규칙형4개 실행, RED W 이동과 H 소유권 전환, 10분 설정, 일시정지 중 시간·공·입력 정지, 경기 재개, 종료 복귀, 2배속 시뮬레이션의 실제300초 경기 완주, 실제 AI 슈팅 집계, 결과 유지와 종료 복귀가 포함된다. 10분 경기는 설정·시작을 확인했으며 600초 전체를 반복 완주한 검증은 아니다. 자동 실행 결과를 정책 성능 평가나 현장 장시간 사용 승인으로 해석하지 않는다.

실제 Player 캡처14개는 [qa-unity-v1](qa-unity-v1)에 저장했다. 예: [시작 화면](qa-unity-v1/01-home-ko.png), [모델 선택](qa-unity-v1/04-models-ko.png), [경기](qa-unity-v1/09-match-ko.png), [일시정지](qa-unity-v1/10-pause-ko.png), [경기 결과](qa-unity-v1/14-result-en.png). HTML 시안 캡처와 별개다.

선방 관측 카운터는 사용자 지시대로 미뤘으며 상황판에는 `—`로 표시한다. 게임 이름·로고·제작자 이미지는 승인된 임시 자산이다. 사전 학습100만 회 문구는 전시 표현의 가정으로 유지하며 실제 모델 계보를 재정의하지 않는다.
