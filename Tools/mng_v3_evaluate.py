"""v3 entry point for the existing frozen evaluator; enforces active lineage."""
import argparse
import json
from pathlib import Path
from mng_v3_policy_guard import ROOT
from mng_v3_policy_guard import validate_evaluation_policy
from mng_v2_evaluate import main

if __name__ == '__main__':
    parser = argparse.ArgumentParser(add_help=False)
    parser.add_argument('--candidate', required=True)
    parser.add_argument('--opponent', default='R0-Full-v2')
    parser.add_argument('--build', required=True)
    parser.add_argument('--candidate-common-rules', default='enabled')
    parser.add_argument('--opponent-common-rules', default='enabled')
    args, _ = parser.parse_known_args()
    validate_evaluation_policy(args.candidate)
    validate_evaluation_policy(args.opponent)
    assert args.candidate_common_rules == args.opponent_common_rules == 'enabled', 'v3 evaluates the current common rules for both teams'
    schema = json.loads((ROOT/'docs/soccer/training/ms-v3-schema.json').read_text())
    info = json.loads((Path(args.build)/'build-info.json').read_text())
    assert info['environmentRevision'] == schema['environmentRevision'], 'v3 runtime mismatch'
    main()
