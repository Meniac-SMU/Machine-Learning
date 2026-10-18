using System;
using MachineLearning.Soccer;
using UnityEngine;

namespace MachineLearning.Soccer.Manager
{
    public sealed partial class MNG_MatchController
    {
        // Training-only start-state curriculum. No in-play teleport or policy override.
        void ApplyPSCurriculum()
        {
            var args=Environment.GetCommandLineArgs();var index=Array.IndexOf(args,"-mngPSCurriculum");
            if(index<0 || index+1>=args.Length || !MNG_PSPassing.Enabled)return;
            var fraction=float.Parse(args[index+1],System.Globalization.CultureInfo.InvariantCulture);
            var rng=new System.Random(CalculateSpawnSeed(m_EpisodeId,m_KickoffIndex,m_SpawnSeedOffset)+7919);
            if(rng.NextDouble()>=fraction)return;
            var team=(Team)rng.Next(2);var sign=team==Team.Red?1f:-1f;
            var kind=rng.Next(3);var lateral=rng.Next(2)==0?1f:-1f;
            Vector2[] own,other;
            if(kind==0)
            {
                own=new[]{new Vector2(-45,0),new Vector2(-17,-12),new Vector2(-10,17),new Vector2(-27,6)};
                other=new[]{new Vector2(48,0),new Vector2(-19,6),new Vector2(-17,-3),new Vector2(-10,22)};
            }
            else if(kind==1)
            {
                own=new[]{new Vector2(-48,0),new Vector2(-12,-14),new Vector2(6,17),new Vector2(-7,5)};
                other=new[]{new Vector2(48,0),new Vector2(0,5),new Vector2(3,-3),new Vector2(10,23)};
            }
            else
            {
                own=new[]{new Vector2(-48,0),new Vector2(27,-7),new Vector2(22,24),new Vector2(9,9)};
                other=new[]{new Vector2(50,0),new Vector2(16,9),new Vector2(23,5),new Vector2(34,20)};
            }
            for(var t=0;t<2;t++)for(var slot=0;slot<4;slot++)
            {
                var points=(Team)t==team?own:other;var point=points[slot];
                point+=new Vector2((float)rng.NextDouble()-.5f,(float)rng.NextDouble()-.5f)*2;
                var avatar=GetPlayerAvatar((Team)t,slot);
                var position=new Vector3(sign*point.x,avatar.Body.position.y,sign*lateral*point.y);
                var rotation=Quaternion.LookRotation(new Vector3((Team)t==team?sign:-sign,0,0));
                avatar.transform.SetPositionAndRotation(position,rotation);avatar.Body.position=position;avatar.Body.rotation=rotation;
            }
            var sender=GetPlayerAvatar(team,3);var ball=sender.Body.position+new Vector3(sign*MNG_KickPlate.DribbleAnchorForward,0,0);
            ball.y=m_BallStartingPosition.y;ballBody.position=ball;ballBody.transform.position=ball;
            Physics.SyncTransforms();
        }
    }
}
