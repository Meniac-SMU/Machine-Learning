using System;
using UnityEngine;

namespace MachineLearning.Soccer.Manager5v5.Demo
{
    public sealed class FiveDemoKickoffLayout : MonoBehaviour
    {
        FiveMatch match;
        readonly System.Random random=new System.Random(Guid.NewGuid().GetHashCode());
        public void Initialize(FiveMatch owner){match=owner;match.RoundReset+=Apply;}
        void OnDestroy(){if(match!=null)match.RoundReset-=Apply;}
        void Apply()
        {
            foreach(var player in match.players)
            {
                if(player.IsKeeper)continue;
                const float radius=2.5f;
                for(int attempt=0;attempt<64;attempt++)
                {
                    double angle=random.NextDouble()*Math.PI*2;
                    float distance=radius*Mathf.Sqrt((float)random.NextDouble());
                    var position=player.spawn+new Vector3((float)Math.Cos(angle)*distance,0,(float)Math.Sin(angle)*distance);
                    if(Mathf.Abs(position.x)>match.geometry.HalfLength-1.5f||Mathf.Abs(position.z)>match.geometry.HalfWidth-1.5f
                        ||Mathf.Sign(position.x)!=Mathf.Sign(player.spawn.x))continue;
                    bool clear=true;
                    foreach(var other in match.players)
                        if(other!=player&&Vector3.Distance(position,other.Body.position)<3)clear=false;
                    if(!clear)continue;
                    player.Body.position=position;break;
                }
                player.SetTarget(player.Position,player.Position+(player.team==Team.Red?Vector2.right:Vector2.left));
            }
        }
    }
}
