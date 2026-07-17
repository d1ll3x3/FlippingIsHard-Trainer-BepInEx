using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace FlippingIsHardTrainer
{
    public class BindMenuRenderer
    {
        private GameObjectFinder _gameObjectFinder;
        private List<MonoBehaviour> _disabledScripts = new List<MonoBehaviour>();

        private bool _isVisible = false;
        private Rect _windowRect = new Rect(Screen.width / 2 - 250, Screen.height / 2 - 220, 500, 440);

        private GUIStyle _titleStyle;
        private bool _stylesReady = false;

        private Color _defaultBgColor;

        private TrainerSettings _tempSettings;
        
        // Listening state
        private string _listeningAction = null;
        private bool _clickHandledThisFrame = false;
        private GUI.WindowFunction _windowDelegate;

        // Gamepad navigation state
        private int _selectedIndex = 0;
        private bool _wasNavUp    = false;
        private bool _wasNavDown  = false;
        private bool _wasNavLeft  = false;
        private bool _wasNavRight = false;
        private const int NAV_ITEM_COUNT = 12; // 8 bind rows + HUD Scale + Reset + Cancel + Save
        private int _lastGpFrame = -1;
        private GamepadButton? _pendingGpModifier = null; // shoulder/trigger held while capturing a combo

        // Gamepad buttons that can act as combo modifiers (shoulders + triggers)
        private static readonly GamepadButton[] GpModifiers =
        {
            GamepadButton.LeftShoulder, GamepadButton.RightShoulder,
            GamepadButton.LeftTrigger, GamepadButton.RightTrigger
        };

        // Canonical button set (no enum aliases like A/Cross) used for capture/iteration.
        private static readonly GamepadButton[] AllButtons =
        {
            GamepadButton.South, GamepadButton.East, GamepadButton.North, GamepadButton.West,
            GamepadButton.LeftShoulder, GamepadButton.RightShoulder,
            GamepadButton.LeftTrigger, GamepadButton.RightTrigger,
            GamepadButton.Start, GamepadButton.Select,
            GamepadButton.LeftStick, GamepadButton.RightStick,
            GamepadButton.DpadUp, GamepadButton.DpadDown, GamepadButton.DpadLeft, GamepadButton.DpadRight
        };

        public static bool IsVisibleGlobally = false;
        public bool IsVisible => _isVisible;
        public Action OnMenuClosed;

        public BindMenuRenderer(GameObjectFinder gameObjectFinder)
        {
            _gameObjectFinder = gameObjectFinder;
            _windowDelegate = new Action<int>(WindowFunction);
        }

        public void ToggleVisibility()
        {
            if (_isVisible)
            {
                CloseMenu();
            }
            else
            {
                _isVisible = true;
                IsVisibleGlobally = true;
                
                // Disable player and camera scripts to prevent game interaction
                DisableGameScripts();

                // Disable New Input System devices
                try {
                    if (UnityEngine.InputSystem.Keyboard.current != null)
                        UnityEngine.InputSystem.InputSystem.DisableDevice(UnityEngine.InputSystem.Keyboard.current);
                    if (UnityEngine.InputSystem.Mouse.current != null)
                        UnityEngine.InputSystem.InputSystem.DisableDevice(UnityEngine.InputSystem.Mouse.current);
                } catch { }
                
                // Clone settings for editing (keeps Version so SAVE doesn't reset config)
                _tempSettings = CloneSettings(TrainerConfig.Settings);
                _listeningAction = null;
            }
        }

        private void InitStyles()
        {
            if (_stylesReady) return;

            _titleStyle = new GUIStyle();
            _titleStyle.normal.textColor = new Color(0.5f, 0.8f, 1f);
            _titleStyle.fontSize = 20;
            _titleStyle.fontStyle = FontStyle.Bold;
            _titleStyle.alignment = TextAnchor.MiddleCenter;

            _defaultBgColor = GUI.backgroundColor;
            _stylesReady = true;
        }

        public void Draw()
        {
            if (!_isVisible) return;

            if (Event.current.type == EventType.Repaint)
            {
                _clickHandledThisFrame = false;
            }

            // Force cursor to be visible and unlocked while menu is open
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            InitStyles();

            // Gamepad input — processed ONCE per frame. IMGUI calls Draw() multiple times
            // per frame (Layout + Repaint), but wasPressedThisFrame stays true across all
            // those passes; without this guard a single press would both activate listening
            // AND get captured (or cancel AND close) in the same frame.
            var gp = Gamepad.current;
            if (gp != null && Time.frameCount != _lastGpFrame)
            {
                _lastGpFrame = Time.frameCount;
                HandleGamepadInput(gp);
            }

            // Handle keyboard / mouse listening input
            if (_listeningAction != null)
            {
                Event e = Event.current;

                // Use raw Input for escape to guarantee it works even if events are swallowed
                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    _listeningAction = null;
                    if (e.type != EventType.Repaint && e.type != EventType.Layout) e.Use();
                }
                else if (e.type == EventType.KeyDown || e.type == EventType.MouseDown)
                {
                    KeyCode key = KeyCode.None;
                    if (e.type == EventType.KeyDown)
                    {
                        key = e.keyCode;
                    }
                    else if (e.type == EventType.MouseDown)
                    {
                        if (e.button == 0) key = KeyCode.Mouse0;
                        else if (e.button == 1) key = KeyCode.Mouse1;
                        else if (e.button == 2) key = KeyCode.Mouse2;
                        else if (e.button == 3) key = KeyCode.Mouse3;
                        else if (e.button == 4) key = KeyCode.Mouse4;
                        else if (e.button == 5) key = KeyCode.Mouse5;
                        else if (e.button == 6) key = KeyCode.Mouse6;
                    }

                    // Ignore None and Main Mouse buttons (Left/Right click)
                    if (key != KeyCode.None && key != KeyCode.Mouse0 && key != KeyCode.Mouse1 && key != KeyCode.Escape)
                    {
                        // Ignore pure modifier keys as main key (we catch them as modifiers)
                        if (key != KeyCode.LeftShift && key != KeyCode.RightShift &&
                            key != KeyCode.LeftControl && key != KeyCode.RightControl &&
                            key != KeyCode.LeftAlt && key != KeyCode.RightAlt)
                        {
                            KeyCode mod = KeyCode.None;
                            if (e.shift || Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) mod = KeyCode.LeftShift;
                            else if (e.control || Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) mod = KeyCode.LeftControl;
                            else if (e.alt || Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt)) mod = KeyCode.LeftAlt;

                            AssignKey(_listeningAction, key, mod);
                            _listeningAction = null;
                            e.Use();
                        }
                    }
                }
            }

            GUI.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 1f);
            _windowRect = GUI.Window(8493, _windowRect, _windowDelegate, "TRAINER KEYBINDS");
            GUI.backgroundColor = _defaultBgColor;
        }

        private void HandleGamepadInput(Gamepad gp)
        {
            if (_listeningAction != null)
            {
                // Circle / B cancels capture. The once-per-frame guard prevents this same
                // press from also closing the menu in a later pass of the frame.
                if (gp[GamepadButton.East].wasPressedThisFrame)
                {
                    FinishListening();
                    return;
                }

                // Combo-aware capture — ANY held button can be a modifier:
                //  - 2 buttons the same frame  → combo (prefer a shoulder/trigger as modifier).
                //  - 1 button while another is still held → combo (held = modifier).
                //  - 1 button alone → deferred; becomes a single bind on release, or the
                //    modifier if a second button arrives while it is held.
                GamepadButton? first = null, second = null;
                foreach (var btn in AllButtons)
                {
                    if (btn == GamepadButton.East) continue; // reserved for cancel
                    if (!gp[btn].wasPressedThisFrame) continue;
                    if (first == null) first = btn;
                    else { second = btn; break; }
                }

                if (first.HasValue && second.HasValue)
                {
                    GamepadButton main, mod;
                    if (IsGpModifier(first.Value) && !IsGpModifier(second.Value)) { mod = first.Value; main = second.Value; }
                    else if (IsGpModifier(second.Value) && !IsGpModifier(first.Value)) { mod = second.Value; main = first.Value; }
                    else { mod = first.Value; main = second.Value; }
                    AssignGamepadButton(_listeningAction, main, mod);
                    FinishListening();
                    return;
                }

                if (first.HasValue)
                {
                    if (_pendingGpModifier.HasValue && _pendingGpModifier.Value != first.Value &&
                        gp[_pendingGpModifier.Value].isPressed)
                    {
                        AssignGamepadButton(_listeningAction, first.Value, _pendingGpModifier);
                        FinishListening();
                        return;
                    }
                    _pendingGpModifier = first.Value; // defer
                    return;
                }

                // Deferred lone button released without a second button → single bind.
                if (_pendingGpModifier.HasValue && gp[_pendingGpModifier.Value].wasReleasedThisFrame)
                {
                    AssignGamepadButton(_listeningAction, _pendingGpModifier.Value, null);
                    FinishListening();
                }
                return;
            }

            // Navigation (not listening)
            bool navUp    = gp.dpad.up.isPressed    || gp.leftStick.y.ReadValue() >  0.7f;
            bool navDown  = gp.dpad.down.isPressed  || gp.leftStick.y.ReadValue() < -0.7f;
            bool navLeft  = gp.dpad.left.isPressed  || gp.leftStick.x.ReadValue() < -0.7f;
            bool navRight = gp.dpad.right.isPressed || gp.leftStick.x.ReadValue() >  0.7f;

            if (navUp   && !_wasNavUp)   _selectedIndex = (_selectedIndex - 1 + NAV_ITEM_COUNT) % NAV_ITEM_COUNT;
            if (navDown && !_wasNavDown) _selectedIndex = (_selectedIndex + 1) % NAV_ITEM_COUNT;
            _wasNavUp   = navUp;
            _wasNavDown = navDown;

            // Left/Right adjusts HUD Scale when that row is selected
            if (_selectedIndex == 8)
            {
                if (navLeft  && !_wasNavLeft)  AdjustTempScale(-0.05f);
                if (navRight && !_wasNavRight) AdjustTempScale(+0.05f);
            }
            _wasNavLeft  = navLeft;
            _wasNavRight = navRight;

            if (gp[GamepadButton.South].wasPressedThisFrame)       // A / Cross — activate
                HandleGamepadActivate(_selectedIndex);
            else if (gp[GamepadButton.East].wasPressedThisFrame)   // B / Circle — close
                CloseMenu();
        }

        private void FinishListening()
        {
            _listeningAction = null;
            _pendingGpModifier = null;
        }

        private static bool IsGpModifier(GamepadButton btn)
        {
            return System.Array.IndexOf(GpModifiers, btn) >= 0;
        }

        private KeyBind GetBind(string action)
        {
            if (_tempSettings == null) return null;
            return action switch
            {
                "Save" => _tempSettings.SavePosition,
                "Teleport" => _tempSettings.Teleport,
                "Fly" => _tempSettings.ToggleFlyMode,
                "Vel" => _tempSettings.ToggleKeepVelocity,
                "Angle" => _tempSettings.ToggleKeepAngle,
                "Repair" => _tempSettings.ToggleAutoRepair,
                "Beam" => _tempSettings.ToggleHelpBeam,
                "Menu" => _tempSettings.OpenBindMenu,
                _ => null
            };
        }

        private void AssignKey(string action, KeyCode main, KeyCode mod)
        {
            var b = GetBind(action);
            if (b == null) return;
            b.MainKey = main;
            b.Modifier = mod;
        }

        private void AssignGamepadButton(string action, GamepadButton main, GamepadButton? mod)
        {
            var b = GetBind(action);
            if (b == null) return;
            b.GamepadBtn = main;
            b.GamepadModifier = mod;
        }

        private void HandleGamepadActivate(int index)
        {
            string[] bindActions = { "Save", "Teleport", "Fly", "Vel", "Angle", "Repair", "Beam", "Menu" };
            if (index < 8)
            {
                _listeningAction = bindActions[index];
                _pendingGpModifier = null;
            }
            else if (index == 8) // HUD Scale — A increments (use ◄ ► to dec/inc)
            {
                AdjustTempScale(+0.05f);
            }
            else if (index == 9) // Reset Defaults
            {
                ResetTempToDefaults();
            }
            else if (index == 10) // Cancel
            {
                CloseMenu();
            }
            else if (index == 11) // Save
            {
                SaveTempSettings();
            }
        }

        private void ResetTempToDefaults()
        {
            TrainerConfig.ResetToDefaults();
            // Re-clone so the menu visuals update immediately without closing.
            _tempSettings = CloneSettings(TrainerConfig.Settings);
        }

        private void SaveTempSettings()
        {
            _tempSettings.Version = TrainerSettings.CURRENT_VERSION; // keep version so it isn't wiped on next load
            TrainerConfig.Settings = _tempSettings;
            TrainerConfig.Save();
            CloseMenu();
        }

        private static TrainerSettings CloneSettings(TrainerSettings src)
        {
            return new TrainerSettings
            {
                Version            = src.Version,
                SavePosition       = src.SavePosition.Clone(),
                Teleport           = src.Teleport.Clone(),
                ToggleFlyMode      = src.ToggleFlyMode.Clone(),
                ToggleKeepVelocity = src.ToggleKeepVelocity.Clone(),
                ToggleKeepAngle    = src.ToggleKeepAngle.Clone(),
                ToggleAutoRepair   = src.ToggleAutoRepair.Clone(),
                ToggleHelpBeam     = src.ToggleHelpBeam.Clone(),
                OpenBindMenu       = src.OpenBindMenu.Clone(),
                OverlayScale       = src.OverlayScale,
                HelpBeamSingleplayer = src.HelpBeamSingleplayer,
                HelpBeamSpawnLevel = src.HelpBeamSpawnLevel,
            };
        }

        private void AdjustTempScale(float delta)
        {
            if (_tempSettings == null) return;
            float next = Mathf.Clamp(_tempSettings.OverlayScale + delta, 0.25f, 2.0f);
            _tempSettings.OverlayScale = Mathf.Round(next * 100f) / 100f;
        }

        private void WindowFunction(int id)
        {
            // Close X button
            GUI.backgroundColor = Color.red;
            if (CustomButton(new Rect(460, 5, 35, 25), "X"))
            {
                CloseMenu();
            }
            GUI.backgroundColor = _defaultBgColor;

            float cy = 40; // Start below the title bar

            DrawBindRow(20, ref cy, "Save Position",       "Save",     _tempSettings.SavePosition,      0);
            DrawBindRow(20, ref cy, "Teleport",            "Teleport", _tempSettings.Teleport,           1);
            DrawBindRow(20, ref cy, "Toggle Fly Mode",     "Fly",      _tempSettings.ToggleFlyMode,      2);
            DrawBindRow(20, ref cy, "Toggle Keep Velocity","Vel",      _tempSettings.ToggleKeepVelocity, 3);
            DrawBindRow(20, ref cy, "Toggle Keep Angle",   "Angle",    _tempSettings.ToggleKeepAngle,    4);
            DrawBindRow(20, ref cy, "Toggle Auto-Repair",  "Repair",   _tempSettings.ToggleAutoRepair,   5);
            DrawBindRow(20, ref cy, "Toggle Help Beam SP", "Beam",     _tempSettings.ToggleHelpBeam,     6);
            DrawBindRow(20, ref cy, "Open Bind Menu",      "Menu",     _tempSettings.OpenBindMenu,       7);

            // ── HUD Scale row ────────────────────────────────────────────
            cy += 4;
            GUI.color = _selectedIndex == 8 ? Color.yellow : Color.white;
            GUI.Label(new Rect(20, cy, 175, 24), "HUD Scale");
            GUI.color = Color.white;
            GUI.backgroundColor = new Color(0.4f, 0.4f, 0.4f, 1f);
            if (CustomButton(new Rect(195, cy, 32, 24), "-"))
                AdjustTempScale(-0.05f);
            GUI.Label(new Rect(232, cy, 55, 24), $"{_tempSettings.OverlayScale:F2}x");
            if (CustomButton(new Rect(292, cy, 32, 24), "+"))
                AdjustTempScale(+0.05f);
            GUI.backgroundColor = _defaultBgColor;
            GUI.color = Color.white;
            cy += 30;

            cy = _windowRect.height - 75; // Bottom row

            // Gamepad hint footer (only while the gamepad is the active device)
            if (Gamepad.current != null && InputDeviceTracker.Current == InputDeviceType.Gamepad)
            {
                bool ps = InputDeviceTracker.IsDualShock;
                string sel = ps ? "Cross" : "A";
                string close = ps ? "Circle" : "B";
                string hint = _selectedIndex == 8
                    ? $"Mando: ◄ ► ajustar escala  |  {close} cancelar/cerrar"
                    : $"Mando: D-Pad navegar  |  {sel} seleccionar  |  {close} cancelar/cerrar";
                GUI.color = new Color(0.7f, 0.9f, 1f);
                GUI.Label(new Rect(20, cy, 460, 20), hint);
                GUI.color = Color.white;
            }

            cy = _windowRect.height - 50;

            GUI.color = _selectedIndex == 9 ? Color.yellow : Color.white;
            if (CustomButton(new Rect(20, cy, 140, 30), "Reset Defaults"))
            {
                ResetTempToDefaults();
            }
            GUI.color = _selectedIndex == 10 ? Color.yellow : Color.white;
            if (CustomButton(new Rect(180, cy, 140, 30), "Cancel"))
            {
                CloseMenu();
            }
            GUI.color = _selectedIndex == 11 ? Color.yellow : Color.white;
            if (CustomButton(new Rect(340, cy, 140, 30), "SAVE"))
            {
                SaveTempSettings();
            }
            GUI.color = Color.white;

            GUI.DragWindow(new Rect(0, 0, _windowRect.width, 40));
        }

        private bool CustomButton(Rect rect, string text)
        {
            // Draw visually as a box to prevent GUI.Button from eating events
            GUI.Box(rect, text);
            
            if (_clickHandledThisFrame) return false;

            if (Input.GetMouseButtonDown(0))
            {
                Vector2 rawMouse = Input.mousePosition;
                rawMouse.y = Screen.height - rawMouse.y;
                
                Rect absRect = new Rect(_windowRect.x + rect.x, _windowRect.y + rect.y, rect.width, rect.height);
                
                if (absRect.Contains(rawMouse))
                {
                    if (Event.current.type == EventType.Repaint)
                    {
                        _clickHandledThisFrame = true;
                        return true;
                    }
                }
            }
            return false;
        }

        private void CloseMenu()
        {
            _isVisible = false;
            IsVisibleGlobally = false;
            
            // Restore disabled scripts
            EnableGameScripts();

            // Enable New Input System devices
            try {
                if (UnityEngine.InputSystem.Keyboard.current != null)
                    UnityEngine.InputSystem.InputSystem.EnableDevice(UnityEngine.InputSystem.Keyboard.current);
                if (UnityEngine.InputSystem.Mouse.current != null)
                    UnityEngine.InputSystem.InputSystem.EnableDevice(UnityEngine.InputSystem.Mouse.current);
            } catch { }

            // Hide and lock the cursor when the menu is closed
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            OnMenuClosed?.Invoke();
        }

        private void DisableGameScripts()
        {
            _disabledScripts.Clear();
            
            // 1. Disable InputSystem globally to stop Pause menus and interactions
            var allMonos = UnityEngine.Object.FindObjectsOfType<MonoBehaviour>();
            foreach (var comp in allMonos)
            {
                if (comp == null || !comp.enabled) continue;
                
                string ns = comp.GetType().Namespace ?? "";
                string name = comp.GetType().Name.ToLower();
                
                if (ns.StartsWith("UnityEngine.InputSystem") || 
                    name.Contains("input") || 
                    name.Contains("pause") ||
                    name.Contains("camera") ||
                    name.Contains("look"))
                {
                    if (ns != "FlippingIsHardTrainer")
                    {
                        comp.enabled = false;
                        _disabledScripts.Add(comp);
                    }
                }
            }

            // 2. Disable custom player scripts to stop movement
            var player = _gameObjectFinder.FindPlayerTransform();
            if (player != null)
            {
                foreach (var comp in player.GetComponents<MonoBehaviour>())
                {
                    if (comp != null && comp.enabled && !(comp.GetType().Namespace ?? "").StartsWith("UnityEngine"))
                    {
                        if ((comp.GetType().Namespace ?? "") != "FlippingIsHardTrainer")
                        {
                            comp.enabled = false;
                            if (!_disabledScripts.Contains(comp))
                            {
                                _disabledScripts.Add(comp);
                            }
                        }
                    }
                }
            }

            // 3. Disable custom camera scripts
            var camera = _gameObjectFinder.FindCameraTransform();
            if (camera != null)
            {
                foreach (var comp in camera.GetComponents<MonoBehaviour>())
                {
                    if (comp != null && comp.enabled && !(comp.GetType().Namespace ?? "").StartsWith("UnityEngine"))
                    {
                        if ((comp.GetType().Namespace ?? "") != "FlippingIsHardTrainer")
                        {
                            comp.enabled = false;
                            if (!_disabledScripts.Contains(comp))
                            {
                                _disabledScripts.Add(comp);
                            }
                        }
                    }
                }
            }
        }

        private void EnableGameScripts()
        {
            foreach (var comp in _disabledScripts)
            {
                if (comp != null)
                {
                    comp.enabled = true;
                }
            }
            _disabledScripts.Clear();
        }

        private void DrawBindRow(float x, ref float y, string label, string actionId, KeyBind currentBind, int rowIndex)
        {
            GUI.Label(new Rect(x, y, 200, 25), label);

            bool isListening = _listeningAction == actionId;
            bool isSelected  = _selectedIndex == rowIndex;

            // Show the binding for whichever device the player is currently using.
            string bindStr = currentBind.ToString(InputDeviceTracker.Current, InputDeviceTracker.IsDualShock);
            string btnText = isListening
                ? "[ Pulsa tecla o botón (Esc / B para cancelar) ]"
                : $"[ {bindStr} ]";

            if (isListening)
                GUI.color = Color.green;
            else if (isSelected)
                GUI.color = Color.yellow;

            if (CustomButton(new Rect(x + 180, y, 280, 25), btnText))
            {
                _listeningAction = actionId;
                _pendingGpModifier = null;
            }

            GUI.color = Color.white;

            y += 35;
        }
    }
}
