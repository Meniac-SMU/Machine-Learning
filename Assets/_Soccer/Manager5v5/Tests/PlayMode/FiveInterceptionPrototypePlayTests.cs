using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MachineLearning.Soccer.Manager5v5.Tests
{
    // Candidate-only experiment. This does not replace the production T5 command until its role is confirmed.
    public sealed class FiveInterceptionPrototypePlayTests
    {
        [UnityTearDown] public IEnumerator Cleanup(){FiveTestWorld.DisposeOutstanding();yield return null;}
        internal static Team ArrangeRunning(FiveTestWorld world,int seed,bool mirror)
        {
            var points=new[]{new Vector2(-59,0),new Vector2(-35,-15),new Vector2(-40,15),new Vector2(-20,0),new Vector2(-3,-20),
                new Vector2(59,0),new Vector2(5,-15),new Vector2(5,15),new Vector2(38,-20),new Vector2(38,20)};
            var random=new System.Random(seed);
            for(int i=0;i<10;i++)
            {
                if(i%5!=0)points[i]+=new Vector2((float)random.NextDouble()-.5f,(float)random.NextDouble()-.5f)*3;
                world.Place(mirror?(i+5)%10:i,points[i]*(mirror?-1:1),new Vector2(mirror?-1:1,0));
            }
            world.Give(mirror?8:3);return mirror?Team.Red:Team.Navy;
        }
        internal static void PrototypePlan(FiveMatch m,Team own)
        {
            var sign=FiveMatch.AttackSign(own);var fields=m.players.Where(p=>p.team==own&&!p.IsKeeper).ToArray();
            if(m.Carrier!=null&&m.Carrier.team==own)
            {foreach(var p in fields)p.SetTarget(p.Position,m.BallPosition);return;}
            var b=m.BallPosition*sign;
            var motion=(m.Carrier!=null?m.Carrier.Velocity:m.BallVelocity)*sign;
            // Close the lead distance as the defender reaches the runner; a fixed horizon retreats forever.
            var distance=Mathf.Min(Vector2.Distance(fields[0].Position,m.BallPosition),Vector2.Distance(fields[1].Position,m.BallPosition));
            var horizon=Mathf.Min(1.5f,distance/(m.profile.maxSpeed+motion.magnitude));
            var lead=b+motion*horizon;
            var behind=lead+new Vector2(-4,6);
            var a=lead*sign;var c=behind*sign;
            var direct=(fields[0].Position-a).sqrMagnitude+(fields[1].Position-c).sqrMagnitude;
            var swapped=(fields[0].Position-c).sqrMagnitude+(fields[1].Position-a).sqrMagnitude;
            fields[0].SetTarget(m.ClampField(swapped<direct?c:a),m.BallPosition);
            fields[1].SetTarget(m.ClampField(swapped<direct?a:c),m.BallPosition);
            fields[2].SetTarget(m.ClampField(new Vector2(b.x-25,-12)*sign),m.BallPosition);
            fields[3].SetTarget(m.ClampField(new Vector2(b.x-25,12)*sign),m.BallPosition);
        }
        [UnityTest,Category("LongRunning"),Timeout(1800000)] public IEnumerator DiagnoseRunningInterception()
        {
            var report=new FiveMatchupPlayTests.Report();
            for(int seed=11501;seed<=11508;seed++)for(int candidate=0;candidate<2;candidate++)for(int mirror=0;mirror<2;mirror++)
            {
                using(var world=new FiveTestWorld())
                {
                    var m=world.match;var own=ArrangeRunning(world,seed,mirror==1);
                    var command=candidate==0?FiveCommand.WideTrap:FiveCommand.AllRecover;
                    m.SelectCommand(own,command,"interception-prototype",true);
                    m.SelectCommand(FiveMatch.Other(own),FiveCommand.FastTransition,"running-threat",true);
                    if(candidate==0)m.teamPlanningEnabled[(int)own]=false;
                    var row=FiveMatchupPlayTests.Begin(m,own,0,(int)command,seed,mirror==1,false,"interception-prototype");
                    row.scenario="central-running-threat";row.opponent=0;row.frames=new List<FiveMatchupPlayTests.Frame>();
                    float start=m.Elapsed,secure=0;int steps=0;
                    while(m.Elapsed-start<12&&steps++<2000)
                    {
                        if(candidate==0)PrototypePlan(m,own);
                        world.Step(1);FiveMatchupPlayTests.Sample(m,own,row,start,ref secure);
                    }
                    FiveMatchupPlayTests.Finish(m,own,row,start);report.trials.Add(row);
                }
                yield return null;FiveP7Evidence.Save("interception-prototype",report);
            }
            Assert.That(report.trials,Has.Count.EqualTo(32));
            Assert.That(report.trials.All(r=>r.elapsed>=12&&r.corrections==0),Is.True);
        }
    }
}
