"""Read-only event accounting. No gameplay, reward or policy changes."""
from collections import Counter


def rates(stats):
    result = dict(stats)
    result['passSuccessRate'] = stats['passSuccesses']/stats['passAttempts'] if stats['passAttempts'] else None
    result['shotConversionRate'] = stats['shotGoals']/stats['shots'] if stats['shots'] else None
    result['goalsPerShot'] = stats['goals']/stats['shots'] if stats['shots'] else None
    return result


def event_metrics(events):
    sides = [dict(passAttempts=0, passSuccesses=0, shots=0, shotGoals=0, goals=0) for _ in range(2)]
    passes = {}; received = set(); last_shot = None
    for e in events:
        kind, actor = e['kind'], e['actor']
        if kind == 'goal':
            assert actor in (0, 1)
            sides[actor]['goals'] += 1
            if last_shot is not None and last_shot == actor:
                sides[actor]['shotGoals'] += 1
            last_shot = None
        elif kind == 'pass-kicked':
            side = actor//5
            assert e['id'] not in passes
            passes[e['id']] = (side, actor, e['other'])
            sides[side]['passAttempts'] += 1
            last_shot = None
        elif kind == 'pass-received':
            assert e['id'] in passes and e['id'] not in received
            side, sender, receiver = passes[e['id']]
            assert (actor, e['other']) == (sender, receiver)
            received.add(e['id']); sides[side]['passSuccesses'] += 1
        elif kind == 'shot':
            last_shot = actor//5
            sides[last_shot]['shots'] += 1
        elif kind in ('possession', 'keeper-clearance'):
            last_shot = None
    for side in sides:
        assert 0 <= side['passSuccesses'] <= side['passAttempts']
        assert 0 <= side['shotGoals'] <= side['shots']
        assert side['shotGoals'] <= side['goals']
    return [rates(s) for s in sides]


def aggregate(rows):
    keys = ('passAttempts', 'passSuccesses', 'shots', 'shotGoals', 'goals')
    return rates({key: sum(row[key] for row in rows) for key in keys})
