using UnityEngine;

namespace FlippingIsHardTrainer
{
    public class InputHandler
    {
        // Key state tracking
        private bool _rKeyDown = false;
        private bool _shiftKeyDown = false;
        private bool _fKeyDown = false;
        
        // Key press detection (to avoid holding)
        private bool _rKeyPressed = false;
        private bool _shiftRCombinationPressed = false;
        private bool _fKeyPressed = false;
        
        public void Update()
        {
            // Update key states
            bool currentRKey = UnityEngine.Input.GetKey(UnityEngine.KeyCode.R);
            bool currentShiftKey = UnityEngine.Input.GetKey(UnityEngine.KeyCode.LeftShift) || UnityEngine.Input.GetKey(UnityEngine.KeyCode.RightShift);
            bool currentFKey = UnityEngine.Input.GetKey(UnityEngine.KeyCode.F);
            
            // Detect key presses (transition from not pressed to pressed)
            _rKeyPressed = currentRKey && !_rKeyDown;
            _fKeyPressed = currentFKey && !_fKeyDown;
            
            // Detect Shift+R combination
            if (currentShiftKey && _rKeyPressed)
            {
                _shiftRCombinationPressed = true;
            }
            else
            {
                _shiftRCombinationPressed = false;
            }
            
            // Update key down states
            _rKeyDown = currentRKey;
            _shiftKeyDown = currentShiftKey;
            _fKeyDown = currentFKey;
        }
        
        public bool IsSavePositionPressed()
        {
            return _shiftRCombinationPressed;
        }
        
        public bool IsTeleportPressed()
        {
            return _rKeyPressed && !_shiftKeyDown;
        }
        
        public bool IsToggleFlyModePressed()
        {
            return _fKeyPressed;
        }
        
        // Fly mode movement keys
        public bool IsWPressed() => UnityEngine.Input.GetKey(UnityEngine.KeyCode.W);
        public bool IsSPressed() => UnityEngine.Input.GetKey(UnityEngine.KeyCode.S);
        public bool IsAPressed() => UnityEngine.Input.GetKey(UnityEngine.KeyCode.A);
        public bool IsDPressed() => UnityEngine.Input.GetKey(UnityEngine.KeyCode.D);
        public bool IsSpacePressed() => UnityEngine.Input.GetKey(UnityEngine.KeyCode.Space);
        public bool IsCtrlPressed() => UnityEngine.Input.GetKey(UnityEngine.KeyCode.LeftControl) || UnityEngine.Input.GetKey(UnityEngine.KeyCode.RightControl);
        public bool IsShiftPressed() => UnityEngine.Input.GetKey(UnityEngine.KeyCode.LeftShift) || UnityEngine.Input.GetKey(UnityEngine.KeyCode.RightShift);
        
        // Utility methods
        public bool IsAnyMovementKeyPressed()
        {
            return IsWPressed() || IsSPressed() || IsAPressed() || IsDPressed() || 
                   IsSpacePressed() || IsCtrlPressed();
        }
    }
}