# 빌드 생성·사용·정리 기준

마지막 검토: 2026-09-26. 빌드는 실행용 생성물이며 모델·학습 결과·원본 증거와 별도로 관리한다. 현재 보존 목록은 [build-retention.json](build-retention.json), 삭제 이력은 [2026-09-25 정리 보고서](build-cleanup-20260925.md)다. 대량 빌드/로그를 평소 Context에 넣지 않는다.

## 현재 보존 대상

| Player | 단계·용도 | 보존 근거 |
|---|---|---|
| `Builds/MNG_V2/MS3-v3-WorkerIOFix-20260924T182752604Z/MS3V2` | 현행 r002 2M 학습 runtime | IO 수정과 완료 학습 증거 |
| `Builds/MNG_V2/MS3-v3-WorkerIOFix-20260924T182752604Z/EvaluationV2` | 현행 평가·2M 챔피언 선발 | 토너먼트220경기 증거 |
| `Builds/MNG_V2/MS2-forward-pass-center-20260923/MS3V2` | 이전 D5/r001 원본 runtime | 원래 준비/실패 증거, 별도 판단 전 보존 |
| `Builds/MNG_V2/MS2-forward-pass-center-20260923/EvaluationV2` | 이전 D4/D5 평가 runtime | 과거 평가 증거, 별도 판단 전 보존 |
| `Builds/SoccerTraining` | 기존 선수 Curriculum 최종 공용 빌드 | 학습·평가 도구의 기본 경로, r017 미승인 종료 |

V2는 ABI 이름이기도 하다. 폴더의 MNG/V2/MS 숫자나 날짜만으로 삭제하지 않는다. 기존 MNG 빌드 두 개는 2026-09-23 문서로 날짜를 확인했지만 정확한 완료 시각은 기록되어 있지 않아 소급 등록의 `builtAtUtc`를 비워 뒀다. 등록 시각이나 파일 접근 시각을 생성·실사용 시각으로 간주하지 않는다.

## 새 Manager 빌드

프로젝트 루트에서 `Tools/Build-MNGCurrent.ps1`을 사용한다. 단계와 목적은 필수이며 출력은 `Builds/MNG_V2/<Stage>-<UTC 시각>/{MS3V2,EvaluationV2}`다. 같은 디렉터리를 덮어쓰지 않는다. 기존 `MNG_V2Builder.BuildStabilityPreparationBatch`로 Player 두 개를 만들고 각 디렉터리에 `build-lifecycle.json`을 자동 기록한다. 학습·평가·정책 승격은 실행하지 않는다.

```powershell
# 경로와 호출 메서드만 확인. 실제 빌드 없음.
./Tools/Build-MNGCurrent.ps1 -Stage 'MS3-v3-D5' -Purpose '수정 후 학습·평가 Player 검증' -PlanOnly

# 빌드가 필요한 작업에서 Editor를 닫은 뒤 실행
./Tools/Build-MNGCurrent.ps1 -Stage 'MS3-v3-D5' -Purpose '수정 후 학습·평가 Player 검증'
```

UTC 시작/완료 시각, 단계, 목적, Git commit과 미커밋 경로, 빌드 메서드·로그, 씬/runtime revision, 실행 파일·level0·runtime SHA를 기록한다. 미커밋 소스는 Git commit만으로 재현할 수 없으므로 기존 source snapshot 절차도 유지한다. 실패 시 로그와 요청 JSON을 남기고 성공 빌드로 등록하지 않는다. 새 빌드가 기존 D5 manifest·retention·모델 자격을 자동 변경하지 않는다.

다른 Builder/Unity 메뉴/기존 Core·M/MS 스크립트로 빌드했다면 성공 직후 아래 등록을 반드시 수행한다. 원래 `build-info.json` 및 manifest는 덮어쓰지 않는 별도 sidecar다. 새 빌드에는 실제 생성 시각을 전달하고, 소급 등록에서 모르면 생략한다.

```powershell
./Tools/Build-Lifecycle.ps1 -Action Register -BuildDirectory '<Builds 아래 Player 폴더>' `
  -Stage '<단계>' -Purpose '<생성 목적>' -Evidence '<빌드 로그/보고서>' `
  -BuiltAtUtc '<실제 완료 시각 ISO 8601>' -SourceCommit '<빌드 소스 commit>'
```

실제 학습·평가·시연에 사용한 후에는 `-Action Use`에 같은 BuildDirectory, Purpose, Evidence를 전달한다. `build-usage.jsonl`에 실제 사용 시각과 근거를 추가한다. 단순 조회·해시 확인·실패한 실행을 사용 이력으로 기록하지 않는다.

## 삭제 검토

새 빌드 완료 직후, runtime/정책 변경과 단계 전환 시, 용량 정리 요청 시 아래 읽기 전용 검토를 한다. 별도 예약 작업이나 자동 삭제는 없다.

```powershell
./Tools/Build-Lifecycle.ps1 -Action Review
./Tools/Build-Lifecycle.ps1 -Action Review -Json
# 필요하면 작업자가 정한 장기 미사용 검토 일수를 -UnusedDays로 전달
```

- `protected`: 현행 보존 목록. 대체 빌드 검증과 소비자 경로/manifest 갱신 전까지 유지한다.
- `review-unregistered` / `review-unpinned`: 기록 누락 또는 보존 목록 밖. 목적·사용자·대체 상태를 조사한다.
- `review-long-unused`: 명시한 일수를 실제 마지막 사용 기록이 넘긴 경우. 자동 삭제 승인이 아니다.
- 사용 기록이 없으면 마지막 사용과 미사용 일수는 `null`이다. 생성 후 경과일은 미사용 기간과 구분한다.

삭제 전 현행 문서·준비 manifest·도구 기본값·실행 중 프로세스와 대체 빌드 검증을 확인한다. 회귀·복원·동결 비교에 여전히 필요하거나 판단이 불확실하면 보존한다. 보존 목록 변경에는 근거와 날짜를 남긴다. 폐기 확정 빌드는 파일 목록·용량·주요 SHA·원래 build-info/설명/실행 안내를 보관하고, 프로젝트 `Builds` 내부의 검토된 절대 경로만 삭제한다. 재분석 지점/링크를 따라 프로젝트 밖으로 나가지 않는다.

모델 PT/ONNX, results, source snapshots, 원본 평가 로그, registry는 빌드 정리와 함께 삭제하지 않는다. 바이너리 삭제 후 당시 실행 파일 그대로의 재실행은 불가능하다. 해시·메타데이터는 바이너리 백업이 아니다.
