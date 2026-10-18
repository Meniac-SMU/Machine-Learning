using UnityEngine;
using UnityEngine.InputSystem;

namespace MachineLearning.Soccer.Manager5v5.Demo
{
    [DefaultExecutionOrder(-400)]
    public sealed class FiveDemoInput : MonoBehaviour
    {
        public FiveMatch match;
        public bool AllowHuman = true;
        public bool IsHuman => match != null && match.HumanPlayerIndex == 3;
        public void ToggleOwner() { if(AllowHuman && !match.Paused && match.State==FiveMatchState.Playing)match.SetHumanControl(!IsHuman); }
        static float Dead(float v) => Mathf.Abs(v)<.15f?0:Mathf.Clamp(v,-1,1);
        void Update()
        {
            if(match==null||match.Paused||match.State!=FiveMatchState.Playing||!Application.isFocused) { match?.ClearHumanInput(); return; }
            ReadInput(Keyboard.current,Gamepad.current);
        }
        internal void ReadInput(Keyboard k,Gamepad g)
        {
            if(k?.hKey.wasPressedThisFrame==true||g?.buttonNorth.wasPressedThisFrame==true)ToggleOwner();
            if(!IsHuman)return;
            float throttle=(k?.wKey.isPressed==true||k?.upArrowKey.isPressed==true?1:0)-(k?.sKey.isPressed==true||k?.downArrowKey.isPressed==true?1:0);
            float turn=(k?.dKey.isPressed==true||k?.rightArrowKey.isPressed==true?1:0)-(k?.aKey.isPressed==true||k?.leftArrowKey.isPressed==true?1:0);
            if(g!=null)
            {
                var gt=g.rightTrigger.ReadValue()-g.leftTrigger.ReadValue();
                if(Mathf.Abs(gt)>Mathf.Abs(throttle))throttle=gt;
                if(Mathf.Abs(g.leftStick.x.ReadValue())>Mathf.Abs(turn))turn=g.leftStick.x.ReadValue();
            }
            match.SetHumanInput(Dead(throttle),Dead(turn),k?.eKey.wasPressedThisFrame==true||k?.digit1Key.wasPressedThisFrame==true||k?.numpad1Key.wasPressedThisFrame==true||g?.buttonSouth.wasPressedThisFrame==true,
                k?.spaceKey.wasPressedThisFrame==true||k?.digit0Key.wasPressedThisFrame==true||k?.numpad0Key.wasPressedThisFrame==true||g?.buttonEast.wasPressedThisFrame==true);
        }
        void OnDisable()=>match?.ClearHumanInput();
    }
}
