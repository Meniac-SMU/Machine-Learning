using System;
using System.IO;
using MachineLearning.Soccer;
using UnityEngine;

namespace MachineLearning.Soccer.Manager
{
    public sealed partial class MNG_MatchController
    {
        readonly MNG_PlayerTask[] passBuildKick = new MNG_PlayerTask[2];
        readonly int[] passBuildCarrier = { -1, -1 };
        readonly bool[] passBuildInFlight = new bool[2];

        public bool IsPassBuildExecutionValid(Team team, int carrier, int receiver, Vector2 target)
        {
            // Capture live bodies, including opponents moving during plate extension.
            CaptureSnapshot();
            return m_Snapshot.Carrier.IsValid && m_Snapshot.Carrier.Team == team && m_Snapshot.Carrier.Slot == carrier
                && MNG_TacticalTargetResolver.IsPassBuildReceiverValid(m_Snapshot, team, carrier, receiver)
                && MNG_TacticalTargetResolver.IsPassBuildLaneOpen(m_Snapshot, team, target);
        }
        public void RecordPassBuildStrike(Team team)
        {
            passBuildInFlight[(int)team] = true;
            passBuildKick[(int)team].ExpirySeconds = EpisodeElapsedSeconds + MNG_TeamPlanner.KickExecutionSeconds;
        }
        void UpdatePassBuildRules()
        {
            for (var t=0;t<2;t++)
            {
                if(passBuildCarrier[t]<0) continue;
                var task=passBuildKick[t]; var carrier=m_Snapshot.Carrier;
                var valid=task.ExpirySeconds>EpisodeElapsedSeconds && (passBuildInFlight[t] ? !carrier.IsValid
                    : carrier.IsValid && carrier.Team==(Team)t && carrier.Slot==passBuildCarrier[t]
                    && MNG_TacticalTargetResolver.IsPassBuildReceiverValid(m_Snapshot,(Team)t,carrier.Slot,task.ReceiverSlot)
                    && MNG_TacticalTargetResolver.IsPassBuildLaneOpen(m_Snapshot,(Team)t,task.Target));
                if(valid) continue;
                var sender=passBuildCarrier[t];
                passBuildCarrier[t]=-1;passBuildInFlight[t]=false;
                // End only this contract; do not invent a replacement manager command.
                m_ExecutorsByIndex[GetIndex((Team)t,sender)]?.CancelPassBuild(task.ParentCommandId);
                m_ExecutorsByIndex[GetIndex((Team)t,task.ReceiverSlot)]?.CancelPassBuild(task.ParentCommandId);
            }
        }
        void OverlayPassBuildRules(Team team, MNG_Command command, MNG_PlayerTask[] tasks, long revision)
        {
            var t=(int)team;var carrier=m_Snapshot.Carrier;
            if(commonCarrier[t]>=0) {passBuildCarrier[t]=-1;return;}
            if(passBuildCarrier[t]<0 && command==MNG_Command.PassBuild && carrier.IsValid && carrier.Team==team)
            {
                var task=tasks[carrier.Slot];
                if(task.Skill!=MNG_PlayerSkill.AimPass || !MNG_TacticalTargetResolver.IsPassBuildReceiverValid(m_Snapshot,team,carrier.Slot,task.ReceiverSlot)) return;
                task.PassBuildRule=true;task.ParentCommandId=revision*2+t;task.TaskId=task.ParentCommandId*4+carrier.Slot;
                task.Source=m_Managers[t]!=null && m_Managers[t].isActiveAndEnabled ? MNG_ActionSource.PolicyCommand : MNG_ActionSource.RuleCommand;
                passBuildKick[t]=task;passBuildCarrier[t]=carrier.Slot;passBuildInFlight[t]=false;
            }
            if(passBuildCarrier[t]<0 || passBuildKick[t].ExpirySeconds<=EpisodeElapsedSeconds)return;
            var active=passBuildKick[t];active.Revision=revision;
            if(!passBuildInFlight[t]) tasks[passBuildCarrier[t]]=active;
            active.Skill=MNG_PlayerSkill.ReceivePass;active.TaskId=active.ParentCommandId*4+active.ReceiverSlot;
            tasks[active.ReceiverSlot]=active;
        }

        readonly bool[] commonRulesEnabled = { true, true };
        readonly MNG_PlayerTask[] commonKick = new MNG_PlayerTask[2];
        readonly int[] commonCarrier = { -1, -1 };
        readonly bool[] commonInFlight = new bool[2];
        readonly long[] commonAttempts = new long[2], commonClearances = new long[2], commonNoTargets = new long[2], commonStrikes = new long[2];
        public bool CommonRulesEnabled(Team team) => commonRulesEnabled[(int)team];
        // Retired stagnation counters remain zero for existing result readers.
        public long CommonPassAttempts(Team team) => commonAttempts[(int)team];
        public long CommonClearanceAttempts(Team team) => commonClearances[(int)team];
        public long CommonNoTargetAttempts(Team team) => commonNoTargets[(int)team];
        public long CommonRuleStrikes(Team team) => commonStrikes[(int)team];
        public void ConfigureCommonRules(bool red, bool navy)
        { commonRulesEnabled[0]=red; commonRulesEnabled[1]=navy; ResetCommonRules(false); }
        void ResetCommonRules(bool counters)
        {
            for(var t=0;t<2;t++)
            {
                commonCarrier[t]=-1; commonInFlight[t]=false;
                commonKick[t]=default;
                passBuildCarrier[t]=-1;passBuildInFlight[t]=false;passBuildKick[t]=default;
                if(counters) commonAttempts[t]=commonClearances[t]=commonNoTargets[t]=commonStrikes[t]=0;
            }
        }
        public static int SelectCommonPassReceiver(MNG_MatchSnapshot snapshot, Team team, int carrier)
        {
            var sign=team==Team.Red?1f:-1f; var origin=snapshot.BallPosition;
            var best=-1; var bestScore=float.NegativeInfinity;
            for(var slot=0;slot<4;slot++)
            {
                var player=snapshot.GetPlayer(team,slot);
                if(slot==carrier || !player.Active || player.IsHuman) continue;
                var target=MNG_TeamPlanner.SelectPassTarget(snapshot,team,slot);
                var distance=Vector2.Distance(origin,target);
                if(distance<MNG_KickSolver.MinimumPassDistance || distance>MNG_KickSolver.MaximumPassDistance) continue;
                var forward=(target.x-origin.x)*sign;
                if(forward<=0f) continue;
                var lane=MNG_TacticalTargetResolver.NearestOpponentToSegment(snapshot,team,origin,target);
                var score=Mathf.Min(lane,8f)*2f + forward - distance*0.12f;
                if(score<=bestScore) continue;
                bestScore=score;best=slot;
            }
            return best;
        }
        void UpdateCommonRules()
        {
            for(var t=0;t<2;t++)
            {
                var team=(Team)t;var carrier=m_Snapshot.Carrier;
                var own=carrier.IsValid && carrier.Team==team;
                var active=commonCarrier[t]>=0 && commonKick[t].ExpirySeconds>EpisodeElapsedSeconds;
                if(active && (commonInFlight[t] ? carrier.IsValid : !own || carrier.Slot!=commonCarrier[t]))
                    active=false;
                if(!active) {commonCarrier[t]=-1;commonInFlight[t]=false;}
                if(!commonRulesEnabled[t]) continue;
                var slot=own?carrier.Slot:-1;
                if(active || !own || m_Snapshot.GetPlayer(team,slot).IsHuman) continue;
                var danger=SoccerDefensiveClearanceRules.IsBallInOwnGoalDanger(team,
                    new Vector3(m_Snapshot.BallPosition.x,0,m_Snapshot.BallPosition.y),arenaGeometry);
                if(!danger) continue;
                var receiver=SelectCommonPassReceiver(m_Snapshot,team,slot);
                var revision=++m_TaskRevisions[t];
                commonKick[t]=new MNG_PlayerTask {Skill=receiver>=0?MNG_PlayerSkill.AimPass:MNG_PlayerSkill.AimShot,
                    Target=receiver>=0?MNG_TeamPlanner.SelectPassTarget(m_Snapshot,team,receiver):Vector2.zero,
                    ReceiverSlot=receiver,Revision=revision,ParentCommandId=revision*2+t,TaskId=(revision*2+t)*4+slot,
                    ExpirySeconds=EpisodeElapsedSeconds+MNG_TeamPlanner.KickExecutionSeconds,
                    Source=MNG_ActionSource.RuleCommand,CommonRule=true};
                commonCarrier[t]=slot;
                if(danger)commonClearances[t]++;
                RecordCommonRule(team,"OwnGoalClearance",slot,receiver);
                DispatchTeamPlan(team,m_Decisions[t].PreviousCommand,m_Decisions[t]);
            }
        }
        void OverlayCommonRules(Team team,MNG_PlayerTask[] tasks,long revision)
        {
            var t=(int)team;var carrier=commonCarrier[t];var task=commonKick[t];
            if(!commonRulesEnabled[t] || carrier<0 || task.ExpirySeconds<=EpisodeElapsedSeconds) return;
            task.Revision=revision;
            if(!commonInFlight[t]) tasks[carrier]=task;
            if(task.ReceiverSlot>=0)
            {
                task.Skill=MNG_PlayerSkill.ReceivePass;task.TaskId=task.ParentCommandId*4+task.ReceiverSlot;
                tasks[task.ReceiverSlot]=task;
            }
        }
        public void RecordCommonRuleKick(Team team,long kickId)
        {
            var t=(int)team;commonStrikes[t]++;
            RecordCommonRule(team,"PhysicalStrike",commonCarrier[t],commonKick[t].ReceiverSlot,kickId);
            commonInFlight[t]=true;
            commonKick[t].ExpirySeconds=EpisodeElapsedSeconds+MNG_TeamPlanner.KickExecutionSeconds;
        }
        void RecordCommonRule(Team team,string reason,int slot,int receiver,long kickId=0)
        {
            var args=Environment.GetCommandLineArgs();var i=Array.IndexOf(args,"-mngEvidenceDir");
            if(i<0 || i+1>=args.Length)return;
            Directory.CreateDirectory(args[i+1]);
            File.AppendAllText(Path.Combine(args[i+1],MNG_MSController.EvidenceFileName("common-rules")),JsonUtility.ToJson(new CommonRuleRecord {
                episode=m_EpisodeId,tick=m_TickId,time=EpisodeElapsedSeconds,team=(int)team,slot=slot,receiver=receiver,
                reason=reason,kickId=kickId,parentCommandId=commonKick[(int)team].ParentCommandId,policyCommand=(int)m_Decisions[(int)team].PreviousCommand})+"\n");
        }
        [Serializable] sealed class CommonRuleRecord
        {public long episode,tick,kickId,parentCommandId;public float time;public int team,slot,receiver,policyCommand;public string reason;}
    }
}
