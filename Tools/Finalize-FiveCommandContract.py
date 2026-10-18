"""Verify and record the 2026-10-12 single-command review; never simulate a pass."""
import hashlib
import json
import re
import zipfile
from datetime import datetime, timezone
from pathlib import Path

root = Path(__file__).resolve().parent.parent
evidence = root / 'Logs/Manager5v5/command-contract-20261012'
def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))
def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest().upper()

receipt = read(evidence / 'single-command-01.json')
games = read(evidence / 'single-command-matches.json')
if receipt['testResult'] != 'Passed' or receipt['passed'] != 18 or receipt['failed'] or receipt['exitCode']:
    raise ValueError('The recorded test run did not pass the required 18 checks.')
if len(games['games']) != 2 or any(g['elapsed'] < 300 or g['corrections'] for g in games['games']):
    raise ValueError('Incomplete or corrected physical matches.')

originals = read(root / 'Logs/Manager5v5/initial-20261012/original-files.json')
changed = [entry['path'] for entry in originals if digest(root / entry['path']) != entry['sha256'].upper()]
if changed:
    raise ValueError(f'Original files changed: {changed}')

unchanged_runtime = []
with zipfile.ZipFile(evidence / 'single-command-01-source.zip') as archive:
    for path in sorted((root / 'Assets/_Soccer/Manager5v5/Runtime').glob('*.cs')):
        relative = path.relative_to(root).as_posix()
        if archive.read(relative) != path.read_bytes():
            raise ValueError(f'Runtime changed after verification: {relative}')
        unchanged_runtime.append(relative)
    test_path = 'Assets/_Soccer/Manager5v5/Tests/PlayMode/FiveCommandContractPlayTests.cs'
    if archive.read(test_path) != (root / test_path).read_bytes():
        raise ValueError('Contract tests changed after execution.')

with zipfile.ZipFile(root / 'Logs/Manager5v5/retention-20261012/retention-source-final.zip') as archive:
    for relative in unchanged_runtime:
        if archive.read(relative) != (root / relative).read_bytes():
            raise ValueError(f'Runtime differs from preceding review: {relative}')

out = evidence / 'completion.json'
if out.exists():
    raise ValueError('Preserve previous completion evidence.')
missing = []
for relative in ['docs/soccer/rebuild-5v5/14_상황별_명령구성과_단일선택_검토.md', 'docs/soccer/rebuild-5v5/README.md']:
    path = root / relative
    for link in re.findall(r'(?<!!)\[[^\]]+\]\(([^)]+)\)', path.read_text(encoding='utf-8-sig')):
        if '://' in link or link.startswith('#'):
            continue
        target = (path.parent / link.split('#')[0]).resolve()
        if not target.exists() and target != out.resolve():
            missing.append(f'{relative}: {link}')
if missing:
    raise ValueError(f'Missing document targets: {missing}')

snapshot = evidence / 'command-contract-source-final.zip'
result = {
    'completedUtc': datetime.now(timezone.utc).isoformat(),
    'commandNames': games['commandNames'], 'retainedCommandCount': 8,
    'testResult': receipt['testResult'], 'passed': receipt['passed'], 'failed': receipt['failed'],
    'testExitCode': receipt['exitCode'], 'auditedMatches': len(games['games']),
    'physicsSteps': sum(g['physicsSteps'] for g in games['games']),
    'teamCommandExecutions': sum(g['teamPlans'] for g in games['games']),
    'singleCommandContractPassed': True,
    'scope': 'Supported game/selection lifecycle and executed PhysX fixtures; not corrupted external state.',
    'lateDeficitPriorityImplemented': False, 'inPossessionFatiguePriorityImplemented': False,
    'passTechniqueContinuityPreserved': True,
    'originalFilesChecked': len(originals), 'originalFilesChanged': changed,
    'runtimeUnchangedFromPreviousReviewAndTestSnapshot': unchanged_runtime,
    'documentMissingTargets': missing,
    'sourceSnapshot': snapshot.relative_to(root).as_posix(), 'sourceSnapshotSha256': digest(snapshot),
    'newPlayerBuilt': False, 'trainingStarted': False, 'globalBalancePassed': False,
    'weeklyUsageStopRemoved': True,
}
out.write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf-8')
print(json.dumps({key: value for key, value in result.items() if key != 'runtimeUnchangedFromPreviousReviewAndTestSnapshot'}, ensure_ascii=False, indent=2))
