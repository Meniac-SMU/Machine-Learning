"""Read-only compact monitor for the ongoing PS training."""
import json
import math
from pathlib import Path
import time
from tensorboard.backend.event_processing.event_accumulator import EventAccumulator
from mng_player_log_guard import PlayerLogGuard

ROOT=Path(__file__).resolve().parent.parent
E=ROOT/'Logs/MNG-Rebuild/MNG_MS3V3-20260929-r002'
R=ROOT/'results/MNG_MS3V3-20260929-r002'
p=json.loads((E/'progress.json').read_text())
step=p.get('steps',{}).get('MNG_ManagerV2',p.get('parentStep',2001834))
a=EventAccumulator(str(R/'MNG_ManagerV2'));a.Reload();scalars={}
for tag in ['Policy/Entropy','Policy/Approx KL','Policy/Clip Fraction','Losses/Policy Loss','Losses/Value Loss','Self-play/ELO','PS/Auxiliary CE','PS/Auxiliary KL']:
    if tag in a.Tags()['scalars']:
        values=a.Scalars(tag);assert all(math.isfinite(v.value) for v in values)
        scalars[tag]=dict(step=values[-1].step,value=values[-1].value)
failures=PlayerLogGuard(R/'run_logs').scan()
print(json.dumps(dict(step=step,additionalSteps=step-2001834,ageSeconds=round(time.time()-p.get('utc',time.time())),
    stopRequested=(E/'STOP').exists(),playerErrors=failures,scalars=scalars)))
