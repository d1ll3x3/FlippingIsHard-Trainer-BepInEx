using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;

namespace FlippingIsHardTrainer
{
    public enum InputDeviceType { Keyboard, Gamepad }

    // Tracks the last input device the player actually used, so binds and the HUD
    // can switch automatically between keyboard and gamepad (like the base game).
    public static class InputDeviceTracker
    {
        private const float ACTIVITY_DEADZONE = 0.5f;

        public static InputDeviceType Current { get; private set; } = InputDeviceType.Keyboard;
        public static bool IsDualShock { get; private set; } = false;

        public static void Update()
        {
            var gp = Gamepad.current;
            bool gpActive = gp != null && (
                AnyGamepadButtonDown(gp) ||
                StickBeyond(gp.leftStick) || StickBeyond(gp.rightStick) ||
                gp.rightTrigger.ReadValue() > ACTIVITY_DEADZONE ||
                gp.leftTrigger.ReadValue() > ACTIVITY_DEADZONE);

            if (gpActive)
            {
                Current = InputDeviceType.Gamepad;
                IsDualShock = IsPlayStation(gp);
                return;
            }

            // Switch back to PC ONLY on a real keyboard key (InputSystem Keyboard excludes
            // gamepad buttons) or a mouse click. Mouse MOVEMENT does not switch — otherwise
            // looking around with the right stick (routed to mouse-look) would flip to PC.
            bool kbActive =
                (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) ||
                Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2);
            if (kbActive)
                Current = InputDeviceType.Keyboard;
        }

        private static bool StickBeyond(StickControl s)
        {
            var v = s.ReadValue();
            return Mathf.Abs(v.x) > ACTIVITY_DEADZONE || Mathf.Abs(v.y) > ACTIVITY_DEADZONE;
        }

        private static bool AnyGamepadButtonDown(Gamepad gp)
        {
            foreach (GamepadButton btn in System.Enum.GetValues(typeof(GamepadButton)))
            {
                if (gp[btn].wasPressedThisFrame) return true;
            }
            return false;
        }

        private static bool IsPlayStation(Gamepad gp)
        {
            string name = gp.GetType().Name;
            return name.Contains("DualShock") || name.Contains("DualSense");
        }
    }

    public class InputHandler
    {
        private System.Collections.Generic.Dictionary<string, bool> _wasPressed = new System.Collections.Generic.Dictionary<string, bool>();

        private const float STICK_DEADZONE = 0.3f;

        public void Update() { }

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

        // Gamepad buttons usable as combo modifiers (shoulders + triggers).
        private static readonly GamepadButton[] GamepadModifiers =
        {
            GamepadButton.LeftShoulder, GamepadButton.RightShoulder,
            GamepadButton.LeftTrigger, GamepadButton.RightTrigger
        };

        private bool IsAnyGamepadModifierHeld(Gamepad gp, GamepadButton exclude)
        {
            foreach (var mod in GamepadModifiers)
            {
                if (mod == exclude) continue;
                if (gp[mod].isPressed) return true;
            }
            return false;
        }

        private bool KeyboardComboPressed(KeyBind bind)
        {
            if (!bind.HasKeyboard) return false;

            bool isMainHeld = Input.GetKey(bind.MainKey);
            bool isModHeld = IsModifierHeld(bind.Modifier);

            // Si el bind exige modificador, tiene que estar pulsado.
            // Si no exige modificador, NO debe haber ningún modificador pulsado para evitar conflictos.
            bool modValid = (bind.Modifier == KeyCode.None) ? !IsAnyModifierHeld() : isModHeld;

            return isMainHeld && modValid;
        }

        private bool GamepadComboPressed(KeyBind bind)
        {
            var gp = Gamepad.current;
            if (gp == null || !bind.HasGamepad) return false;

            bool mainHeld = gp[bind.GamepadBtn.Value].isPressed;
            bool modValid = bind.GamepadModifier.HasValue
                ? gp[bind.GamepadModifier.Value].isPressed
                : !IsAnyGamepadModifierHeld(gp, bind.GamepadBtn.Value);

            return mainHeld && modValid;
        }

        private bool CheckBindPressed(string bindId, KeyBind bind)
        {
            if (bind == null) return false;

            // Only the active device fires its binding.
            bool isCurrentlyPressed = InputDeviceTracker.Current == InputDeviceType.Gamepad
                ? GamepadComboPressed(bind)
                : KeyboardComboPressed(bind);

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

        // Fly mode movement — keyboard + gamepad left stick / triggers
        public bool IsWPressed() => Input.GetKey(KeyCode.W) || GetLeftStickY() > STICK_DEADZONE;
        public bool IsSPressed() => Input.GetKey(KeyCode.S) || GetLeftStickY() < -STICK_DEADZONE;
        public bool IsAPressed() => Input.GetKey(KeyCode.A) || GetLeftStickX() < -STICK_DEADZONE;
        public bool IsDPressed() => Input.GetKey(KeyCode.D) || GetLeftStickX() > STICK_DEADZONE;
        public bool IsSpacePressed() => Input.GetKey(KeyCode.Space) || GetRightTrigger() > STICK_DEADZONE;
        public bool IsCtrlPressed() => Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl) || GetLeftTrigger() > STICK_DEADZONE;
        // Fly turbo: Shift (keyboard) or B / Circle (gamepad)
        public bool IsShiftPressed() => Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) || (Gamepad.current?[GamepadButton.East].isPressed == true);

        private float GetLeftStickX() => Gamepad.current?.leftStick.x.ReadValue() ?? 0f;
        private float GetLeftStickY() => Gamepad.current?.leftStick.y.ReadValue() ?? 0f;
        private float GetRightTrigger() => Gamepad.current?.rightTrigger.ReadValue() ?? 0f;
        private float GetLeftTrigger() => Gamepad.current?.leftTrigger.ReadValue() ?? 0f;

        public bool IsAnyMovementKeyPressed()
        {
            return IsWPressed() || IsSPressed() || IsAPressed() || IsDPressed() ||
                   IsSpacePressed() || IsCtrlPressed();
        }
    }
}