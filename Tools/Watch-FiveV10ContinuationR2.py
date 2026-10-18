"""Continuous foreground observer; no recurring scheduler and no automatic restart."""
import shutil
import time
from five_v10_run_common_r2 import *

base=BASE/RUN
last_operation=None
while True:
    state=read(base/'orchestrator-status.json')
    operation=state.get('operation',state['state'])
    summary=dict(state=state['state'],operation=operation,freeGiB=round(shutil.disk_usage(ROOT).free/1024**3,1))
    if operation.startswith('train-'):
        target=int(operation.split('-')[1]);folder=base/f'to-{target}-32env-cuda'
        progress=folder/'progress.json'
        if progress.exists():
            record=read(progress)
            summary.update(step=record['steps'][BEHAVIOR],updates=record['optimizerUpdates'],progressAge=round(time.time()-record['utc']))
            assert summary['step']<=2800000,'Unexpected step beyond authorized cap'
            if summary['progressAge']>600 or summary['freeGiB']<3:
                reason='Monitor detected stale progress (>600 seconds) or low disk (<3GiB)'
                (folder/'STOP').write_text(reason,encoding='utf-8')
                summary['safeStopRequested']=reason
                write(base/'monitor-intervention.json',summary)
        elif time.time()-state.get('updated',time.time())>600:
            (folder/'STOP').write_text('No progress after 600 seconds',encoding='utf-8')
            summary['safeStopRequested']='No progress after 600 seconds'
            write(base/'monitor-intervention.json',summary)
    write(base/'continuous-monitor.json',dict(**summary,utc=time.time(),scheduled=False))
    print(json.dumps(summary),flush=True)
    if state['state'] in ('complete','failed') or summary.get('safeStopRequested'):break
    time.sleep(55)
