"""Compact read-only snapshot of the authorized 600k run."""
import json
import time
from pathlib import Path

BASE = Path(__file__).resolve().parent.parent / 'Logs/Manager5v5/p9-tactics-20261014/MNG5_V2-20261014-r002'


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def main():
    status = read(BASE / 'orchestrator-status.json')
    operation = status.get('operation', '')
    output = dict(state=status['state'], operation=operation,
                  elapsedSeconds=round(time.time()-status['started']),
                  completedStages=[s['step'] for s in status.get('completed', [])])
    if operation.startswith('train-to-'):
        segment = BASE / operation.removeprefix('train-')
        for name in ('progress.json', 'safe-stop.json'):
            path = segment / name
            if path.exists():
                value = read(path)
                output[name] = {k: value[k] for k in ('steps', 'optimizerUpdates', 'elapsedSeconds', 'utc', 'requestedStop') if k in value}
                if 'traceAudit' in value:
                    output['traces'] = value['traceAudit']['traceFiles']
                    output['trainingMatchTerminals'] = value['traceAudit']['terminals']//2
    elif operation.startswith(('eval-', 'baseline-')):
        folder = BASE / operation
        results = [read(p) for p in folder.glob('match-*/result.json')]
        output.update(completedGames=len(results), expectedGames=24,
                      failures=[r for r in results if not r['passed']])
    if status['state']=='failed':
        output['error'] = status.get('error')
    completed = sorted(BASE.glob('*-process.json'))
    output['completedOperations'] = len(completed)
    print(json.dumps(output, ensure_ascii=False))


if __name__ == '__main__':
    main()
