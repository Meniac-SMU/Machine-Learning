# Legacy 보관 Asset

`Assets/_Legacy`는 현재 Soccer 개발 대상이 아닌 자료를 보존한다. 신규 Soccer 기능과 Model을 이곳에 추가하지 않으며, Asset과 `.meta`를 함께 유지한다.

| 폴더 | 상태 | 용도 |
| --- | --- | --- |
| `Turtle` | 실행 보존 | 목표 찾기 Scene·Prefab·Code·ONNX·Training 기록 |
| `Escape` | 실행 보존 | 종료된 던전 탈출 구현 |
| `UnityTemplate` | 기록 보존 | Unity 기본 Tutorial |
| `Diagnostics` | 기록 보존 | Timer·Upgrade·IDE 생성물 |
| `ArchivedSoccer~` | import 제외 보존 | 폐기 Pass와 ML-Agents sample |

`Training~`과 `ArchivedSoccer~`는 Unity import 대상이 아니다. `Turtle/Training~/Config/Turtle.yaml`은 필요할 때 CLI 경로로 직접 지정할 수 있다.

Legacy는 임의 삭제·통합·덮어쓰지 않는다. 구조 이동으로 깨진 GUID·Builder·Scene 참조만 최소 복구한다. `.csproj`, `.sln`, Build와 일부 결과는 ignore된 로컬 생성물일 수 있으므로 Git 전달 여부를 별도로 확인한다.

문서: [Asset 구조](../../docs/project/asset-layout.md), [Archive 안내](../../docs/archive/README.md).
