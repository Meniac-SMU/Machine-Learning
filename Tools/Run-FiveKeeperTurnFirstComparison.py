"""Matched 40+40 full games for the authorized goalkeeper correction. No training."""
import os
import subprocess
import sys
import time
from five_v11_run_common import ROOT, BASE, read, write, verify

candidate = ROOT/'results/Manager5v5/MNG5_V6-20261016-r002/MNG5_Manager_v6/MNG5_Manager_v6-2800000.onnx'
opponent = ROOT/'results/Manager5v5/MNG5_V5-20261016-r001/MNG5_Manager_v5/MNG5_Manager_v5-2400000.onnx'
status = BASE/'comparison-status.json'
assert not status.exists(), 'Do not duplicate or automatically restart a comparison'
env = os.environ.copy()
env.update(OMP_NUM_THREADS='1', OPENBLAS_NUM_THREADS='1', MKL_NUM_THREADS='1', PYTHONUNBUFFERED='1')
for mode in ('off', 'on'):
    verify()
    with (BASE/f'matches-{mode}-stdout.log').open('w') as stdout, (BASE/f'matches-{mode}-stderr.log').open('w') as stderr:
        proc = subprocess.Popen([sys.executable, str(ROOT/'Tools/Evaluate-FiveV11ModelPair40.py'),
            '--candidate', str(candidate), '--opponent', str(opponent), '--output', str(BASE/f'matches-{mode}'),
            '--seed', '67000', '--workers', '16', '--worker', '400', '--keeper-turn-first', mode],
            cwd=ROOT, env=env, stdout=stdout, stderr=stderr, creationflags=subprocess.CREATE_NO_WINDOW)
        write(status, dict(state='running', mode=mode, pid=proc.pid, utc=time.time(), gamesPerMode=40))
        code = proc.wait()
    write(BASE/f'matches-{mode}-process.json', dict(exitCode=code, ended=time.time()))
    if code:
        write(status, dict(state='failed', mode=mode, exitCode=code, utc=time.time()))
        raise SystemExit(code)
    report = read(BASE/f'matches-{mode}/summary.json')
    assert report['passed'] and report['games'] == 40
    print(mode, '40/40', 'side-contact concessions', report['keeperSlowSideContactConceded'], flush=True)
verify()
write(status, dict(state='complete', games=80, utc=time.time(), trainingStarted=False))
