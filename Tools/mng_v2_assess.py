"""Preparation gate and predeclared paired-bootstrap promotion gate."""
import argparse
import hashlib
import json
from pathlib import Path
import torch

def sha(path): return hashlib.sha256(Path(path).read_bytes()).hexdigest()

def compatible(reports):
    first=reports[0]['manifest']
    for report in reports:
        for field in ('runtimeSha','schema','seconds','protocol','inference'):
            assert report['manifest'][field]==first[field], f'Incompatible {field}'
        assert report['summary']['integrity']
    return first

def preparation(candidate, baseline):
    manifest=compatible([candidate,baseline])
    assert manifest['opponent']==baseline['manifest']['opponent']=='R0-Full-v2'
    assert baseline['manifest']['candidate']=='uniform-valid'
    assert candidate['manifest']['seedOffset']==baseline['manifest']['seedOffset']
    assert candidate['summary']['matches']==baseline['summary']['matches']==40
    score=candidate['summary']['scoreRate']; gain=score-baseline['summary']['scoreRate']
    signals=[]
    for team in (0,1):
        matches=[r for r in candidate['matches'] if r['policyTeam']==team]
        signals.append(dict(team=team,shots=sum(r['shotStrikes'] for r in matches),
            recoveries=sum(r['observedRecoveries'] for r in matches)))
    checks=dict(scoreRate=score>=.40,gainOverRandom=gain>=.10-1e-12,
        bothTeamsHaveShotAndRecovery=all(r['shots']>0 and r['recoveries']>0 for r in signals),integrity=True)
    return dict(passed=all(checks.values()),checks=checks,scoreRate=score,gainOverRandom=gain,signals=signals,
        runtimeSha=manifest['runtimeSha'],candidateSha=manifest['candidate'],
        note='Quantitative gate; task/long-stall/abuse review remains required before self-play.')

def promotion(holdout, history, step, candidate_r0, random_r0):
    compatible([holdout]+history+[candidate_r0,random_r0])
    assert 1 <= len(history) <= 4
    assert len({r['manifest']['opponent'] for r in history})==len(history), 'Duplicate league opponent'
    for report in history:
        assert report['summary']['matches']==40, 'Forty-game development league required'
        assert report['manifest']['candidate']==holdout['manifest']['candidate'], 'Mixed league candidate'
        assert not ({r['seed'] for r in report['matches']} & {r['seed'] for r in holdout['matches']}), 'Holdout seeds reused'
    assert candidate_r0['manifest']['candidate']==holdout['manifest']['candidate']
    assert candidate_r0['manifest']['opponent']==random_r0['manifest']['opponent']=='R0-Full-v2'
    assert random_r0['manifest']['candidate']=='uniform-valid'
    assert candidate_r0['manifest']['seedOffset']==random_r0['manifest']['seedOffset']
    summary=holdout['summary']
    assert holdout['manifest']['opponent'] in {r['manifest']['opponent'] for r in history}, 'Holdout opponent missing from league'
    assert summary['matches'] in (80,160), 'Predeclared holdout budget required'
    rates=[r['summary']['scoreRate'] for r in history]
    checks=dict(experience=step>=400000,score=summary['scoreRate']>=.60,
        pairedLower=summary['paired95'][0]>.50,historyMean=sum(rates)/len(rates)>=.55,
        historyFloor=min(rates)>=.45,teams=min(summary['perTeam'])>=.50,
        goalDifference=summary['goalsFor']>=summary['goalsAgainst'],
        aboveRandom=candidate_r0['summary']['scoreRate']>=random_r0['summary']['scoreRate'],integrity=True)
    return dict(passed=all(checks.values()),checks=checks,
        note='R0 regression and fixed baseline review must also be recorded; no automatic registry promotion.')

def main():
    p=argparse.ArgumentParser(); p.add_argument('--candidate',required=True); p.add_argument('--baseline',required=True)
    p.add_argument('--checkpoint',required=True); p.add_argument('--output',required=True); a=p.parse_args()
    path=Path(a.output); assert not path.exists(), 'Preserve previous assessment'
    candidate=json.loads(Path(a.candidate).read_text()); baseline=json.loads(Path(a.baseline).read_text())
    checkpoint_path=Path(a.checkpoint).resolve()
    assert checkpoint_path.parent.parent.name.startswith('MNG_MS2V2-'), 'Preparation must originate in an MS2-v2 Run'
    assert sha(checkpoint_path.with_suffix('.onnx'))==candidate['manifest']['candidate'], 'Checkpoint/ONNX pair mismatch'
    result=preparation(candidate,baseline)
    result.update(candidateReportSha=sha(a.candidate),baselineReportSha=sha(a.baseline),parentCheckpointSha=sha(a.checkpoint))
    if result['passed']:
        checkpoint=torch.load(a.checkpoint,map_location='cpu',weights_only=False)
        actor={k:v for k,v in checkpoint.items() if k in ('Policy','global_step')}
        assert 'Policy' in actor
        initial=path.with_name(path.stem+'-initial-policy.pt')
        assert not initial.exists()
        torch.save(actor,initial)
        result.update(initialPolicy=str(initial.resolve()),initialPolicySha=sha(initial))
    path.write_text(json.dumps(result,indent=2)); print(json.dumps(result,indent=2))

if __name__=='__main__': main()
