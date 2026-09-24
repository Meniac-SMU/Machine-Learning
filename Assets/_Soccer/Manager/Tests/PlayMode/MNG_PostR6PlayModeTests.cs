using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
    public sealed class MNG_PostR6PlayModeTests
    {
        bool stepping; float scale;
        [SetUp] public void Setup() { stepping = Academy.Instance.AutomaticSteppingEnabled; scale = Time.timeScale; }
        [TearDown] public void Cleanup() { Academy.Instance.AutomaticSteppingEnabled = stepping; Time.timeScale = scale; }

        // Low-level kick/reception regression: tactical legality is tested separately.
        static bool IssueTechnicalPass(MNG_MatchController match, Team team)
        {
            var revisions=(long[])typeof(MNG_MatchController).GetField("m_TaskRevisions",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(match);
            var revision=++revisions[(int)team];var parent=revision*2+(int)team;
            var task=new MNG_PlayerTask {Skill=MNG_PlayerSkill.AimPass,ReceiverSlot=1,Target=MNG_TeamPlanner.SelectPassTarget(match.Snapshot,team,1),
                Revision=revision,TaskId=parent*4+3,ParentCommandId=parent,ExpirySeconds=match.EpisodeElapsedSeconds+2};
            var result=match.GetPlayerAvatar(team,3).GetComponent<MNG_PlayerSkillExecutor>().RequestTaskV2(task);
            task.Skill=MNG_PlayerSkill.ReceivePass;task.TaskId=parent*4+1;
            match.GetPlayerAvatar(team,1).GetComponent<MNG_PlayerSkillExecutor>().RequestTaskV2(task);
            return result.Kind==MNG_TaskResultKind.Accepted || result.Kind==MNG_TaskResultKind.RetainedEquivalent;
        }
        [UnityTest]
        public IEnumerator PassPipelineRunsTwentyTwoMirroredPhysicalFixtures()
        {
#if UNITY_EDITOR
            Academy.Instance.AutomaticSteppingEnabled = false;
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/_Soccer/Manager/Curriculum/MS_V2/MNG_MS2V2_Train.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var match = Object.FindFirstObjectByType<MNG_MatchController>();
            foreach (var rule in match.GetComponentsInChildren<MNG_RuleBasedManager>(true)) rule.enabled = false;
            foreach (var manager in match.GetComponentsInChildren<MNG_ManagerAgent>(true))
            { manager.GetComponent<DecisionRequester>().enabled = false; manager.enabled = false; }
            match.GetComponent<MNG_MSController>().enabled = false;
            match.ConfigureMatchDuration(300, false, MNG_MatchFinishMode.TerminalResult);
            var ballControl = match.GetComponentInChildren<MNG_BallControl>();
            var ball = ballControl.GetComponent<Rigidbody>();
            var tracker = match.GetComponent<MNG_TacticalRewardTracker>();
            var failures = new List<string>();
            match.ConfigureCommonRules(false,false); // Isolate the original technical pass/cancellation contract.
            var receivers = new[] { new Vector2(10,0), new Vector2(2,10), new Vector2(-10,0),
                new Vector2(12,5), new Vector2(5,12), new Vector2(12,0), new Vector2(12,8), new Vector2(12,8), new Vector2(10,0), new Vector2(10,0), new Vector2(10,0) };
            Time.timeScale = 10;
            for (var scenario = 0; scenario < receivers.Length; scenario++)
            foreach (var team in new[] { Team.Red, Team.Navy })
            {
                var sign = team == Team.Red ? 1f : -1f;
                var other = team == Team.Red ? Team.Navy : Team.Red;
                // Keep all bindings, isolate only the fixture's physical participants.
                foreach (var avatar in match.GetComponentsInChildren<MNG_PlayerAvatar>(true)) avatar.gameObject.SetActive(true);
                match.ResetMatch();
                var sender = match.GetPlayerAvatar(team,3); var receiver = match.GetPlayerAvatar(team,1);
                var opponent = match.GetPlayerAvatar(other,3);
                foreach (var avatar in match.GetComponentsInChildren<MNG_PlayerAvatar>(true))
                {
                    avatar.GetComponent<MNG_PlayerSkillExecutor>().enabled = false;
                    if (avatar != sender && avatar != receiver && !(scenario == 4 || scenario == 5 || scenario == 6) ) avatar.gameObject.SetActive(false);
                    else if (avatar != sender && avatar != receiver && avatar != opponent) avatar.gameObject.SetActive(false);
                }
                var receiverPoint = sign * receivers[scenario];
                Pose(receiver, receiverPoint, new Vector2(sign,0));
                if (scenario == 3) receiver.Body.linearVelocity = new Vector3(0,0,sign);
                // Align the sender to the actual lead target, except the replacement fixture.
                var target = receiverPoint + new Vector2(sign * MNG_TeamPlanner.PassTargetForwardLead,0);
                target.y = Mathf.MoveTowards(target.y,0,MNG_TeamPlanner.PassTargetCenterBias);
                var forward = scenario == 7 ? -target.normalized : target.normalized;
                if (scenario>=9) forward=new Vector2(-target.normalized.y,target.normalized.x);
                Pose(sender, -forward * MNG_KickPlate.DribbleAnchorForward, forward);
                if (opponent.gameObject.activeSelf) Pose(opponent, sign * new Vector2(scenario == 4 ? 5 : 7,0), new Vector2(-sign,0));
                ball.isKinematic=false; ball.position=new Vector3(0,ball.position.y,0);
                ball.linearVelocity=Vector3.zero; ball.angularVelocity=Vector3.zero; Physics.SyncTransforms();
                for (var tick=0; tick<20; tick++)
                {
                    sender.GetComponent<MNG_PlayerMotor>().ApplyDesiredVelocity(forward,forward,MNG_InputOwner.Manager,sender.Ownership.Revision,Time.fixedDeltaTime);
                    yield return new WaitForFixedUpdate();
                    if (ballControl.Carrier.IsValid && ballControl.Carrier.Team==team && ballControl.Carrier.Slot==3) break;
                }
                yield return new WaitForFixedUpdate();
                sender.GetComponent<MNG_PlayerSkillExecutor>().enabled=true;
                receiver.GetComponent<MNG_PlayerSkillExecutor>().enabled=true;
                var selected=1;
                var accepted=IssueTechnicalPass(match,team);
                var passBefore=tracker.GetPassStrikeCount(team);
                var receptionsBefore=tracker.GetIntendedReceptionCount(team);
                var replacedDuringFlight=false;
                for (var tick=0; tick<200; tick++)
                {
                    if (scenario==10 && !replacedDuringFlight && tracker.GetPassStrikeCount(team)>passBefore)
                    {
                        match.AcceptPolicyCommand(team,MNG_Command.ProtectBack);
                        replacedDuringFlight=true;
                    }
                    if (scenario==8 && tick>0 && tick%25==0 && ballControl.Carrier.IsValid && ballControl.Carrier.Team==team && ballControl.Carrier.Slot==3)
                        IssueTechnicalPass(match,team);
                    if (scenario==7 && tick==2) match.AcceptPolicyCommand(team,MNG_Command.ProtectBack);
                    if (scenario==6 && tick==2)
                    {
                        ballControl.ResetLedger();
                        ball.position=opponent.KickPlate.DribblePosition;
                        ball.linearVelocity=Vector3.zero; Physics.SyncTransforms();
                    }
                    yield return new WaitForFixedUpdate();
                    if (team == Team.Red && (scenario < 3 || scenario == 9) && tick % 5 == 0 && tick < 80)
                        Debug.Log($"PASS STEP fixture={scenario} tick={tick} ball={ball.position:F2} velocity={ball.linearVelocity:F2} receiver={receiver.Body.position:F2} forward={receiver.transform.forward:F2} plate={receiver.KickPlate.DribblePosition:F2} task={receiver.GetComponent<MNG_PlayerSkillExecutor>().CurrentTask.Skill} target={receiver.GetComponent<MNG_PlayerSkillExecutor>().CurrentTask.Target:F2} carrier={ballControl.Carrier.IsValid}/{ballControl.Carrier.Team}/{ballControl.Carrier.Slot}");
                    if (team == Team.Red && scenario == 9 && tick % 5 == 0 && tick < 80)
                        Debug.Log($"PASS TURN tick={tick} sender={sender.Body.position:F2} forward={sender.transform.forward:F2} plate={sender.KickPlate.DribblePosition:F2} task={sender.GetComponent<MNG_PlayerSkillExecutor>().CurrentTask.Skill} magnet={ballControl.DribbleCorrectionAppliedLastStep}");
                }
                var strikes=tracker.GetPassStrikeCount(team)-passBefore;
                var receptions=tracker.GetIntendedReceptionCount(team)-receptionsBefore;
                var row=$"POSTR6 PASS fixture={scenario} team={team} accepted={accepted} selected={selected} strikes={strikes} receptions={receptions} ball={ball.position:F3} receiver={receiver.Body.position:F3} task={receiver.GetComponent<MNG_PlayerSkillExecutor>().CurrentTask.Skill}";
                Debug.Log(row);
                if (!accepted || selected!=1 || ((scenario<=4 || scenario==8 || scenario==9) && (strikes!=1 || receptions!=1)) || (scenario>=5 && scenario<=7 && receptions!=0) || ((scenario==6 || scenario==7) && strikes!=0)) failures.Add(row);
                if (scenario==7 && sender.GetComponent<MNG_PlayerSkillExecutor>().CurrentTask.Skill==MNG_PlayerSkill.AimPass) failures.Add("Replacement retained pass");
                if (scenario==10 && (!replacedDuringFlight || receiver.GetComponent<MNG_PlayerSkillExecutor>().CurrentTask.Skill==MNG_PlayerSkill.ReceivePass)) failures.Add("In-flight reception resisted replacement");
            }
            Assert.That(failures, Is.Empty, string.Join("\n",failures));
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator WallTrappedTeammatesSeparateOnBothTeams()
        {
#if UNITY_EDITOR
            Academy.Instance.AutomaticSteppingEnabled = false;
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/_Soccer/Manager/Curriculum/MS_V2/MNG_MS2V2_Train.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var match = Object.FindFirstObjectByType<MNG_MatchController>();
            foreach (var rule in match.GetComponentsInChildren<MNG_RuleBasedManager>(true)) rule.enabled=false;
            foreach (var manager in match.GetComponentsInChildren<MNG_ManagerAgent>(true))
            { manager.GetComponent<DecisionRequester>().enabled=false; manager.enabled=false; }
            match.GetComponent<MNG_MSController>().enabled=false;
            Time.timeScale=10;
            foreach (var team in new[]{Team.Red,Team.Navy})
            {
                var sign=team==Team.Red ? 1f : -1f;
                foreach (var a in match.GetComponentsInChildren<MNG_PlayerAvatar>(true)) a.gameObject.SetActive(true);
                match.ResetMatch();
                var first=match.GetPlayerAvatar(team,1); var second=match.GetPlayerAvatar(team,2);
                foreach (var a in match.GetComponentsInChildren<MNG_PlayerAvatar>(true))
                    if(a!=first && a!=second) a.gameObject.SetActive(false);
                Pose(first, sign*new Vector2(59.7f,23.4f), sign*Vector2.up);
                Pose(second, sign*new Vector2(61.1f,23.8f), sign*Vector2.up);
                Physics.SyncTransforms();
                foreach (var a in new[]{first,second}) a.GetComponent<MNG_PlayerSkillExecutor>().RequestTaskV2(new MNG_PlayerTask {
                    Skill=MNG_PlayerSkill.Press, Target=sign*new Vector2(61,38), ReceiverSlot=-1,
                    Revision=12000, TaskId=12000+a.Slot, ParentCommandId=3000, ExpirySeconds=match.EpisodeElapsedSeconds+5 });
                for(var tick=0; tick<100; tick++) yield return new WaitForFixedUpdate();
                var distance=Vector3.Distance(first.Body.position,second.Body.position);
                Debug.Log($"POSTR6 WALL team={team} separation={distance:F3} first={first.Body.position:F3} second={second.Body.position:F3}");
                Assert.That(distance,Is.GreaterThan(3.5f));
                Assert.That(Mathf.Abs(second.Body.position.x),Is.LessThan(62));
            }
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator GoalPauseRejectionIsNotReportedAsPossessionLoss()
        {
#if UNITY_EDITOR
            Academy.Instance.AutomaticSteppingEnabled=false;
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/_Soccer/Manager/Curriculum/MS_V2/MNG_MS2V2_Train.unity",new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var match=Object.FindFirstObjectByType<MNG_MatchController>();
            match.ResetMatch();
            match.AcceptPolicyCommand(Team.Red,MNG_Command.Balanced);
            var lost=match.GetTaskResultCount(MNG_TaskResultKind.CancelledPossessionLost);
            var stopped=match.GetTaskResultCount(MNG_TaskResultKind.CancelledMatchState);
            match.GoalTouched(Team.Red);
            match.AcceptPolicyCommand(Team.Red,MNG_Command.Balanced);
            Assert.That(match.GetTaskResultCount(MNG_TaskResultKind.CancelledPossessionLost),Is.EqualTo(lost));
            Assert.That(match.GetTaskResultCount(MNG_TaskResultKind.CancelledMatchState),Is.GreaterThan(stopped));
#else
            yield break;
#endif
        }

        [System.Serializable] sealed class ReplayFrame
        {
            public int tick, carrierTeam, carrierSlot;
            public Vector3 ball, ballVelocity;
            public Vector3[] positions = new Vector3[8], velocities = new Vector3[8], forwards = new Vector3[8];
            public Vector2[] desired = new Vector2[8], targets = new Vector2[8];
            public int[] skills = new int[8], contacts = new int[8];
            public int[] contactOrder;
        }
        [System.Serializable] sealed class ReplayRun
        {
            public int seed, variant;
            public bool mirror, reverseRegistration;
            public List<ReplayFrame> frames = new List<ReplayFrame>();
        }

        [UnityTest]
        [Explicit("Bounded diagnostic export; specify a new -mngReplayOutput directory for each run")]
        public IEnumerator FixedRecoverFirstContactReplaySeparatesMirrorAndRegistration()
        {
#if UNITY_EDITOR
            if(System.Array.IndexOf(System.Environment.GetCommandLineArgs(),"-mngReplayOutput")<0)
                Assert.Ignore("Explicit replay output required; excluded from normal regression runs");
            Academy.Instance.AutomaticSteppingEnabled = false;
            var args = System.Environment.GetCommandLineArgs();
            var outputIndex = System.Array.IndexOf(args,"-mngReplayOutput");
            var output = outputIndex>=0 ? args[outputIndex+1] : "Logs/MNG-Rebuild/MS3-D3D5-validation-20260923/replay";
            var variantsIndex=System.Array.IndexOf(args,"-mngReplayVariants");
            var variants=variantsIndex>=0?args[variantsIndex+1].Split(',').Select(int.Parse).ToArray():Enumerable.Range(0,9).ToArray();
            System.IO.Directory.CreateDirectory(output);
            foreach (var seed in new[] { 592311, 592312 })
            foreach (var variant in variants)
            {
                yield return EditorSceneManager.LoadSceneAsyncInPlayMode(
                    "Assets/_Soccer/Manager/Curriculum/MS_V2/MNG_MS2V2_Train.unity", new LoadSceneParameters(LoadSceneMode.Single));
                yield return null;
                var match = Object.FindFirstObjectByType<MNG_MatchController>();
                foreach (var rule in match.GetComponentsInChildren<MNG_RuleBasedManager>(true)) rule.enabled = false;
                foreach (var manager in match.GetComponentsInChildren<MNG_ManagerAgent>(true))
                { manager.GetComponent<DecisionRequester>().enabled = false; manager.enabled = false; }
                match.GetComponent<MNG_MSController>().enabled = false;
                var run = new ReplayRun { seed=seed, variant=variant, mirror=variant==2 || variant==4 || variant==7 || variant==8 || variant==10,
                    reverseRegistration=variant==3 || variant==4 || variant==6 || variant==8 };
                var avatars = new MNG_PlayerAvatar[8];
                for (var i=0;i<8;i++) { avatars[i]=match.GetPlayerAvatar((Team)(i/4),i%4); avatars[i].gameObject.SetActive(false); }
                if (variant>=5 && variant<=8)
                {
                    var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
                    var positions=(Vector3[])typeof(MNG_MatchController).GetField("m_PlayerStartingPositions",flags).GetValue(match);
                    var rotations=(Quaternion[])typeof(MNG_MatchController).GetField("m_PlayerStartingRotations",flags).GetValue(match);
                    for (var step=0;step<8;step++)
                    {
                        var i=run.reverseRegistration?7-step:step;
                        var original=avatars[i];
                        var clone=Object.Instantiate(original.gameObject,original.transform.parent);
                        avatars[i]=clone.GetComponent<MNG_PlayerAvatar>();
                        clone.transform.SetPositionAndRotation(positions[i],rotations[i]);
                        avatars[i].Body.position=positions[i]; avatars[i].Body.rotation=rotations[i];
                        Object.DestroyImmediate(original.gameObject);
                    }
                    match.Configure(match.GetComponent<SoccerArenaGeometry>(),match.GetComponentInChildren<MNG_BallControl>().GetComponent<Rigidbody>(),avatars);
                }
                for (var i=0;i<8;i++) avatars[run.reverseRegistration ? 7-i : i].gameObject.SetActive(true);
                match.ConfigureSpawnSeedOffset(seed);
                match.ConfigureEvaluationMirror(run.mirror);
                typeof(MNG_MatchController).GetField("m_EpisodeId",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).SetValue(match,0L);
                match.ResetMatch();
                Physics.SyncTransforms();
                var control=match.GetComponentInChildren<MNG_BallControl>();
                var diagnosticFlags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
                typeof(MNG_BallControl).GetField("SuppressContactResponseForDiagnostics",diagnosticFlags).SetValue(control,variant>=9);
                var contactOrder=(List<int>)typeof(MNG_BallControl).GetField("ContactOrderForDiagnostics",diagnosticFlags).GetValue(control);
                var body=control.GetComponent<Rigidbody>();
                Debug.Log($"MS3 SOLVER actual ball={body.solverIterations}/{body.solverVelocityIterations} player={avatars[3].Body.solverIterations}/{avatars[3].Body.solverVelocityIterations}");
                var solverIndex=System.Array.IndexOf(args,"-mngSolverCycles");
                if(solverIndex>=0)
                {
                    var cycles=args[solverIndex+1].Split(',').Select(int.Parse).ToArray();
                    foreach(var rb in match.GetComponentsInChildren<Rigidbody>(true))
                    {rb.solverIterations=cycles[0];rb.solverVelocityIterations=cycles[1];}
                }
                Time.timeScale=10;
                for (var tick=0;tick<=250;tick++)
                {
                    if(tick%25==0)
                    {
                        match.AcceptPolicyCommand(Team.Red,MNG_Command.ActiveRecover);
                        match.AcceptPolicyCommand(Team.Navy,MNG_Command.ActiveRecover);
                    }
                    var frame=new ReplayFrame { tick=tick,ball=body.position,ballVelocity=body.linearVelocity,
                        carrierTeam=control.Carrier.IsValid?(int)control.Carrier.Team:-1,carrierSlot=control.Carrier.Slot,
                        contactOrder=contactOrder.ToArray() };
                    for(var i=0;i<8;i++)
                    {
                        var avatar=avatars[i]; var task=avatar.GetComponent<MNG_PlayerSkillExecutor>().CurrentTask;
                        frame.positions[i]=avatar.Body.position; frame.velocities[i]=avatar.Body.linearVelocity;
                        frame.forwards[i]=avatar.transform.forward; frame.desired[i]=avatar.GetComponent<MNG_PlayerMotor>().LastDesiredVelocity;
                        frame.targets[i]=task.Target; frame.skills[i]=(int)task.Skill;
                        frame.contacts[i]=control.GetPhysicalContactCount(avatar.Team,avatar.Slot);
                    }
                    run.frames.Add(frame);
                    yield return new WaitForFixedUpdate();
                }
                var path=$"{output}/seed-{seed}-variant-{variant}.json";
                Assert.That(System.IO.File.Exists(path),Is.False,"Preserve previous replay evidence");
                System.IO.File.WriteAllText(path,JsonUtility.ToJson(run));
                Assert.That(run.frames.Count,Is.EqualTo(251));
                Debug.Log($"MS3 REPLAY seed={seed} variant={variant} contacts={run.frames[250].contacts.Sum()}");
            }
#else
            yield break;
#endif
        }

        [UnityTest]
        [Explicit("Isolated native-physics replay from preserved first-contact states")]
        public IEnumerator FirstContactWithoutGameplayCallbacks()
        {
#if UNITY_EDITOR
            if(System.Array.IndexOf(System.Environment.GetCommandLineArgs(),"-mngNativeDrive")<0
                && System.Array.IndexOf(System.Environment.GetCommandLineArgs(),"-mngNativeContact")<0)
                Assert.Ignore("Explicit native contact diagnostic argument required");
            Academy.Instance.AutomaticSteppingEnabled=false;
            var originalMode=Physics.simulationMode;
            var driven=System.Array.IndexOf(System.Environment.GetCommandLineArgs(),"-mngNativeDrive")>=0;
            var solverSweep=System.Array.IndexOf(System.Environment.GetCommandLineArgs(),"-mngSolverSweep")>=0;
            var settings=solverSweep?new[]{new Vector2Int(6,1),new Vector2Int(12,4),new Vector2Int(24,8),new Vector2Int(48,16)}:new[]{new Vector2Int(6,1)};
            try
            {
                foreach(var solver in settings)
                for(var variant=0;variant<4;variant++)
                {
                    yield return EditorSceneManager.LoadSceneAsyncInPlayMode(
                        "Assets/_Soccer/Manager/Curriculum/MS_V2/MNG_MS2V2_Train.unity",new LoadSceneParameters(LoadSceneMode.Single));
                    yield return null;
                    var match=Object.FindFirstObjectByType<MNG_MatchController>();
                    var control=match.GetComponentInChildren<MNG_BallControl>();
                    var ball=control.GetComponent<Rigidbody>();
                    foreach(var behaviour in match.GetComponentsInChildren<MonoBehaviour>(true)) behaviour.enabled=false;
                    // Disabled MonoBehaviours can still receive collision messages.
                    Object.DestroyImmediate(control);
                    foreach(var rb in match.GetComponentsInChildren<Rigidbody>(true))
                    {rb.solverIterations=solver.x;rb.solverVelocityIterations=solver.y;}
                    Physics.simulationMode=SimulationMode.Script;
                    var sourceFrames=JsonUtility.FromJson<ReplayRun>(System.IO.File.ReadAllText(
                        "Logs/MNG-Rebuild/MS3-D3D5-validation-20260923/replay-creation/seed-592312-variant-0.json")).frames;
                    var source=sourceFrames[143];
                    var mirror=variant%2==1; var sign=mirror?-1f:1f;
                    var avatars=new MNG_PlayerAvatar[8];
                    for(var i=0;i<8;i++)
                    {
                        var j=mirror?(i+4)%8:i;
                        avatars[j]=match.GetPlayerAvatar((Team)(j/4),j%4);
                        var avatar=avatars[j];
                        var p=source.positions[i];p.x*=sign;p.z*=sign;
                        var f=source.forwards[i];f.x*=sign;f.z*=sign;
                        avatar.Body.isKinematic=true;
                        avatar.transform.SetPositionAndRotation(p,Quaternion.LookRotation(f,Vector3.up));
                        avatar.Body.position=p;avatar.Body.rotation=avatar.transform.rotation;
                        avatar.Body.isKinematic=false;
                        var v=source.velocities[i];v.x*=sign;v.z*=sign;
                        avatar.Body.linearVelocity=v;avatar.Body.angularVelocity=Vector3.zero;
                    }
                    var bp=source.ball;bp.x=sign*(bp.x+(variant>=2?.00001f:0f));bp.z*=sign;
                    ball.isKinematic=true;ball.position=bp;ball.isKinematic=false;
                    var bv=source.ballVelocity;bv.x*=sign;bv.z*=sign;
                    ball.linearVelocity=bv;ball.angularVelocity=Vector3.zero;Physics.SyncTransforms();
                    var run=new ReplayRun { seed=592312,variant=variant,mirror=mirror };
                    for(var tick=0;tick<=10;tick++)
                    {
                        var frame=new ReplayFrame {tick=tick,ball=ball.position,ballVelocity=ball.linearVelocity};
                        for(var i=0;i<8;i++) {frame.positions[i]=avatars[i].Body.position;frame.velocities[i]=avatars[i].Body.linearVelocity;}
                        run.frames.Add(frame);
                        if(driven)
                        {
                            for(var i=0;i<8;i++)
                            {
                                var j=mirror?(i+4)%8:i;
                                var desired=sourceFrames[144+tick].desired[i]*sign;
                                var forward=sourceFrames[144+tick].forwards[i];
                                var avatar=avatars[j];
                                avatar.GetComponent<MNG_PlayerMotor>().ApplyDesiredVelocity(desired,
                                    new Vector2(forward.x,forward.z)*sign,MNG_InputOwner.Manager,avatar.Ownership.Revision,Time.fixedDeltaTime);
                            }
                        }
                        Physics.Simulate(Time.fixedDeltaTime);
                    }
                    var prefix=driven?"native-driven":"native-contact";
                    if(solverSweep)prefix+=$"-solver-{solver.x}-{solver.y}";
                    var path=$"Logs/MNG-Rebuild/MS3-D3D5-validation-20260923/{prefix}-{variant}.json";
                    Assert.That(System.IO.File.Exists(path),Is.False);
                    System.IO.File.WriteAllText(path,JsonUtility.ToJson(run));
                    Physics.simulationMode=originalMode;
                }
            }
            finally {Physics.simulationMode=originalMode;}
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator ResetRestoresBodyAndObservationHeadingImmediately()
        {
#if UNITY_EDITOR
            Academy.Instance.AutomaticSteppingEnabled=false;
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/_Soccer/Manager/Curriculum/MS_V2/MNG_MS2V2_Train.unity",new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var match=Object.FindFirstObjectByType<MNG_MatchController>();
            var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
            var initial=(Quaternion[])typeof(MNG_MatchController).GetField("m_PlayerStartingRotations",flags).GetValue(match);
            foreach(var avatar in match.GetComponentsInChildren<MNG_PlayerAvatar>())
            {
                avatar.transform.rotation=Quaternion.Euler(0,37,0);
                avatar.Body.rotation=avatar.transform.rotation;
            }
            Physics.SyncTransforms();
            match.ResetMatch();
            for(var i=0;i<8;i++)
            {
                var avatar=match.GetPlayerAvatar((Team)(i/4),i%4);
                var expected=initial[i]*Vector3.forward;
                var actual=avatar.CaptureState().Forward;
                Assert.That(Vector2.Distance(actual,new Vector2(expected.x,expected.z)),Is.LessThan(.0001f),$"slot {i} observation");
                Assert.That(Quaternion.Angle(avatar.Body.rotation,initial[i]),Is.LessThan(.01f),$"slot {i} body");
            }
#else
            yield break;
#endif
        }

        static void Pose(MNG_PlayerAvatar avatar, Vector2 position, Vector2 forward)
        {
            var p=new Vector3(position.x,.52f,position.y);
            var q=Quaternion.LookRotation(new Vector3(forward.x,0,forward.y),Vector3.up);
            avatar.transform.SetPositionAndRotation(p,q); avatar.Body.position=p; avatar.Body.rotation=q;
            avatar.Body.linearVelocity=Vector3.zero; avatar.Body.angularVelocity=Vector3.zero;
        }
    }
}
