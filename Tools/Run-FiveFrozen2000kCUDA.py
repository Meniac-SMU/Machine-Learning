"""Authorized new execution: from scratch, score-only self-play, authorized continuation to exactly 2,000,000; preserve the prepared 2M learning-rate schedule."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import time
import traceback
from five_evaluation_audit import audit

ROOT = Path(__file__).resolve().parent.parent
EVIDENCE = ROOT / 'Logs/Manager5v5/p9-frozen-v8-20261015'
BUILD = ROOT / 'Builds/Manager5v5/stamina-turn-learning-v8-20261015/Soccer5v5Learning.exe'
BEHAVIOR = 'MNG5_Manager_v5'


def read(path): return json.loads(Path(path).read_text(encoding='utf-8-sig'))
def sha(path): return hashlib.sha256(Path(path).read_bytes()).hexdigest()
def write(path, value):
    path = Path(path); path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_suffix('.tmp')
    temporary.write_text(json.dumps(value, ensure_ascii=False, indent=2), encoding='utf-8')
    os.replace(temporary, path)

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--mode', choices=['preflight', 'train'], required=True)
    args = parser.parse_args(); preflight = args.mode == 'preflight'
    run = 'P9-V8-preflight-20261015' if preflight else 'MNG5_V5-20261016-r001'
    seed = 20016 if preflight else 20015
    base = EVIDENCE / run; base.mkdir(parents=True, exist_ok=True)
    status = base / 'orchestrator-status.json'; started = time.time(); completed = []
    env = os.environ.copy()
    env.update(CUDA_VISIBLE_DEVICES='0', OMP_NUM_THREADS='1', OPENBLAS_NUM_THREADS='1', MKL_NUM_THREADS='1', PYTHONUNBUFFERED='1')
    powershell = shutil.which('pwsh') or shutil.which('powershell')
    def frozen():
        from importlib.machinery import SourceFileLoader
        SourceFileLoader('five_frozen_verify', str(ROOT/'Tools/Verify-FiveFrozen.py')).load_module().verify()
    def execute(label, command):
        frozen()
        receipt = base / (label + '-process.json')
        if receipt.exists():
            assert read(receipt)['exitCode'] == 0, f'Failed operation must be diagnosed before rerun: {label}'
            return
        write(status, dict(environments=16, transitionStep=30167, device='cuda', cudaTransitionStep=168948, state='running', mode=args.mode, run=run, operation=label, completed=completed,
                          targetSteps=2000000, eventualTarget=2000000, started=started, updated=time.time()))
        print('START', label, flush=True)
        with (base/(label+'-stdout.log')).open('w', encoding='utf-8') as out, (base/(label+'-stderr.log')).open('w', encoding='utf-8') as err:
            process = subprocess.Popen(command, cwd=ROOT, env=env, stdout=out, stderr=err)
            write(base/'active-process.json', dict(label=label, pid=process.pid, arguments=list(map(str, command)), started=time.time()))
            code = process.wait()
        write(receipt, dict(exitCode=code, ended=time.time(), arguments=list(map(str, command))))
        assert code == 0, f'{label} failed, exit {code}'
        print('DONE', label, flush=True)
    def train(step, resume):
        assert 0 <= step <= 2000000
        segment = f'to-{step:07d}-16env-cuda' if step else 'to-0000000'
        command = [powershell, '-NoProfile', '-File', 'Tools/Run-FiveFrozenTraining16CUDA.ps1', '-RunName', run,
                   '-Segment', segment, '-StopAt', str(step), '-Seed', str(seed), '-Build', BUILD.relative_to(ROOT).as_posix(),
                   '-EvidenceDirectory', 'Logs/Manager5v5/p9-frozen-v8-20261015', '-PlayerRuntime', 'tactics-v8-stamina-owned-turn']
        if resume: command += ['-Resume']
        execute('train-'+segment, command)
        result = read(base/segment/'process-result.json'); assert not result['failures']
        assert result['traceAudit']['traceFiles'] == (16 if step else 32)
        if step:
            stop = read(base/segment/'safe-stop.json')
            assert stop['steps'][BEHAVIOR] == step and not stop['requestedStop']
            assert result['optimizerUpdates'] > 0
            assert read(base/segment/'cuda-device.json')['devices'] == ['cuda:0']
        path = ROOT/'results/Manager5v5'/run/BEHAVIOR/f'{BEHAVIOR}-{step}.onnx'
        assert path.is_file() and path.with_suffix('.pt').is_file()
        return path
    def evaluate(step, candidate, opponent, label, pairs=12, prefix='eval'):
        operation = f'{prefix}-{step:07d}-{label}'; output = base/operation
        execute(operation, [sys.executable, 'Tools/five_frozen_evaluate.py', '--candidate', str(candidate),
            '--opponent', str(opponent), '--build', str(BUILD), '--output', str(output), '--pairs', str(pairs),
            '--seed', '63000', '--workers', str(min(16, pairs*2)), '--worker', '200'])
        summary = read(output/'summary.json'); assert summary['passed'] and summary['games'] == 2*pairs
        check = audit(output, expected_runtime='tactics-v8-stamina-owned-turn'); write(output/'final-match-audit.json', check)
        summary['opponentActionCounts'] = check['opponentActionCounts']
        return summary
    def parity(step, model, observation_step=None):
        observation_step = step if observation_step is None else observation_step
        observations = base/f'eval-{observation_step:07d}-strong'
        if not preflight and step < 400000:
            observations = EVIDENCE/'P9-V8-preflight-20261015/eval-0012000-strong'
        execute(f'parity-{step:07d}', [sys.executable, 'Tools/Audit-FiveCheckpoint.py', '--checkpoint', str(model.with_suffix('.pt')),
            '--observations', str(observations), '--output', str(base/f'parity-{step:07d}.json')])
        assert read(base/f'parity-{step:07d}.json')['step'] == step
    try:
        frozen(); assert BUILD.is_file()
        assert read(EVIDENCE/'P9-V8-CUDA-verify-20261016/verification.json')['passed'], 'CUDA validation must pass before production continuation'
        if preflight:
            train(5000, False); model = train(12000, True)
            summary = evaluate(12000, model, 'rule', 'strong', 2); parity(12000, model)
            completed.append(dict(step=12000, evaluations={'strong': summary}))
        else:
            assert read(EVIDENCE/'P9-V8-preflight-20261015/orchestrator-status.json')['state'] == 'complete'
            assert read(base/'extension-1200-to-2000/authorization.json')['targetSteps'] == 2000000
            previous = read(base/'orchestrator-status.json')
            assert previous['state'] == 'complete' and previous['targetSteps'] == 1200000, 'Finish the 1.2M evaluation phase first'
            registry = read(base/'model-registry.json')['stages']
            completed = read(base/'stage-results.json')
            assert [r['step'] for r in registry] == list(range(0,1200001,200000))
            assert completed[-1]['step'] == 1200000
            for r in registry:
                assert sha(Path(r['onnx'])) == r['onnxSha256']
                assert sha(Path(r['onnx']).with_suffix('.pt')) == r['ptSha256']
            fixed = [('strong', 'rule')]
            plan = read(ROOT/'docs/soccer/rebuild-5v5/p9-frozen-v8-plan.json')
            assert plan['targetDecisions'] == 2000000 and plan['evaluationMatches'] == 480
            for stage in plan['stages']:
                step = stage['step']
                if step <= 1200000: continue
                assert step <= 2000000
                model = train(step, step > 0)
                opponents = fixed + [(f'stage-{r["step"]:07d}', r['onnx']) for r in registry if r['step'] % 400000 == 0]
                summaries = {}
                if stage['evaluate']:
                    assert len(opponents) * 24 == stage['matches']
                    for label, opponent in opponents:
                        summaries[label] = evaluate(step, model, opponent, label)
                parity(step, model, step if step % 400000 == 0 else step-200000)
                registry.append(dict(step=step, onnx=str(model), onnxSha256=sha(model), ptSha256=sha(model.with_suffix('.pt'))))
                completed.append(dict(step=step, evaluations=summaries))
                write(base/'model-registry.json', dict(run=run, fromScratch=True, selfPlayOnly=True, champion=None, stages=registry))
                write(base/'stage-results.json', completed)
                from importlib.machinery import SourceFileLoader
                SourceFileLoader('frozen_progress', str(ROOT/'Tools/Report-FiveFrozenProgressCUDA2000.py')).load_module().report(base)
                report_path = base/'stage-report.md'
                report_path.write_text(report_path.read_text(encoding='utf-8').replace('목표 200만 감독 결정.', '승인 상한 200만 감독 결정. 기존 200만 학습률 일정 유지.'), encoding='utf-8')
        write(status, dict(environments=16, transitionStep=30167, device='cuda', cudaTransitionStep=168948, state='complete', mode=args.mode, run=run, completed=completed, targetSteps=2000000,
                           eventualTarget=2000000, started=started, ended=time.time()))
        print('COMPLETE', args.mode, flush=True)
    except BaseException:
        write(status, dict(state='failed', mode=args.mode, run=run, completed=completed, started=started,
                           ended=time.time(), error=traceback.format_exc()))
        raise

if __name__ == '__main__': main()
