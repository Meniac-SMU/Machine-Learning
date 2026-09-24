# MNG 감독 강화학습 진입점

마지막 검토: 2026-09-24. 현행 단계는 MS2-v3 출발 기준 / MS3-v3 학습 전 D5 준비 완료이며 새 R6 학습은 미시작이다.

- 현재 상태: [Soccer 현재 상태](../../../docs/soccer/current-status.md)
- 기술·학습·평가 경계: [MS v3 운영 기준](../../../docs/soccer/training/ms-v3-current.md)
- 실행 도구와 준비물: [Tools 색인](../../../Tools/README.md)
- 현행 증거: [D4·D5 보고서](../../../docs/soccer/training/ms3-v3-d4-d5-report-20260923.md)
- 과거 M0~M3·R0·MS v1/v2 계획: [Archive](../../../docs/archive/README.md), 필요할 때만 선택한다.

## 코드·에셋 위치

| 폴더 | 용도 |
|---|---|
| Runtime | 감독 Agent·244관측·Planner·임무 수명·선수 기술·계측 |
| Editor | MNG 전용 Builder, v3도 MNG_V2Builder의 기술 ABI 사용 |
| Curriculum/MS_V2 | 현행 v3에서 재사용하는 씬·기술 기반 |
| Profiles, Prefabs | 공유 물리·보상·커리큘럼 설정 |
| Tests | MNG EditMode·PlayMode 회귀 |
| Training | 버전별 YAML. 현행 실행은 준비 manifest의 설정을 사용 |
| Curriculum의 기타 폴더, Scenes, Evaluation | 이전 단계 재현·Builder 참조. 이름만으로 이동/제거하지 않음 |
| Models, EvaluationModels | 과거 정책·평가 사본. v3 자격은 registry로 판단 |

내부 `MNG_ManagerV2`, 관측244/명령6, MS3V2 빌드 이름은 v3에서도 유지하는 ABI다. 구 v2 400k 정책의 선택 자격과 기술 코드 재사용을 구분한다. 정체 강제 패스는 제거되었으며 감독이 선택한 PassBuild와 자기 골문 앞 걷어내기 규칙이 현행이다.
