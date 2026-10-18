using System.Collections;
using System.Linq;
using UnityEngine;

namespace MachineLearning.Soccer.Manager5v5.Demo
{
    public sealed partial class FiveDemoApp
    {
        IEnumerator GoalContinuationSmokeBody()
        {
            ConfigureMatch(true,300,0,0);Kickoff();yield return null;
            session.automaticSteps=false;human.enabled=false;
            CheckDemo(match.continueBallAfterGoal,"demo-enables-post-goal-ball-physics");
            foreach(var team in new[]{Team.Red,Team.Navy})
            {
                RobotReset();
                var original=match.players.Select(p=>p.Body.constraints).ToArray();
                match.ball.position=new Vector3(0,match.ballSpawn.y,0);
                match.ball.linearVelocity=new Vector3(6,0,2);match.ball.angularVelocity=new Vector3(0,2,0);
                Physics.SyncTransforms();var velocity=match.ball.linearVelocity;var angular=match.ball.angularVelocity;
                match.Score(team);
                CheckDemo(match.ball.linearVelocity==velocity&&match.ball.angularVelocity==angular,"goal-keeps-ball-momentum-"+team);
                var positions=match.players.Select(p=>p.Body.position).ToArray();
                var ballBefore=match.ball.position;float elapsed=match.Elapsed;
                RobotStep(10);
                CheckDemo(Vector3.Distance(ballBefore,match.ball.position)>.5f&&match.State==FiveMatchState.GoalPause,"ball-continues-during-goal-wait-"+team);
                CheckDemo(match.players.Select((p,i)=>p.Body.position==positions[i]&&p.Body.constraints==RigidbodyConstraints.FreezeAll).All(v=>v)
                    &&match.Elapsed==elapsed,"goal-wait-freezes-players-and-clock-"+team);
                int goals=match.RedScore+match.NavyScore;match.Score(team);
                CheckDemo(match.RedScore+match.NavyScore==goals,"goal-wait-rejects-duplicate-score-"+team);
                match.Paused=true;ballBefore=match.ball.position;float remaining=match.PauseRemaining;
                RobotStep(10);
                CheckDemo(match.ball.position==ballBefore&&match.PauseRemaining==remaining,"esc-still-freezes-ball-and-goal-countdown-"+team);
                match.Paused=false;RobotStep(5);
                CheckDemo(Vector3.Distance(ballBefore,match.ball.position)>.1f,"resume-restores-ball-motion-"+team);
                // Complete countdown without an extra Playing physics tick.
                match.Tick(match.PauseRemaining+.001f);
                CheckDemo(match.State==FiveMatchState.Playing&&match.ball.position==match.ballSpawn&&match.ball.linearVelocity==Vector3.zero
                    &&match.players.Select((p,i)=>p.Body.constraints==original[i]).All(v=>v),"kickoff-restores-spawns-and-player-constraints-"+team);
                match.Score(team);match.Paused=true;match.ResetMatch();
                CheckDemo(!match.Paused&&match.players.Select((p,i)=>p.Body.constraints==original[i]).All(v=>v),"reset-during-paused-goal-restores-constraints-"+team);
            }
            match.continueBallAfterGoal=false;RobotReset();match.ball.linearVelocity=Vector3.right*6;match.ball.angularVelocity=Vector3.up*2;
            match.Score(Team.Red);
            CheckDemo(match.ball.linearVelocity==Vector3.zero&&match.ball.angularVelocity==Vector3.zero
                &&match.players.All(p=>p.Body.constraints!=RigidbodyConstraints.FreezeAll),"legacy-training-goal-behaviour-preserved");
            Home();yield return null;
        }
    }
}
