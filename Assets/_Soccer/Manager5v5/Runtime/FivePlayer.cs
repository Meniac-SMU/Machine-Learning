using UnityEngine;

namespace MachineLearning.Soccer.Manager5v5
{
    public enum FiveRole { Keeper, DefenderLeft, DefenderRight, AttackerLeft, AttackerRight }

    [DisallowMultipleComponent, RequireComponent(typeof(Rigidbody))]
    public sealed class FivePlayer : MonoBehaviour
    {
        public Team team;
        public FiveRole role;
        public int slot;
        public Vector3 baselineScale;
        public Vector3 spawn;
        public float radius;
        public float headHeight;
        public Vector2 Target { get; private set; }
        public Vector2 Facing { get; private set; }
        public bool Dribbling { get; set; }
        public bool SharpDribbleTurn { get; private set; }
        public Vector2 DrivenForward { get; private set; }
        public bool Evading { get; set; }
        public bool KeeperFrontApproach { get; private set; }
        public float KickReadyAt { get; set; }
        Rigidbody body;
        FiveStamina stamina;
        bool freeFacing;
        bool manualDrive;
        float manualThrottle, manualTurn;
        public void SetManualDrive(float throttle, float turn)
        { manualDrive = true; manualThrottle = throttle; manualTurn = turn; }
        Vector3[] plateFaces, plateNormals;
        float plateMouth;
        float plateCenterFace,plateHalfWidth;
        float plateRotationRadius;
        BoxCollider centerPlate;
        bool singlePlate;
        public void InvalidateKickPlateCache()
        {
            plateFaces=null; centerPlate=null; plateMouth=plateCenterFace=plateHalfWidth=plateRotationRadius=0;
        }
        public Rigidbody Body => body != null ? body : body = GetComponent<Rigidbody>();
        public FiveStamina Stamina => stamina != null ? stamina : stamina = GetComponent<FiveStamina>();
        public bool IsKeeper => role == FiveRole.Keeper;
        public int Index => (int)team * 5 + slot;
        public Vector2 Position => new Vector2(Body.position.x, Body.position.z);
        public Vector2 Velocity => new Vector2(Body.linearVelocity.x, Body.linearVelocity.z);
        public Vector2 Forward { get {var forward=Body.rotation*Vector3.forward;return new Vector2(forward.x,forward.z);} }
        public float SpeedFactor => FiveStamina.SpeedFactor(IsKeeper ? 1 : Stamina.Value, Dribbling);
        public float MaximumMoveSpeed(FiveProfile profile) => profile.maxSpeed * (IsKeeper ? profile.keeperSpeedMultiplier : 1) * SpeedFactor;
        public float MaximumTurnRate(FiveProfile profile) => profile.turnRate * (Dribbling ? profile.dribbleTurnMultiplier : 1) * (Evading ? 2 : 1);

        public void SetTarget(Vector2 target, Vector2 facing, bool allowFreeFacing = false)
        {
            if (!float.IsFinite(target.x) || !float.IsFinite(target.y)) throw new System.ArgumentOutOfRangeException();
            Target = target; Facing = facing; freeFacing = allowFreeFacing; KeeperFrontApproach = false;
        }
        public void LimitTarget(Vector2 target) { Target=target; }
        public void RequireKeeperFrontApproach()
        {
            if (!IsKeeper) return;
            KeeperFrontApproach = true; freeFacing = true;
        }

        void CacheKickPlate()
        {
            if (plateFaces == null)
            {
                var plates = System.Array.FindAll(GetComponentsInChildren<BoxCollider>(true),
                    c => c.name == "KickPlateCenter" || c.name == "KickPlateLeftWing" || c.name == "KickPlateRightWing");
                if (plates.Length != 3 && !(plates.Length == 1 && plates[0].name == "KickPlateCenter"))
                    throw new System.InvalidOperationException("Expected one robot plate or three legacy kick plate surfaces.");
                singlePlate=plates.Length==1;
                plateFaces = new Vector3[plates.Length]; plateNormals = new Vector3[plates.Length];
                for (var i = 0; i < plates.Length; i++)
                {
                    var plate = plates[i];
                    plateFaces[i] = transform.InverseTransformPoint(plate.transform.TransformPoint(plate.center + Vector3.forward * plate.size.z * .5f));
                    plateNormals[i] = transform.InverseTransformDirection(plate.transform.forward).normalized;
                    if(plate.name=="KickPlateCenter"){plateCenterFace=plateFaces[i].z;centerPlate=plate;}
                    for (var x = -1; x <= 1; x += 2) for (var z = -1; z <= 1; z += 2)
                    {
                        var corner = plate.transform.TransformPoint(plate.center + Vector3.Scale(plate.size, new Vector3(x, 0, z)) * .5f);
                        plateMouth = Mathf.Max(plateMouth, transform.InverseTransformPoint(corner).z);
                        plateHalfWidth=Mathf.Max(plateHalfWidth,Mathf.Abs(transform.InverseTransformPoint(corner).x));
                        plateRotationRadius=Mathf.Max(plateRotationRadius,
                            new Vector2(corner.x-transform.position.x,corner.z-transform.position.z).magnitude);
                    }
                }
            }
        }
        public float CloseHoldOffset(float ballRadius,float gap)
        {
            return CloseHoldOffset(ballRadius,gap,ballRadius);
        }
        public float CloseHoldOffset(float ballRadius,float gap,float ballCenterY)
        {
            CacheKickPlate();
            if(!singlePlate)return plateCenterFace*Mathf.Abs(transform.lossyScale.z)+ballRadius+gap;
            // A low plate meets the lower hemisphere, not the ball's equator.
            var low=centerPlate.transform.TransformPoint(centerPlate.center-Vector3.up*centerPlate.size.y*.5f).y;
            var high=centerPlate.transform.TransformPoint(centerPlate.center+Vector3.up*centerPlate.size.y*.5f).y;
            var dy=ballCenterY-Mathf.Clamp(ballCenterY,Mathf.Min(low,high),Mathf.Max(low,high));
            var horizontalRadius=Mathf.Sqrt(Mathf.Max(0,ballRadius*ballRadius-dy*dy));
            return plateCenterFace*Mathf.Abs(transform.lossyScale.z)+horizontalRadius+gap;
        }
        public float BallRotationClearance(float ballRadius)
        {
            CacheKickPlate();return Mathf.Max(radius,plateRotationRadius)+ballRadius+.15f;
        }
        public bool FrontReceptionPath(Vector3 ballPosition,Vector2 relativeVelocity,float seconds,float ballRadius,float tolerance)
        {
            CacheKickPlate();
            if(Vector2.Dot(relativeVelocity,Forward)>=-.5f)return false;
            var end=ballPosition+new Vector3(relativeVelocity.x,0,relativeVelocity.y)*seconds;
            var relative=Quaternion.Inverse(Body.rotation)*(end-Body.position);
            // Only trajectories into the mouth; a sideways graze or ball behind the body is not trapped.
            if(Vector2.Dot(new Vector2(ballPosition.x,ballPosition.z)-Position,Forward)<=0
                ||Mathf.Abs(relative.x)>plateHalfWidth*Mathf.Abs(transform.lossyScale.x)-ballRadius*.25f)return false;
            return InsideKickPlate(ballPosition,ballRadius,tolerance)||InsideKickPlate(end,ballRadius,tolerance);
        }
        public bool InsideKickPlate(Vector3 ballPosition, float ballRadius, float contactTolerance)
        {
            CacheKickPlate();
            var scale = transform.lossyScale;
            var relative = Quaternion.Inverse(Body.rotation) * (ballPosition - Body.position);
            var point = new Vector3(relative.x / scale.x, 0, relative.z / scale.z);
            var tolerance = contactTolerance / Mathf.Min(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            if (point.z <= 0 || point.z > plateMouth + (ballRadius + contactTolerance) / Mathf.Abs(scale.z)) return false;
            if(singlePlate && Mathf.Abs(point.x)>plateHalfWidth+tolerance)return false;
            for (var i = 0; i < plateFaces.Length; i++)
            {
                var delta = point - plateFaces[i]; delta.y = 0;
                if (Vector3.Dot(delta, plateNormals[i]) < -tolerance) return false;
            }
            return true;
        }

        public void Drive(float seconds, FiveProfile profile)
        {
            var offset = Target - Position;
            var distance = offset.magnitude;
            var speed = MaximumMoveSpeed(profile) * Mathf.Clamp01(distance / 1.5f);
            var desired = distance > .03f ? offset.normalized * speed : Vector2.zero;
            var look = freeFacing ? Facing - Position : distance > radius ? offset : Facing - Position;
            var manual = manualDrive; manualDrive = false;
            if (manual)
            {
                freeFacing = true;
                desired = Forward * (MaximumMoveSpeed(profile) * manualThrottle);
                var facing = Quaternion.AngleAxis(manualTurn * MaximumTurnRate(profile) * seconds, Vector3.up)
                    * new Vector3(Forward.x, 0, Forward.y);
                look = new Vector2(facing.x, facing.z);
            }
            var turningToCollect = IsKeeper && KeeperFrontApproach
                && look.sqrMagnitude > .001f && Vector2.Angle(Forward, look) > 5;
            if (IsKeeper && KeeperFrontApproach)
                desired = turningToCollect ? Vector2.zero
                    : Forward * speed * Mathf.Clamp01(Vector2.Dot(Forward, offset.normalized));
            if (Dribbling && !freeFacing && !Evading && distance > .03f)
                desired = Forward * speed * Mathf.Clamp01(Vector2.Dot(Forward, offset.normalized));
            var current = Velocity;
            var braking = desired.sqrMagnitude < current.sqrMagnitude || Vector2.Dot(desired, current) < 0;
            // Higher keeper acceleration is needed to overcome the same Rigidbody damping at the higher cap.
            var acceleration=profile.acceleration*(IsKeeper?profile.keeperAccelerationMultiplier:1);
            var next = Vector2.MoveTowards(current, desired, (braking ? profile.deceleration : acceleration) * seconds);
            Body.linearVelocity = new Vector3(next.x, 0, next.y);
            Body.angularVelocity = Vector3.zero;
            SharpDribbleTurn=Dribbling&&look.sqrMagnitude>.001f&&Vector2.Angle(Forward,look)>65;
            DrivenForward=Forward;
            // Stop residual motion before rotating: a moving wing can push a side ball into our goal.
            // The usual physical deceleration is retained; no teleport or instantaneous velocity reset.
            if (turningToCollect && next.sqrMagnitude > .03f * .03f) return;
            if (!freeFacing && distance <= radius && look.sqrMagnitude < radius * radius * 4) return;
            if (look.sqrMagnitude > .001f)
            {
                var rotation=Quaternion.RotateTowards(Body.rotation,
                    Quaternion.LookRotation(new Vector3(look.x,0,look.y)), MaximumTurnRate(profile) * seconds);
                var forward=rotation*Vector3.forward;DrivenForward=new Vector2(forward.x,forward.z);
                Body.MoveRotation(rotation);
            }
        }

        public void ResetPlayer(bool newMatch)
        {
            Body.position = spawn;
            Body.rotation = Quaternion.LookRotation(team == Team.Red ? Vector3.right : Vector3.left);
            Body.linearVelocity = Vector3.zero;
            Body.angularVelocity = Vector3.zero;
            Dribbling = false; SharpDribbleTurn=false; Evading=false; KickReadyAt = 0;
            manualDrive = false;
            if (newMatch && !IsKeeper) Stamina.ResetForMatch();
            SetTarget(Position, Position + (team == Team.Red ? Vector2.right : Vector2.left));
        }
    }
}
