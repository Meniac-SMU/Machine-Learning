using System;
using UnityEngine;

namespace MachineLearning.Soccer.Manager5v5
{
    public enum FiveMatchState { Playing, GoalPause, Finished }

    [DefaultExecutionOrder(-200)]
    public sealed partial class FiveMatch : MonoBehaviour
    {
        public FiveProfile profile;
        public SoccerArenaGeometry geometry;
        public FivePlayer[] players;
        public Rigidbody ball;
        public Camera overview;
        public Vector3 ballSpawn;
        public Vector3 baselineBallScale;
        public float ballRadius;
        public bool automaticTick = true;
        // Presentation opt-in; training and validation arenas retain their existing goal reset behaviour.
        [NonSerialized] public bool continueBallAfterGoal;
        readonly RigidbodyConstraints[] goalPlayerConstraints=new RigidbodyConstraints[10];
        bool goalPlayersFrozen;
        public float Elapsed { get; private set; }
        public float PauseRemaining { get; private set; }
        public FiveMatchState State { get; private set; }
        public int RedScore { get; private set; }
        public int NavyScore { get; private set; }
        bool paused;
        readonly Vector3[] savedVelocity = new Vector3[11];
        readonly Vector3[] savedAngularVelocity = new Vector3[11];
        readonly RigidbodyConstraints[] savedConstraints = new RigidbodyConstraints[11];
        public bool Paused
        {
            get => paused;
            set
            {
                if (paused == value) return;
                for (var i=0;i<11;i++)
                {
                    var body=i==10?ball:players[i].Body;
                    if(value)
                    {
                        savedVelocity[i]=body.linearVelocity;savedAngularVelocity[i]=body.angularVelocity;
                        savedConstraints[i]=body.constraints;
                        body.linearVelocity=body.angularVelocity=Vector3.zero;
                        body.constraints=RigidbodyConstraints.FreezeAll;
                    }
                    else
                    {
                        body.constraints=savedConstraints[i];body.linearVelocity=savedVelocity[i];body.angularVelocity=savedAngularVelocity[i];
                    }
                }
                paused=value;
            }
        }
        public int BoundaryCorrections { get; private set; }
        public int AnticipatedWallBounces { get; private set; }
        SphereCollider boundarySphere;
        Collider[] staticBoundaryColliders;
        public event Action<Team> Scored;
        public event Action RoundReset;
        partial void OnBeforeMovement(float dt);
        partial void OnAfterMovement(float dt);
        partial void OnRoundReset();
        partial void OnNewMatch();

        SimulationMode previousSimulationMode;
        bool ownsSimulation;
        void Start()
        {
            if(!automaticTick)return;
            previousSimulationMode=Physics.simulationMode;Physics.simulationMode=SimulationMode.Script;ownsSimulation=true;
            ResetMatch();
        }
        void OnDisable()
        {
            if(ownsSimulation){Physics.simulationMode=previousSimulationMode;ownsSimulation=false;}
        }
        void FixedUpdate() { if (automaticTick&&ownsSimulation) SimulatePhysicsStep(Time.fixedDeltaTime); }
        public void SimulatePhysicsStep(float seconds)
        {
            if(!float.IsFinite(seconds)||seconds<=0)throw new ArgumentOutOfRangeException(nameof(seconds));
            // This prototype owns one arena. Resolve its dense contacts at most 10 ms apart,
            // using the same controller/physics substeps in the Player and manual test harness.
            var count=Mathf.CeilToInt(seconds/.01f);var dt=seconds/count;
            for(var i=0;i<count;i++){Tick(dt);Physics.Simulate(dt);}
        }
        public void Validate()
        {
            profile.Validate();
            if (geometry == null || ball == null || players == null || players.Length != 10)
                throw new InvalidOperationException("5v5 needs a ball, arena and exactly ten players.");
            for (var i = 0; i < 10; i++)
            {
                if (players[i] == null || players[i].Index != i || (int)players[i].role != i % 5)
                    throw new InvalidOperationException("5v5 roster index/role mismatch.");
                if (players[i].IsKeeper == (players[i].Stamina != null))
                    throw new InvalidOperationException("Only the eight outfield players have stamina.");
            }
        }
        public void ResetMatch()
        {
            Validate(); Paused=false; Elapsed = 0; RedScore = NavyScore = BoundaryCorrections = AnticipatedWallBounces = 0;
            boundarySphere=ball.GetComponent<SphereCollider>();
            staticBoundaryColliders=Array.FindAll(GetComponentsInChildren<Collider>(),c=>!c.isTrigger&&c.attachedRigidbody==null);
            ball.collisionDetectionMode=CollisionDetectionMode.ContinuousSpeculative;
            ball.GetComponent<SphereCollider>().contactOffset=profile.ballContactOffset;
            foreach(var player in players)player.Body.collisionDetectionMode=CollisionDetectionMode.ContinuousSpeculative;
            OnNewMatch(); ResetRound(true);
        }
        public void ResetRound(bool newMatch = false)
        {
            RestoreGoalPlayers();
            ball.position = ballSpawn; ball.linearVelocity = ball.angularVelocity = Vector3.zero;
            foreach (var player in players) player.ResetPlayer(newMatch);
            RoundReset?.Invoke();
            OnRoundReset(); PauseRemaining = 0; State = FiveMatchState.Playing;
            Physics.SyncTransforms();
        }
        public void Score(Team team)
        {
            if (State != FiveMatchState.Playing) return;
            if (team == Team.Red) RedScore++; else NavyScore++;
            RecordKeeperGoalContact(team);
            Record("goal",(int)team,Owner);
            State = FiveMatchState.GoalPause; PauseRemaining = profile.goalPause;
            if(!continueBallAfterGoal)ball.linearVelocity = ball.angularVelocity = Vector3.zero;
            for(int i=0;i<players.Length;i++)
            {
                var body=players[i].Body;body.linearVelocity=Vector3.zero;
                if(!continueBallAfterGoal)continue;
                goalPlayerConstraints[i]=body.constraints;
                body.angularVelocity=Vector3.zero;body.constraints=RigidbodyConstraints.FreezeAll;
            }
            goalPlayersFrozen=continueBallAfterGoal;
            Scored?.Invoke(team);
        }
        void RestoreGoalPlayers()
        {
            if(!goalPlayersFrozen)return;
            for(int i=0;i<players.Length;i++)
                if(paused)savedConstraints[i]=goalPlayerConstraints[i];
                else players[i].Body.constraints=goalPlayerConstraints[i];
            goalPlayersFrozen=false;
        }
        public void Tick(float dt)
        {
            if (Paused || State == FiveMatchState.Finished) return;
            if (!float.IsFinite(dt) || dt <= 0) throw new ArgumentOutOfRangeException(nameof(dt));
            if (State == FiveMatchState.GoalPause)
            {
                PauseRemaining -= dt;
                if (PauseRemaining <= 0) ResetRound();
                return;
            }
            Elapsed += dt;
            if (Elapsed >= profile.duration)
            {
                State = FiveMatchState.Finished;
                foreach (var player in players) player.Body.linearVelocity = Vector3.zero;
                ball.linearVelocity = Vector3.zero; return;
            }
            foreach (var player in players)
                if (!player.IsKeeper) player.Stamina.Advance(player.Velocity.magnitude, dt, profile);
            OnBeforeMovement(dt);
            ApplyHumanControl(dt);
            foreach (var player in players) player.Drive(dt, profile);
            OnAfterMovement(dt);
            AnticipateWall(dt);
            if (!float.IsFinite(ball.position.x) || !float.IsFinite(ball.position.z))
                throw new InvalidOperationException("Non-finite ball position.");
            if (!BallInsidePlayableBounds(ball.position))
            {
                var constrained=geometry.ClampBallPosition(ball.position,ballRadius);
                float penetration=0;
                foreach(var wall in staticBoundaryColliders)
                    if(wall!=null&&Physics.ComputePenetration(boundarySphere,ball.position,ball.rotation,wall,wall.transform.position,wall.transform.rotation,out _,out var depth))penetration=Mathf.Max(penetration,depth);
                var nearby=Array.FindAll(players,p=>(p.Position-BallPosition).sqrMagnitude<16);
                Record("boundary-fallback",Owner,reason:$"position={ball.position.ToString("F5")};velocity={ball.linearVelocity.ToString("F5")};radius={ballRadius:F5};delta={(ball.position-constrained).ToString("F5")};wallPenetration={penetration:F5};nearby="+string.Join("/",Array.ConvertAll(nearby,p=>$"{p.Index}:{p.Position.ToString("F3")}:{p.Velocity.ToString("F3")}")));
                ball.position = geometry.ClampBallPosition(ball.position, ballRadius);
                BoundaryCorrections++;
            }
        }
        public bool BallInsidePlayableBounds(Vector3 position,float tolerance=.02f)
        {
            if(geometry.ContainsBallPosition(position,ballRadius,tolerance))return true;
            // The union of two radius-shrunk rectangles excludes valid spherical positions around a goal-mouth corner.
            // Only in that small corner region, check the unchanged physical walls instead of snapping the ball inward.
            var x=Mathf.Abs(position.x);var z=Mathf.Abs(position.z);
            if(x<geometry.HalfLength-ballRadius||x>geometry.HalfLength
                ||z<geometry.GoalHalfWidth-ballRadius||z>geometry.GoalHalfWidth)return false;
            foreach(var wall in staticBoundaryColliders)
                if(wall!=null&&Physics.ComputePenetration(boundarySphere,position,ball.rotation,wall,wall.transform.position,wall.transform.rotation,out _,out var depth)&&depth>tolerance)return false;
            return true;
        }
        void AnticipateWall(float dt)
        {
            // A swept boundary guard supplements the original thin PhysX walls.
            // It reflects an approaching free ball before a fixed step would penetrate,
            // and does not teleport it or treat a pass as successfully received.
            if(Owner>=0)return;
            var predicted=ball.position+ball.linearVelocity*dt;
            var safe=geometry.ClampBallPosition(predicted,ballRadius+.04f);
            var excess=predicted-safe;excess.y=0;
            if(excess.sqrMagnitude<.000001f)return;
            var normal=excess.normalized;
            var outward=Vector3.Dot(ball.linearVelocity,normal);
            if(outward<=0)return;
            ball.linearVelocity-=(1f+.65f)*outward*normal;
            AnticipatedWallBounces++;Record("anticipated-wall-bounce",reason:$"position={ball.position};normal={normal}");
        }
    }
}
