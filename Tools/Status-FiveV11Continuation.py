"""Compact read-only progress for the current authorized job."""
import json
import time
from five_v11_continuation_common import *

state=read(BASE/'pipeline-status.json')
result=dict(state=state['state'],operation=state.get('operation'))
if state.get('operation')=='training' and state['state']=='running':
    training=read(BASE/RUN/'orchestrator-status.json')
    operation=training.get('operation','')
    result.update(operation=operation,trainingState=training['state'])
    if operation.startswith('train-'):
        step=int(operation.split('-')[1])
        folder=BASE/RUN/f'to-{step}-24env-cuda'
        if (folder/'progress.json').exists():
            p=read(folder/'progress.json')
            result.update(step=p['steps'][BEHAVIOR],updates=p['optimizerUpdates'],environments=p['traceAudit']['traceFiles'],ageSeconds=round(time.time()-p['utc']))
        if (folder/'player-failure.json').exists():result['failure']=read(folder/'player-failure.json')
if state['state']=='failed':result['error']=state.get('error')
print(json.dumps(result))
