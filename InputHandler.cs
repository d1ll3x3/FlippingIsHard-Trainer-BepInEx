using UnityEngine;

namespace FlippingIsHardTrainer
{
    public class InputHandler
    {
        // Replaced static keys with dictionary of pressed states
        private System.Collections.Generic.Dictionary<string, bool> _wasPressed = new System.Collections.Generic.Dictionary<string, bool>();

        public void Update()
        {
            // Only update logic is needed here for other things, but our actions will be checked dynamically.
        }

        private bool IsModifierHeld(KeyCode modifier)
        {
            if (modifier == KeyCode.None) return false;
            if (modifier == KeyCode.LeftShift || modifier == KeyCode.RightShift)
                return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            if (modifier == KeyCode.LeftControl || modifier == KeyCode.RightControl)
                return Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            if (modifier == KeyCode.LeftAlt || modifier == KeyCode.RightAlt)
                return Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
            
            return Input.GetKey(modifier);
        }

        private bool IsAnyModifierHeld()
        {
            return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) ||
                   Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl) ||
                   Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
        }

        private bool CheckBindPressed(string bindId, KeyBind bind)
        {
            if (bind == null || bind.MainKey == KeyCode.None) return false;

            bool isMainHeld = Input.GetKey(bind.MainKey);
            bool isModHeld = IsModifierHeld(bind.Modifier);
            
            // Si el bind exige modificador, tiene que estar pulsado. 
            // Si no exige modificador, NO debe haber ningún modificador pulsado para evitar conflictos.
            bool modValid = (bind.Modifier == KeyCode.None) ? !IsAnyModifierHeld() : isModHeld;

            bool isCurrentlyPressed = isMainHeld && modValid;

            if (!_wasPressed.ContainsKey(bindId)) _wasPressed[bindId] = false;

            bool justPressed = isCurrentlyPressed && !_wasPressed[bindId];
            _wasPressed[bindId] = isCurrentlyPressed;

            return justPressed;
        }

        public bool IsSavePositionPressed() => CheckBindPressed("Save", TrainerConfig.Settings.SavePosition);
        public bool IsTeleportPressed() => CheckBindPressed("Teleport", TrainerConfig.Settings.Teleport);
        public bool IsToggleFlyModePressed() => CheckBindPressed("Fly", TrainerConfig.Settings.ToggleFlyMode);
        public bool IsToggleKeepVelocityPressed() => CheckBindPressed("Vel", TrainerConfig.Settings.ToggleKeepVelocity);
        public bool IsToggleKeepAnglePressed() => CheckBindPressed("Angle", TrainerConfig.Settings.ToggleKeepAngle);
        public bool IsOpenBindMenuPressed() => CheckBindPressed("Bind", TrainerConfig.Settings.OpenBindMenu);
        
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