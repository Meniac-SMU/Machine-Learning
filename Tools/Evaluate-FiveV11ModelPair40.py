"""Current v11 formal evaluation: exactly 20 seeds x both sides, no model updates."""
import argparse
import subprocess
import sys
from five_v11_run_common import ROOT, BUILD, verify

p = argparse.ArgumentParser(description=__doc__)
p.add_argument('--candidate', required=True)
p.add_argument('--opponent', required=True)
p.add_argument('--output', required=True)
p.add_argument('--seed', type=int, default=67000)
p.add_argument('--workers', type=int, default=16)
p.add_argument('--worker', type=int, default=400)
p.add_argument('--keeper-turn-first', choices=['on', 'off'], default='on')
a = p.parse_args()
assert 1 <= a.workers <= 32
verify()
subprocess.run([sys.executable, str(ROOT/'Tools/five_v11_evaluate.py'), '--candidate', a.candidate,
    '--opponent', a.opponent, '--build', str(BUILD), '--output', a.output, '--pairs', '20',
    '--seed', str(a.seed), '--workers', str(a.workers), '--worker', str(a.worker),
    '--keeper-turn-first', a.keeper_turn_first], cwd=ROOT, check=True)
verify()
