"""Evidence-only summary of the authorized goalkeeper correction and matched full games."""
import csv
from io import StringIO
import zipfile
import hashlib
from five_v11_run_common import ROOT, BASE, read, write, verify, sha

assert read(BASE/'comparison-status.json')['state'] == 'complete'
summaries = {mode: read(BASE/f'matches-{mode}/summary.json') for mode in ('off', 'on')}
assert all(s['passed'] and s['games'] == 40 for s in summaries.values())
rows = []
for mode in ('off', 'on'):
    protocol = read(BASE/f'matches-{mode}/protocol.json')
    assert protocol['seeds'] == list(range(67000, 67020))
    assert protocol['seconds'] == 300 and protocol['keeperTurnFirst'] == mode
    games = [read(p) for p in sorted((BASE/f'matches-{mode}').glob('match-*/result.json'))]
    assert len(games) == 40 and all(g['passed'] and g['exitCode'] == 0 for g in games)
    assert len({(g['seed'], g['candidateTeam']) for g in games}) == 40
    for game in games:
        row = dict(mode=mode, seed=game['seed'], candidateTeam=game['candidateTeam'],
                   goals2800=game['score']['own'], goals2400=game['score']['other'],
                   keeperLastTouchConceded=game['keeperLastTouchConceded'],
                   keeperSlowSideContactConceded=game['keeperSlowSideContactConceded'])
        for name, key in (('2800', 'candidateMetrics'), ('2400', 'opponentMetrics')):
            row.update({f'{name}_{k}': v for k, v in game[key].items()})
        rows.append(row)
table = StringIO(newline='')
writer = csv.DictWriter(table, fieldnames=list(rows[0])); writer.writeheader(); writer.writerows(rows)
(BASE/'matches-80.csv').write_text(table.getvalue(), encoding='utf-8-sig')

fixtures = {}
for mode, path in [('original', 'reports-baseline-matrix-01/keeper-turn-matrix.json'),
                   ('off', 'reports-keeper-ab-control-04/keeper-legacy-matrix.json'),
                   ('on', 'reports-keeper-ab-control-04/keeper-turn-matrix.json')]:
    cases = read(BASE/path)['cases']
    fixtures[mode] = dict(cases=len(cases), ownGoals=sum(c['ownGoals'] for c in cases),
                         captures=sum(c['captured'] for c in cases),
                         maxCaptureSeconds=max(c['captureSeconds'] for c in cases),
                         unalignedApproach=sum(c['unalignedApproach'] for c in cases))
assert fixtures['original']['ownGoals'] == 8 and fixtures['on']['ownGoals'] == 0
assert fixtures['on']['captures'] == 96 and read(BASE/'ab-control-verification.json')['passed']
tests = {name: read(BASE/f'{name}.json') for name in
         ('keeper-regression-03-all', 'keeper-ab-control-04', 'keeper-edit-05')}
assert all(t['exitCode'] == 0 and t['failed'] == 0 for t in tests.values())
build = read(BASE/'build-keeper-turn-first-learning-v11-20261016/build-result.json')
assert build['exitCode'] == 0
preservation = dict(originalModelAndRecordFiles=verify(BASE/'original-model-records.json'),
                    oldBuildFiles=verify(BASE/'original-build-preservation.json'),
                    frozenInputs=verify(),
                    projectSettingsRestored=sha(ROOT/'ProjectSettings/ProjectSettings.asset') == sha(BASE/'ProjectSettings-before.asset'))
assert preservation['projectSettingsRestored']
with zipfile.ZipFile(BASE/'source-before.zip') as archive:
    manifest = __import__('json').loads(archive.read('source-manifest.json').decode('utf-8-sig'))
    for item in manifest:
        assert hashlib.sha256(archive.read(item['path'])).hexdigest().lower() == item['sha256'].lower()
    preservation['originalSourceArchiveFiles'] = len(manifest)
write(BASE/'completion.json', dict(completed=True, runtime='tactics-v11-keeper-turn-first',
      fixtures=fixtures, matches=summaries, games=80, tests=tests, preservation=preservation,
      trainingStarted=False, championPromoted=False,
      limits='Fixed fixtures demonstrate the targeted fix. Full-match side-contact concessions are a proxy, not causal proof of an own goal; no claim of zero general-match risk.'))
print('COMPLETE', fixtures, {k: {n: s[n] for n in ('games','wins','draws','losses','goalsFor','goalsAgainst','keeperLastTouchConceded','keeperSlowSideContactConceded')} for k,s in summaries.items()}, preservation)
