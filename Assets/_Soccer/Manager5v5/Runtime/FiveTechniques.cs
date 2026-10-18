using System;
using System.Collections.Generic;
using UnityEngine;

namespace MachineLearning.Soccer.Manager5v5
{
    public enum FivePassPhase { None, Preparing, InFlight }
    [Serializable] public sealed class FiveEvent
    {
        public float time;
        public string kind, reason;
        public int actor, other, id;
        public FiveEvent(float time,string kind,int actor=-1,int other=-1,int id=0,string reason="")
        {this.time=time;this.kind=kind;this.actor=actor;this.other=other;this.id=id;this.reason=reason;}
    }
    public sealed partial class FiveMatch
    {
        public bool techniquesEnabled=true;
        public bool keepersEnabled=true;
        public bool keeperSideRecoveryEnabled=true;
        public bool keeperTurnFirstEnabled=true;
        readonly bool[] keeperSideRecovering=new bool[2];
        readonly int[] keeperSidePhase=new int[2];
        int lastBallTouch=-1;
        float lastBallTouchAt;
        bool lastTouchWasSlowSideCollection;
        readonly bool[] slowSideCollectionBeforePhysics=new bool[2];
        public int Owner { get; private set; }=-1;
        public float OwnerSince { get; private set; }
        public FivePassPhase PassPhase { get; private set; }
        public int PassSender { get; private set; }=-1;
        public int PassReceiver { get; private set; }=-1;
        public int PassId { get; private set; }
        public int PassAttempts { get; private set; }
        public int PassReceptions { get; private set; }
        public int Interceptions { get; private set; }
        public int Shots { get; private set; }
        public int Clearances { get; private set; }
        public List<FiveEvent> Events { get; }=new List<FiveEvent>();
        public Vector2 BallPosition => new Vector2(ball.position.x,ball.position.z);
        public Vector2 BallVelocity => new Vector2(ball.linearVelocity.x,ball.linearVelocity.z);
        public FivePlayer Carrier => Owner>=0 ? players[Owner] : null;
        float releaseElapsed, previousOwnerUnlock, passDeadline;
        int previousOwner=-1, pendingReception=-1, shotPlayer=-1;
        float receptionStarted,shotDeadline;
        bool shotClearance;
        Vector2 receiverHold, shotTarget;
        readonly float[] contactTimes=new float[10];
        readonly float[] challengeReadyAt=new float[10];
        readonly float[] shotRetryAt=new float[10];
        readonly float[] passRetryAt=new float[10];
        int deflectedSender=-1;
        float deflectionDeadline;
        partial void PlanTactics(float dt);
        partial void ResetTactics(bool newMatch);
        partial void ResetRuleObservation();
        partial void ObserveBall();

        partial void OnNewMatch()
        {
            Events.Clear();PassAttempts=PassReceptions=Interceptions=Shots=Clearances=PassId=0;
            ResetTactics(true);
        }
        partial void OnRoundReset()
        {
            ClearHumanInput();
            Array.Clear(keeperSideRecovering,0,2);Array.Clear(keeperSidePhase,0,2);
            lastBallTouch=-1;lastBallTouchAt=-100;lastTouchWasSlowSideCollection=false;
            Array.Clear(slowSideCollectionBeforePhysics,0,2);
            Owner=previousOwner=pendingReception=shotPlayer=-1;
            PassPhase=FivePassPhase.None;PassSender=PassReceiver=-1;
            previousOwnerUnlock=releaseElapsed=0;
            for(var i=0;i<contactTimes.Length;i++){contactTimes[i]=-100;challengeReadyAt[i]=shotRetryAt[i]=passRetryAt[i]=0;}
            deflectedSender=-1;
            ResetTactics(false);
            ResetRuleObservation();
            ResetContestRelief();
        }
        partial void OnBeforeMovement(float dt)
        {
            if(!techniquesEnabled)return;
            // Diagnostic only: classify the incoming state, before a collision changes ball velocity.
            foreach(var player in players)if(player.IsKeeper)
                slowSideCollectionBeforePhysics[(int)player.team]=Owner<0 && BallVelocity.magnitude<=4
                    && InsideKeeperArea(player.team,BallPosition)
                    && Vector2.Angle(player.Forward,BallPosition-player.Position)>20;
            Array.Clear(formationDeferredPlan,0,2);
            CushionIncomingBall(dt);
            UpdateOwnership(dt);
            RefreshDefensiveFormation();
            ObserveBall();
            PlanTactics(dt);
            if(keepersEnabled)foreach(var player in players)if(player.IsKeeper)PlanKeeper(player);
            PlanContestRelief(dt);
            PlanContestReinforcements(dt);
            PlanCarrierMobility();
            UpdatePassAndShot();
            PlanChallengeRoutes();
            KeepTargetsAwayFromWalls();
            ApplyDefensiveFormationTargets(); // Fixed state has final authority over field targets.
            // Pass reception can retarget a keeper after PlanKeeper; retain the same activity boundary.
            if(keepersEnabled)foreach(var player in players)if(player.IsKeeper)
            {
                PlanKeeperSideRecovery(player);
                player.LimitTarget(ClampKeeperTarget(player,player.Target));
                // Close side contact still needs separation with a fixed plate before turning.
                // Otherwise every collection approach, including distant loose balls, faces first.
                if(keeperTurnFirstEnabled && keeperSidePhase[(int)player.team]!=1
                    && InsideKeeperArea(player.team,BallPosition)
                    && ((Owner<0 && BallVelocity.magnitude<=player.MaximumMoveSpeed(profile))
                        || (Carrier!=null && Carrier.team!=player.team
                            && Vector2.Distance(player.Position,BallPosition)<8)))
                    player.RequireKeeperFrontApproach();
            }
        }
        partial void OnAfterMovement(float dt)
        {
            if(techniquesEnabled)ApplyDribble(dt);
        }
        void CushionIncomingBall(float dt)
        {
            if(Owner>=0)return;
            FivePlayer receiver=null;float nearest=float.PositiveInfinity;
            foreach(var player in players)
            {
                if((player.Index==previousOwner&&Elapsed<previousOwnerUnlock)||Elapsed<player.KickReadyAt)continue;
                var relative=BallVelocity-player.Velocity;
                if(relative.magnitude<=profile.frontReceiveSpeed+.1f
                    ||!player.FrontReceptionPath(ball.position,relative,dt,ballRadius,profile.ballContactOffset)
                    ||!ClearReach(player))continue;
                var distance=Vector2.Distance(BallPosition,player.Position);
                if(distance<nearest){nearest=distance;receiver=player;}
            }
            if(receiver==null)return;
            var incoming=BallVelocity-receiver.Velocity;
            var caught=receiver.Velocity+Vector2.ClampMagnitude(incoming,profile.frontReceiveSpeed);
            ball.linearVelocity=new Vector3(caught.x,0,caught.y);ball.angularVelocity=Vector3.zero;
            Record("front-cushion",receiver.Index,reason:receiver.IsKeeper?"keeper-front-save":"field-front-reception");
            // Acquisition is still checked against the CURRENT front pocket, not a future position.
            // An incoming ball outside it slows first, then physically enters before receiving ownership.
        }
        public void RegisterBallContact(FivePlayer player)
        {
            if(State!=FiveMatchState.Playing || !techniquesEnabled)return;
            contactTimes[player.Index]=Elapsed;
            BallContact?.Invoke(player);
            if(lastBallTouch!=player.Index || Elapsed-lastBallTouchAt>.1f)
                lastTouchWasSlowSideCollection=false;
            lastBallTouch=player.Index;lastBallTouchAt=Elapsed;
            lastTouchWasSlowSideCollection|=player.IsKeeper && slowSideCollectionBeforePhysics[(int)player.team];
            if(PassPhase==FivePassPhase.InFlight&&player.team!=players[PassSender].team)
            {
                deflectedSender=PassSender;deflectionDeadline=Elapsed+1;
                Record("pass-deflected",player.Index,PassSender,PassId);
                CancelPass("opponent-ball-contact");
            }
        }
        public void Record(string kind,int actor=-1,int other=-1,int id=0,string reason="")
        {
            var e = new FiveEvent(Elapsed,kind,actor,other,id,reason);
            Events.Add(e); EventRecorded?.Invoke(e);
        }
        void RecordKeeperGoalContact(Team scoringTeam)
        {
            if(lastBallTouch<0)return;
            var player=players[lastBallTouch];
            if(!player.IsKeeper || player.team==scoringTeam)return;
            // Last-touch concessions include save deflections, so do not call every one an own goal.
            Record("keeper-last-touch-conceded",player.Index,(int)scoringTeam);
            if(lastTouchWasSlowSideCollection && Elapsed-lastBallTouchAt<=2)
                Record("keeper-slow-side-contact-conceded",player.Index,(int)scoringTeam,
                    reason:"loose ball <=4 m/s; side angle >20 degrees; last touch <=2 seconds");
        }
        public Vector2 Anchor(FivePlayer player) => player.Position+player.Forward*player.CloseHoldOffset(ballRadius,profile.dribblePlateGap,ball.position.y);
        public bool InDribbleArea(FivePlayer player) => player.InsideKickPlate(ball.position,ballRadius,profile.ballContactOffset);
        public static float AttackSign(Team team) => team==Team.Red ? 1 : -1;
        public static Team Other(Team team) => team==Team.Red ? Team.Navy : Team.Red;
        public Vector2 ClampField(Vector2 point,float margin=2)
        {
            var clamped=geometry.ClampTarget(new Vector3(Mathf.Clamp(point.x,-geometry.HalfLength+margin,geometry.HalfLength-margin),0,
                Mathf.Clamp(point.y,-geometry.HalfWidth+margin,geometry.HalfWidth-margin)));
            return new Vector2(clamped.x,clamped.z);
        }
        public float NearestOpponentDistance(FivePlayer player)
        {
            var distance=float.PositiveInfinity;
            foreach(var other in players)if(other.team!=player.team)distance=Mathf.Min(distance,Vector2.Distance(player.Position,other.Position));
            return distance;
        }
        public bool LaneOpen(Vector2 start,Vector2 end,Team team,float padding=.5f)
        {
            foreach(var other in players)
            {
                if(other.team==team)continue;
                if(DistanceToSegment(other.Position,start,end)<other.radius+ballRadius+padding)return false;
            }
            return true;
        }
        public static float DistanceToSegment(Vector2 point,Vector2 start,Vector2 end)
        {
            var path=end-start;
            var t=path.sqrMagnitude>.001f?Mathf.Clamp01(Vector2.Dot(point-start,path)/path.sqrMagnitude):0;
            return Vector2.Distance(point,start+path*t);
        }
        bool ClearReach(FivePlayer player)
        {
            var origin=new Vector3(player.Position.x,ball.position.y,player.Position.y);
            var offset=ball.position-origin;
            foreach(var hit in RuleSceneRaycasts(origin,offset.normalized,offset.magnitude,~0,QueryTriggerInteraction.Ignore))
                if(hit.rigidbody!=ball && hit.rigidbody!=player.Body)return false;
            return true;
        }
        void UpdateOwnership(float dt)
        {
            var position=BallPosition;
            if(Owner>=0)
            {
                var owner=players[Owner];
                var distance=Vector2.Distance(owner.Position,position);
                releaseElapsed=distance>profile.releaseDistance?releaseElapsed+dt:0;
                if(!InDribbleArea(owner))
                    ReleaseOwner("outside-kick-plate");
                else if(distance>profile.releaseDistance+1 || releaseElapsed>profile.releaseGrace)
                    ReleaseOwner("outside-control-area");
                else
                {
                    FivePlayer challenger=null;
                    float nearest=float.PositiveInfinity;
                    foreach(var candidate in players)
                    {
                        if(candidate.team==owner.team || Elapsed-contactTimes[candidate.Index]>.06f || Elapsed-OwnerSince<.25f || Elapsed<challengeReadyAt[candidate.Index])continue;
                        var d=Vector2.Distance(candidate.Position,position);
                        if(d<nearest && d<candidate.radius+ballRadius+.35f && InDribbleArea(candidate) && ClearReach(candidate)) {challenger=candidate;nearest=d;}
                    }
                    if(challenger!=null) {Interceptions++; Acquire(challenger,"opponent-contact");}
                }
            }
            if(Owner<0)
            {
                FivePlayer nearest=null;float nearestDistance=float.PositiveInfinity;
                foreach(var player in players)
                {
                    if(player.Index==previousOwner && Elapsed<previousOwnerUnlock)continue;
                    var d=Vector2.Distance(position,player.Position);
                    var reach=player.radius+ballRadius+profile.acquirePadding;
                    if(d>reach || d>=nearestDistance || !InDribbleArea(player) || !ClearReach(player))continue;
                    nearest=player;nearestDistance=d;
                }
                if(nearest!=null)Acquire(nearest,PassPhase==FivePassPhase.InFlight?"pass-contact":"loose-ball");
            }
            if(pendingReception>=0)
            {
                if(Owner!=pendingReception){Record("pass-failed",PassSender,pendingReception,PassId,"lost-before-stable-reception");pendingReception=-1;}
                else if(Elapsed-receptionStarted>=.12f)
                {PassReceptions++;Record("pass-received",PassSender,pendingReception,PassId);pendingReception=-1;}
            }
        }
        void Acquire(FivePlayer player,string reason)
        {
            var old=Owner;
            if(old>=0)
            {
                players[old].Dribbling=false;
                if(players[old].team!=player.team)challengeReadyAt[old]=Elapsed+profile.challengeRecoverySeconds;
            }
            Owner=player.Index;OwnerSince=Elapsed;releaseElapsed=0;player.Dribbling=true;
            var firstTouch=player.Velocity+Vector2.ClampMagnitude(BallVelocity-player.Velocity,profile.frontReceiveSpeed);
            ball.linearVelocity=new Vector3(firstTouch.x,0,firstTouch.y);ball.angularVelocity=Vector3.zero;
            Record("possession",player.Index,old,0,reason);
            if(deflectedSender>=0)
            {
                if(Elapsed<=deflectionDeadline&&player.team!=players[deflectedSender].team)Interceptions++;
                deflectedSender=-1;
            }
            if(PassPhase==FivePassPhase.InFlight)
            {
                if(player.Index==PassReceiver){pendingReception=player.Index;receptionStarted=Elapsed;PassPhase=FivePassPhase.None;}
                else {if(player.team!=players[PassSender].team)Interceptions++;CancelPass("intercepted-or-other-receiver");}
            }
            else if(PassPhase==FivePassPhase.Preparing && player.Index!=PassSender)CancelPass("lost-during-preparation");
            RefreshDefensiveFormation();
        }
        void ReleaseOwner(string reason)
        {
            if(Owner<0)return;
            previousOwner=Owner;previousOwnerUnlock=Elapsed+.35f;
            players[Owner].Dribbling=false;Record("release",Owner,reason:reason);Owner=-1;releaseElapsed=0;
            if(PassPhase==FivePassPhase.Preparing&&reason!="kick")CancelPass("lost-during-preparation");
            RefreshDefensiveFormation();
        }
        void ApplyDribble(float dt)
        {
            if(Owner<0)return;
            var owner=players[Owner];
            if(!InDribbleArea(owner)){ReleaseOwner("outside-kick-plate");return;}
            var rawAnchor=Anchor(owner);
            // The control target must remain inside the physical pitch. It never pulls through a wall.
            var safeAnchor=geometry.ClampBallPosition(new Vector3(rawAnchor.x,ball.position.y,rawAnchor.y),ballRadius+profile.dribbleWallMargin);
            var delta=new Vector2(safeAnchor.x,safeAnchor.z)-BallPosition;
            // Use this substep's player velocity so the ball does not lag through acceleration/braking.
            var touchLimit=Elapsed-OwnerSince<.18f?profile.frontReceiveSpeed:9f;
            // Follow the plate's swept front pocket, including its sideways motion during a controlled turn.
            // Eligibility is still tested before assistance: side/rear balls and abrupt physical displacement release.
            var orbit=(owner.DrivenForward-owner.Forward)*owner.CloseHoldOffset(ballRadius,profile.dribblePlateGap)/dt;
            var desired=owner.Velocity+orbit+Vector2.ClampMagnitude(delta*profile.closeDribbleGain,touchLimit);
            var velocity=Vector2.MoveTowards(BallVelocity,desired,profile.closeDribbleAcceleration*dt);
            velocity=Vector2.ClampMagnitude(velocity,profile.maximumBallSpeed);
            var predicted=ball.position+new Vector3(velocity.x,0,velocity.y)*dt;
            var constrained=geometry.ClampBallPosition(predicted,ballRadius+profile.dribbleWallMargin);
            // Remove only outward driven displacement before the physics step; never relocate the ball.
            velocity-=new Vector2(predicted.x-constrained.x,predicted.z-constrained.z)/dt;
            ball.linearVelocity=new Vector3(velocity.x,0,velocity.y);
            ball.maxAngularVelocity=Mathf.Max(ball.maxAngularVelocity,profile.maximumBallSpeed/ballRadius);
            ball.angularVelocity=Vector3.Cross(Vector3.up,new Vector3(velocity.x,0,velocity.y))/ballRadius;
        }
        public bool RequestPass(FivePlayer sender,FivePlayer receiver)
        {
            if(IsHuman(sender))return false;
            if(sender!=null&&CarrierEscaping(sender))return false;
            if(sender!=null&&ContestControls(sender)&&!requestingContestPass)return false;
            if(!techniquesEnabled || State!=FiveMatchState.Playing || sender==null || receiver==null || sender==receiver || sender.team!=receiver.team
                || Owner!=sender.Index || Elapsed<sender.KickReadyAt || Elapsed<passRetryAt[sender.Index] || PassPhase!=FivePassPhase.None || pendingReception>=0)return false;
            var distance=Vector2.Distance(sender.Position,receiver.Position);
            if(distance<4 || distance>85)return false;
            shotPlayer=deflectedSender=-1;PassId++;PassPhase=FivePassPhase.Preparing;
            PassSender=sender.Index;PassReceiver=receiver.Index;receiverHold=receiver.Position;
            passDeadline=Elapsed+2.2f;Record("pass-signal",PassSender,PassReceiver,PassId);
            return true;
        }
        public void CancelPass(string reason)
        {
            if(PassPhase==FivePassPhase.Preparing&&(reason=="timeout"||reason=="pressure-escape")&&Owner==PassSender)
            {
                passRetryAt[PassSender]=Elapsed+profile.decisionSeconds;
                Record("pass-retry-cooldown",PassSender,PassReceiver,PassId,"preparation-timeout");
            }
            if(PassPhase!=FivePassPhase.None)Record("pass-failed",PassSender,PassReceiver,PassId,reason);
            PassPhase=FivePassPhase.None;
        }
        public bool RequestShot(FivePlayer player,Vector2 target,bool clearance=false)
        {
            if(IsHuman(player))return false;
            if(player!=null&&!clearance&&ContestControls(player))return false;
            if(player==null || Owner!=player.Index || Elapsed<player.KickReadyAt || PassPhase!=FivePassPhase.None || pendingReception>=0)return false;
            if(!clearance&&Elapsed<shotRetryAt[player.Index])return false;
            if(clearance && !player.IsKeeper) return false;
            if(shotPlayer==player.Index)return true;
            shotPlayer=player.Index;shotTarget=target;shotClearance=clearance;shotDeadline=Elapsed+2.2f;
            return true;
        }
        void UpdatePassAndShot()
        {
            // Keep the receiving plate stable until the existing physical reception has settled.
            if(pendingReception>=0&&Owner==pendingReception)
            {
                var receiving=players[pendingReception];
                receiving.SetTarget(receiving.Position,receiving.Position+receiving.Forward*5,true);
            }
            if(PassPhase!=FivePassPhase.None)
            {
                if(Elapsed>passDeadline){CancelPass("timeout");return;}
                var sender=players[PassSender];var receiver=players[PassReceiver];
                var receiveFacing=PassPhase==FivePassPhase.InFlight&&BallVelocity.sqrMagnitude>1
                    ?receiver.Position-BallVelocity.normalized*10:BallPosition;
                var receiveTarget=receiverHold;
                if(PassPhase==FivePassPhase.InFlight&&BallVelocity.magnitude<12
                    &&Vector2.Distance(receiver.Position,BallPosition)<14
                    &&Vector2.Dot(BallVelocity,receiver.Position-BallPosition)>0)
                {
                    // Meet a slowing pass with the plate still facing upstream, instead of watching it stop short.
                    var drag=profile.EffectiveFreeBallDamping;
                    var travel=drag>.001f?(1-Mathf.Exp(-drag*.35f))/drag:.35f;
                    var intercept=BallPosition+BallVelocity*travel+BallVelocity.normalized*receiver.CloseHoldOffset(ballRadius,profile.dribblePlateGap);
                    // A pass that will reach the prepared plate needs no retreat. Only meet
                    // a short ball on the incoming side; following the projected point behind
                    // the receiver made it run away after the first cushioning touch.
                    var approach=intercept-receiverHold;
                    // Once the receiver has advanced, compare against its current position.
                    // Comparing only to the initial hold point makes a short flat plate retreat
                    // ahead of a slowing ball and miss the stable-reception deadline.
                    receiveTarget=receiver.Position;
                    if(Vector2.Dot(intercept-receiver.Position,BallPosition-receiver.Position)>0)
                        receiveTarget=ClampField(receiverHold+Vector2.ClampMagnitude(approach,8),receiver.radius+.1f);
                }
                receiver.SetTarget(receiveTarget,receiveFacing,true);
                if(PassPhase==FivePassPhase.Preparing)
                {
                    if(Owner!=PassSender){CancelPass("no-longer-owner");return;}
                    // Keep a moving passer rolling while both plates align. A stationary passer can turn in place.
                    var roll=sender.Position+sender.Velocity*.3f;
                    sender.SetTarget(ClampField(roll,profile.fieldWallClearance+sender.radius),receiverHold,true);
                    var ready=FacingPoint(sender,receiverHold,.90f)&&FacingPoint(receiver,BallPosition,.99f)
                        && receiver.Velocity.magnitude<2 && Vector2.Distance(BallPosition,Anchor(sender))<.7f;
                    // The keeper's six-second distribution rule does not wait indefinitely for
                    // receiver alignment. The ball still flies physically and can be intercepted.
                    var keeperDeadline=sender.IsKeeper&&Elapsed-OwnerSince>=KeeperDistributionReleaseAt;
                    if(ready||keeperDeadline)
                    {
                        // Aim at the prepared plate instead of waiting for a collision with the torso.
                        var direction=(ready?Anchor(receiver):receiver.Position+receiver.Velocity*.25f)-BallPosition;
                        var speed=Mathf.Clamp(direction.magnitude/1.7f+profile.EffectiveFreeBallDamping*direction.magnitude,12,profile.maximumBallSpeed);
                        if(ready)Record("pass-receiver-ready",receiver.Index,sender.Index,PassId,
                            Vector2.Angle(receiver.Forward,-direction).ToString("F2",System.Globalization.CultureInfo.InvariantCulture));
                        if(keeperDeadline)Record("keeper-deadline-pass",sender.Index,receiver.Index,PassId);
                        Launch(direction,speed,sender);
                        var drag=profile.EffectiveFreeBallDamping;
                        var travel=drag>.001f?-Mathf.Log(Mathf.Max(.01f,1-drag*direction.magnitude/speed))/drag:direction.magnitude/speed;
                        PassPhase=FivePassPhase.InFlight;passDeadline=Elapsed+Mathf.Max(6,travel+1.5f);
                        PassAttempts++;Record("pass-kicked",sender.Index,receiver.Index,PassId);
                    }
                }
            }
            if(shotPlayer>=0)
            {
                if(Owner!=shotPlayer){shotPlayer=-1;return;}
                if(Elapsed>shotDeadline)
                {
                    Record("shot-failed",shotPlayer,reason:shotClearance?"clearance-preparation-timeout":"shot-preparation-timeout");
                    if(!shotClearance)shotRetryAt[shotPlayer]=Elapsed+profile.decisionSeconds;
                    shotPlayer=-1;return;
                }
                var player=players[shotPlayer];player.SetTarget(player.Position,shotTarget,true);
                if(FacingPoint(player,shotTarget,.94f)&&Vector2.Distance(BallPosition,Anchor(player))<.7f)
                {
                    Launch(shotTarget-BallPosition,profile.maximumBallSpeed,player);
                    if(shotClearance){Clearances++;Record("keeper-clearance",player.Index);}
                    else {Shots++;Record("shot",player.Index);}
                    shotPlayer=-1;
                }
            }
        }
        public static bool FacingPoint(FivePlayer player,Vector2 point,float dot)
            => Vector2.Dot(player.Forward,(point-player.Position).normalized)>=dot;
        void Launch(Vector2 direction,float speed,FivePlayer sender)
        {
            ReleaseOwner("kick");sender.KickReadyAt=Elapsed+.6f;
            var velocity=direction.normalized*speed;
            ball.linearVelocity=new Vector3(velocity.x,0,velocity.y);ball.angularVelocity=Vector3.zero;
        }
        public bool InsideKeeperArea(Team team,Vector2 point)
        {
            var x=point.x*AttackSign(team);
            return x>=-geometry.HalfLength && x<=-geometry.HalfLength+profile.keeperAreaDepth
                && Mathf.Abs(point.y)<=profile.keeperAreaHalfWidth;
        }
        public Vector2 ClampKeeperTarget(FivePlayer keeper,Vector2 point)
        {
            var sign=AttackSign(keeper.team);var margin=keeper.radius;
            return new Vector2(Mathf.Clamp(point.x*sign,-geometry.HalfLength+margin,
                -geometry.HalfLength+profile.keeperAreaDepth-margin)*sign,
                Mathf.Clamp(point.y,-profile.keeperAreaHalfWidth+margin,profile.keeperAreaHalfWidth-margin));
        }
        public Vector2 KeeperTarget(FivePlayer keeper)
        {
            float sign=AttackSign(keeper.team);
            var localBallX=BallPosition.x*sign;
            var x=-geometry.HalfLength+3;
            var z=Mathf.Clamp(BallPosition.y*.7f,-geometry.GoalHalfWidth+1,geometry.GoalHalfWidth-1);
            if(InsideKeeperArea(keeper.team,BallPosition))
            {
                if(Owner<0 && BallVelocity.magnitude<=keeper.MaximumMoveSpeed(profile))
                    return ClampKeeperTarget(keeper,BallPosition);
                if(Carrier!=null&&Carrier.team!=keeper.team && Vector2.Distance(keeper.Position,BallPosition)<8)
                    return ClampKeeperTarget(keeper,BallPosition);
            }
            if(BallVelocity.x*sign < -1)
            {
                var t=(x-localBallX)/(BallVelocity.x*sign);
                if(t>=0 && t<2)z=Mathf.Clamp(BallPosition.y+BallVelocity.y*t,-geometry.GoalHalfWidth+1,geometry.GoalHalfWidth-1);
            }
            return ClampKeeperTarget(keeper,new Vector2(x*sign,z));
        }
        void PlanKeeperSideRecovery(FivePlayer keeper)
        {
            var t=(int)keeper.team;
            var delta=BallPosition-keeper.Position;var distance=delta.magnitude;
            var pocket=keeper.CloseHoldOffset(ballRadius,profile.dribblePlateGap);
            var turnClearance=keeper.BallRotationClearance(ballRadius);
            if(!keeperSideRecoveryEnabled||Owner>=0||!InsideKeeperArea(keeper.team,BallPosition)
                ||BallVelocity.magnitude>4||distance>Mathf.Max(pocket+1.2f,turnClearance+.6f))
            {keeperSideRecovering[t]=false;keeperSidePhase[t]=0;return;}
            var angle=Vector2.Angle(keeper.Forward,delta);
            if(!keeperSideRecovering[t])
            {
                if(distance>pocket+.65f||angle<55)return;
                keeperSideRecovering[t]=true;
                Record("keeper-side-recovery",keeper.Index);
            }
            int phase;
            if(angle>25&&distance<turnClearance)
            {
                // Keep the plate still while making a short gap. Turning against a touching
                // side ball can sweep it toward our goal before it enters the front pocket.
                // Pure backward motion is tangent to a side ball: the rear edge of a
                // wing plate can still sweep through it. Add separation away from the ball.
                var retreat=(-delta.normalized-keeper.Forward*.5f).normalized;
                var target=ClampKeeperTarget(keeper,keeper.Position+retreat*.4f);
                var move=target-keeper.Position;
                if(move.sqrMagnitude<.001f||Vector2.Dot(move,-delta)<-.001f)
                    target=ClampKeeperTarget(keeper,keeper.Position-delta.normalized*.4f);
                keeper.SetTarget(target,keeper.Position+keeper.Forward*10,true);phase=1;
            }
            else if(angle>20)
            {
                keeper.SetTarget(keeper.Position,BallPosition,true);phase=2;
            }
            else
            {
                // Approach slowly with independent facing; ownership still requires real
                // front-plate contact. This never moves the ball or grants a remote catch.
                var step=Mathf.Min(.35f,Mathf.Max(0,distance-pocket*.85f));
                keeper.SetTarget(keeper.Position+delta.normalized*step,BallPosition,true);phase=3;
            }
            if(keeperSidePhase[t]!=phase)
            {keeperSidePhase[t]=phase;Record("keeper-side-recovery-phase",keeper.Index,id:phase);}
        }
        void PlanKeeper(FivePlayer keeper)
        {
            keeper.SetTarget(KeeperTarget(keeper),BallPosition);
            if(Owner!=keeper.Index)return;
            var age=Elapsed-OwnerSince;
            if(age<.4f)return;
            // Owned-ball distribution always addresses a teammate, including under pressure.
            shotPlayer=-1;
            if(PassPhase==FivePassPhase.Preparing && PassSender==keeper.Index)
            {
                // A timeout may not postpone the hard ownership deadline.
                passDeadline=Mathf.Max(passDeadline,OwnerSince+6);
                return;
            }
            if(PassPhase==FivePassPhase.None && pendingReception<0)
            {
                var command=Decision(keeper.team).command;
                var receiver=FindKeeperDistribution(keeper,command);
                if(receiver==null)receiver=FindKeeperSafetyOutlet(keeper);
                if(receiver==null)return; // A valid 5v5 roster always has four teammates.
                if(RequestPass(keeper,receiver))
                    Record("keeper-pass-order",keeper.Index,receiver.Index,(int)command);
                else if(age>=3)
                {
                    // Relax tactical range and retry cooldown, never team identity or ownership.
                    PassId++;PassPhase=FivePassPhase.Preparing;PassSender=keeper.Index;PassReceiver=receiver.Index;
                    receiverHold=receiver.Position;passDeadline=OwnerSince+6;deflectedSender=-1;
                    Record("pass-signal",PassSender,PassReceiver,PassId,"keeper-deadline-preparation");
                    Record("keeper-pass-order",keeper.Index,receiver.Index,(int)command,"safety-outlet");
                }
            }
        }
        // Leave a tenth of a second for discrete simulation scheduling, without exceeding six.
        public const float KeeperDistributionReleaseAt=5.9f;
        FivePlayer FindKeeperSafetyOutlet(FivePlayer keeper)
        {
            FivePlayer best=null;float bestScore=float.NegativeInfinity;
            foreach(var candidate in players)
            {
                if(candidate.team!=keeper.team||candidate.IsKeeper)continue;
                var distance=Vector2.Distance(candidate.Position,keeper.Position);
                var forward=(candidate.Position.x-keeper.Position.x)*AttackSign(keeper.team);
                var score=(LaneOpen(BallPosition,candidate.Position,keeper.team)?30:0)
                    +Mathf.Min(NearestOpponentDistance(candidate),12)-distance*.2f
                    +(forward>0?4:-4)-(distance<4?15:0);
                if(score>bestScore){best=candidate;bestScore=score;}
            }
            return best;
        }
    }
}
