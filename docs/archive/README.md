# 문서 Archive

이 폴더는 완료된 작업, 대체된 결정과 현재 개발 대상이 아닌 실험을 보관한다. 일반적인 Soccer 작업에서는 이 폴더 전체를 읽지 않는다. 현재 문서가 특정 근거를 요구할 때 해당 파일 하나만 선택한다.

## 분류

| 폴더 | 내용 | 기본 Context |
| --- | --- | --- |
| `history` | 과거 결정 원문, 작업 Log, 이전 문서 구조 | 제외 |
| `completed` | 완료된 Camera·Material 같은 변경 기록 | 제외 |
| `legacy` | Turtle·Escape 기획과 기술 자료 | 제외 |

Unity Asset의 보관 Root는 `Assets/_Legacy`다. Markdown Archive는 설명과 이력을 보관하고 Unity Asset을 대신하지 않는다.

## 현재 파일

### History

- [전체 결정 원문](history/decision-log-full.md) — D-001~D-026 배경과 대체 관계
- [2026 작업 기록](history/work-log-2026.md) — 날짜별 상세 구현 Log

### Completed

- [Camera·외형·Material 변경](completed/visual-updates.md)

### Legacy

- [Turtle 기술 메모](legacy/turtle-baseline.md)
- [Escape 프로토타입](legacy/escape-prototype.md)
- [Escape 성능 계획](legacy/escape-performance.md)

## 보관 규칙

- 문서 상단에 보관 이유, 대체 문서와 마지막 상태를 적는다.
- 당시 경로와 실패 기록은 사실로 보존하되 현재 지침으로 사용하지 않는다고 표시한다.
- 현재 규칙이나 수치는 활성 문서에만 갱신한다. Archive에 같은 값을 동기화하지 않는다.
- Archive 문서의 향후형 문장은 취소·완료 여부가 드러나는 과거형으로 바꾼다.
- 비밀번호, Token, 개인 Key는 Archive에도 기록하지 않는다.
- 삭제가 필요한 중복은 활성 문서에 정보가 이관됐는지 먼저 확인한다.

[문서 색인으로 돌아가기](../README.md)
