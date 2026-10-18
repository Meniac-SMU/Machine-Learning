"""Verify preserved v8/v9 evidence and the approved v10 command-limit derivative."""
import hashlib
import json
from pathlib import Path
import zipfile

ROOT = Path(__file__).resolve().parent.parent
OUT = ROOT / 'Logs/Manager5v5/all-command-limits-v10-20261016'


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def sha(path):
    h = hashlib.sha256()
    with path.open('rb') as stream:
        for block in iter(lambda: stream.read(4 * 1024 * 1024), b''):
            h.update(block)
    return h.hexdigest()


def main():
    allowed_source = {
        'Runtime/FiveTactics.cs', 'Runtime/FiveLearningContract.cs',
        'Runtime/FiveContestRelief.cs', 'Runtime/FiveRuleManager.cs',
        'Learning/FiveLearningSession.cs', 'Learning/FiveManagerAgent.cs',
        'Tests/PlayMode/FiveDefensiveStatePlayTests.cs',
        'Tests/PlayMode/FiveLearningPlayTests.cs',
        'Scenes/FiveLearning.unity', 'Scenes/FiveSpectator.unity',
    }
    allowed_docs = {
        'AGENTS.md', 'docs/README.md', 'docs/soccer/current-status.md',
        'docs/soccer/rebuild-5v5/AGENTS.md', 'docs/soccer/rebuild-5v5/README.md',
        'docs/soccer/rebuild-5v5/04_결정사항과_변경기록.md',
    }
    allowed = {'Assets/_Soccer/Manager5v5/' + p for p in allowed_source} | allowed_docs
    changed = []
    with zipfile.ZipFile(OUT / 'source-before.zip') as archive:
        entries = json.loads(archive.read('source-manifest.json').decode('utf-8-sig'))
        for entry in entries:
            before = hashlib.sha256(archive.read(entry['path'])).hexdigest()
            assert before == entry['sha256'].lower(), entry['path']
            after = sha(ROOT / entry['path'])
            if before != after:
                assert entry['path'] in allowed, entry['path']
                changed.append(dict(path=entry['path'], beforeSha=before, afterSha=after))

    manifest = read(ROOT / 'docs/soccer/rebuild-5v5/p9-frozen-v8-manifest.json')
    frozen_build = [x for x in manifest['inputs'] if x['path'].startswith('Builds/')]
    for item in frozen_build:
        assert sha(ROOT / item['path']) == item['sha256'].lower(), item['path']

    v9_builds = []
    for directory in ('keeper-accel12-learning-v9-20261016', 'keeper-accel12-display-v9-20261016-r001'):
        lifecycle = read(ROOT / 'Builds/Manager5v5' / directory / 'build-lifecycle.json')
        for item in lifecycle['keyHashes']:
            assert sha(ROOT / item['path']) == item['sha256'].lower(), item['path']
        v9_builds.append(dict(directory=directory, verifiedKeyFiles=len(lifecycle['keyHashes'])))

    old = ROOT / 'Logs/Manager5v5/p9-frozen-v8-20261015/MNG5_V5-20261016-r001'
    prior = read(ROOT / 'Logs/Manager5v5/keeper-accel12-v9-20261016/preservation-verification.json')
    assert sha(old / 'completion-verification-2400.json') == prior['completionRecordSha']
    assert sha(old / 'model-registry.json') == prior['registrySha']
    done = read(old / 'completion-verification-2400.json')
    assert done['passed'] and done['targetSteps'] == 2400000
    assert read(old / 'model-registry.json')['champion'] is None
    models = ROOT / 'results/Manager5v5/MNG5_V5-20261016-r001/MNG5_Manager_v5'
    checked = []
    for checkpoint in done['checkpoints']:
        step = checkpoint['step']
        for name, key in ((f'MNG5_Manager_v5-{step}.onnx', 'onnxSha256'),
                          (f'MNG5_Manager_v5-{step}.pt', 'ptSha256'),
                          (f'five-pool-{step}.pt', 'poolSha256')):
            path = models / name
            assert sha(path) == checkpoint[key], name
            checked.append(dict(path=path.relative_to(ROOT).as_posix(), sha256=checkpoint[key]))
    assert len(checked) == 39
    assert sha(ROOT / 'ProjectSettings/ProjectSettings.asset') == sha(OUT / 'ProjectSettings-before.asset')
    receipt = dict(passed=True, sourceArchiveEntriesVerified=len(entries), authorizedChangedFiles=changed,
                   frozenV8BuildFilesVerified=len(frozen_build), v9BuildKeyFilesVerified=v9_builds,
                   checkpointCount=13, checkpointFilesVerified=checked, targetSteps=2400000,
                   completionRecordSha=prior['completionRecordSha'], registrySha=prior['registrySha'],
                   projectSettingsRestored=True, champion=None, trainingStarted=False)
    (OUT / 'preservation-verification.json').write_text(json.dumps(receipt, ensure_ascii=False, indent=2), encoding='utf-8')
    print(json.dumps({k: v for k, v in receipt.items() if k not in ('authorizedChangedFiles', 'checkpointFilesVerified')}, ensure_ascii=False))


if __name__ == '__main__':
    main()
