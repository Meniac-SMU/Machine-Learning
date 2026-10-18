"""Verify P7 receipts, current source identity, Player evidence and preserved originals."""
import hashlib
import json
import re
import zipfile
from datetime import datetime, timezone
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
BASE = ROOT / 'Logs/Manager5v5/p7-20261012'

def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))

def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest().upper()

def require(condition, message):
    if not condition:
        raise ValueError(message)

def main():
    out = BASE / 'completion.json'
    require(not out.exists(), 'Preserve previous completion receipt.')
    assessments = {}
    for name, key, count in [('roles-qualified-assessment-01', 'trials', 1920),
                             ('matrix-final-assessment-01', 'trials', 2048),
                             ('natural-final-assessment-01', 'games', 64),
                             ('period-final-assessment-01', 'games', 48),
                             ('repeat-final-assessment-01', 'games', 32)]:
        data = read(BASE / (name+'.json'))
        require(data['passed'] and data['complete'] and data[key] == count, name+' did not pass.')
        assessments[name] = {'passed': True, key: count, 'sha256': digest(BASE / (name+'.json'))}
    run = read(BASE / 'p7-final-validation-01.json')
    require(run['exitCode'] == 0 and run['testResult'] == 'Passed' and run['passed'] == 48
            and run['failed'] == 0 and run['skipped'] == 0, 'Expected 43 regressions and five final experiments.')
    additional=read(BASE/'p7-wide-role-confirmation-01.json')
    require(additional['exitCode']==0 and additional['passed']==1 and additional['failed']==0, 'Wide-role confirmation did not finish.')
    edit = read(BASE / 'p6-foundation-01.json')
    require(edit['exitCode'] == 0 and edit['passed'] == 11 and edit['failed'] == 0, 'Foundation checks failed.')
    with zipfile.ZipFile(BASE/'p6-foundation-01-source.zip') as snapshot:
        foundation_paths=[p for p in snapshot.namelist() if p.startswith('Assets/_Soccer/Manager5v5/')
                          and (p.endswith(('FiveProfile.cs','FiveStamina.cs','FiveProfile.asset')) or '/Tests/EditMode/' in p)]
        require(bool(foundation_paths) and all(snapshot.read(p)==(ROOT/p).read_bytes() for p in foundation_paths),
                'Foundation code changed after its EditMode checks.')
    audited=read(BASE/'reports-p7-final-validation-01/single-command-matches.json')
    require(len(audited['games'])==2 and all(g['elapsed']>=300 and g['corrections']==0 for g in audited['games']),
            'Missing completed single-command matches.')
    build_dir = ROOT / 'Logs/Manager5v5/initial-20261012/p7-review-final'
    build = read(build_dir / 'build-result.json')
    require(build['exitCode'] == 0 and Path(build['executable']).is_file(), 'Missing successful build.')
    player_dir = BASE / 'player-final'
    player = read(player_dir / 'player-result.json')
    process = read(player_dir / 'process-result.json')
    visual = read(player_dir / 'visual-review.json')
    require(process['exitCode'] == 0 and player['finished'] and player['elapsed'] >= 300
            and player['headBars'] == 8 and player['boundaryCorrections'] == 0
            and player['passes'] > 0 and player['shots'] > 0 and not player['trainingStarted'], 'Player gate failed.')
    require(visual['reviewed'] and visual['passed'], 'Actual frames need visual review.')
    for frame in visual['frames']:
        require((player_dir / frame).is_file(), 'Missing reviewed frame.')
    originals = read(ROOT / 'Logs/Manager5v5/initial-20261012/original-files.json')
    changed = [entry['path'] for entry in originals if digest(ROOT / entry['path']) != entry['sha256']]
    require(len(originals) == 693 and not changed, 'Original preservation mismatch: '+str(changed))
    require(digest(ROOT/'ProjectSettings/EditorBuildSettings.asset') == digest(BASE/'EditorBuildSettings.before.asset'),
            'Editor Build Settings changed.')
    verified_sources = []
    for archive in [BASE/'p7-final-validation-01-source.zip', BASE/'p7-wide-role-confirmation-01-source.zip', build_dir/'source.zip', BASE/'p7-source-final.zip']:
        with zipfile.ZipFile(archive) as snapshot:
            paths = [p for p in snapshot.namelist() if p.startswith('Assets/_Soccer/Manager5v5/Runtime/') and p.endswith('.cs')]
            require(bool(paths), 'No runtime source in snapshot.')
            require(all(snapshot.read(p) == (ROOT/p).read_bytes() for p in paths), 'Runtime drift after '+archive.name)
        verified_sources.append({'path':archive.relative_to(ROOT).as_posix(),'sha256':digest(archive),'runtimeFiles':len(paths)})
    missing = []
    for doc in [ROOT/'docs/soccer/rebuild-5v5/16_P7완료_결과와_인계.md', ROOT/'docs/soccer/rebuild-5v5/README.md']:
        for link in re.findall(r'(?<!!)\[[^\]]+\]\(([^)]+)\)', doc.read_text(encoding='utf-8-sig')):
            if '://' in link or link.startswith('#'): continue
            target = (doc.parent / link.split('#')[0]).resolve()
            if not target.exists() and target != out.resolve(): missing.append(str(target))
    require(not missing, 'Missing document links: '+str(missing))
    result = {'completedUtc':datetime.now(timezone.utc).isoformat(),'completedThrough':'P7',
              'scope':'Finite predeclared physical scenarios and match health, not universal game balance or trained policy quality.',
              'assessments':assessments,'editModePassed':11,'playModePassed':run['passed'],
              'regressionPassed':43,'longExperimentCollectorsPassed':5,
              'additionalWideRoleCollectorPassed':1,'originalLongPassRoleHypothesisPassed':False,
              'singleActiveCommandPerTeam':True,'retainedCommands':8,
              'auditedPhysicsSteps':sum(g['physicsSteps'] for g in audited['games']),
              'auditedTeamCommandExecutions':sum(g['teamPlans'] for g in audited['games']),
              'originalFilesChecked':len(originals),'originalFilesChanged':changed,
              'sourceArchives':verified_sources,'player':player,'playerExitCode':process['exitCode'],
              'executable':build['executable'],'executableSha256':digest(Path(build['executable'])),
              'visualReview':visual,'P8Started':False,'trainingStarted':False,'weeklyUsageStopRemoved':True}
    out.write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps({k:v for k,v in result.items() if k not in ['sourceArchives','visualReview']},ensure_ascii=True))

if __name__ == '__main__':
    main()
