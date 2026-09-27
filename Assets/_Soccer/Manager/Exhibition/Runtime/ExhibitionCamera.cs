using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;
using Unity.Cinemachine.TargetTracking;

namespace MachineLearning.Soccer.Manager.Exhibition
{
    [DefaultExecutionOrder(100)]
    [RequireComponent(typeof(Camera))]
    public sealed class ExhibitionCamera : MonoBehaviour
    {
        public enum View { RedKeeper, Broadcast, NavyKeeper, RedStriker }
        [SerializeField] float mouseSensitivity=.18f;
        [SerializeField] float maximumYaw=30f;
        [SerializeField] float recenterDelay=1.5f;
        [SerializeField] float recenterSeconds=.3f;
        [SerializeField] float followDamping=.2f;
        MNG_HumanInput human;
        MNG_MatchController match;
        CinemachineBrain brain;
        readonly CinemachineCamera[] views=new CinemachineCamera[4];
        readonly CinemachineFollow[] followers=new CinemachineFollow[4];
        static readonly Vector3 FollowOffset=new Vector3(0,3.4f,-7.5f);
        readonly Transform[] targets=new Transform[4];
        readonly Vector3[] previousPositions=new Vector3[4];
        int aiView=1;
        float yaw,idle,yawVelocity;
        bool wasHuman;
        public bool InputEnabled { get; set; } = true;
        public View CurrentView { get; private set; } = View.Broadcast;
        public float MouseYaw => yaw;
        public bool IsFollowingHuman => CurrentView==View.RedStriker;
        public bool IsTransitioning => brain!=null&&brain.IsBlending;
        public CinemachineBrain Brain => brain;
        public CinemachineCamera ActiveCamera => views[(int)CurrentView];

        void Awake()
        {
            match=GetComponentInParent<MNG_MatchController>();
            human=match.GetComponentInChildren<MNG_HumanInput>(true);
            var output=GetComponent<Camera>();
            brain=gameObject.AddComponent<CinemachineBrain>();
            brain.UpdateMethod=CinemachineBrain.UpdateMethods.ManualUpdate;
            brain.IgnoreTimeScale=true;
            brain.DefaultBlend=new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut,0);
            var rig=new GameObject("Exhibition Cinemachine Views").transform;
            rig.SetParent(match.transform,false);
            for(int i=0;i<views.Length;i++)
            {
                var go=new GameObject(((View)i).ToString());go.transform.SetParent(rig,false);
                go.transform.SetPositionAndRotation(output.transform.position,output.transform.rotation);
                var camera=go.AddComponent<CinemachineCamera>();views[i]=camera;
                camera.Lens.FieldOfView=i==1?output.fieldOfView:62;
                camera.Lens.NearClipPlane=.1f;camera.Lens.FarClipPlane=output.farClipPlane;
                camera.Priority=i==1?20:0;
                if(i==1)continue;
                targets[i]=match.GetPlayerAvatar(i==2?Team.Navy:Team.Red,i==3?3:0).transform;
                previousPositions[i]=targets[i].position;
                camera.Follow=targets[i];
                var follow=go.AddComponent<CinemachineFollow>();followers[i]=follow;
                follow.FollowOffset=FollowOffset;
                follow.TrackerSettings.BindingMode=BindingMode.LockToTargetWithWorldUp;
                follow.TrackerSettings.PositionDamping=Vector3.one*followDamping;
                follow.TrackerSettings.RotationDamping=Vector3.zero;
            }
        }

        void LateUpdate()
        {
            bool active=InputEnabled&&Time.timeScale>0&&match.State!=MNG_MatchState.Finished;
            bool isHuman=human.IsHuman;
            SetCursor(active&&isHuman&&Application.isFocused);
            if(!active)return;
            if(isHuman!=wasHuman)
            {
                yaw=idle=yawVelocity=0;wasHuman=isHuman;
                Select(isHuman?View.RedStriker:(View)aiView,true);
            }
            if(isHuman)
            {
                float dx=Application.isFocused?(Mouse.current?.delta.x.ReadValue()??0):0;
                if(Mathf.Abs(dx)>.01f){yaw=Mathf.Clamp(yaw+dx*mouseSensitivity,-maximumYaw,maximumYaw);idle=0;yawVelocity=0;}
                else
                {
                    idle+=Time.unscaledDeltaTime;
                    if(idle>=recenterDelay)yaw=Mathf.SmoothDamp(yaw,0,ref yawVelocity,recenterSeconds,Mathf.Infinity,Time.unscaledDeltaTime);
                }
            }
            else
            {
                int direction=(Keyboard.current?.rightArrowKey.wasPressedThisFrame==true?1:0)
                    -(Keyboard.current?.leftArrowKey.wasPressedThisFrame==true?1:0);
                if(direction!=0){aiView=(aiView+direction+3)%3;Select((View)aiView,false);}
            }
            for(int i=0;i<views.Length;i++)
            {
                if(targets[i]==null)continue;
                if(Vector3.Distance(previousPositions[i],targets[i].position)>12)views[i].PreviousStateIsValid=false;
                previousPositions[i]=targets[i].position;
                var forward=Vector3.ProjectOnPlane(targets[i].forward,Vector3.up).normalized;
                var direction=forward*10.1f-Vector3.up*2.3f;
                if(i==3)
                {
                    // Orbit the position and viewing direction together around the player's vertical axis.
                    var orbit=Quaternion.AngleAxis(yaw,Vector3.up);
                    followers[i].FollowOffset=orbit*FollowOffset;
                    direction=orbit*direction;
                }
                views[i].transform.rotation=Quaternion.LookRotation(direction,Vector3.up);
            }
            brain.ManualUpdate();
        }
        void Select(View view,bool blend)
        {
            CurrentView=view;
            brain.DefaultBlend=new CinemachineBlendDefinition(blend?CinemachineBlendDefinition.Styles.EaseInOut:CinemachineBlendDefinition.Styles.Cut,blend ? .8f : 0);
            if(!blend)views[(int)view].PreviousStateIsValid=false;
            for(int i=0;i<views.Length;i++)views[i].Priority=i==(int)view?20:0;
        }
        static void SetCursor(bool captured)
        {
            var mode=captured?CursorLockMode.Locked:CursorLockMode.None;
            if(Cursor.lockState!=mode)Cursor.lockState=mode;
            Cursor.visible=!captured;
        }
        void OnDisable()=>SetCursor(false);
    }
}
