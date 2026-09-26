# Machine-Learning

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
