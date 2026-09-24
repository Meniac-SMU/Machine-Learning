using System.Collections;
using System.Reflection;
using MachineLearning.Soccer;
using NUnit.Framework;
using Unity.MLAgents;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif
namespace MachineLearning.Soccer.Manager.Tests
{
    public sealed class MNG_CommonRulesPlayModeTests
    {
        [UnityTest] public IEnumerator ClearanceProducesPhysicalPassesAndShotsForBothTeamsAndRoles()
        {
#if UNITY_EDITOR
            var automatic=Academy.Instance.AutomaticSteppingEnabled;var scale=Time.timeScale;
            try
            {
                Academy.Instance.AutomaticSteppingEnabled=false;
                yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/_Soccer/Manager/Curriculum/MS_V2/MNG_MS2V2_Train.unity",new LoadSceneParameters(LoadSceneMode.Single));
                var match=Object.FindFirstObjectByType<MNG_MatchController>();
                foreach(var manager in match.GetComponentsInChildren<MNG_ManagerAgent>(true)){manager.enabled=false;manager.GetComponent<DecisionRequester>().enabled=false;}
                foreach(var manager in match.GetComponentsInChildren<MNG_RuleBasedManager>(true))manager.enabled=false;
                match.GetComponent<MNG_MSController>().enabled=false;
                var control=match.GetComponentInChildren<MNG_BallControl>();var ball=control.GetComponent<Rigidbody>();
                var tracker=match.GetComponent<MNG_TacticalRewardTracker>();
                Time.timeScale=10;
                foreach(var team in new[]{Team.Red,Team.Navy}) foreach(var slot in new[]{0,3}) foreach(var mode in new[]{0,1,2})
                {
                    var danger=mode>0;var noReceiver=mode==2;
                    foreach(var avatar in match.GetComponentsInChildren<MNG_PlayerAvatar>(true))avatar.gameObject.SetActive(true);
                    match.ResetMatch();match.ConfigureCommonRules(false,false);
                    var sign=team==Team.Red?1f:-1f;var forward=new Vector2(sign,0);var origin=new Vector2(danger?-sign*55:0,0);
                    var sender=match.GetPlayerAvatar(team,slot);var receiver=match.GetPlayerAvatar(team,1);
                    foreach(var avatar in match.GetComponentsInChildren<MNG_PlayerAvatar>(true))
                    {avatar.GetComponent<MNG_PlayerSkillExecutor>().enabled=false;if(avatar!=sender&&(avatar!=receiver||noReceiver))avatar.gameObject.SetActive(false);}
                    Pose(sender,origin-forward*MNG_KickPlate.DribbleAnchorForward,forward);Pose(receiver,origin+forward*10,forward);
                    ball.position=new Vector3(origin.x,ball.position.y,origin.y);ball.linearVelocity=ball.angularVelocity=Vector3.zero;Physics.SyncTransforms();
                    for(var tick=0;tick<20;tick++)
                    {
                        sender.GetComponent<MNG_PlayerMotor>().ApplyDesiredVelocity(forward,forward,MNG_InputOwner.Manager,sender.Ownership.Revision,Time.fixedDeltaTime);
                        yield return new WaitForFixedUpdate();
                        if(control.Carrier.IsValid&&control.Carrier.Team==team&&control.Carrier.Slot==slot)break;
                    }
                    Assert.IsTrue(control.Carrier.IsValid,$"Fixture possession {team}/{slot}/{danger}");
                    sender.GetComponent<MNG_PlayerSkillExecutor>().enabled=true;receiver.GetComponent<MNG_PlayerSkillExecutor>().enabled=true;
                    match.ConfigureCommonRules(true,true);
                    if(mode==0)
                    {
                        match.SetDecisionTargets(team,true,false,1);
                        match.AcceptPolicyCommand(team,MNG_Command.PassBuild);
                    }
                    var strikesBefore=tracker.GetPassStrikeCount(team);
                    var receptionsBefore=tracker.GetIntendedReceptionCount(team);
                    for(var tick=0;tick<350;tick++)
                    {
                        if(tick%25==0)match.AcceptPolicyCommand(team,MNG_Command.ProtectBack);
                        yield return new WaitForFixedUpdate();
                        if(danger && tick%20==0)Debug.Log($"COMMON PHYS {team}/{slot} tick={tick} attempts={match.CommonClearanceAttempts(team)} ball={ball.position} sender={sender.Body.position} task={sender.GetComponent<MNG_PlayerSkillExecutor>().CurrentTask.Skill} common={sender.GetComponent<MNG_PlayerSkillExecutor>().CurrentTask.CommonRule} receiver={receiver.GetComponent<MNG_PlayerSkillExecutor>().CurrentTask.Skill} carrier={control.Carrier.IsValid}/{control.Carrier.Slot}");
                        if(noReceiver?match.CommonRuleStrikes(team)>0&&(ball.position.x-origin.x)*sign>2f:tracker.GetIntendedReceptionCount(team)>receptionsBefore)break;
                    }
                    Assert.Greater(danger?match.CommonRuleStrikes(team):tracker.GetPassStrikeCount(team)-strikesBefore,0,$"Physical rule strike {team}/{slot}/{danger}");
                    if(!noReceiver)Assert.Greater(tracker.GetIntendedReceptionCount(team),receptionsBefore,$"Physical reception {team}/{slot}/{danger}");
                    if(danger)Assert.Greater((ball.position.x-origin.x)*sign,0,"Clearance must move away from own goal");
                }
            }
            finally {Academy.Instance.AutomaticSteppingEnabled=automatic;Time.timeScale=scale;}
#else
            yield break;
#endif
        }
        static void Pose(MNG_PlayerAvatar avatar,Vector2 point,Vector2 direction)
        {
            var p=new Vector3(point.x,.52f,point.y);var q=Quaternion.LookRotation(new Vector3(direction.x,0,direction.y));
            avatar.transform.SetPositionAndRotation(p,q);avatar.Body.position=p;avatar.Body.rotation=q;
            avatar.Body.linearVelocity=avatar.Body.angularVelocity=Vector3.zero;
        }
        [UnityTest] public IEnumerator SelectedForwardPassPersistsButStagnationNeverForcesPass()
        {
#if UNITY_EDITOR
            var automatic=Academy.Instance.AutomaticSteppingEnabled;var scale=Time.timeScale;
            try
            {
                Academy.Instance.AutomaticSteppingEnabled=false;
                yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/_Soccer/Manager/Curriculum/MS_V2/MNG_MS2V2_Train.unity",new LoadSceneParameters(LoadSceneMode.Single));
                var match=Object.FindFirstObjectByType<MNG_MatchController>();
                foreach(var manager in match.GetComponentsInChildren<MNG_ManagerAgent>(true)){manager.enabled=false;manager.GetComponent<DecisionRequester>().enabled=false;}
                foreach(var manager in match.GetComponentsInChildren<MNG_RuleBasedManager>(true))manager.enabled=false;
                match.GetComponent<MNG_MSController>().enabled=false;
                var flags=BindingFlags.Instance|BindingFlags.NonPublic;
                var clock=typeof(MNG_MatchController).GetField("<EpisodeElapsedSeconds>k__BackingField",flags);
                var update=typeof(MNG_MatchController).GetMethod("UpdateCommonRules",flags);
                foreach(var team in new[]{Team.Red,Team.Navy}) foreach(var slot in new[]{0,3})
                {
                    match.ResetMatch();match.ConfigureCommonRules(team==Team.Red,team==Team.Navy);
                    var sign=team==Team.Red?1f:-1f;var snapshot=match.Snapshot;
                    for(var t=0;t<2;t++)for(var s=0;s<4;s++)
                    {var p=snapshot.GetPlayer((Team)t,s);p.Active=t==(int)team && (s==slot || s==1);p.Position=sign*new Vector2(s==1?10:0,0);snapshot.SetPlayer((Team)t,s,p);}
                    snapshot.BallPosition=Vector2.zero;
                    var far=snapshot.GetPlayer(team,1);far.Position=new Vector2(sign*40,0);snapshot.SetPlayer(team,1,far);
                    Assert.AreEqual(-1,MNG_MatchController.SelectCommonPassReceiver(snapshot,team,slot),"Clearance has a finite physical pass range");
                    far.Position=new Vector2(sign*10,0);snapshot.SetPlayer(team,1,far);
                    match.SetPossession(team==Team.Red?MNG_Possession.Red:MNG_Possession.Navy,MNG_CarrierRef.For(team,slot));
                    // Executor validates against the live possession ledger as well.
                    var ball=match.GetComponentInChildren<MNG_BallControl>();
                    var candidate=new[]{new MNG_PossessionCandidate {Team=team,Slot=slot,Active=true,HasPhysicalContact=true,IsInControlZone=true,CenterDistance=1,AcquisitionDistance=2}};
                    ball.Ledger.Update(candidate,1,.1f);ball.Ledger.Update(candidate,1,.1f);
                    match.SetPossession(team==Team.Red?MNG_Possession.Red:MNG_Possession.Navy,MNG_CarrierRef.For(team,slot));
                    clock.SetValue(match,0f);update.Invoke(match,null);
                    clock.SetValue(match,1.99f);update.Invoke(match,null);Assert.AreEqual(0,match.CommonPassAttempts(team));
                    clock.SetValue(match,20f);update.Invoke(match,null);Assert.AreEqual(0,match.CommonPassAttempts(team));
                    snapshot.EpisodeElapsedSeconds=20f;
                    match.SetDecisionTargets(team,true,false,1);
                    match.AcceptPolicyCommand(team,MNG_Command.PassBuild);
                    match.AcceptPolicyCommand(team,MNG_Command.ProtectBack);
                    Assert.AreEqual(MNG_Command.ProtectBack,match.GetDecisionState(team).PreviousCommand);
                    Assert.AreEqual(MNG_PlayerSkill.AimPass,match.GetPlayerAvatar(team,slot).GetComponent<MNG_PlayerSkillExecutor>().CurrentTask.Skill);
                    var receiver=match.GetPlayerAvatar(team,1).GetComponent<MNG_PlayerSkillExecutor>().CurrentTask;
                    Assert.IsTrue(receiver.PassBuildRule);Assert.AreEqual(MNG_PlayerSkill.ReceivePass,receiver.Skill);
                    var other=team==Team.Red?Team.Navy:Team.Red;
                    snapshot.SetPlayer(other,3,new MNG_PlayerState {Active=true,Position=receiver.Target*.5f});
                    typeof(MNG_MatchController).GetMethod("UpdatePassBuildRules",flags).Invoke(match,null);
                    Assert.AreNotEqual(MNG_PlayerSkill.AimPass,match.GetPlayerAvatar(team,slot).GetComponent<MNG_PlayerSkillExecutor>().CurrentTask.Skill,"New blocker cancels preparation immediately");
                    match.ConfigureCommonRules(false,false);match.AcceptPolicyCommand(team,MNG_Command.ActiveRecover);
                    Assert.IsFalse(match.GetPlayerAvatar(team,slot).GetComponent<MNG_PlayerSkillExecutor>().CurrentTask.CommonRule);
                    match.ConfigureCommonRules(true,true);match.ResetMatch();
                    snapshot.BallPosition=new Vector2(-sign*55,0);
                    match.SetPossession(team==Team.Red?MNG_Possession.Red:MNG_Possession.Navy,MNG_CarrierRef.For(team,slot));
                    ball.Ledger.Update(candidate,1,.1f);ball.Ledger.Update(candidate,1,.1f);
                    update.Invoke(match,null);Assert.AreEqual(1,match.CommonClearanceAttempts(team));
                    Assert.That(MNG_MatchController.SelectCommonPassReceiver(snapshot,team,slot),Is.InRange(-1,3));
                }
            }
            finally {Academy.Instance.AutomaticSteppingEnabled=automatic;Time.timeScale=scale;}
#else
            yield break;
#endif
        }
    }
}
