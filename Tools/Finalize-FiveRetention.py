"""Create a factual completion receipt after the named validation and Player runs end."""
import hashlib,json,zipfile
from datetime import datetime,timezone
from pathlib import Path

root=Path(__file__).resolve().parent.parent
ev=root/'Logs/Manager5v5/retention-20261012'
initial=root/'Logs/Manager5v5/initial-20261012'
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
tests=read(initial/'retention-final-regression-01.json')
player=read(ev/'player-final/player-result.json')
process=read(ev/'player-final/process-result.json')
if tests['failed'] or tests['passed']!=32 or process['exitCode'] or not player['finished'] or player['boundaryCorrections']:
    raise ValueError('Final checks did not pass')
baseline=read(ev/'baseline-all-alternatives.json')
development=read(ev/'focused-development.json')
confirmation=read(ev/'focused-confirmation.json')
if not all(r['physicalCollectionValid'] for r in [baseline,development,confirmation]):raise ValueError('Collection failure')
original=read(initial/'original-files.json')
changed=[r['path'] for r in original if hashlib.sha256((root/r['path']).read_bytes()).hexdigest().upper()!=r['sha256'].upper()]
if changed:raise ValueError(f'Original changes: {changed}')
unchanged=[]
with zipfile.ZipFile(initial/'retention-baseline-02-source.zip') as z:
    for name in ['FiveMatch','FivePlayer','FiveStamina','FiveTechniques','FiveProfile','FiveTactics','FiveRuleManager']:
        path=f'Assets/_Soccer/Manager5v5/Runtime/{name}.cs'
        same=z.read(path)==(root/path).read_bytes()
        if not same:raise ValueError(f'Gameplay changed: {path}')
        unchanged.append(path)
receipt={'completedUtc':datetime.now(timezone.utc).isoformat(),'weeklyUsageStopRemoved':True,
         'retainedCommands':list(range(8)),'removedCommands':[],'mergedCommands':[],
         'criterion':'Situational unique value and replacement cost, not equal usage',
         'physicalTrials':sum(r['trials'] for r in [baseline,development,confirmation]),
         'baselineTrials':baseline['trials'],'focusedDevelopmentTrials':development['trials'],'focusedConfirmationTrials':confirmation['trials'],
         'ruleGameSummary':read(ev/'rule-games-baseline.json')['summary'],
         'finalRegressionPassed':tests['passed'],'finalRegressionFailed':tests['failed'],
         'finalPlayer':player,'finalPlayerExitCode':process['exitCode'],
         'finalExecutable':'Builds/Manager5v5/retention-review-final/Soccer5v5.exe',
         'originalFilesChecked':len(original),'originalFilesChanged':changed,
         'unchangedGameplaySourcesComparedWithBaseline':unchanged,
         'changes':'T3/T5 display names, review frame capture, diagnostic tests, analysis tools, current instructions and reports',
         'trainingStarted':False,'retentionReviewCompleted':True,'globalBalancePassed':False,
         'priorIncomplete64GamesClosed':False,'latestPeriod48ComparisonCompleted':False}
out=ev/'completion.json'
if out.exists():raise ValueError('Preserve previous completion receipt')
out.write_text(json.dumps(receipt,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(receipt,ensure_ascii=False,indent=2))
