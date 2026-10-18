using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MachineLearning.Soccer.Manager5v5.Tests
{
    public sealed class FiveTacticExpansionPlayTests
    {
        [UnityTearDown] public IEnumerator Cleanup(){FiveTestWorld.DisposeOutstanding();yield return null;}
        static void SetVariant(FiveMatch m,int variant)
        {
            m.improvedPassPreparationEnabled=variant!=0;
            m.expandedCutbackEnabled=variant==2||variant==4;
            m.adaptiveGoalBlockEnabled=variant==3||variant==4;
        }
        [UnityTest] public IEnumerator CutbackStartsBeforeTheEndLineOnBothSides()
        {
            for(int mirror=0;mirror<2;mirror++)
            {
                using(var w=new FiveTestWorld())
                {
                    var m=w.match;var sign=mirror==0?1:-1;var offset=mirror*5;var own=(Team)mirror;
                    m.keepersEnabled=false;m.teamPlanningEnabled[1-mirror]=false;
                    for(int i=0;i<10;i++)w.Place(i,new Vector2(-40+(i%5)*15,i/5==mirror?-32:35)*sign,Vector2.right*sign);
                    w.Place(offset+1,new Vector2(38,23)*sign,new Vector2(8,-31).normalized*sign);
                    w.Place(offset+3,new Vector2(4,28)*sign,Vector2.right*sign);
                    w.Place(offset+4,new Vector2(46,-8)*sign,new Vector2(-8,31).normalized*sign);
                    w.Give(offset+1);m.SelectCommand(own,FiveCommand.WingCutback,"expanded-cutback-test",true);
                    var began=m.Elapsed;w.Step(1);
                    Assert.That(m.Events.Any(e=>e.kind=="pass-signal"&&e.actor==offset+1),Is.True,"A prepared side-of-box pass must not wait for the old end-line trigger.");
                    w.Step(400);
                    Assert.That(m.Events.Any(e=>e.kind=="pass-kicked"&&e.actor==offset+1),Is.True);
                    Assert.That(m.Events.Any(e=>e.kind=="pass-received"&&e.actor==offset+1),Is.True);
                    Assert.That(m.Decision(own).command,Is.EqualTo(FiveCommand.WingCutback));
                    Assert.That(m.BoundaryCorrections,Is.Zero);
                }
                yield return null;
            }
        }
        [UnityTest] public IEnumerator GoalBlockDoesNotSendASecureCarrierBackTowardItsOwnGoal()
        {
            for(int mirror=0;mirror<2;mirror++)foreach(int slot in new[]{1,2,3,4})
            {
                using(var w=new FiveTestWorld())
                {
                    var m=w.match;var sign=mirror==0?1:-1;var offset=mirror*5;var own=(Team)mirror;
                    m.keepersEnabled=false;m.teamPlanningEnabled[1-mirror]=false;
                    for(int i=0;i<10;i++)w.Place(i,new Vector2(-45+(i%5)*18,i/5==mirror?-25:35)*sign,Vector2.right*sign);
                    w.Place(offset+slot,new Vector2(-25,0)*sign,Vector2.right*sign);
                    w.Give(offset+slot);m.SelectCommand(own,FiveCommand.GoalBlock,"protected-possession-test",true);w.Step(1);
                    var carrier=m.players[offset+slot];
                    Assert.That(carrier.Target.x*sign,Is.GreaterThanOrEqualTo(carrier.Position.x*sign-.2f));
                    Assert.That(m.Decision(own).command,Is.EqualTo(FiveCommand.GoalBlock));
                    w.Step(100);Assert.That(m.BoundaryCorrections,Is.Zero);
                }
                yield return null;
            }
        }
        [Serializable] sealed class Row
        {
            public int variant,scenario,seed,mirror,command,goalsFor,goalsAgainst,passes,receptions,shots,corrections;
            public float seconds,ownPossession;
        }
        [Serializable] sealed class Report {public string note="Matched-state diagnostics, not independent full-match quality proof";public List<Row> rows=new List<Row>();}
        [UnityTest,Timeout(600000)] public IEnumerator MatchedSituationsCompareEachExecutionChangeWithoutSelectingForTheManager()
        {
            var report=new Report();var output=Path.Combine(FiveP7Evidence.DirectoryPath,"tactic-expansion-ablations.json");
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            foreach(int variant in new[]{0,1,2,3,4})foreach(int scenario in new[]{1,3,7,8,9,10})
            for(int mirror=0;mirror<2;mirror++)foreach(int command in Enumerable.Range(0,8))
            {
                using(var w=new FiveTestWorld())
                {
                    var m=w.match;SetVariant(m,variant);
                    var attacking=FiveMatchupPlayTests.Arrange(w,scenario,63117,mirror!=0,1.2f);
                    var own=scenario==7?FiveMatch.Other(attacking):attacking;
                    m.SelectCommand(own,(FiveCommand)command,"matched-execution-diagnostic",true);
                    m.SelectCommand(FiveMatch.Other(own),scenario==7?FiveCommand.FastTransition:scenario==3||scenario==9?FiveCommand.GoalBlock:FiveCommand.AllRecover,"matched-opposition",true);
                    var row=new Row{variant=variant,scenario=scenario,seed=63117,mirror=mirror,command=command};
                    var start=m.Elapsed;
                    for(int step=0;step<600;step++)
                    {
                        w.Step(1);
                        if(m.Carrier!=null&&m.Carrier.team==own)row.ownPossession+=.02f;
                        if(step==149||step==299||step==599)
                        {
                            report.rows.Add(new Row{variant=variant,scenario=scenario,seed=63117,mirror=mirror,command=command,seconds=m.Elapsed-start,
                                goalsFor=own==Team.Red?m.RedScore:m.NavyScore,goalsAgainst=own==Team.Red?m.NavyScore:m.RedScore,
                                passes=m.Events.Count(e=>e.kind=="pass-kicked"&&e.actor/5==(int)own),
                                receptions=m.Events.Count(e=>e.kind=="pass-received"&&e.actor/5==(int)own),
                                shots=m.Events.Count(e=>e.kind=="shot"&&e.actor/5==(int)own),corrections=m.BoundaryCorrections,ownPossession=row.ownPossession});
                        }
                    }
                    Assert.That(m.Decision(own).command,Is.EqualTo((FiveCommand)command));
                    Assert.That(m.BoundaryCorrections,Is.Zero);
                }
                File.WriteAllText(output,JsonUtility.ToJson(report,true));yield return null;
            }
            Assert.That(report.rows.Count,Is.EqualTo(1440));
        }
    }
}
