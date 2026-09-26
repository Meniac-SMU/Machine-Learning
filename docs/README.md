# 프로젝트 문서 색인

마지막 검토: 2026-09-26. 필요한 행의 문서만 읽는다. 기본 진입점은 이 색인과 [현재 상태](soccer/current-status.md)이며 전체 문서 순독은 하지 않는다.

| 작업 | 먼저 읽기 | 필요할 때만 추가 |
|---|---|---|
| 현재 MNG 개발·학습 준비 | [MS v3 운영 기준](soccer/training/ms-v3-current.md) | [D4·D5 증거](soccer/training/ms3-v3-d4-d5-report-20260923.md), v3 registry/schema |
| MNG 경기 기술 변경 | [MS v3 운영 기준](soccer/training/ms-v3-current.md) | [패스·드리블 보고서](soccer/training/ms2-forward-pass-center-report-20260923.md), 해당 Runtime·Tests |
| 실행 도구 찾기 | [Tools 색인](../Tools/README.md) | 특정 스크립트·준비 manifest |
| Core 경기·입력·물리 | [경기 계약](soccer/gameplay-contract.md) | [아키텍처](soccer/architecture.md) |
| Core·팀 보상 | [보상 기준표](soccer/rewards.md) | 해당 Profile·팀 README·Tests |
| 기존 선수 Curriculum | [학습 운용](soccer/training/overview.md) | [커리큘럼](soccer/training/curriculum.md), L2-Find 중단 상태 |
| 팀별 수정 | [학습형 가이드](soccer/training/learning-teams.md) / [Rule 가이드](soccer/training/rule-team.md) | [경기 계약](soccer/gameplay-contract.md) |
| Unity·검증 | [설정 및 검증](project/setup-and-validation.md) | 필요한 검사 하나 |
| 구조·파일 이동 | [Asset 구조](project/asset-layout.md) | [2026-09-24 정리 보고서](project/cleanup-20260924.md) |
| 전체 용량·삭제 가능성 | [2026-09-26 용량 점검](project/storage-review-20260926.md) | [과거 Run 보존 결정](archive/history/storage-candidates-20260926.json) |
| 빌드 생성·사용·용량 정리 | [빌드 관리 기준](project/build-lifecycle.md) | [보존 목록](project/build-retention.json), [2026-09-25 삭제 보고서](project/build-cleanup-20260925.md) |
| 과거 결정·실험 조사 | [Archive 색인](archive/README.md) | 지정된 보고서 하나 |

## 검색 범위

`Tools/Find-ProjectContext.ps1 -Scope CurrentDocs`로 현행 문서 목록을, `-Scope Manager -Pattern 'SetTask'`로 MNG 소스를 찾는다. 기본 검색은 Archive·Legacy·Logs·results·Builds·캐시를 제외한다. 과거 실험을 조사할 때만 `-Scope Archive` 또는 정확한 증거 경로를 지정한다. 도구는 Git ignore 설정을 변경하지 않는다.

## 작성 규칙

진행 상태는 current-status, 모델 자격은 v3 registry, ABI는 v3 schema, 폴더 책임은 asset-layout을 단일 기준으로 쓴다. 최신 규칙을 오래된 계획 위에 계속 덧붙이지 않는다. 완료/대체 기록은 Archive로 옮기고 현행 문서에는 결론과 링크를 남긴다. 문서 이동 시 상대 링크와 코드의 경로 문자열을 함께 검사한다.
