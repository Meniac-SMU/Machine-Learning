"""Authorized new execution: from scratch, score-only self-play, stop exactly at 600k."""
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
EVIDENCE = ROOT / 'Logs/Manager5v5/p9-tactics-20261014'
BUILD = ROOT / 'Builds/Manager5v5/p9-tactics-v3-20261014/Soccer5v5Learning.exe'
BEHAVIOR = 'MNG5_Manager_v2'
OLD = ROOT / 'results/Manager5v5/MNG5_V2-20261014-r001' / BEHAVIOR
EARLY = ROOT / 'results/Manager5v5/MNG5_V2-20261013-r001' / BEHAVIOR / f'{BEHAVIOR}-200011.onnx'

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
    run = 'P9-Tactics-preflight-20261014' if preflight else 'MNG5_V2-20261014-r002'
    seed = 19017 if preflight else 19016
    base = EVIDENCE / run; base.mkdir(exist_ok=True)
    status = base / 'orchestrator-status.json'; started = time.time(); completed = []
    env = os.environ.copy()
    env.update(CUDA_VISIBLE_DEVICES='-1', OMP_NUM_THREADS='1', OPENBLAS_NUM_THREADS='1', MKL_NUM_THREADS='1', PYTHONUNBUFFERED='1')
    powershell = shutil.which('pwsh') or shutil.which('powershell')
    def frozen():
        manifest = read(EVIDENCE / 'frozen-version.json'); assert manifest['passed']
        for item in manifest['executionFiles']:
            assert sha(ROOT / item['path']).lower() == item['sha256'].lower(), item['path']
    def execute(label, command):
        frozen()
        receipt = base / (label + '-process.json')
        if receipt.exists():
            assert read(receipt)['exitCode'] == 0, f'Failed operation must be diagnosed before rerun: {label}'
            return
        write(status, dict(state='running', mode=args.mode, run=run, operation=label, completed=completed,
                          authorizedStop=600000, eventualTarget=1200000, started=started, updated=time.time()))
        print('START', label, flush=True)
        with (base/(label+'-stdout.log')).open('w', encoding='utf-8') as out, (base/(label+'-stderr.log')).open('w', encoding='utf-8') as err:
            process = subprocess.Popen(command, cwd=ROOT, env=env, stdout=out, stderr=err)
            write(base/'active-process.json', dict(label=label, pid=process.pid, arguments=list(map(str, command)), started=time.time()))
            code = process.wait()
        write(receipt, dict(exitCode=code, ended=time.time(), arguments=list(map(str, command))))
        assert code == 0, f'{label} failed, exit {code}'
        print('DONE', label, flush=True)
    def train(step, resume):
        assert 0 <= step <= 600000
        segment = f'to-{step:07d}'
        command = [powershell, '-NoProfile', '-File', 'Tools/Run-FiveSelfPlayTraining.ps1', '-RunName', run,
                   '-Segment', segment, '-StopAt', str(step), '-Seed', str(seed), '-Build', BUILD.relative_to(ROOT).as_posix(),
                   '-EvidenceDirectory', 'Logs/Manager5v5/p9-tactics-20261014', '-PlayerRuntime', 'tactics-v3-expansion']
        if resume: command += ['-Resume']
        execute('train-'+segment, command)
        result = read(base/segment/'process-result.json'); assert not result['failures']
        assert result['traceAudit']['traceFiles'] == 32
        if step:
            stop = read(base/segment/'safe-stop.json')
            assert stop['steps'][BEHAVIOR] == step and not stop['requestedStop']
            assert result['optimizerUpdates'] > 0
        path = ROOT/'results/Manager5v5'/run/BEHAVIOR/f'{BEHAVIOR}-{step}.onnx'
        assert path.is_file() and path.with_suffix('.pt').is_file()
        return path
    def evaluate(step, candidate, opponent, label, pairs=12, prefix='eval'):
        operation = f'{prefix}-{step:07d}-{label}'; output = base/operation
        execute(operation, [sys.executable, 'Tools/five_tactic_evaluate.py', '--candidate', str(candidate),
            '--opponent', str(opponent), '--build', str(BUILD), '--output', str(output), '--pairs', str(pairs),
            '--seed', '63000', '--workers', str(min(24, pairs*2)), '--worker', '200'])
        summary = read(output/'summary.json'); assert summary['passed'] and summary['games'] == 2*pairs
        check = audit(output, expected_runtime='tactics-v3-expansion'); write(output/'final-match-audit.json', check)
        summary['opponentActionCounts'] = check['opponentActionCounts']
        return summary
    def parity(step, model):
        execute(f'parity-{step:07d}', [sys.executable, 'Tools/Audit-FiveCheckpoint.py', '--checkpoint', str(model.with_suffix('.pt')),
            '--observations', str(base/f'eval-{step:07d}-strong'), '--output', str(base/f'parity-{step:07d}.json')])
        assert read(base/f'parity-{step:07d}.json')['step'] == step
    try:
        frozen(); assert BUILD.is_file()
        if preflight:
            train(5000, False); model = train(12000, True)
            summary = evaluate(12000, model, 'rule', 'strong', 2); parity(12000, model)
            completed.append(dict(step=12000, evaluations={'strong': summary}))
        else:
            assert read(EVIDENCE/'P9-Tactics-preflight-20261014/orchestrator-status.json')['state'] == 'complete'
            registry = []
            historical = [(f'old-{s:07d}', OLD/f'{BEHAVIOR}-{s}.onnx') for s in range(0, 1200001, 200000)]
            assert all(p.is_file() for _, p in historical) and EARLY.is_file()
            # Fixed older opponents have the same weight at every stage. Same-run history is reported separately.
            fixed = [('strong', 'rule')] + historical
            for step in range(0, 600001, 200000):
                model = train(step, step > 0)
                opponents = fixed + [(f'stage-{r["step"]:07d}', r['onnx']) for r in registry] + [('early-0200011', EARLY)]
                summaries = {}
                for label, opponent in opponents:
                    summaries[label] = evaluate(step, model, opponent, label)
                parity(step, model)
                registry.append(dict(step=step, onnx=str(model), onnxSha256=sha(model), ptSha256=sha(model.with_suffix('.pt'))))
                completed.append(dict(step=step, evaluations=summaries))
                write(base/'model-registry.json', dict(run=run, fromScratch=True, selfPlayOnly=True, champion=None, stages=registry))
                write(base/'stage-results.json', completed)
            baseline = {}
            for label, opponent in fixed:
                baseline[label] = evaluate(1200000, OLD/f'{BEHAVIOR}-1200000.onnx', opponent, label, prefix='baseline')
                write(base/'baseline-results.json', baseline)
        write(status, dict(state='complete', mode=args.mode, run=run, completed=completed, authorizedStop=600000,
                           eventualTarget=1200000, started=started, ended=time.time()))
        print('COMPLETE', args.mode, flush=True)
    except BaseException:
        write(status, dict(state='failed', mode=args.mode, run=run, completed=completed, started=started,
                           ended=time.time(), error=traceback.format_exc()))
        raise

if __name__ == '__main__': main()
