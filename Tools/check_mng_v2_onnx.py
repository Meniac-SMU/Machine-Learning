"""Numerical parity of exported ONNX probabilities and the frozen PPO actor."""
import json
from pathlib import Path
import sys
import subprocess
import tempfile
import numpy as np
import torch
import mlagents.trainers.trainer
from mlagents_envs.base_env import ObservationSpec, DimensionProperty, ObservationType, ActionSpec
from mlagents.trainers.settings import NetworkSettings
from mlagents.trainers.torch_entities.networks import SimpleActor
from mlagents.trainers.torch_entities.agent_action import AgentAction

def check(checkpoint_path):
    torch.set_default_device('cpu')
    torch.set_num_threads(1)
    checkpoint_path=Path(checkpoint_path)
    weights=torch.load(checkpoint_path,map_location='cpu',weights_only=False)
    actor=SimpleActor([ObservationSpec((244,),(DimensionProperty.NONE,),ObservationType.DEFAULT,'VectorSensor')],
        NetworkSettings(hidden_units=128,num_layers=2,normalize=False),ActionSpec(0,np.array([6])),
        conditional_sigma=False,tanh_squash=False)
    actor.load_state_dict(weights['Policy'],strict=True); actor.eval()
    rng=np.random.RandomState(42); obs=rng.uniform(-1,1,(64,244)).astype(np.float32)
    masks=(rng.uniform(size=(64,6))>.3).astype(np.float32); masks[:,4]=1
    # NumPy's ONNX reference BLAS and Torch bundle different Windows OpenMP DLLs.
    # Use separate processes instead of allowing duplicate OpenMP runtimes.
    with tempfile.TemporaryDirectory(dir=Path(__file__).resolve().parent.parent/'Logs/MNG-Rebuild') as directory:
        data=Path(directory)/'inputs.npz'; output=Path(directory)/'probabilities.npy'
        np.savez(data,obs=obs,masks=masks)
        code="import sys,numpy as np; from mng_v2_evaluate import FrozenPolicy; p=FrozenPolicy(sys.argv[1],42); d=np.load(sys.argv[2]); np.save(sys.argv[3],p.evaluator.run(None,{'obs_0':d['obs'],'action_masks':d['masks']})[0])"
        subprocess.run([sys.executable,'-c',code,str(checkpoint_path.with_suffix('.onnx').resolve()),str(data.resolve()),str(output.resolve())],
            cwd=Path(__file__).resolve().parent,check=True)
        actual=np.load(output)
    expected=[]
    with torch.no_grad():
        for action in range(6):
            stats=actor.get_stats([torch.from_numpy(obs)],AgentAction(None,[torch.full((64,),action)]),masks=torch.from_numpy(masks))
            expected.append(stats['log_probs'].flatten().exp().numpy().reshape(64))
    expected=np.stack(expected,axis=1)
    error=float(np.max(np.abs(actual-expected)))
    assert error<1e-6, error
    return dict(checkpoint=str(checkpoint_path),samples=64,actions=6,maximumAbsoluteProbabilityError=error,passed=True)

if __name__=='__main__': print(json.dumps(check(sys.argv[1]),indent=2))
