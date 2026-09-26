# 2026-09-24 정리 완료 인계

상태: 요청된 보수적 문서·에셋 정리와 최종 정적 검증 완료. 미완료 이동은 없다.

후속 갱신: 아래는 2026-09-24 완료 시점의 기록이다. 이후 과거 빌드 삭제와 기존 D5 준비 소스 해시 재검증 필요 사항은 [2026-09-25 보고서](build-cleanup-20260925.md), 현재 보존 원칙은 [빌드 관리](build-lifecycle.md)를 우선한다.

## 다음 AI의 최소 읽기

1. `AGENTS.md`
2. `docs/README.md` → `docs/soccer/current-status.md`
3. 작업에 따라 `docs/soccer/training/ms-v3-current.md` 또는 `Tools/README.md`

정리 결과는 [정리 보고서](cleanup-20260924.md)에 있다. 과거 계획42개 및 미참조 타이머를 이동했고, 기존 큰 상태 문서를 Archive에 보존했다. 전체112파일 이동의 대응과 SHA는 `docs/archive/history/cleanup-moves-20260924.json`을 따른다. 과거 경로를 찾느라 Archive 전체를 읽지 않는다.

## 검증·보존

- `Logs/ProjectCleanup-20260924/validation.json`: 소스 기준선1,804파일, 누락0, 비Markdown 내용 변경0, 이동 비Markdown70개 원본 SHA 일치, 문서 파일 경로 링크 누락0, 이전 활성 경로 잔존0, import 대상 GUID 중복/고아 meta0.
- 준비 소스97개와 MS2 actor·ONNX SHA 일치. D5 `-ValidateOnly` 통과, 학습0. 결과는 같은 폴더의 `d5-validate-only-result.json`.
- 기존 사용자 변경이 많다. `git-status-before.txt`와 `source-before.json`이 이번 작업 기준선이다. `originals/`는 수정 문서 원문 백업이며 기존 git HEAD로 일괄 복구하지 않는다.
- `organize.cjs`, `finish-docs.cjs`는 일회성 실행 기록이므로 다시 실행하지 않는다. 정적 재확인만 `node Logs/ProjectCleanup-20260924/validate.cjs`로 수행한다.
- Unity/학습/경기는 실행하지 않았다. C#·Scene·Prefab·모델·Packages·ProjectSettings·기존 도구 내용·gitignore를 변경하지 않았다. commit/push 없음.

## 유지 판단과 후속 경계

현재 세대는 v3이나 코드·YAML·빌드의 V2 이름은 공유 ABI다. v2 도구는 v3에서 import하는 것과 과거 재현용이 섞여 있다. `Tools/README.md`를 먼저 보고 이름만으로 이동/삭제하지 않는다. 모델78개 중11개는 조사 범위에서 직접 참조가 확인됐으며 나머지도 무참조만으로 미사용을 확정하지 않았다. 원본 모델·빌드·학습 기록의 삭제는0건이다.

MS3-v3 학습 전 D5 완료, 새 R6 학습 미시작, champion 없음. 이번 정리는 새 학습 승인이 아니다. 후속 학습을 요청받으면 현행 v3 registry와 준비 manifest를 확인하고 첫100k 점검→최대200k 경계를 따른다. 구400k를 초기값/상대/평가기준/champion으로 되돌리지 않는다.

타이머 원본은 `Assets/_Legacy/Diagnostics/*~`에 보존했다. Unity가 `Assets/ML-Agents`를 다시 만들면 정상적인 생성물 재발생일 수 있다. Assets 전체 정리가 되돌아갔다고 판단하거나 자동 삭제하지 않는다.
