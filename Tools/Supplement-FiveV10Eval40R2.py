"""Preserve completed 24-game batches and add 16 unique games per opponent."""
import os
import subprocess
import sys
import time
import traceback
import numpy as np
from five_v10_run_common_r2 import *
from five_match_metrics import aggregate


def summarize(rows):
    assert len(rows)==40 and all(r['passed'] and r['commandLimitsVerified'] for r in rows)
    assert {(r['seed'],r['candidateTeam']) for r in rows}=={(s,t) for s in range(63000,63020) for t in (0,1)}
    differences=[r['score']['own']-r['score']['other'] for r in rows]
    result=dict(passed=True,games=40,wins=sum(d>0 for d in differences),draws=sum(d==0 for d in differences),
        losses=sum(d<0 for d in differences),goalsFor=sum(r['score']['own'] for r in rows),
        goalsAgainst=sum(r['score']['other'] for r in rows),meanGoalDifference=float(np.mean(differences)),
        candidateMetrics=aggregate([r['candidateMetrics'] for r in rows]),opponentMetrics=aggregate([r['opponentMetrics'] for r in rows]))
    for field in ('actionCounts','stateCounts','probabilitySums','executionSteps','opponentExecutionSteps','opponentActionCounts'):
        result[field]=np.sum([r[field] for r in rows],axis=0).tolist()
    result['actionRatios']=(np.sum([r['actionCounts'] for r in rows],axis=(0,1))/sum(sum(r['stateCounts']) for r in rows)).tolist()
    result['managerDecisions']=sum(sum(r['stateCounts']) for r in rows)
    result['managerDecisionsPerMinute']=result['managerDecisions']/(40*5)
    result['opponentManagerDecisions']=int(np.sum(result['opponentActionCounts']))
    result['opponentManagerDecisionsPerMinute']=result['opponentManagerDecisions']/(40*5)
    return result


def main():
    base=BASE/RUN;status=base/'evaluation40-status.json';assert not status.exists(),'Preserve existing execution'
    write(status,dict(state='waiting-for-original-evaluation',requiredGamesPerOpponent=40,totalGames=320))
    env=os.environ.copy();env.update(OMP_NUM_THREADS='1',OPENBLAS_NUM_THREADS='1',MKL_NUM_THREADS='1',PYTHONUNBUFFERED='1')
    try:
        while True:
            original=read(base/'orchestrator-status.json')
            assert original['state']!='failed','Original run failed; no automatic restart'
            if original['state']=='complete':break
            time.sleep(55)
        original_stages=read(base/'stage-results.json');evaluations=original_stages[-1]['evaluations'];combined={}
        for label,old_summary in evaluations.items():
            assert old_summary['passed'] and old_summary['games']==24
            verify();old_folder=base/f'eval-2800000-{label}';protocol=read(old_folder/'protocol.json')
            assert protocol['seeds']==list(range(63000,63012))
            folder=base/f'eval-2800000-{label}-supplement16'
            write(status,dict(state='running',opponent=label,completedOpponents=len(combined),requiredGamesPerOpponent=40,totalGames=320))
            command=[sys.executable,'Tools/five_v10_evaluate.py','--candidate',protocol['candidate'],
                '--opponent',protocol['opponent'],'--build',str(BUILD),'--output',str(folder),
                '--pairs','8','--seed','63012','--workers','16','--worker','300']
            with (base/f'supplement-{label}-stdout.log').open('w',encoding='utf-8') as out,(base/f'supplement-{label}-stderr.log').open('w',encoding='utf-8') as err:
                process=subprocess.Popen(command,cwd=ROOT,env=env,stdout=out,stderr=err,creationflags=subprocess.CREATE_NO_WINDOW)
                write(base/'evaluation40-active-process.json',dict(pid=process.pid,opponent=label,arguments=command,started=time.time()))
                code=process.wait()
            write(base/f'supplement-{label}-process.json',dict(exitCode=code,ended=time.time()))
            assert code==0,f'Supplement failed: {label}; preserve and stop'
            added=read(folder/'summary.json');assert added['passed'] and added['games']==16
            new_protocol=read(folder/'protocol.json')
            for key in ('candidateSha','opponentSha','evaluatorSha','contract','seconds','sampling','executionVariant'):
                assert new_protocol[key]==protocol[key],key
            sources=[p for directory in (old_folder,folder) for p in directory.glob('match-*/result.json')]
            rows=[read(p) for p in sources];rows.sort(key=lambda r:(r['seed'],r['candidateTeam']))
            summary=summarize(rows);summary['commandNames']=protocol['commandNames']
            merged=base/f'eval-2800000-{label}-combined40'
            write(merged/'summary.json',summary);write(merged/'matches.json',rows)
            write(merged/'provenance.json',dict(originalGames=24,supplementGames=16,seeds=list(range(63000,63020)),candidateTeams=[0,1],
                results=[dict(path=p.relative_to(ROOT).as_posix(),sha256=sha(p)) for p in sources],
                candidateSha=protocol['candidateSha'],opponentSha=protocol['opponentSha'],evaluatorSha=protocol['evaluatorSha']))
            combined[label]=summary
            write(base/'evaluation40-results.json',dict(passed=len(combined)==8,games=sum(s['games'] for s in combined.values()),evaluations=combined))
            print(json.dumps(dict(event='evaluated40',opponent=label,**{k:summary[k] for k in ('games','wins','draws','losses','goalsFor','goalsAgainst')})),flush=True)
        assert len(combined)==8
        write(status,dict(state='complete',requiredGamesPerOpponent=40,totalGames=320,completedOpponents=8,ended=time.time()))
    except BaseException:
        write(status,dict(state='failed',error=traceback.format_exc(),ended=time.time()));raise


if __name__=='__main__':main()
