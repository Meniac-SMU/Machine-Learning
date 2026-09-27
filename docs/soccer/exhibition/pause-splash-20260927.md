# 일시정지 제목 배경 및 Unity 시작 로고 제거

최신 빌드: `Builds/MNG_Exhibition/UI1-20260927T074211233Z/MANAGER.exe`.

- 하이드레이션 브레이크 제목 전용 사각형 배경: 짙은 남색 `#101C38`, 투명도 5%(alpha 0.95). 제목만 자식으로 넣었으며 하단 버튼과 화면 전체 배경에는 적용하지 않는다.
- 제목 글자: 흰색에 가까운 하늘색 `#EAF7FF`. 흰색 테두리를 제거했다. 한영 모두 동일하며 언어별 고정 폭을 사용한다.
- 전시 Builder에서 `PlayerSettings.SplashScreen.show`와 `showUnityLogo`를 모두 false로 설정한다. 빌드 후 프로젝트 설정은 복구하므로 다른 학습/평가 빌드 설정에는 영향을 주지 않는다. MENIAC 팀 로고와 페이드 효과는 유지한다.

## 검증

- Unity Windows 빌드 성공. `Logs/Exhibition/build-20260927T074211233Z/build.log`.
- 실제 Player 한영 검사 **36개 통과, 오류 0**, 실제 종료 버튼 클릭 후 exit code 0.
- 제목 패널은 자식 1개(제목)·높이139·배경 alpha0.95, 글자 테두리0 확인. 실제 [한국어](qa-pause-splash-20260927/pause-ko.png) / [영어](qa-pause-splash-20260927/pause-en.png) 화면도 확인했다.
- 빌드의 `splash-settings.json`과 실제 `MANAGER_Data/globalgamemanagers`를 읽어 Unity splash/로고가 이전 true→새 false이며 로고 자산 참조가 null인 것을 확인했다. 증거: `Logs/Exhibition/pause-splash-20260927/serialized-splash-comparison.json`. UnityPy의 Unity6 스키마가 끝의4바이트를 읽지 못해 전체 크기 검사를 끄고 알려진 필드를 이전 빌드와 대조했다. 최초 실행 전 과정을 영상으로 녹화한 검증은 아니다.
- MNG.Runtime.dll은 기존 검증 SHA256과 동일하다. ProjectSettings/EditorBuildSettings는 작업 전 해시와 동일하게 복구했다. 이번 검증 범위는 UI·빌드 설정이며 전체5분 게임플레이 재검증은 하지 않았다.
- 기존 최신본만 유지 요청에 따라 이전 UI 빌드는 새 검증본으로 교체한다. 삭제 증거는 `Logs/Exhibition/pause-splash-20260927/retired-builds/cleanup.json`이다. 과거 검증·모델·학습/평가 빌드는 보존한다.
- 주간 잔여량 마지막 확인88%. 4% 이하 저장·중단 조건 유지.
