"""Finalize only after current Player, real-time run and regression evidence pass."""
import argparse
import hashlib
import json
import zipfile
from pathlib import Path

ROOT=Path(__file__).resolve().parent.parent
EVIDENCE=ROOT/'Logs/Manager5v5/demo-20261017'
p=argparse.ArgumentParser()
p.add_argument('--build',required=True)
p.add_argument('--smoke',required=True)
p.add_argument('--live',required=True)
args=p.parse_args()
build=ROOT/args.build
smoke=EVIDENCE/args.smoke
live=EVIDENCE/args.live
read=lambda path:json.loads(path.read_text(encoding='utf-8-sig'))
sha=lambda path:hashlib.sha256(path.read_bytes()).hexdigest()
ui=read(smoke/'result.json');normal=read(live/'result.json')
play=read(EVIDENCE/'regression-r001.json');edit=read(EVIDENCE/'edit-r002.json')
assert ui['passed'] and normal['passed']
assert (smoke/'exit-code.txt').read_text().strip()=='0' and (live/'exit-code.txt').read_text().strip()=='0'
assert play['testResult']==edit['testResult']=='Passed' and play['failed']==edit['failed']==0
assert sha(ROOT/'ProjectSettings/ProjectSettings.asset')==sha(EVIDENCE/'ProjectSettings-before.asset')
assert sha(ROOT/'ProjectSettings/EditorBuildSettings.asset')==sha(EVIDENCE/'EditorBuildSettings-before.asset')
expected={f'Assets/_Soccer/Manager5v5/Runtime/{name}.cs' for name in ['FiveCarrierMotion','FiveContestRelief','FiveHud','FiveMatch','FivePlayer','FiveTactics','FiveTechniques']}
changed=[];preserved=[]
for row in read(EVIDENCE/'preservation-before.json'):
    path=Path(row['Path']);assert path.exists(),path
    rel=path.relative_to(ROOT).as_posix()
    (preserved if sha(path).lower()==row['Hash'].lower() else changed).append(rel)
assert set(changed)==expected,changed
catalog=read(ROOT/'Assets/_Soccer/Manager5v5/Demo/Models/catalog.json')
assert [m['step'] for m in catalog['models']]==list(range(0,2800001,400000))
for model in catalog['models']:
    assert sha(ROOT/model['source'])==model['sourceSha256']
    assert sha(ROOT/model['asset'])==model['assetSha256']
dependencies=(build/'asset-dependencies.txt').read_text(encoding='utf-8-sig').splitlines()
models=[path for path in dependencies if path.endswith('.onnx')]
assert len(models)==8 and all('/Manager5v5/Demo/Models/' in path for path in models)
readme='''# MENIAC 5대5 시연 빌드

이 폴더의 MENIAC.exe를 실행하세요. 다른 PC로 옮길 때는 폴더 전체를 복사하거나 제공된 ZIP 전체를 풀어야 합니다. Python이나 Unity Editor 없이 실행할 수 있습니다.

모델 선택은 규칙형, 0회 초기 모델, 40만, 80만, 120만, 160만, 200만, 240만, 280만의 9개 항목입니다. RED와 NAVY에 각각 지정할 수 있습니다. 구 4대4 신경망은 포함하지 않습니다.

| 조작 | 키 |
| --- | --- |
| RED 공격수 1 인간 조작과 AI 복귀 | H |
| 전진과 후진 | W / S |
| 좌우 회전 | A / D |
| 패스 | E |
| 슈팅 | Space |
| 관전 시점 전환 | ← / → |
| 일시정지와 재개 | Esc |

인간 조작 개입 모드에서 H를 누르면 RED 공격수 1을 조작합니다. 관전 모드에서는 양 팀 골키퍼와 전체 시점을 전환할 수 있습니다. 인간 시점의 마우스 좌우 회전은 ±30도이며 입력 없이 1.5초가 지나면 정면으로 돌아옵니다. 게임패드는 Y 전환, 트리거 전후 이동, 왼쪽 스틱 회전, A 패스, B 슈팅입니다.

한 팀은 공격수 2명, 수비수 2명, 골키퍼 1명입니다. 감독 명령은 8개이며 선수의 속도, 가속, 회전, 체력, 드리블과 킥 제한은 인간 조작에도 공통 적용됩니다. 경기는 5분 또는 10분, 1배속입니다. 첫 화면 가이드에서 명령과 조작법을 확인할 수 있습니다.

종료 화면은 슈팅 횟수, 슈팅 성공률, 패스 횟수, 패스 성공률, 골키퍼 선방 횟수를 표시합니다. 각 막대는 RED와 NAVY 수치의 합을 기준으로 나눕니다. 양 팀 모두 0이면 빈 막대입니다. 슈팅 성공률은 득점으로 끝난 슛을 전체 슛으로 나눈 값, 패스 성공률은 안정적인 수신을 전체 패스로 나눈 값입니다.
'''
(build/'README.md').write_text(readme,encoding='utf-8')
receipt=dict(passed=True,build=str(build.relative_to(ROOT)),executableSha256=sha(build/'MENIAC.exe'),
             runtime=ui['runtime'],uiChecks=len(ui['checks']),liveChecks=len(normal['checks']),
             playMode=play['passed'],editMode=edit['passed'],preservedFiles=len(preserved),
             intentionalChanges=changed,unexpectedChanges=[],modelCount=8,choices=9,
             models=catalog['models'],trainingStarted=False,
             acceleratedFullMatch=dict(red=ui['redScore'],navy=ui['navyScore'],elapsed=ui['elapsed']),
             realtimeFullMatch=dict(red=normal['redScore'],navy=normal['navyScore'],elapsed=normal['elapsed']))
report=f'''# 5대5 시연 빌드와 검증

2026년 10월 17일 사용자 요청을 반영한 Windows 시연 빌드다. 실행 경로는 `{args.build}/MENIAC.exe`이며 같은 폴더의 README.md에 조작법을 제공한다. 경기 중 HUD 배치는 유지하고 일시정지와 새 경기 버튼만 제거했다. 모델 표시와 영어 번역은 선택한 5대5 모델에 맞춘다.

## 적용 범위

- 규칙형과 0·40·80·120·160·200·240·280만 모델을 9칸의 단일 선택 화면으로 통합했다.
- 0~240만은 `MNG5_V5-20261016-r001`, 280만은 `MNG5_V6-20261016-r002`의 정확한 해당 체크포인트다. 원본 weights를 유지한 채 기존 평가 도구의 Softmax 출력만 추론용 복사본에 노출했다. `model-catalog.json`에 원본과 추론 자산 SHA-256을 기록했다. 구 4대4 신경망 참조는 빌드 의존성 검사에서 0개다.
- 기존 전시의 홈 디자인과 제작자 이미지, H·WASD·E·Space·게임패드 조작, 전체/양쪽 골키퍼/인간 카메라, Esc 일시정지를 새 5대5 런타임에 연결했다. H의 대상은 RED slot 3, 공격수 1이다.
- 인간 입력은 `FivePlayer.Drive`의 공통 속도·가속·회전·체력·드리블 제한을 사용한다. 실제 전면 킥 영역, 도달 가능 여부와 0.6초 공통 킥 쿨다운을 유지한다. 인간 조작 중 자동 감독 킥을 거부하고 이동 입력이 최종 권한을 가진다.
- 첫 화면 가이드를 한·영 5페이지로 교체했다. 5대5 구성, T1~T8 명령, 골키퍼 독립 동작, 인간/관전 입력, 연속 선택 제한, 종료 통계를 안내한다.
- 종료 통계 5개는 팀별 값과 하나의 RED/NAVY 비율 막대를 함께 표시한다. 골키퍼의 슛 접촉은 한 슛당 한 번만 집계하고, 그대로 실점한 접촉은 선방으로 세지 않는다.

## 검증 결과

| 검증 | 결과 |
| --- | --- |
| Unity PlayMode | {play['passed']} 통과, 실패 0 |
| Unity EditMode | {edit['passed']} 통과, 실패 0 |
| 최종 Player 자동 기능 검사 | {len(ui['checks'])} 통과, 프로세스 종료 0 |
| 모델 추론 대조 | 8개 모델 × 3입력, 확률 오차 0.0001 미만 |
| 실제 물리 300초 자동 경기 | {ui['redScore']} : {ui['navyScore']}, 종료 통계 표시 |
| 일반 FixedUpdate 1배속 300초 경기 | {normal['redScore']} : {normal['navyScore']}, 정상 종료, 프로세스 종료 0 |
| 원본 보존 | 기존 {len(preserved)}개 파일 해시 유지, 의도된 공통 소스 7개 외 변경 0 |
| ProjectSettings와 EditorBuildSettings | 작업 전 파일 해시와 일치 |

Player 검사는 메뉴 선택, 한·영 가이드, 모든 모델의 양 진영 추론, 인간 조작 전환, 양쪽 골키퍼 시점, Esc 일시정지/재개, 5개 막대와 실제 사건 합계, 다음 경기와 시작 화면 복귀를 포함한다. 1600×1000과 1280×720 렌더 이미지를 확인했다. 물리 검사와 자동 입력 경로 검증이며 실제 키보드·게임패드의 수동 조작감 평가는 별개다. 경기 결과는 기능 검증이며 모델 우열이나 champion 판정이 아니다.

## 증거와 재빌드

- [최종 완료 기록](../../../Logs/Manager5v5/demo-20261017/completion.json)
- [Player 자동 검사](../../../Logs/Manager5v5/demo-20261017/{args.smoke}/result.json)
- [일반 1배속 경기](../../../Logs/Manager5v5/demo-20261017/{args.live}/result.json)
- [PlayMode](../../../Logs/Manager5v5/demo-20261017/regression-r001.json), [EditMode](../../../Logs/Manager5v5/demo-20261017/edit-r002.json)
- 재빌드: `Tools/Build-FiveDemo.ps1 -RunName <새 이름>`. 기존 경로는 덮어쓰지 않는다.
- 최초 라이선스 IPC 실패와 타입명 충돌, 자동 검증기에서 세션을 해제한 실패는 각 r001~r004 로그에 보존했다. r005의 217개 통과 뒤 화면 여백과 카메라 전환 완료 검사를 보완했다.

추가 학습, 보상 변경, 모델 승격, 기존 빌드 삭제, commit과 push는 하지 않았다.
'''
doc=ROOT/'docs/soccer/rebuild-5v5/58_시연빌드_모델통합과_인간개입.md'
doc.write_text(report,encoding='utf-8')
for rel,prefix in [('docs/soccer/current-status.md','rebuild-5v5/'),('docs/soccer/rebuild-5v5/README.md','')]:
    path=ROOT/rel;text=path.read_text(encoding='utf-8-sig');line=f'> 2026-10-17 **5대5 시연 빌드 완료**. 통합9모델, 인간 공격수1 개입, 골키퍼 시점, Esc 일시정지, 종료5지표 비율막대. PlayMode{play["passed"]}·EditMode{edit["passed"]}·Player{len(ui["checks"])} 검사와 일반1배속300초 경기를 통과했다. [시연 빌드와 검증]({prefix}{doc.name}).\n\n'
    assert '2026-10-17 **5대5 시연 빌드 완료**' not in text
    first,rest=text.split('\n',1);path.write_text(first+'\n\n'+line+rest.lstrip('\n'),encoding='utf-8')
(EVIDENCE/'completion.json').write_text(json.dumps(receipt,ensure_ascii=False,indent=2),encoding='utf-8')
archive=ROOT/'Builds/Manager5v5/MENIAC-5v5-demo-20261017.zip'
assert not archive.exists(),archive
files=[path for path in build.rglob('*') if path.is_file()]
with zipfile.ZipFile(archive,'x',zipfile.ZIP_DEFLATED,compresslevel=6) as z:
    for path in files:z.write(path,Path(build.name)/path.relative_to(build))
with zipfile.ZipFile(archive) as z:
    assert z.testzip() is None and len(z.namelist())==len(files)
    for path in files:assert hashlib.sha256(z.read(str(Path(build.name)/path.relative_to(build)).replace('\\','/'))).hexdigest()==sha(path)
receipt.update(archive=str(archive.relative_to(ROOT)),archiveSha256=sha(archive),archiveBytes=archive.stat().st_size,archiveEntries=len(files),archiveContentVerified=True)
(EVIDENCE/'completion.json').write_text(json.dumps(receipt,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(receipt,ensure_ascii=False,indent=2))
