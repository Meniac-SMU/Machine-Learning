"""PS demonstration teacher. Used ONLY to collect examples, never during PPO/evaluation.

All features are reconstructed from the current 244 observations. A legal pass is
not automatically a good pass. Thresholds are dataset labels, not action masks.
"""
import numpy as np

VERSION = 'PS-teacher-v2-20260929'
SCALE = np.array([62., 42.32727])


def context(obs, valid):
    o = np.asarray(obs)
    result = dict(reason='preserve-parent', beneficial=False)
    if o[243] > .5 or o[101] < .5 or not valid[1]:
        return result
    carrier = int(np.argmax(o[103:112])) - 1
    receiver = int(np.argmax(o[123:128])) - 1
    if not 0 <= carrier < 4 or not 0 <= receiver < 4 or carrier == receiver:
        return dict(result, reason='missing-target')
    players = o[:96].reshape(8, 12)
    origin = players[carrier, 1:3] * SCALE
    point = players[receiver, 1:3] * SCALE
    velocity = players[receiver, 3:5] * 9
    velocity *= min(1., 9 / max(float(np.linalg.norm(velocity)), 1e-9))
    target = point + .35 * velocity + [3.5, 0]
    target[1] = np.sign(target[1]) * max(0., abs(target[1]) - 1.5)
    target = np.clip(target, -SCALE + .55, SCALE - .55)
    opponents = players[4:][players[4:, 0] > .5, 1:3] * SCALE
    if not len(opponents):
        return dict(result, reason='no-opponents')
    delta = target - origin
    distance = float(np.linalg.norm(delta))
    t = np.clip((opponents-origin) @ delta / max(distance**2, 1e-9), 0, 1)
    lane = float(np.linalg.norm(opponents-origin-t[:, None]*delta, axis=1).min())
    pressure = float(np.linalg.norm(opponents-origin, axis=1).min())
    reception_space = float(np.linalg.norm(opponents-target, axis=1).min())
    offset = opponents-origin
    blocked = bool(np.any((offset[:, 0] >= .5) & (offset[:, 0] <= 8) & (abs(offset[:, 1]) <= 3)))
    goal = np.array([62., 0.])
    progress = float(np.linalg.norm(goal-origin)-np.linalg.norm(goal-target))
    # Time advantage is a geometric proxy, not an observed counterfactual.
    time_gain = float(progress / 9 - distance / 22)
    safe = 5 <= distance <= 28 and lane >= 1.75 and reception_space >= 3
    escape = safe and (blocked or pressure <= 8) and reception_space >= pressure + 1
    fast = safe and progress >= 8 and time_gain >= .2 and reception_space >= 4
    block = o[133+27*carrier:133+27*(carrier+1)]
    continuing = bool(safe and block[4] > .5 and (block[14]>.5 or block[15]>.5))
    reason = 'continue-pass-preparation' if continuing else 'pressure-escape' if escape else 'faster-attack' if fast else 'legal-unnecessary'
    return dict(reason=reason, beneficial=bool(escape or fast or continuing), carrier=carrier,
                receiver=receiver, lane=lane, pressure=pressure, receiverSpace=reception_space,
                distance=distance, progress=progress, estimatedTimeGain=time_gain, blocked=blocked)
