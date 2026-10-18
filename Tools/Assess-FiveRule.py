"""Assess the predeclared strong-rule holdout without discarding seeds or draws."""
import argparse
import hashlib
import json
from pathlib import Path
import numpy as np


def read(p): return json.loads(p.read_text(encoding="utf-8-sig"))
def stats(rows):
    diff = [r["score"]["other"]-r["score"]["own"] for r in rows]
    return dict(games=len(rows), wins=sum(x>0 for x in diff), draws=sum(x==0 for x in diff),
                losses=sum(x<0 for x in diff), goalsFor=sum(r["score"]["other"] for r in rows),
                goalsAgainst=sum(r["score"]["own"] for r in rows), meanGoalDifference=float(np.mean(diff)),
                maximumObservedStall=max(r["maximumObservedStall"] for r in rows),
                boundaryCorrections=sum(r["boundaryCorrections"] for r in rows))


def main():
    p=argparse.ArgumentParser();p.add_argument("--root",type=Path,required=True)
    p.add_argument("--direct",type=Path,required=True);p.add_argument("--output",type=Path,required=True)
    args=p.parse_args();assert not args.output.exists()
    comparisons={};improvement=[];all_games=[];all_verified=True
    expected={"final":32,"middle":16,"initial":16,"fixed-0":8,"fixed-3":8,"fixed-6":8}
    for opponent,number in expected.items():
        variants={};paired={}
        for variant in ("strong","legacy"):
            folder=args.root/f"{opponent}-{variant}"
            verified=read(folder/"verification.json");assert verified["passed"]
            rows=[read(f) for f in sorted(folder.glob("match-*/result.json"))]
            assert len(rows)==number and all(r["passed"] for r in rows)
            variants[variant]=stats(rows);all_games+=rows
            paired[variant]={(r["seed"],r["candidateTeam"]):r["score"]["other"]-r["score"]["own"] for r in rows}
        assert paired["strong"].keys()==paired["legacy"].keys()
        by_seed={}
        for (seed,team),value in paired["strong"].items():
            by_seed.setdefault(seed,[]).append(value-paired["legacy"][(seed,team)])
        cluster=np.asarray([np.mean(by_seed[seed]) for seed in sorted(by_seed)])
        rng=np.random.RandomState(593017)
        samples=rng.choice(cluster,size=(20000,len(cluster)),replace=True).mean(axis=1)
        variants["pairedImprovement"]=dict(mean=float(cluster.mean()),
            bootstrap95=np.percentile(samples,[2.5,97.5]).tolist(),clusters=len(cluster),
            sampling="seed-cluster bootstrap; both sides kept together; 20000 resamples, RNG 593017",
            perSeed={str(seed):float(np.mean(values)) for seed,values in by_seed.items()})
        comparisons[opponent]=variants
    direct=read(args.direct);matches=direct["matches"];assert len(matches)==32
    differences=[r["goalsFor"]-r["goalsAgainst"] for r in matches]
    direct_stats=dict(games=32,wins=sum(x>0 for x in differences),draws=sum(x==0 for x in differences),
        losses=sum(x<0 for x in differences),goalsFor=sum(r["goalsFor"] for r in matches),
        goalsAgainst=sum(r["goalsAgainst"] for r in matches),maximumStall=max(r["maximumStall"] for r in matches),
        boundaryCorrections=sum(r["corrections"] for r in matches))
    final=comparisons["final"];checks=dict(
        positiveFinalPaired95Lower=final["pairedImprovement"]["bootstrap95"][0]>0,
        winsMoreThanLossesAgainstFinal=final["strong"]["wins"]>final["strong"]["losses"],
        winsMoreThanLossesAgainstLegacy=direct_stats["wins"]>direct_stats["losses"],
        noBoundaryCorrections=all(r["boundaryCorrections"]==0 for r in all_games) and direct_stats["boundaryCorrections"]==0,
        noThirtySecondStall=all(r["maximumObservedStall"]<30 for r in all_games if r["ruleVariant"]=="strong") and direct_stats["maximumStall"]<30)
    result=dict(passed=all(checks.values()),checks=checks,games=len(all_games)+len(matches),
                comparisons=comparisons,directStrongAgainstLegacy=direct_stats,
                assessorSha256=hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),
                limits="Frozen P9 models, standard kickoff sampled trajectories; direct games use mirrored spawn jitter. Not proof against future learned policies.")
    args.output.write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding="utf-8")
    print(json.dumps(result,ensure_ascii=False,indent=2))
    return 0 if result["passed"] else 1


if __name__=="__main__": raise SystemExit(main())
