using System;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace MachineLearning.Soccer.Manager5v5
{
    /// <summary>Short counterfactual attacks in an isolated physics scene, using visible state only.</summary>
    internal sealed class FiveRuleForecast : IDisposable
    {
        readonly Scene scene;
        readonly PhysicsScene physics;
        readonly FiveMatch preview;
        public FiveRuleForecast(FiveMatch source)
        {
            scene=SceneManager.CreateScene("FiveRuleForecast-"+Guid.NewGuid().ToString("N"),new CreateSceneParameters(LocalPhysicsMode.Physics3D));
            physics=scene.GetPhysicsScene();
            var root=new GameObject("Rule forecast only");root.SetActive(false);SceneManager.MoveGameObjectToScene(root,scene);
            preview=root.AddComponent<FiveMatch>();preview.automaticTick=false;preview.ruleManagers=new[]{false,false};
            preview.isRulePreview=true;preview.rulePreviewPhysics=physics;preview.profile=source.profile;preview.geometry=source.geometry;
            preview.ballSpawn=source.ballSpawn;preview.ballRadius=source.ballRadius;preview.baselineBallScale=source.baselineBallScale;
            preview.improvedPassPreparationEnabled=source.improvedPassPreparationEnabled;
            preview.expandedCutbackEnabled=source.expandedCutbackEnabled;
            preview.adaptiveGoalBlockEnabled=source.adaptiveGoalBlockEnabled;
            preview.players=new FivePlayer[10];
            try
            {
            foreach(var original in source.players)
            {
                var copy=Object.Instantiate(original.gameObject,root.transform,true).GetComponent<FivePlayer>();
                copy.transform.localScale=original.transform.lossyScale;preview.players[copy.Index]=copy;
            }
            preview.ball=Object.Instantiate(source.ball.gameObject,root.transform,true).GetComponent<Rigidbody>();
            preview.ball.transform.localScale=source.ball.transform.lossyScale;
            preview.ball.GetComponent<FiveGoalContact>().match=preview;
            foreach(var original in source.GetComponentsInChildren<Collider>().Where(c=>c.attachedRigidbody==null))
            {
                var go=new GameObject(original.name);go.layer=original.gameObject.layer;go.transform.SetParent(root.transform,false);
                go.transform.SetPositionAndRotation(original.transform.position,original.transform.rotation);go.transform.localScale=original.transform.lossyScale;
                Collider copy;
                if(original is BoxCollider box){var c=go.AddComponent<BoxCollider>();c.center=box.center;c.size=box.size;copy=c;}
                else if(original is SphereCollider sphere){var c=go.AddComponent<SphereCollider>();c.center=sphere.center;c.radius=sphere.radius;copy=c;}
                else if(original is CapsuleCollider capsule){var c=go.AddComponent<CapsuleCollider>();c.center=capsule.center;c.radius=capsule.radius;c.height=capsule.height;c.direction=capsule.direction;copy=c;}
                else if(original is MeshCollider mesh){var c=go.AddComponent<MeshCollider>();c.sharedMesh=mesh.sharedMesh;c.convex=mesh.convex;copy=c;}
                else if(original is TerrainCollider terrain){var c=go.AddComponent<TerrainCollider>();c.terrainData=terrain.terrainData;copy=c;}
                else throw new InvalidOperationException("Unsupported forecast collider: "+original.GetType().Name);
                copy.sharedMaterial=original.sharedMaterial;copy.contactOffset=original.contactOffset;copy.isTrigger=original.isTrigger;copy.enabled=original.enabled;
                var goal=original.GetComponent<SoccerGoalSurface>();
                if(goal!=null)go.AddComponent<SoccerGoalSurface>().Configure(goal.DefendingTeam,goal.Scores);
            }
            foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))renderer.enabled=false;
            root.SetActive(true);preview.ResetMatch();
            }
            catch { Dispose();throw; }
        }
        public FiveRuleDecision Choose(FiveMatch source,Team team,FiveRuleDecision prior)
        {
            var scores=Enumerable.Repeat(-10000f,8).ToArray();
            var own=source.Carrier.team==team;
            var candidates=own?new[]{0,1,2,3}:new[]{0,1,2,3,4,5,6,7};
            var hypotheses=own?new[]{6,3}:new[]{0,3};
            var scenarioScores=new float[candidates.Length*hypotheses.Length];var count=0;
            var clock=System.Diagnostics.Stopwatch.StartNew();
            // Opponent orders are hypotheses, not the actual hidden command or its reservations.
            foreach(var action in candidates)
            {
                var command=(FiveCommand)action;
                var outcomes=new float[2];var scenario=0;
                foreach(var opposition in hypotheses)
                {
                    preview.LoadRulePreview(source);
                    preview.SelectCommand(team,command,"forecast-candidate",true);
                    preview.SelectCommand(FiveMatch.Other(team),(FiveCommand)opposition,"forecast-hypothesis",true);
                    for(var step=0;step<150&&preview.State==FiveMatchState.Playing;step++)
                    {preview.Tick(.02f);physics.Simulate(.02f);}
                    var value=Value(preview,team);
                    if(!float.IsFinite(value))throw new InvalidOperationException("Non-finite forecast value.");
                    outcomes[scenario++]=value;scenarioScores[count++]=value;
                }
                scores[(int)command]=.7f*outcomes.Min()+.3f*outcomes.Average()+prior.scores[(int)command]*.08f;
            }
            var best=0;for(var i=1;i<8;i++)if(scores[i]>scores[best])best=i;
            prior.action=best;prior.scores=scores;prior.reason="visible-state-three-second-tactic-forecast";
            prior.forecastActions=candidates;prior.forecastOpponents=hypotheses;
            prior.forecastOutcomes=scenarioScores;prior.forecastMilliseconds=(float)clock.Elapsed.TotalMilliseconds;
            return prior;
        }
        static float Value(FiveMatch match,Team team)
        {
            var sign=FiveMatch.AttackSign(team);var score=(match.RedScore-match.NavyScore)*sign;
            if(score!=0)return score*250;
            var b=match.BallPosition*sign;var forward=b.x/match.geometry.HalfLength;
            var ours=match.players.Where(p=>p.team==team&&!p.IsKeeper).ToArray();
            var others=match.players.Where(p=>p.team!=team&&!p.IsKeeper).ToArray();
            float Arrival(FivePlayer p)=>Vector2.Distance(p.Position,match.BallPosition)/Mathf.Max(1,match.profile.maxSpeed*p.SpeedFactor);
            if(match.Carrier==null)
                return forward*12+Mathf.Clamp(others.Min(Arrival)-ours.Min(Arrival),-2,2)*6;
            var own=match.Carrier.team==team;
            var point=match.Carrier.Position*FiveMatch.AttackSign(match.Carrier.team);
            var danger=Mathf.Clamp01((point.x-match.geometry.HalfLength+35)/30)*(Mathf.Abs(point.y)<19?1:.3f);
            var shotOpen=match.LaneOpen(match.BallPosition,new Vector2(FiveMatch.AttackSign(match.Carrier.team)*match.geometry.HalfLength,0),match.Carrier.team,.1f);
            var control=14+danger*(shotOpen?18:6);
            return forward*14+(own?control:-control);
        }
        public void Dispose()
        {
            if(preview!=null)preview.gameObject.SetActive(false);
            if(scene.IsValid()&&scene.isLoaded)SceneManager.UnloadSceneAsync(scene);
        }
    }
    public sealed partial class FiveMatch
    {
        internal bool isRulePreview;
        internal PhysicsScene rulePreviewPhysics;
        FiveRuleForecast ruleForecast;
        RaycastHit[] previewHits=new RaycastHit[128];
        bool applicationQuitting;
        RaycastHit[] RuleSceneRaycasts(Vector3 origin,Vector3 direction,float distance,int mask,QueryTriggerInteraction triggers)
        {
            if(!isRulePreview)return Physics.RaycastAll(origin,direction,distance,mask,triggers);
            var count=rulePreviewPhysics.Raycast(origin,direction,previewHits,distance,mask,triggers);
            while(count==previewHits.Length)
            {Array.Resize(ref previewHits,previewHits.Length*2);count=rulePreviewPhysics.Raycast(origin,direction,previewHits,distance,mask,triggers);}
            var result=new RaycastHit[count];Array.Copy(previewHits,result,count);return result;
        }
        internal void LoadRulePreview(FiveMatch source)
        {
            ResetMatch();
            contestReliefEnabled=source.contestReliefEnabled;
            for(var index=0;index<10;index++)
            {
                var p=players[index];var original=source.players[index];
                p.Body.position=original.Body.position;p.Body.rotation=original.Body.rotation;p.Body.linearVelocity=original.Body.linearVelocity;p.Body.angularVelocity=original.Body.angularVelocity;
                if(!p.IsKeeper)p.Stamina.SetForScenario(original.Stamina.Value);
                p.SetTarget(p.Position,p.Position+p.Forward);p.Dribbling=index==source.Owner;
            }
            ball.position=source.ball.position;ball.rotation=source.ball.rotation;ball.linearVelocity=source.ball.linearVelocity;ball.angularVelocity=source.ball.angularVelocity;
            // Ownership and elapsed control age are observable. Hidden task/cooldown/target state is not copied.
            Owner=source.Owner;OwnerSince=-Mathf.Min(1,source.Elapsed-source.OwnerSince);
            Physics.SyncTransforms();
        }
        FiveRuleDecision ForecastRule(Team team,FiveObservation seen,FiveRuleDecision prior)
        {
            if((!seen.ownPossession&&!seen.enemyPossession)||seen.ownPassPreparing||seen.ownPassInFlight)return prior;
            if(seen.stallSeconds>12&&seen.ownCommand==FiveCommand.AllRecover)return prior;
            ruleForecast=ruleForecast??new FiveRuleForecast(this);
            return ruleForecast.Choose(this,team,prior);
        }
        void OnApplicationQuit(){applicationQuitting=true;}
        void OnDestroy(){if(!applicationQuitting)ruleForecast?.Dispose();}
    }
}
