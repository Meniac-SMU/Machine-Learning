"""Verify the resumed P7 gates before creating a completion receipt. Never relax failed gates."""
import hashlib
import json
import re
import zipfile
from datetime import datetime, timezone
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
BASE = ROOT / 'Logs/Manager5v5/p7-resume-20261013'
BUILD = ROOT / 'Logs/Manager5v5/initial-20261012/p7-complete-20261013'


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest().upper()


def require(condition, message):
    if not condition:
        raise ValueError(message)


def main():
    out = BASE / 'completion.json'
    require(not out.exists(), 'Preserve previous completion receipts.')
    assessed = {}
    for name, key, count in [('roles-revalidation-05', 'trials', 3072),
                             ('matrix-revalidation-02', 'trials', 4608),
                             ('natural-revalidation-02', 'games', 64),
                             ('period-revalidation-02', 'games', 48),
                             ('repeat-regression-02-assessment', 'games', 32),
                             ('repeat-failure-regression-02-assessment', 'games', 32),
                             ('repeat-confirmation-02-assessment', 'games', 32)]:
        data = read(BASE / (name + '.json'))
        require(data['passed'] and data['complete'] and data[key] == count, name + ' failed.')
        assessed[name] = {'passed': True, key: count, 'sha256': sha(BASE / (name + '.json'))}
    role = read(BASE / 'roles-revalidation-05.json')
    required = [c for c in role['checks'] if c['kind'] != 'original-hypothesis']
    require(role['seeds'] == list(range(12001,12009)), 'Independent role seed mismatch.')
    require(len(required) == 16 and all(c['passed'] and c['seedPairCount'] == 8 for c in required), 'Role confirmation incomplete.')
    matrix = read(BASE / 'matrix-revalidation-02.json')
    require(matrix['seeds'] == [11711,11712], 'Independent matrix seed mismatch.')
    require(len(matrix['contexts']) == 144 and not matrix['universalDominators'] and not matrix['pairwiseDominators'], 'A command is dominated in the declared matrix.')
    runs = {}
    for name, total in [('containment-role-revalidation-05', 46), ('cross-revalidation-02', 1),
                        ('natural-period-revalidation-02', 2), ('repeat-regression-02', 1),
                        ('repeat-failure-regression-02', 1), ('repeat-confirmation-02', 1), ('foundation-final', 11)]:
        run = read(BASE / (name + '.json'))
        require(run['exitCode'] == 0 and run['testResult'] == 'Passed' and run['passed'] == total
                and run['failed'] == 0 and run['skipped'] == 0, name + ' did not finish all required tests.')
        runs[name] = run
    audited = read(BASE / 'reports-containment-role-revalidation-05/single-command-matches.json')
    require(len(audited['games']) == 2 and all(g['elapsed'] >= 300 and g['corrections'] == 0 for g in audited['games']), 'Missing single-command physical audit.')
    build = read(BUILD / 'build-result.json')
    require(build['exitCode'] == 0 and Path(build['executable']).is_file(), 'Missing successful Windows build.')
    player_dir = BASE / 'player-final'
    player = read(player_dir / 'player-result.json')
    process = read(player_dir / 'process-result.json')
    visual = read(player_dir / 'visual-review.json')
    require(process['exitCode'] == 0 and player['finished'] and player['elapsed'] >= 300
            and player['headBars'] == 8 and player['boundaryCorrections'] == 0 and player['passes'] > 0
            and player['shots'] > 0 and not player['trainingStarted'], 'Rendered Player gate failed.')
    require(visual['reviewed'] and visual['passed'] and len(visual['frames']) >= 3, 'Actual frames need review.')
    for frame in visual['frames']:
        require((player_dir / frame).is_file(), 'Missing reviewed frame: ' + frame)
    originals = read(ROOT / 'Logs/Manager5v5/initial-20261012/original-files.json')
    changed = [x['path'] for x in originals if sha(ROOT / x['path']) != x['sha256'].upper()]
    require(len(originals) == 693 and not changed, 'Original preservation mismatch: ' + str(changed))
    require(sha(ROOT / 'ProjectSettings/EditorBuildSettings.asset') == sha(BASE / 'EditorBuildSettings.before.asset'), 'Editor Build Settings drift.')
    require(sha(ROOT / 'ProjectSettings/ProjectSettings.asset') == sha(BASE / 'ProjectSettings.before.asset'), 'Project settings drift after final cleanup.')
    archives = [Path(r['sourceArchive']) for r in runs.values()] + [BUILD / 'source.zip', BASE / 'p7-source-final.zip']
    verified = []
    current_runtime = {p.relative_to(ROOT).as_posix() for p in (ROOT / 'Assets/_Soccer/Manager5v5/Runtime').glob('*.cs')}
    for archive in archives:
        with zipfile.ZipFile(archive) as snapshot:
            paths = [p for p in snapshot.namelist() if p.startswith('Assets/_Soccer/Manager5v5/Runtime/') and p.endswith('.cs')]
            require(set(paths) == current_runtime and all(snapshot.read(p) == (ROOT / p).read_bytes() for p in paths), 'Runtime drift after ' + archive.name)
            profile = 'Assets/_Soccer/Manager5v5/Profiles/FiveProfile.asset'
            require(snapshot.read(profile) == (ROOT / profile).read_bytes(), 'Match profile drift after ' + archive.name)
        verified.append({'path': archive.relative_to(ROOT).as_posix(), 'sha256': sha(archive), 'runtimeFiles': len(paths)})
    with zipfile.ZipFile(BASE / 'p7-source-final.zip') as snapshot:
        manifest = json.loads(snapshot.read('source-manifest.json').decode('utf-8-sig'))
        require(all(sha(ROOT / x['path']) == x['sha256'] for x in manifest), 'Final source or documents changed after snapshot.')
    missing = []
    for doc in [ROOT / 'docs/soccer/rebuild-5v5/17_P7재개_최종검증과인계.md', ROOT / 'docs/soccer/rebuild-5v5/README.md']:
        for link in re.findall(r'(?<!!)\[[^\]]+\]\(([^)]+)\)', doc.read_text(encoding='utf-8-sig')):
            if '://' in link or link.startswith('#'):
                continue
            target = (doc.parent / link.split('#')[0]).resolve()
            if not target.exists() and target != out.resolve():
                missing.append(str(target))
    require(not missing, 'Missing links: ' + str(missing))
    result = {'completedUtc': datetime.now(timezone.utc).isoformat(), 'completedThrough': 'P7',
              'scope': 'Predeclared finite physical scenario, counter and match-health gates; not universal mathematical balance or trained-policy quality.',
              'assessments': assessed, 'editModePassed': 11, 'playModeRegressionsPassed': 45,
              'roleChecksPassed': 16, 'roleSeeds': 8, 'roleEvidence': 'Same-seed revalidation after public stall-observation fix; prior independent role confirmation retained.', 'retainedCommands': 8,
              'commandNames': role['commandNames'], 'singleActiveCommandPerTeam': True,
              'auditedPhysicsSteps': sum(g['physicsSteps'] for g in audited['games']),
              'auditedTeamCommandExecutions': sum(g['teamPlans'] for g in audited['games']),
              'originalFilesChecked': len(originals), 'originalFilesChanged': changed,
              'sourceArchives': verified, 'player': player, 'playerExitCode': process['exitCode'],
              'visualReview': visual, 'executable': build['executable'], 'executableSha256': sha(Path(build['executable'])),
              'P8Started': False, 'trainingStarted': False, 'weeklyRemainingStopThresholdPercent': 5,
              'automaticTrainingResume': False}
    out.write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print(json.dumps({'completedThrough': result['completedThrough'], 'originals': len(originals), 'receipt': str(out)}, ensure_ascii=True))


if __name__ == '__main__':
    main()
