using System;
using Unity.InferenceEngine;
using UnityEngine;

namespace MachineLearning.Soccer.Manager5v5.Demo
{
    public sealed class FiveDemoPolicy : IDisposable
    {
        readonly Worker worker;
        readonly System.Random random;
        public FiveDemoPolicy(ModelAsset asset, int seed)
        { worker = new Worker(ModelLoader.Load(asset), BackendType.CPU); random = new System.Random(seed); }
        public float[] Probabilities(float[] values, float[] mask)
        {
            using var observation = new Tensor<float>(new TensorShape(1,676), values);
            using var actions = new Tensor<float>(new TensorShape(1,8), mask);
            worker.SetInput("obs_0", observation); worker.SetInput("action_masks", actions); worker.Schedule();
            using var output = (worker.PeekOutput() as Tensor<float>).ReadbackAndClone();
            return output.DownloadToArray();
        }
        public int Choose(float[] values, float[] mask)
        {
            var p = Probabilities(values, mask); double sum = 0;
            if (p.Length != 8) throw new InvalidOperationException("5v5 policy must return eight probabilities.");
            for(int i=0;i<8;i++)
            {
                if(!float.IsFinite(p[i]) || p[i]<0) throw new InvalidOperationException("Invalid policy probability.");
                p[i] *= mask[i]; sum += p[i];
            }
            if(sum<=0) throw new InvalidOperationException("Policy has no legal action.");
            var sample = random.NextDouble()*sum; int last = -1;
            for(int i=0;i<8;i++) { if(p[i]<=0)continue; last=i; sample-=p[i]; if(sample<0)return i; }
            return last;
        }
        public void Dispose() => worker.Dispose();
    }

    [DefaultExecutionOrder(-300)]
    public sealed class FiveDemoSession : MonoBehaviour
    {
        public FiveMatch match;
        public readonly FiveObservationHistory history = new FiveObservationHistory();
        readonly FiveDemoPolicy[] policies = new FiveDemoPolicy[2];
        public readonly int[] decisions = new int[2];
        public bool Ready { get; private set; }
        public bool automaticSteps = true;
        SimulationMode previous;
        FiveDemoMotion motion;
        public void Initialize(FiveMatch target, FiveDemoAssets assets, int red, int navy, int seconds)
        {
            match = target; match.automaticTick = false;
            match.continueBallAfterGoal = true;
            match.profile = Instantiate(match.profile); match.profile.duration = seconds;
            match.ruleManagers = new[]{red==0,navy==0};
            match.ruleVariants = new[]{FiveRuleVariant.Strong,FiveRuleVariant.Strong};
            match.improvedPassPreparationEnabled=match.expandedCutbackEnabled=match.adaptiveGoalBlockEnabled=true;
            match.keeperTurnFirstEnabled=true;
            var ids=new[]{red,navy};
            for(int i=0;i<2;i++) if(ids[i]>0) policies[i]=new FiveDemoPolicy(assets.models[ids[i]-1], UnityEngine.Random.Range(1,int.MaxValue));
            previous=Physics.simulationMode; Physics.simulationMode=SimulationMode.Script;
            gameObject.AddComponent<FiveDemoKickoffLayout>().Initialize(match);
            match.ResetMatch(); history.Reset(match); Ready=true;
            motion=gameObject.AddComponent<FiveDemoMotion>();motion.Initialize(this);
        }
        void FixedUpdate()
        {
            if(!Ready||!automaticSteps||match.Paused)return;
            motion.BeginStep();Step(Time.fixedDeltaTime);motion.EndStep(Time.fixedDeltaTime);
        }
        public void Step(float seconds)
        {
            if(match.Paused||match.State==FiveMatchState.Finished)return;
            history.Record(match);
            for(int i=0;i<2;i++)
            {
                if(policies[i]==null || !FiveLearningContract.CanDecide(match,(Team)i))continue;
                var mask=new float[8];
                for(int action=0;action<8;action++) mask[action]=match.CommandBlockedSelections((Team)i,(FiveCommand)action)==0?1:0;
                var selected=policies[i].Choose(history.Capture(match,(Team)i).values,mask);
                if(!FiveLearningContract.Apply(match,(Team)i,selected))throw new InvalidOperationException("Demo decision rejected.");
                decisions[i]++;
            }
            match.SimulatePhysicsStep(seconds); history.Record(match);
        }
        void OnDisable()
        {
            if(!Ready)return;
            Ready=false; Physics.simulationMode=previous;
            foreach(var policy in policies)policy?.Dispose();
            Destroy(match.profile);
        }
    }
}
