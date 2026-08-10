# 개발 환경 설정과 검증

- 대상: 모든 개발·자동화 담당자
- 상태: 실행·검증 절차의 단일 기준
- 마지막 검토: 2026-08-10

## 확인된 환경

| 항목 | Version | 기준 파일 |
| --- | --- | --- |
| Unity Editor | `6000.3.16f1` | `ProjectSettings/ProjectVersion.txt` |
| ML-Agents | `4.0.3` | `Packages/manifest.json`, lock file |
| AI Inference | `2.6.1` | Package lock |
| URP | `17.3.0` | Package manifest |
| Input System | `1.19.0` | Package manifest |
| Test Framework | `1.6.0` | Package manifest |

Unity는 저장소 Root `C:\GitHub\Machine-Learning`을 연다. Package Version은 이 문서보다 manifest와 lock file을 우선한다. Trainer를 시작하기 전에 사용 중인 Python 환경에서 `mlagents-learn --help`와 PyTorch device를 확인한다.

## Unity Licensing 주의

현재 설치 조합에서는 Unity Hub가 실행 중일 때 batch mode가 간헐적으로 60초 후 실패할 수 있다.

확인된 원인:

- Hub Licensing Client: `1.17.4`
- Editor 내장 Licensing Client: `1.18.1`
- Hub Client Log: `Unsupported protocol version '1.18.1'`
- Student entitlement의 `com.unity.editor.headless`: 정상 granted

따라서 Enterprise License 부족 문제가 아니다. Hub Client가 Editor의 protocol을 거부한 뒤 entitlement를 읽지 못하면서 `'com.unity.editor.headless' was not found`라는 후속 오류가 나타난다.

### 안정적인 batch 실행

1. 모든 Unity Editor를 정상 종료한다.
2. Unity Hub를 완전히 종료한다.
3. Hub가 띄운 `Unity.Licensing.Client`가 남아 있지 않은지 확인한다.
4. Editor 실행 파일을 직접 호출한다.

Editor가 열려 있는 동안 Licensing Client를 강제 종료하지 않는다.

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.3.16f1\Editor\Unity.exe' `
  -batchmode -nographics -quit `
  -projectPath 'C:\GitHub\Machine-Learning' `
  -logFile 'C:\GitHub\Machine-Learning\Logs\Soccer-Compile.log'
```

2026-08-10에 이 방식으로 Asset import와 C# compile이 성공했다. 기준 Log는 `Logs/Licensing-Isolated-Compile.log`다. Hub를 다시 열면 다음 batch 전에 동일 충돌 여부를 재확인한다.

## 검증 단계

변경 범위에 맞는 최소 단계부터 실행하고 공통 계약 변경일수록 아래쪽 단계까지 확장한다.

| 단계 | 대상 | 통과 기준 |
| --- | --- | --- |
| 정적 | Markdown, YAML, 경로, Serialize 값 | link·path 오류와 diff whitespace 오류 없음 |
| Compile | C#과 Asset import | Console compile error 0 |
| Builder Validate | Scene·Prefab·Profile·정책 계약 | Validation exception 없음 |
| EditMode | 순수 계산, 연결, parity | 관련 Test 전부 Passed |
| PlayMode | 경기, UI, 입력, Reward | Soccer Test 전부 Passed |
| Training smoke | Trainer communicator | Behavior 연결과 Step 증가 |
| Build·수동 | Windows 실행 | 실제 화면·Controller·장시간 안정성 |

## 정적 검사

```powershell
git status --short
git diff --check
rg -n "Assets/Soccer|Assets/Escape|docs/[0-9]" AGENTS.md README.md docs Assets/_Soccer Assets/_Legacy
```

`git diff --check`의 CRLF 안내는 오류가 아니지만 실제 trailing whitespace와 충돌 Marker는 수정한다. `git status`에서 사용자 변경과 자동 생성 파일을 구분한다.

Markdown 링크는 상대 경로를 기준으로 모두 해석해야 한다. Archive 문서의 과거 Asset 경로는 당시 사실일 수 있으므로 단순 문자열 치환하지 않는다.

## Builder

Unity Menu에서는 다음을 사용한다.

- 비파괴 검사: `Tools/Soccer/Validate 4v4 Prototype`
- 공통 생성·동기화: `Tools/Soccer/Build 4v4 Prototype`

Model·Profile·YAML만 바꿨다면 Validate를 우선한다. 공통 Geometry·Physics·UI 변경에서만 Build 후 다섯 독립 Prefab의 허용 차이가 보존됐는지 확인한다.

Batch Validate 예시:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.3.16f1\Editor\Unity.exe' `
  -batchmode -nographics -quit `
  -projectPath 'C:\GitHub\Machine-Learning' `
  -executeMethod MachineLearning.Soccer.Editor.SoccerProjectBuilder.ValidateBatch `
  -logFile 'C:\GitHub\Machine-Learning\Logs\Soccer-Validate.log'
```

생성까지 필요할 때만 `ValidateBatch` 대신 `BuildAllBatch`를 사용한다.

## Test Runner

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.3.16f1\Editor\Unity.exe' `
  -batchmode -nographics -quit `
  -projectPath 'C:\GitHub\Machine-Learning' `
  -runTests -testPlatform EditMode `
  -testResults 'C:\GitHub\Machine-Learning\Logs\Soccer-EditMode.xml' `
  -logFile 'C:\GitHub\Machine-Learning\Logs\Soccer-EditMode.log'
```

PlayMode는 `-testPlatform PlayMode`와 별도 결과 파일을 사용한다. XML의 `<test-run result="Passed">`, total·failed 수와 Log의 compile/licensing 오류를 함께 확인한다. 결과 파일이 생성되지 않았으면 Test 실패인지 Test 수집 이전 종료인지 Log로 구분한다.

## 변경별 최소 검증

| 변경 | 최소 검증 |
| --- | --- |
| Markdown만 | 전체 link·stale path·`git diff --check` |
| Profile·YAML | Serialize/YAML 검사, Builder Validate, Reward 관련 EditMode |
| Team Policy·Rule FSM | Compile, 관련 EditMode, 해당 Scene PlayMode, 성능 비교 |
| Core Runtime | Compile, Builder Validate, 전체 Soccer EditMode·PlayMode |
| Scene·Prefab·Physics·UI | Build/Validate, parity, 전체 Test, 실제 화면 |
| ONNX | Tensor, 네 선수 Model 일치, Inference Scene, 기준 경기 |

## Unity 실행 후 정리

- 예상하지 않은 Scene·Prefab·ProjectSettings 변경을 확인한다.
- 새 Asset의 `.meta`와 GUID를 확인한다.
- Unity가 `Assets/ML-Agents/Timers`를 다시 생성할 수 있다. 이는 진단 산출물이므로 활성 Source와 섞지 않되, 다른 Asset을 함께 삭제하지 않는다.
- `Library`, `Temp`, `obj`, Build와 Log는 Source가 아니다. 삭제가 필요하면 정확한 경로와 복구 가능성을 확인한다.
- 최신 검증 결과와 남은 항목만 [현재 상태](../soccer/current-status.md)에 반영하고 상세 과정은 필요할 때 Archive로 옮긴다.
