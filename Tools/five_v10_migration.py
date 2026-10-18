"""Strict behavior-name migration of a preserved v8 opponent pool to v10."""
import copy
import numpy as np
import torch
from mlagents.trainers.buffer import AgentBuffer

OLD='MNG5_Manager_v5'
NEW='MNG5_Manager_v6'
OLD_CONTRACT='MNG5-OBS676-A8-R1-M2-v5-T2T7MAX2-COOLDOWN2-GK6'
NEW_CONTRACT='MNG5-OBS676-A8-R1-M2-v6-ALLMAX3-T7MAX2-COOLDOWN2-GK6'

def same_state(a,b):
    if torch.is_tensor(a):return torch.is_tensor(b) and a.dtype==b.dtype and torch.equal(a,b)
    if isinstance(a,np.ndarray):return isinstance(b,np.ndarray) and a.dtype==b.dtype and np.array_equal(a,b)
    if isinstance(a,dict):return isinstance(b,dict) and a.keys()==b.keys() and all(same_state(a[k],b[k]) for k in a)
    if isinstance(a,(list,tuple)):return type(a)==type(b) and len(a)==len(b) and all(same_state(x,y) for x,y in zip(a,b))
    return a==b

def validate_pool(pool,behavior=NEW,contract=NEW_CONTRACT):
    assert pool['contract']==contract,'Pool contract mismatch'
    assert set(pool['deployed'])=={f'{behavior}?team=0',f'{behavior}?team=1'},'Deployed behavior mismatch'
    assert pool['ghost']['policy_snapshots'],'Empty opponent snapshot pool'
    for index,snapshot in enumerate(pool['ghost']['policy_snapshots']):
        assert set(snapshot)=={behavior},f'Opponent snapshot {index} behavior mismatch'
    assert set(pool['ghost']['current_policy_snapshot'])=={behavior},'Current snapshot behavior mismatch'

def migrate_pool(original):
    validate_pool(original,OLD,OLD_CONTRACT)
    migrated=copy.deepcopy(original)
    migrated['contract']=NEW_CONTRACT
    migrated['deployed']={key.replace(OLD,NEW):weights for key,weights in migrated['deployed'].items()}
    migrated['ghost']['policy_snapshots']=[{NEW:snapshot[OLD]} for snapshot in migrated['ghost']['policy_snapshots']]
    migrated['ghost']['current_policy_snapshot']={NEW:migrated['ghost']['current_policy_snapshot'][OLD]}
    migrated['ppo_buffer']=AgentBuffer()
    validate_pool(migrated)
    # Reverse only approved metadata changes and compare every retained value,
    # including all actor/critic tensors, ghost counters and Python/NumPy/Torch/CUDA RNG states.
    reverted=copy.deepcopy(migrated)
    reverted['contract']=OLD_CONTRACT
    reverted['deployed']={key.replace(NEW,OLD):weights for key,weights in reverted['deployed'].items()}
    reverted['ghost']['policy_snapshots']=[{OLD:snapshot[NEW]} for snapshot in reverted['ghost']['policy_snapshots']]
    reverted['ghost']['current_policy_snapshot']={OLD:reverted['ghost']['current_policy_snapshot'][NEW]}
    for key in original:
        if key!='ppo_buffer':assert same_state(original[key],reverted[key]),f'Unexpected state change: {key}'
    assert migrated['ppo_buffer'].num_experiences==0
    return migrated
