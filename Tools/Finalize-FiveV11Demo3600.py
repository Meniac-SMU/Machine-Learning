"""Verify exact milestones and bundled inference before recording completion."""
import hashlib
import argparse
import zipfile
from five_v11_continuation_common import *

def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('--build',default='demo-3600-20261017-r001')
    parser.add_argument('--smoke',default='demo-smoke')
    parser.add_argument('--live',default='demo-live')
    args=parser.parse_args()
    build=ROOT/'Builds/Manager5v5'/args.build
    training=read(BASE/RUN/'training-completion.json')
    smoke=read(BASE/args.smoke/'result.json')
    live=read(BASE/args.live/'result.json')
    visual=read(BASE/'visual-review.json')
    assert visual['passed'] and visual['smokeEvidence']==args.smoke and visual['liveEvidence']==args.live
    assert training['passed'] and training['step']==3600000
    assert smoke['passed'] and live['passed']
    assert sum(c['name'].startswith('inference-parity-') for c in smoke['checks'])==30
    assert sum('picker-selects-3200000-' in c['name'] for c in smoke['checks'])==4
    assert read(BASE/(args.smoke+'-exit.json'))['exitCode']==read(BASE/(args.live+'-exit.json'))['exitCode']==0
    assert smoke['elapsed']>=300 and live['elapsed']>=300
    for name in ['ProjectSettings','EditorBuildSettings']:
        assert sha(ROOT/f'ProjectSettings/{name}.asset')==sha(BASE/f'{name}-before.asset')
    catalog=read(build/'model-catalog.json')
    assert [m['step'] for m in catalog['models']]==list(range(0,3600001,400000))
    for row in catalog['models']:
        assert sha(ROOT/row['source'])==row['sourceSha256']
        assert sha(ROOT/row['asset'])==row['assetSha256']
    dependencies=(build/'asset-dependencies.txt').read_text(encoding='utf-8-sig').splitlines()
    models=[p for p in dependencies if p.endswith('.onnx')]
    assert len(models)==10 and {p.replace('\\','/') for p in models}=={r['asset'].replace('\\','/') for r in catalog['models']}
    changed=[];preserved=0
    allowed={'Assets/_Soccer/Manager5v5/Demo/'+p for p in [
        'Runtime/FiveDemoAssets.cs','Runtime/FiveDemoApp.Views.cs','Runtime/FiveDemoApp.Smoke.cs',
        'Editor/FiveDemoBuilder.cs','Models/catalog.json','Models/inference-fixtures.json',
        'FiveDemoAssets.asset','FiveDemo.unity','FiveDemoArena.prefab']}
    for row in read(BASE/'demo-preservation-before.json'):
        if sha(ROOT/row['path'])==row['sha256']:preserved+=1
        else:changed.append(row['path'])
    assert set(changed).issubset(allowed),changed
    original_readme=(ROOT/'Builds/Manager5v5/demo-20261017-r009/README.md').read_text(encoding='utf-8')
    readme=original_readme.replace('240만, 280만의 9개 항목','240만, 280만, 320만, 360만의 11개 항목')
    readme=readme.replace('현재 280만','현재 360만')
    readme=readme.replace('이 폴더의 MENIAC.exe를 실행하세요.', '이 폴더의 Play-MENIAC.cmd를 실행하세요. 검증한 Direct3D11 옵션으로 MENIAC.exe를 시작합니다.')
    readme+='\n2026-10-17 추가 학습 시연판: v11 최신 환경에서 280만 모델을 24환경 CUDA로 360만까지 이어 학습했습니다. 320만과 360만 모델이 추가됐습니다. 가장 큰 학습 횟수가 기본값이며, 최고 성능 판정은 뜻하지 않습니다.\n'
    (build/'README.md').write_text(readme,encoding='utf-8')
    archive=ROOT/'Builds/Manager5v5'/f'MENIAC-5v5-{build.name}.zip'
    files=[p for p in build.rglob('*') if p.is_file()]
    with zipfile.ZipFile(archive,'x',zipfile.ZIP_DEFLATED,compresslevel=6) as z:
        for path in files:z.write(path,build.name+'/'+path.relative_to(build).as_posix())
    with zipfile.ZipFile(archive) as z:
        assert z.testzip() is None and len(z.namelist())==len(files)
        for path in files:
            digest=hashlib.sha256()
            with z.open(build.name+'/'+path.relative_to(build).as_posix()) as stream:
                for block in iter(lambda:stream.read(4*1024*1024),b''):digest.update(block)
            assert digest.hexdigest()==sha(path)
    receipt=dict(passed=True,run=RUN,parentRun=PARENT,parentStep=2800000,step=3600000,
        environments=24,device='cuda',runtime=RUNTIME,formalGames=200,
        optimizerUpdates=training['stages'][-1]['saved']['optimizerUpdates'],
        modelCount=10,choices=11,defaultStep=3600000,models=catalog['models'],
        build=build.relative_to(ROOT).as_posix(),executableSha256=sha(build/'MENIAC.exe'),
        archive=archive.relative_to(ROOT).as_posix(),archiveSha256=sha(archive),archiveContentVerified=True,
        smokeChecks=len(smoke['checks']),liveChecks=len(live['checks']),
        graphics='Direct3D11 via Play-MENIAC.cmd',
        smokeEvidence=args.smoke,liveEvidence=args.live,
        visualInspection=visual,
        preservedDemoFiles=preserved,intentionalChanges=changed,preservedParentFiles=verify_parent(),
        frozenInputs=verify(),champion=None)
    write(BASE/'completion.json',receipt)
    rows=[]
    for item in training['evaluations']:
        result=item['summary']
        opponent={'strong':'규칙형','stage-2800000':'280만','stage-3200000':'320만'}.get(item['opponent'],item['opponent'])
        rows.append(f"| {item['step']//10000}만 | {opponent} | {result['games']} | {result['wins']} | {result['draws']} | {result['losses']} | {result['goalsFor']}:{result['goalsAgainst']} |")
    report=f'''# 최신 v11 환경 360만 이어학습과 시연 모델 통합

2026-10-17 사용자 지시에 따라 원본280만에서 정확히360만까지 이어 학습했다. 24환경 병렬, CUDA, 300초 경기, time scale100, 득실점 보상만을 유지했다. 시연 빌드에는320만·360만을 추가했다.

- Run: `{RUN}`. 부모 `{PARENT}`의 policy·critic·optimizer·상대 풀·RNG를 복사했다. 환경 변경으로 이전 v10 미처리 PPO 경험만 새 Run에서 비웠으며 원본은 보존했다.
- 누적 optimizer 갱신: {receipt['optimizerUpdates']}. 선형 학습률 종료점을360만으로 연장했다.
- 280만→280만5천 저장→281만2천 재개 사전 검증 후 본 학습은 원본280만에서 다시 시작했다.
- 300만·320만·340만·360만 PT/ONNX·상대 풀 저장과 실제 관측 PT/ONNX 대조를 완료했다.
- 최초 제한 환경의 Unity 라이선스 연결 실패는 보존했고 r002 빌드로 실행했다.

## 고정 평가

조합당20개 seed(71000~71019) × 양 진영40경기다. 320만의 규칙형·280만 상대, 360만의 규칙형·280만·320만 상대, 합계200경기를 평가했다. 전체 모델 상호 순위나 champion 승격은 판정하지 않는다.

| 모델 | 상대 | 경기 | 승 | 무 | 패 | 득점:실점 |
|---|---|---:|---:|---:|---:|---:|
{chr(10).join(rows)}

## 시연 빌드

`{build.relative_to(ROOT).as_posix()}/Play-MENIAC.cmd`로 실행한다. 검증한 Direct3D11 옵션으로 같은 폴더의 MENIAC.exe를 연다. 규칙형과0~360만(40만 간격) 신경망10개, 총11선택지다. 양 팀 기본값은360만이며320만도 각각 선택할 수 있다. 기존 UI·인간 조작·골키퍼 카메라를 유지하고 모델창의 높이를4행에 맞췄다.

- 최종 Player 자동 검사 {receipt['smokeChecks']}개와1배속300초 검사 {receipt['liveChecks']}개 통과.
- 10개 모델 ×3입력 추론 대조,320만/360만 양 팀 선택, 모든 모델의 양 팀 실제 결정, 전체300초 경기·종료 통계 확인.
- 원본280만 Run {receipt['preservedParentFiles']}파일과 기존 시연 빌드 보존. 프로젝트 설정 해시 일치.
- 배포ZIP 모든 파일의 해시를 대조했다. 배포 실행기로 얻은 한국어 RED·영어 NAVY 모델 선택창과 실제 경기 캡처를 직접 열어 320만·360만 표시, 4행 배치, 경기 중 양 팀 모델 표시를 확인했다. 앞선 직접 숨김 실행 r003의 검은 PNG는 시각 검증 근거에서 제외했다. 수동 키보드·게임패드 조작은 별도로 시험하지 않았다.

## 실패 기록과 복구

- 첫 사전 검증의 결과 기록 단계에서 json import 누락이 발생했다. 실패본을 보존하고 수정 후 새 사전 Run으로 저장·재개를 검증했다. 본 학습에는 사전 학습분을 합치지 않았다.
- 학습 완료 후 최초 시연 빌드 호출이 Windows PowerShell 5의 압축 API 호환 문제로 중단됐다. 원본 실패 기록을 보존하고 PowerShell 7에서 새 r002 빌드를 생성했다. 학습을 반복하지 않았다.
- 숨김 Direct3D12 검사 r002/r004는 첫 화면 캡처에서 실패해 해당 검사 프로세스만 종료했다. 같은 빌드의 Direct3D11 검사 {args.smoke} 및 실제 배포 실행기를 이용한 {args.live}는 정상 종료했다. 숨김 캡처 오류를 일반 화면 실행의 실패로 단정하지 않는다.

[완료 기록](../../../Logs/Manager5v5/v11-3600-20261017/completion.json)
'''
    (ROOT/'docs/soccer/rebuild-5v5/60_최신v11_360만_이어학습과_시연통합.md').write_text(report,encoding='utf-8')
    for relative in ['docs/soccer/current-status.md','docs/soccer/rebuild-5v5/README.md']:
        path=ROOT/relative
        content=path.read_text(encoding='utf-8-sig').replace(
            '24환경 280만→360만 이어학습 및320만·360만 시연 통합 진행 중',
            '24환경 360만 이어학습 및320만·360만 시연 통합 완료')
        path.write_text(content,encoding='utf-8')
    print('VERIFIED',str(build),flush=True)

if __name__=='__main__':main()
