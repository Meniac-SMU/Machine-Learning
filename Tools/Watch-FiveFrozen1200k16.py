"""Continuous local supervision, without scheduled model invocations."""
import json
import time
import shutil
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
BASE = ROOT / 'Logs/Manager5v5/p9-frozen-v8-20261015/MNG5_V5-20261016-r001'
MODEL = ROOT / 'results/Manager5v5/MNG5_V5-20261016-r001/MNG5_Manager_v5'


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def main():
    reported_saves = {0}
    reported_evaluations = {0}
    last_print = 0
    while True:
        status = read(BASE / 'orchestrator-status.json')
        operation = status.get('operation', '')
        progress = None
        if operation.startswith('train-to-'):
            segment = BASE / operation.removeprefix('train-')
            progress_path = segment / 'progress.json'
            if progress_path.exists():
                progress = read(progress_path)
            stop = segment / 'safe-stop.json'
            result = segment / 'process-result.json'
            if stop.exists() and result.exists():
                saved = read(stop)
                step = saved['steps']['MNG5_Manager_v5']
                if step % 200000 == 0 and step not in reported_saves:
                    assert not saved['requestedStop']
                    report = read(result)
                    assert not report['failures']
                    assert report['traceAudit']['traceFiles'] == 16
                    for name in [f'MNG5_Manager_v5-{step}.pt', f'MNG5_Manager_v5-{step}.onnx', f'five-pool-{step}.pt']:
                        assert (MODEL / name).is_file(), name
                    print(json.dumps(dict(event='saved', step=step, updates=report['optimizerUpdates'])), flush=True)
                    reported_saves.add(step)
        results_path = BASE / 'stage-results.json'
        if results_path.exists():
            for stage in read(results_path):
                step = stage['step']
                if stage['evaluations'] and step not in reported_evaluations:
                    print(json.dumps(dict(event='evaluated', step=step, opponents={k:{n:v[n] for n in ['games','wins','draws','losses','goalsFor','goalsAgainst']} for k,v in stage['evaluations'].items()})), flush=True)
                    reported_evaluations.add(step)
        observation = dict(state=status['state'], operation=operation, checkedUtc=time.time(), freeGiB=round(shutil.disk_usage(ROOT).free/1024**3, 2))
        if progress:
            observation.update(step=progress['steps']['MNG5_Manager_v5'], updates=progress['optimizerUpdates'], traceRows=progress['traceAudit']['rows'], progressAge=round(time.time()-progress['utc'],1))
        (BASE/'continuous-monitor.json').write_text(json.dumps(observation,indent=2),encoding='utf-8')
        if status['state'] in ('failed', 'complete'):
            print(json.dumps(dict(event=status['state'], status=status)), flush=True)
            return
        if time.time()-last_print >= 60:
            print(json.dumps(dict(event='pulse', **observation)), flush=True)
            last_print=time.time()
        time.sleep(5)


if __name__ == '__main__':
    main()
