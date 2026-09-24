"""Read-only post-R6 evidence audit. Does not launch Unity or train a policy."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import yaml

ROOT = Path(__file__).resolve().parent.parent
TELEMETRY = re.compile(r'MNG POLICY TELEMETRY team=(Red|Navy) decisions=(\d+) masks=(\d+) passAvailable=(\d+) shotAvailable=(\d+)')


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def evidence(name):
    folder = ROOT / 'Logs/MNG-Rebuild' / name
    totals = dict(matches=0, decisionsLogged=0, masksLogged=0, passAvailableLogged=0,
                  shotAvailableLogged=0, finalCommands=0, passCommands=0, passStrikes=0,
                  completedPasses=0, traceDecisions=0, tracePassAvailable=0,
                  tracePassChosen=0, traceInvalidChoices=0, maxUnloggedDecisions=0)
    by_team = [dict(matches=0, masks=0, passAvailable=0), dict(matches=0, masks=0, passAvailable=0)]
    sources = []
    for match in sorted(folder.glob('match-*')):
        result_path = match / 'verified.json'
        row = json.loads(result_path.read_text())
        team = row['policyTeam']
        label = ('Red', 'Navy')[team]
        last = None
        logs = sorted(match.glob('Player-*.log'))
        assert len(logs) == 1
        for item in TELEMETRY.finditer(logs[0].read_text(errors='replace')):
            if item[1] == label:
                last = list(map(int, item.groups()[1:]))
        assert last is not None, str(match)
        decisions, masks, passes, shots = last
        final_commands = sum(row['commands'])
        missing = final_commands - decisions
        assert 0 <= missing < 20, (match, missing)
        totals['matches'] += 1
        for key, value in [('decisionsLogged', decisions), ('masksLogged', masks),
                           ('passAvailableLogged', passes), ('shotAvailableLogged', shots),
                           ('finalCommands', final_commands), ('passCommands', row['commands'][1]),
                           ('passStrikes', row['passStrikes']), ('completedPasses', row['rawEvents'][5])]:
            totals[key] += value
        totals['maxUnloggedDecisions'] = max(totals['maxUnloggedDecisions'], missing)
        by_team[team]['matches'] += 1
        by_team[team]['masks'] += masks
        by_team[team]['passAvailable'] += passes
        seen = set()
        for trace in sorted(match.glob('v2-*.jsonl')):
            for line in trace.read_text().splitlines():
                for entry in json.loads(line).get('trace', []):
                    key = (entry['episode'], entry['sequence'])
                    if key in seen or entry['team'] != team or entry['kind'] != 'PolicyDecision':
                        continue
                    seen.add(key)
                    totals['traceDecisions'] += 1
                    available = bool(entry['maskBits'] & 2)
                    totals['tracePassAvailable'] += available
                    totals['tracePassChosen'] += available and entry['rawCommand'] == 1
                    totals['traceInvalidChoices'] += not bool(entry['maskBits'] & (1 << entry['rawCommand']))
        sources.extend([dict(path=str(p.relative_to(ROOT)), sha256=sha(p)) for p in (result_path, logs[0])])
    totals['loggedPassAvailabilityFraction'] = totals['passAvailableLogged'] / totals['masksLogged']
    totals['traceDecisionCoverage'] = totals['traceDecisions'] / totals['finalCommands']
    return dict(name=name, totals=totals, byTeam=by_team, sources=sources)


def prefab_audit():
    path = ROOT / 'Assets/_Soccer/Manager/Prefabs/MNG_StadiumEnvironment.prefab'
    records = {}
    for kind, identity, body in re.findall(r'--- !u!(\d+) &(\d+)\n(.*?)(?=\n--- !u!|\Z)', path.read_text(), re.S):
        records[int(identity)] = (int(kind), next(iter(yaml.safe_load(body).values())))
    def script_guid(filename):
        return re.search(r'^guid: (\w+)', (ROOT / 'Assets/_Soccer/Manager/Runtime' / (filename + '.meta')).read_text(), re.M)[1]
    avatar_guid = script_guid('MNG_PlayerAvatar.cs')
    players = []
    for _, (kind, obj) in records.items():
        if kind != 114 or obj.get('m_Script', {}).get('guid') != avatar_guid:
            continue
        go = records[obj['m_GameObject']['fileID']][1]
        components = [records[c['component']['fileID']] for c in go['m_Component']]
        transform = next(c for k, c in components if k == 4)
        physical = []
        scripts = []
        for k, c in components:
            excluded = {'m_ObjectHideFlags', 'm_CorrespondingSourceObject', 'm_PrefabInstance', 'm_PrefabAsset', 'm_GameObject'}
            if k in (54, 65, 136, 135):
                physical.append((k, {key: value for key, value in c.items() if key not in excluded}))
            if k == 114 and 'profile' in c:
                scripts.append(dict(script=c['m_Script'], profile=c['profile']))
        players.append(dict(team=obj['team'], slot=obj['slot'], role=obj['role'], name=go['m_Name'],
                            layer=go['m_Layer'], active=go['m_IsActive'], cooldown=obj['maximumKickCooldownSeconds'],
                            position=transform['m_LocalPosition'], rotation=transform['m_LocalRotation'],
                            scale=transform['m_LocalScale'], physical=physical, profiles=scripts))
    players.sort(key=lambda p: (p['team'], p['slot']))
    assert len(players) == 8
    comparisons = []
    for slot in range(4):
        red, navy = [p for p in players if p['slot'] == slot]
        fields = ('role', 'layer', 'active', 'cooldown', 'scale', 'physical', 'profiles')
        comparisons.append(dict(slot=slot, differences=[f for f in fields if red[f] != navy[f]],
                                planarPositionSum={axis: red['position'][axis] + navy['position'][axis] for axis in ('x', 'z')}))
    return dict(path=str(path.relative_to(ROOT)), sha256=sha(path), players=players, comparisons=comparisons,
                limitation='Serialized player-root physics and motor profiles only; not live PhysX or every child collider/scene override.')


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--output', required=True)
    args = parser.parse_args()
    output = Path(args.output)
    if output.exists():
        raise FileExistsError(output)
    names = ['R5-r2-uniform-vs-full', 'R5-r2-200k-vs-full', 'R6-step0-vs-carry-shot',
             'R6-200k-vs-step0', 'R6-400k-vs-step0', 'R6-400k-vs-200k', 'R6-400k-vs-full',
             'R6-400k-vs-uniform-valid', 'R6-400k-vs-recover', 'R6-400k-vs-balanced',
             'R6-400k-vs-carry-shot', 'R6-400k-vs-step0-holdout']
    data = dict(kind='read-only saved evidence audit', evaluations=[evidence(n) for n in names], prefab=prefab_audit())
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(data, indent=2), encoding='utf-8')
    for item in data['evaluations']:
        print(item['name'], json.dumps(item['totals']))
    print('prefab comparisons:', data['prefab']['comparisons'])


if __name__ == '__main__':
    main()
