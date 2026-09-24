# 기존 선수 Curriculum 상태와 진입점

마지막 검토: 2026-09-24. 이 문서는 MNG 감독 RL과 별개인 기존 선수 정책의 보존 상태를 안내한다. 실행 지시는 [현재 상태](../current-status.md)가 우선한다.

기존 선수 정책은 Soccer4v4_Base, 총379관측(벡터43 + Ray336), 행동[3,3,3,3]을 사용한다. MNG 감독의244/[6]와 호환되지 않는다.

| 단계 | 보존 상태 |
|---|---|
| L0 공 접근·L1 운반/슛 | 구현·과거 승인 정책 보존 |
| L2 짧은 패스 | 구현 및 승인 이력 보존. 과거 미통과 문구는 당시 기록 |
| L2-Find | r017 step500248 자연 종료, 강화99% gate 미달·미승인, 고정300 승급평가 미실행 |
| L2-Score | 구현과 중단 이력 보존. L2-Find 후속 실행 자동 승인 없음 |
| L3 ProgressivePlay | 구현·실험 이력 보존. 현재 자동 재개하지 않음 |
| L4 Weak4v4 | 계획·작업공간 보존, 완료로 간주하지 않음 |
| L5·L6 | Core fallback/self-play 씬 보존, 현행 MNG 학습 경로와 구분 |

자산 진입점은 [Curriculum README](../../../Assets/_Soccer/Curriculum/README.md), 수치 계약은 [Core 경기 계약](../gameplay-contract.md)과 [보상 기준표](../rewards.md)다. 후속 학습은 별도 사용자 지시와 현재 소스·설정·모델 호환성 점검이 필요하다.

과거 단계별 예산·승급 제안과 명령은 [이전 Curriculum 문서](../../archive/player-curriculum/curriculum-before-20260924.md), [단계별 보상 실험](../../archive/player-curriculum/staged-reward-plan.md)에 보존했다. 그 문서의 미래형 문장·16환경·구 gate·재개 제안을 현재 실행 지시로 사용하지 않는다.
