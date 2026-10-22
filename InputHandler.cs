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

        // Clear edge-detection state so the first frame after regaining window focus
        // doesn't register a stale "just pressed" (e.g. Shift held during Shift+Tab).
        public void ResetEdges() => _wasPressed.Clear();

        // Left/right variants of Shift, Ctrl and Alt count as the same key.
        private bool IsKeyHeld(KeyCode key)
        {
            if (key == KeyCode.None) return false;
            if (key == KeyCode.LeftShift || key == KeyCode.RightShift)
                return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            if (key == KeyCode.LeftControl || key == KeyCode.RightControl)
                return Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            if (key == KeyCode.LeftAlt || key == KeyCode.RightAlt)
                return Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);

            return Input.GetKey(key);
        }

        // All trainer binds, so a no-modifier bind can check for a real competing combo
        // (same main key/button + a modifier currently held) instead of blocking on ANY
        // modifier. This lets e.g. F (Fly) fire while Shift is held for turbo, yet keeps
        // Save (Shift+R) winning over Teleport (R) when Shift is down.
        private static KeyBind[] AllBinds()
        {
            var s = TrainerConfig.Settings;
            return new[] { s.SavePosition, s.Teleport, s.ToggleFlyMode,
                           s.ToggleKeepVelocity, s.ToggleKeepAngle, s.ToggleAutoRepair,
                           s.ToggleHelpBeam, s.OpenBindMenu };
        }

        // True if another bind shares this bind's main key but requires a modifier that is
        // currently held — that combo takes priority, so this no-modifier bind is suppressed.
        private bool KeyboardComboConflict(KeyBind bind)
        {
            foreach (var other in AllBinds())
            {
                if (other == bind) continue;
                if (other.MainKey == bind.MainKey && other.Modifier != KeyCode.None
                    && IsKeyHeld(other.Modifier))
                    return true;
            }
            return false;
        }

        // Gamepad equivalent: another bind shares this button but its modifier is held now.
        private bool GamepadComboConflict(Gamepad gp, KeyBind bind)
        {
            foreach (var other in AllBinds())
            {
                if (other == bind) continue;
                if (other.GamepadBtn == bind.GamepadBtn && other.GamepadModifier.HasValue
                    && gp[other.GamepadModifier.Value].isPressed)
                    return true;
            }
            return false;
        }

        private bool KeyboardComboPressed(KeyBind bind)
        {
            if (!bind.HasKeyboard) return false;

            bool isMainHeld = Input.GetKey(bind.MainKey);
            bool isModHeld = IsKeyHeld(bind.Modifier);

            // Si el bind exige modificador, tiene que estar pulsado. Si no exige modificador,
            // solo se suprime cuando hay un combo real en conflicto (misma tecla + su
            // modificador pulsado), no ante cualquier modificador suelto.
            bool modValid = (bind.Modifier == KeyCode.None) ? !KeyboardComboConflict(bind) : isModHeld;

            return isMainHeld && modValid;
        }

        private bool GamepadComboPressed(KeyBind bind)
        {
            var gp = Gamepad.current;
            if (gp == null || !bind.HasGamepad) return false;

            bool mainHeld = gp[bind.GamepadBtn.Value].isPressed;
            bool modValid = bind.GamepadModifier.HasValue
                ? gp[bind.GamepadModifier.Value].isPressed
                : !GamepadComboConflict(gp, bind);

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
        public bool IsToggleAutoRepairPressed() => CheckBindPressed("Repair", TrainerConfig.Settings.ToggleAutoRepair);
        public bool IsToggleHelpBeamPressed() => CheckBindPressed("Beam", TrainerConfig.Settings.ToggleHelpBeam);
        public bool IsOpenBindMenuPressed() => CheckBindPressed("Bind", TrainerConfig.Settings.OpenBindMenu);

        // Fly mode movement — held binds from either device (no active-device gate, so you
        // can mix stick + keyboard), plus the gamepad left stick for analog directions.
        public bool IsFlyForwardHeld() => IsBindHeld(TrainerConfig.Settings.FlyForward) || GetLeftStickY() > STICK_DEADZONE;
        public bool IsFlyBackwardHeld() => IsBindHeld(TrainerConfig.Settings.FlyBackward) || GetLeftStickY() < -STICK_DEADZONE;
        public bool IsFlyLeftHeld() => IsBindHeld(TrainerConfig.Settings.FlyLeft) || GetLeftStickX() < -STICK_DEADZONE;
        public bool IsFlyRightHeld() => IsBindHeld(TrainerConfig.Settings.FlyRight) || GetLeftStickX() > STICK_DEADZONE;
        public bool IsFlyUpHeld() => IsBindHeld(TrainerConfig.Settings.FlyUp);
        public bool IsFlyDownHeld() => IsBindHeld(TrainerConfig.Settings.FlyDown);
        public bool IsFlyTurboHeld() => IsBindHeld(TrainerConfig.Settings.FlyTurbo);

        private bool IsBindHeld(KeyBind bind)
        {
            if (bind == null) return false;

            bool kb = bind.HasKeyboard && IsKeyHeld(bind.MainKey)
                      && (bind.Modifier == KeyCode.None || IsKeyHeld(bind.Modifier));
            if (kb) return true;

            var gp = Gamepad.current;
            return gp != null && bind.HasGamepad && gp[bind.GamepadBtn.Value].isPressed
                   && (!bind.GamepadModifier.HasValue || gp[bind.GamepadModifier.Value].isPressed);
        }

        private float GetLeftStickX() => Gamepad.current?.leftStick.x.ReadValue() ?? 0f;
        private float GetLeftStickY() => Gamepad.current?.leftStick.y.ReadValue() ?? 0f;
    }
}