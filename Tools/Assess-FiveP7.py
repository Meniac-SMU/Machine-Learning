"""Assess predeclared P7 physical evidence. Never rewrite failed inputs or fit a score."""
import argparse
import collections
import glob
import json
import math
import statistics
from pathlib import Path

NAMES=['T1 속공 전개','T2 전방 롱패스','T3 와이드 탈압박','T4 측면 컷백','T5 측면 봉쇄 압박','T6 수신자 봉쇄','T7 전체 공 회수','T8 골문 앞 집결']
ROLE_CHECKS=[
 (0,'advantage','central-opening',1,'earlyMaxOwnAdvance',1),
 (0,'weakness','deep-goal-block',3,'ownGoals',-1),
 (1,'advantage','rear-pressure-far-receiver',0,'earlyMaxOwnAdvance',1),
 (1,'weakness','deep-goal-block',3,'ownGoals',-1),
 (2,'advantage','wide-carrier-switch',1,'safe3',1),
 (2,'weakness','central-opening',0,'earlyMaxOwnAdvance',-1),
 (3,'advantage','deep-goal-block',0,'ownGoals',1),
 (3,'weakness','nearby-loose-ball',6,'earlyPossession',-1),
 (4,'advantage','wide-trap-with-deep-outlet',6,'ownPossession',1),
 (4,'original-hypothesis','distant-receiver-denial',5,'enemyReceptions',1),
 (4,'weakness','nearby-loose-ball',6,'earlyPossession',-1),
 (5,'advantage','distant-receiver-denial',6,'enemyReceptions',-1),
 (5,'weakness','central-goal-threat',7,'enemyGoals',1),
 (6,'advantage','nearby-loose-ball',5,'earlyPossession',1),
 (6,'weakness','wide-trap-with-deep-outlet',4,'ownPossession',-1),
 (7,'advantage','central-goal-threat',5,'enemyGoals',-1),
 (7,'weakness','deep-goal-block',3,'ownGoals',-1)]
OBJECTIVES={'central-opening':('earlyMaxOwnAdvance',1),'deep-goal-block':('ownGoals',1),
            'wide-trap-window':('ownPossession',1),'distant-receiver-denial':('enemyReceptions',-1)}
METRICS=['ownGoals','enemyGoals','ownPossession','enemyPossession','earlyMaxOwnAdvance','earlyPossession',
         'ownReceptions','enemyReceptions','ownShots','enemyShots','minimumMeanStamina']

def load(files,key):
    return [row for f in files for row in json.loads(Path(f).read_text(encoding='utf-8-sig'))[key]]
def value(row,metric):
    return float(0<=row['safeExitAt']<=3) if metric=='safe3' else row[metric]
def roles(rows):
    seeds=sorted({r['seed'] for r in rows});checks=[]
    keys={(r['seed'],r['mirror'],r['scenario'],r['candidate'],r['continuation']) for r in rows}
    complete=len(keys)==len(rows)==len(seeds)*352
    for command,kind,scenario,other,metric,direction in ROLE_CHECKS:
        differences=[];raw=[]
        for seed in seeds:
            samples=[]
            for mirror in range(2):
                pair=[]
                for candidate in [command,other]:
                    found=[r for r in rows if r['seed']==seed and r['mirror']==mirror and r['scenario']==scenario
                           and r['candidate']==candidate and r['continuation']=='fixed-twelve-seconds']
                    if len(found)!=1:raise ValueError('Missing or duplicated paired role trial')
                    pair.append(value(found[0],metric))
                samples.append(direction*(pair[0]-pair[1]));raw.append({'seed':seed,'mirror':mirror,'candidate':pair[0],'alternative':pair[1]})
            differences.append(statistics.mean(samples))
        positive=sum(x>1e-4 for x in differences)
        checks.append({'command':NAMES[command],'kind':kind,'scenario':scenario,'alternative':NAMES[other],
                       'metric':metric,'direction':direction,'meanDifference':statistics.mean(differences),
                       'positiveSeedPairs':positive,'seedPairCount':len(seeds),'seedDifferences':differences,
                       'passed':statistics.mean(differences)>1e-4 and positive>=math.ceil(.75*len(seeds)), 'samples':raw})
    contexts=[]
    for scenario in sorted({r['scenario'] for r in rows}):
        for continuation in ['fixed-twelve-seconds','rules-after-three-seconds']:
            vals=[]
            for command in range(8):
                subset=[r for r in rows if r['scenario']==scenario and r['continuation']==continuation and r['candidate']==command]
                vals.append({'command':NAMES[command],'samples':len(subset),
                    **{m:statistics.mean(r[m] for r in subset) for m in METRICS},'safeBy3':sum(0<=r['safeExitAt']<=3 for r in subset)})
            contexts.append({'scenario':scenario,'continuation':continuation,'values':vals})
    return {'complete':complete,'seeds':seeds,'trials':len(rows),'checks':checks,'contexts':contexts,
            'protocol':'v2: retain original distant-receiver hypothesis as diagnostic; T5 weakness is pre-confirmation nearby-loose-ball comparison.',
            'passed':complete and all(c['passed'] for c in checks if c['kind']!='original-hypothesis') and all(r['corrections']==0 and r['elapsed']>=12 for r in rows)}

def matrix(rows):
    seeds=sorted({r['seed'] for r in rows});contexts=[]
    keys={(r['seed'],r['mirror'],r['scenario'],r['phase'],r['candidate'],r['opponent']) for r in rows}
    complete=len(keys)==len(rows)==len(seeds)*1024
    for key in sorted({(r['scenario'],r['phase'],r['opponent']) for r in rows}):
        scenario,phase,opponent=key;metric,direction=OBJECTIVES[scenario];vals=[]
        for command in range(8):
            subset=[r for r in rows if (r['scenario'],r['phase'],r['opponent'])==key and r['candidate']==command]
            if len(subset)!=len(seeds)*2:raise ValueError('Missing matrix samples')
            vals.append({'command':NAMES[command],**{m:statistics.mean(r[m] for r in subset) for m in METRICS}})
        contexts.append({'scenario':scenario,'phase':phase,'opponent':NAMES[opponent],'metric':metric,'direction':direction,'values':vals})
    dominance=[]
    for command in range(8):
        for other in range(8):
            if command==other:continue
            ds=[c['direction']*(c['values'][command][c['metric']]-c['values'][other][c['metric']]) for c in contexts]
            dominance.append({'command':NAMES[command],'alternative':NAMES[other], 'better':sum(d>1e-4 for d in ds),'worse':sum(d<-1e-4 for d in ds),'tied':sum(abs(d)<=1e-4 for d in ds)})
    universal=[name for name in NAMES if all(d['worse']==0 and d['better']>0 for d in dominance if d['command']==name)]
    pairwise=[d for d in dominance if d['worse']==0 and d['better']>0]
    return {'complete':complete,'seeds':seeds,'trials':len(rows),'contexts':contexts,'dominance':dominance,'universalDominators':universal,'pairwiseDominators':pairwise,
            'passed':complete and not universal and all(r['corrections']==0 and r['elapsed']>=12 for r in rows)}

def games(rows):
    assessed=[];groups=[]
    for g in rows:
        issues=[]
        if not g['finished'] or g['elapsed']<g['duration']:issues.append('not-finished')
        if g['corrections']:issues.append('boundary-correction')
        if g['maximumStall']>=30:issues.append('stall-at-least-30-seconds')
        if g['fixedOpponent']<0:
            if g['passes']<=0:issues.append('no-pass')
            if g['shots']<=0:issues.append('no-shot')
            if g['recoveries']<=1:issues.append('insufficient-recovery')
        selected=[0]*8;opportunity=[0]*8;used=[0]*8;last={};changes=0;reselections=0;kicked={};received=set();preparation=[];ready_failed=0
        for d in g['decisions']:
            chosen=d['command'];selected[chosen]+=1
            if d['team'] in last and last[d['team']]!=chosen:changes+=1
            elif d['team'] in last:reselections+=1
            last[d['team']]=chosen
            for c in range(8):
                if d['opportunities']&(1<<c):
                    opportunity[c]+=1
                    if chosen==c:used[c]+=1
        signals={}
        for e in g['events']:
            if e['kind']=='pass-signal':signals[e['id']]=e['time']
            elif e['kind']=='pass-kicked':
                kicked[e['id']]=e['time']
                if e['id'] in signals:preparation.append(e['time']-signals[e['id']])
            elif e['kind']=='pass-received':received.add(e['id'])
            elif e['kind']=='pass-failed' and e['reason']=='timeout' and e['id'] not in kicked:ready_failed+=1
        # A subsequent physical event is a trace landmark, not causal proof that the decision produced it.
        windows=[]
        for team in range(2):
            changes_for_team=[]
            for decision in g['decisions']:
                if decision['team']==team and (not changes_for_team or changes_for_team[-1]['command']!=decision['command']):changes_for_team.append(decision)
            for index,decision in enumerate(changes_for_team):
                end=changes_for_team[index+1]['time'] if index+1<len(changes_for_team) else g['elapsed']
                landmarks=[e for e in g['events'] if decision['time']<=e['time']<end and e['kind'] in ['pass-kicked','pass-received','shot','possession']
                           and 0<=e['actor']<10 and e['actor']//5==team]
                windows.append({'team':team,'command':NAMES[decision['command']],'reason':decision['reason'],'start':decision['time'],'end':end,
                                'firstOwnPhysicalEvent':landmarks[0]['kind'] if landmarks else None,
                                'firstOwnPhysicalEventDelay':landmarks[0]['time']-decision['time'] if landmarks else None})
        assessed.append({**{k:g[k] for k in ['seed','mirror','duration','decisionPeriod','fixedOpponent','redScore','navyScore','passes','receptions','shots','maximumStall','corrections']},
                         'healthy':not issues,'issues':issues,'selections':selected,'opportunities':opportunity,'selectedWithOpportunity':used,
                         'actualChanges':changes,'reselections':reselections,'signals':len(signals),'kickedSignals':len(kicked),'stableReceptions':len(received),
                         'preparationTimeouts':ready_failed,'meanPassPreparation':statistics.mean(preparation) if preparation else None,'commandWindows':windows})
    for group in sorted({(g['duration'],g['decisionPeriod'],g['fixedOpponent']) for g in assessed}):
        subset=[g for g in assessed if (g['duration'],g['decisionPeriod'],g['fixedOpponent'])==group]
        groups.append({'duration':group[0],'period':group[1],'fixedOpponent':NAMES[group[2]] if group[2]>=0 else None,
            'games':len(subset),'healthy':sum(g['healthy'] for g in subset),'goals':sum(g['redScore']+g['navyScore'] for g in subset),
            **{k:sum(g[k] for g in subset) for k in ['passes','receptions','shots','corrections','actualChanges','reselections','signals','kickedSignals','stableReceptions','preparationTimeouts']},
            'maximumStall':max(g['maximumStall'] for g in subset),
            **{k:[sum(g[k][c] for g in subset) for c in range(8)] for k in ['selections','opportunities','selectedWithOpportunity']}})
    unique=len({(g['seed'],g['mirror'],g['duration'],g['decisionPeriod'],g['fixedOpponent']) for g in rows})==len(rows)
    return {'games':len(rows),'healthy':sum(g['healthy'] for g in assessed),'groups':groups,'matches':assessed,'uniqueGameKeys':unique,
            'passed':bool(rows) and unique and all(g['healthy'] for g in assessed),
            'opportunityDefinition':'Overlapping public-space indicators sampled at actual decisions; not an oracle, action mask, or uniform usage target.'}

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('mode',choices=['roles','matrix','games']);p.add_argument('--glob',required=True);p.add_argument('--output',required=True)
    p.add_argument('--expected-seeds',type=int);p.add_argument('--expected-games',type=int)
    a=p.parse_args();files=sorted(glob.glob(a.glob));out=Path(a.output)
    if not files:raise ValueError('No inputs')
    if out.exists():raise ValueError('Preserve previous assessment')
    report=globals()[a.mode](load(files,'matches' if a.mode=='games' else 'trials'))
    if a.mode=='games':
        if a.expected_games is None:raise ValueError('Declare expected game count before assessment')
        report['expectedGames']=a.expected_games;report['complete']=report['games']==a.expected_games and report['uniqueGameKeys']
        report['passed'] &= report['complete']
    else:
        if a.expected_seeds is None:raise ValueError('Declare expected seed count before assessment')
        report['expectedSeeds']=a.expected_seeds;report['complete'] &= len(report['seeds'])==a.expected_seeds
        report['passed'] &= report['complete']
    report.update(files=files,commandNames=NAMES,trainingStarted=False,scope='Predeclared finite physical P7 evidence, not universal football balance or learning success.')
    out.write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps({k:v for k,v in report.items() if k not in ['files','commandNames','matches','contexts','dominance','checks']},ensure_ascii=True))
    for check in report.get('checks',[]):
        print(json.dumps({k:v for k,v in check.items() if k!='samples'},ensure_ascii=True))
