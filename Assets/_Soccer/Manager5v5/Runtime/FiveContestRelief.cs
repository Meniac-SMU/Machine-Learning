using System;
using System.Linq;
using UnityEngine;

namespace MachineLearning.Soccer.Manager5v5
{
    public enum FiveReliefAction { None, Reangle, BackOff, SideCarry, OutletPass, MakeSpace }

    public sealed partial class FiveMatch
    {
        public const string PlayerRuntimeVersion="tactics-v11-keeper-turn-first";
        // Shared player execution: no manager type/action selection or reward is read or changed here.
        public bool contestReliefEnabled=true;
        sealed class ReliefTask
        {
            public FiveReliefAction action;
            public Vector2 target;
            public float started;
        }
        readonly ReliefTask[] reliefTasks=new ReliefTask[10];
        readonly int[] reliefAttempts=new int[2];
        Vector2 reliefOrigin;
        float contestSlowTime,contestRoundStarted,reliefUntil,reliefCooldownUntil,reliefFastTime;
        bool requestingContestPass;
        public bool ContestReliefActive=>reliefUntil>Elapsed;
        public FiveReliefAction ReliefAction(int index)=>reliefTasks[index]?.action??FiveReliefAction.None;
        bool ContestControls(FivePlayer p)=>ContestReliefActive&&reliefTasks[p.Index]!=null;

        void ResetContestRelief()
        {
            Array.Clear(reliefTasks,0,reliefTasks.Length);Array.Clear(reliefAttempts,0,reliefAttempts.Length);
            contestSlowTime=reliefUntil=reliefCooldownUntil=reliefFastTime=0;
            contestRoundStarted=Elapsed;requestingContestPass=false;
            ResetLocalSupport();
            ResetCarrierMobility();
        }
        bool CanRelieve(FivePlayer p)
        {
            if(p.IsKeeper||IsHuman(p)||Vector2.Distance(p.Position,BallPosition)>profile.contestRadius)return false;
            return !IsRecoveryReserve(p);
        }
        bool IsRecoveryReserve(FivePlayer p)
        {
            // T7's excluded fourth field is never recruited into the contest by this support layer.
            var t=(int)p.team;
            return Decision(p.team).command==FiveCommand.AllRecover&&recoveryOrder[t]!=null
                &&recoveryOrder[t][3]==p.Index&&Owner!=p.Index;
        }
        void FinishContestRelief(string reason)
        {
            if(reliefTasks.Any(t=>t!=null))Record("contest-end",reason:reason);
            Array.Clear(reliefTasks,0,reliefTasks.Length);
            reliefUntil=0;contestSlowTime=reliefFastTime=0;reliefCooldownUntil=Elapsed+.35f;
        }
        void PlanContestRelief(float dt)
        {
            if(!contestReliefEnabled){if(reliefTasks.Any(t=>t!=null))FinishContestRelief("disabled");return;}
            if(Elapsed-contestRoundStarted<profile.contestRestartGrace)return;
            if(PassPhase==FivePassPhase.InFlight)
            {if(reliefTasks.Any(t=>t!=null))FinishContestRelief("pass-away");contestSlowTime=0;return;}

            if(reliefTasks.Any(t=>t!=null))
            {
                reliefFastTime=BallVelocity.magnitude>profile.contestSlowSpeed*2?reliefFastTime+dt:0;
                if(Vector2.Distance(BallPosition,reliefOrigin)>2||reliefFastTime>.3f||Elapsed>=reliefUntil)
                    FinishContestRelief(Elapsed>=reliefUntil?"retry-window":"ball-moving");
            }
            if(!ContestReliefActive)
            {
                var nearby=players.Where(CanRelieve).ToArray();
                var slow=BallVelocity.magnitude<=profile.contestSlowSpeed||ObservedStallSeconds>=profile.contestConfirmSeconds;
                // Brief collision spikes must not erase an otherwise slow moving scrum.
                contestSlowTime=nearby.Length<2?0:slow?contestSlowTime+dt:
                    BallVelocity.magnitude>profile.contestSlowSpeed*2?0:Mathf.Max(0,contestSlowTime-dt*2);
                if(contestSlowTime<profile.contestConfirmSeconds||Elapsed<reliefCooldownUntil)return;
                BeginContestRelief(nearby);
            }

            for(int i=0;i<players.Length;i++)
            {
                var task=reliefTasks[i];if(task==null)continue;
                var player=players[i];
                if(IsRecoveryReserve(player)){reliefTasks[i]=null;continue;}
                if(Vector2.Distance(player.Position,BallPosition)>profile.contestRadius+3)
                {reliefTasks[i]=null;continue;}
                if(Owner==i&&(task.action==FiveReliefAction.Reangle||task.action==FiveReliefAction.MakeSpace))
                {
                    if(TryReliefPass(player)){task.action=FiveReliefAction.OutletPass;continue;}
                    if(TryReliefCarry(player,out var exit))
                    {task.action=FiveReliefAction.SideCarry;task.target=exit;task.started=Elapsed;Record("contest-side-carry",i,reason:"after-front-recovery");}
                }
                if(task.action==FiveReliefAction.OutletPass)
                {
                    // The ordinary pass handshake owns the sender/receiver while preparing.
                    if(Owner==i&&PassPhase==FivePassPhase.Preparing)continue;
                    task.action=FiveReliefAction.BackOff;task.started=Elapsed;
                    task.target=ReliefPoint(player,(player.Position-BallPosition).normalized,1.4f);
                    Record("contest-backoff",i,reason:"outlet-lost-or-cancelled");
                }
                if(task.action==FiveReliefAction.SideCarry&&Owner!=i)
                {
                    task.action=FiveReliefAction.Reangle;task.started=Elapsed;
                    task.target=ChooseReliefApproach(player,reliefAttempts[(int)player.team]);
                    Record("contest-reangle",i,reason:"carry-lost-front-pocket");
                }
                var age=Elapsed-task.started;
                if(task.action==FiveReliefAction.BackOff&&age>=.55f)
                {
                    task.action=FiveReliefAction.Reangle;task.started=Elapsed;
                    task.target=ChooseReliefApproach(player,reliefAttempts[(int)player.team]);
                    Record("contest-reangle",i,reason:"after-short-retreat");
                }
                if(task.action==FiveReliefAction.Reangle&&(age>=.9f||Vector2.Distance(player.Position,task.target)<.45f))
                {
                    // Approach from the new angle, looking at the ball even when adjusting backwards.
                    ApproachBall(player);continue;
                }
                player.SetTarget(task.target,task.action==FiveReliefAction.SideCarry?task.target:BallPosition,
                    task.action!=FiveReliefAction.SideCarry);
            }
        }
        void BeginContestRelief(FivePlayer[] nearby)
        {
            reliefOrigin=BallPosition;reliefUntil=Elapsed+profile.contestActionSeconds;reliefFastTime=0;
            if(PassPhase==FivePassPhase.Preparing&&nearby.Any(p=>p.Index==PassSender))CancelPass("contested-slow-ball");
            if(shotPlayer>=0&&nearby.Any(p=>p.Index==shotPlayer))
            {Record("contest-cancel-shot",shotPlayer,reason:"contested-slow-ball");shotPlayer=-1;}
            Record("contest-start",Owner,reason:"slow-ball-with-nearby-fields");
            foreach(var team in new[]{Team.Red,Team.Navy})
            {
                var local=nearby.Where(p=>p.team==team).OrderBy(p=>p.Index==Owner?-1:Vector2.Distance(p.Position,BallPosition)).ThenBy(p=>p.slot).ToArray();
                if(local.Length==0)continue;
                var actor=local[0];var attempt=reliefAttempts[(int)team]++;
                var task=new ReliefTask{started=Elapsed};reliefTasks[actor.Index]=task;
                if(Owner==actor.Index&&TryReliefPass(actor))
                {task.action=FiveReliefAction.OutletPass;task.target=actor.Position;}
                else if(Owner==actor.Index&&TryReliefCarry(actor,out var exit))
                {task.action=FiveReliefAction.SideCarry;task.target=exit;Record("contest-side-carry",actor.Index);}
                else if(attempt%2==0)
                {
                    task.action=FiveReliefAction.BackOff;
                    task.target=ReliefPoint(actor,(actor.Position-BallPosition).normalized,1.4f);
                    Record("contest-backoff",actor.Index);
                }
                else
                {task.action=FiveReliefAction.Reangle;task.target=ChooseReliefApproach(actor,attempt);Record("contest-reangle",actor.Index);}
                foreach(var support in local.Skip(1))
                {
                    var outward=(support.Position-BallPosition).normalized;
                    reliefTasks[support.Index]=new ReliefTask{action=FiveReliefAction.MakeSpace,started=Elapsed,
                        target=ReliefPoint(support,outward,2.2f)};
                    Record("contest-make-space",support.Index,actor.Index);
                }
            }
        }
        Vector2 ReliefPoint(FivePlayer player,Vector2 preferred,float distance)
        {
            if(preferred.sqrMagnitude<.01f)preferred=-player.Forward;
            var best=player.Position;float bestScore=float.NegativeInfinity;
            foreach(var angle in new[]{0f,45f,-45f,90f,-90f,135f,-135f,180f})
            {
                var dir=RotateRelief(preferred,angle);var raw=player.Position+dir*distance;
                var point=ClampField(raw,player.radius+profile.ballContactOffset);
                var score=ReliefClearance(player,point)+Vector2.Distance(point,BallPosition)*.25f
                    -Vector2.Distance(point,raw)*2-Mathf.Abs(angle)*.002f;
                if(score>bestScore){bestScore=score;best=point;}
            }
            return best;
        }
        Vector2 ChooseReliefApproach(FivePlayer player,int attempt)
        {
            var radial=(player.Position-BallPosition).normalized;
            if(radial.sqrMagnitude<.01f)radial=-player.Forward;
            var best=player.Position;float bestScore=float.NegativeInfinity;
            foreach(var angle in new[]{55f,-55f,90f,-90f,135f,-135f})
            {
                var raw=BallPosition+RotateRelief(radial,angle)*2.6f;
                var point=ClampField(raw,player.radius+profile.ballContactOffset);
                if(Vector2.Distance(point,BallPosition)<profile.holdOffset+player.radius)continue;
                var score=ReliefClearance(player,point)-Vector2.Distance(point,raw)*3
                    +Mathf.Sign(angle)*(attempt%2==0?1:-1)*.12f;
                if(score>bestScore){bestScore=score;best=point;}
            }
            return best;
        }
        float ReliefClearance(FivePlayer actor,Vector2 point)
        {
            var clearance=5f;
            foreach(var p in players)
            {
                if(p==actor)continue;
                clearance=Mathf.Min(clearance,Vector2.Distance(p.Position,point)-p.radius-actor.radius);
                var reserved=reliefTasks[p.Index];
                if(reserved!=null)clearance=Mathf.Min(clearance,Vector2.Distance(reserved.target,point)-p.radius-actor.radius);
            }
            return clearance;
        }
        bool TryReliefPass(FivePlayer owner)
        {
            var receiver=players.Where(p=>p.team==owner.team&&p!=owner)
                .Where(p=>Vector2.Distance(p.Position,owner.Position)>=4&&Vector2.Distance(p.Position,owner.Position)<=24)
                .Where(p=>FacingPoint(owner,p.Position,.82f)&&PassingLaneOpen(BallPosition,p.Position,owner.team,p.Index))
                .OrderByDescending(p=>NearestOpponentDistance(p)-Vector2.Distance(p.Position,owner.Position)*.1f).ThenBy(p=>p.Index).FirstOrDefault();
            if(receiver==null)return false;
            requestingContestPass=true;
            try
            {
                if(!RequestPass(owner,receiver))return false;
                Record("contest-outlet-pass",owner.Index,receiver.Index);return true;
            }
            finally{requestingContestPass=false;}
        }
        bool TryReliefCarry(FivePlayer owner,out Vector2 target)
        {
            target=owner.Position;float bestScore=.35f;
            foreach(var angle in new[]{30f,-30f,55f,-55f,80f,-80f})
            {
                var direction=RotateRelief(owner.Forward,angle);var raw=owner.Position+direction*3;
                var point=ClampField(raw,profile.holdOffset+owner.radius);
                if(Vector2.Distance(point,raw)>.4f)continue;
                var pathClear=players.Where(p=>p!=owner).All(p=>DistanceToSegment(p.Position,owner.Position+direction,point)>p.radius+owner.radius+.15f);
                if(!pathClear)continue;
                var score=ReliefClearance(owner,point)-Mathf.Abs(angle)*.005f;
                if(score>bestScore){bestScore=score;target=point;}
            }
            return bestScore>.35f;
        }
        static Vector2 RotateRelief(Vector2 v,float angle)
        {
            var r=angle*Mathf.Deg2Rad;return new Vector2(v.x*Mathf.Cos(r)-v.y*Mathf.Sin(r),v.x*Mathf.Sin(r)+v.y*Mathf.Cos(r));
        }
    }
}
