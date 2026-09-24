"""Current MS2-v3 baseline demonstration, 1x and 300 seconds. Never loads old MS3."""
import sys
from mng_v3_policy_guard import ROOT, registry, validate_evaluation_policy
from watch_mng_v2 import main

if __name__ == '__main__':
    assert len(sys.argv) == 1, 'v3 baseline demonstration takes no overrides'
    validate_evaluation_policy(str(ROOT/registry()['baseOnnx']['path']))
    sys.argv += ['--ms2-self-play', '--build', str(ROOT/'Builds/MNG_V2/MS2-forward-pass-center-20260923/EvaluationV2')]
    main()
