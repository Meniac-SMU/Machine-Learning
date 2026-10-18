"""Descriptive paired-seed uncertainty; never used by policy selection or training."""
import json
import random
from pathlib import Path


def read(path):
    return json.loads(Path(path).read_text(encoding='utf-8-sig'))


def match_rows(base, prefix, step, labels):
    return [(label, read(path)) for label in labels
            for path in sorted((base/f'{prefix}-{step:07d}-{label}').glob('match-*/result.json'))]


def usage_interval(rows):
    blocks = {}
    for _, row in rows:
        counts = [sum(state[i] for state in row['actionCounts']) for i in range(8)]
        block = blocks.setdefault(row['seed'], [0]*8)
        for i in range(8):
            block[i] += counts[i]
    assert len(blocks)==12
    values = list(blocks.values())
    rng = random.Random(63201)
    samples = [[] for _ in range(8)]
    for _ in range(10000):
        chosen = rng.choices(values, k=len(values))
        summed = [sum(v[i] for v in chosen) for i in range(8)]
        total = sum(summed)
        for i in range(8):
            samples[i].append(summed[i]/total)
    intervals = []
    for values in samples:
        values.sort()
        intervals.append([values[249], values[9749]])
    return dict(seedBlocks=12, replicates=10000, intervals95=intervals,
                allIntervalsWithinTarget=all(lo>=.015 and hi<=.5 for lo, hi in intervals),
                grouping='Both sides and all fixed opponents remain together within each policy-sampling seed. This does not measure variation across training initializations.')


def paired_strength(candidate_rows, baseline_rows):
    sides = []
    metrics = []
    for rows in (candidate_rows, baseline_rows):
        mapping = {}
        for label, row in rows:
            difference = row['score']['own']-row['score']['other']
            mapping[(label, row['seed'], row['candidateTeam'])] = (difference, 1 if difference>0 else .5 if difference==0 else 0)
        sides.append(mapping)
        metrics.append(dict(games=len(mapping), meanGoalDifference=sum(v[0] for v in mapping.values())/len(mapping),
                            scoreRate=sum(v[1] for v in mapping.values())/len(mapping)))
    assert sides[0].keys()==sides[1].keys()
    grouped = {}
    for key in sides[0]:
        grouped.setdefault(key[1], []).append(tuple(sides[0][key][i]-sides[1][key][i] for i in range(2)))
    blocks = [tuple(sum(row[i] for row in rows)/len(rows) for i in range(2)) for rows in grouped.values()]
    assert len(blocks)==12
    rng = random.Random(63301)
    samples = [[], []]
    for _ in range(10000):
        chosen = rng.choices(blocks, k=len(blocks))
        for i in range(2):
            samples[i].append(sum(v[i] for v in chosen)/len(chosen))
    for sample in samples:
        sample.sort()
    return dict(candidate=metrics[0], baseline=metrics[1], seedBlocks=12,
                goalDifferenceChange=sum(v[0] for v in blocks)/len(blocks),
                goalDifference95=[samples[0][249], samples[0][9749]],
                scoreRateChange=sum(v[1] for v in blocks)/len(blocks),
                scoreRate95=[samples[1][249], samples[1][9749]],
                positiveGoalDifferenceEvidence=samples[0][249]>0,
                grouping='Paired policy-sampling seed blocks, keeping both sides and all opponents together; one training initialization.')
