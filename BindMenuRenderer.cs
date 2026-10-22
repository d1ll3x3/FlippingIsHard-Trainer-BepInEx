using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace FlippingIsHardTrainer
{
    public class BindMenuRenderer
    {
        // One remappable action. Held = movement-style bind (fly directions / turbo): captured
        // as a single key or button, and Shift/Ctrl/Alt are valid keys on their own.
        private sealed class BindAction
        {
            public readonly string Label;
            public readonly bool Held;
            public readonly Func<TrainerSettings, KeyBind> Get;

            public BindAction(string label, bool held, Func<TrainerSettings, KeyBind> get)
            {
                Label = label;
                Held = held;
                Get = get;
            }
        }

        private static readonly BindAction[] Actions =
        {
            new BindAction("Save position",    false, s => s.SavePosition),
            new BindAction("Teleport",         false, s => s.Teleport),
            new BindAction("Keep velocity",    false, s => s.ToggleKeepVelocity),
            new BindAction("Keep angle",       false, s => s.ToggleKeepAngle),
            new BindAction("Auto-repair",      false, s => s.ToggleAutoRepair),
            new BindAction("Help beam (solo)", false, s => s.ToggleHelpBeam),
            new BindAction("Open this menu",   false, s => s.OpenBindMenu),
            new BindAction("Toggle fly mode",  false, s => s.ToggleFlyMode),
            new BindAction("Forward",          true,  s => s.FlyForward),
            new BindAction("Backward",         true,  s => s.FlyBackward),
            new BindAction("Left",             true,  s => s.FlyLeft),
            new BindAction("Right",            true,  s => s.FlyRight),
            new BindAction("Up",               true,  s => s.FlyUp),
            new BindAction("Down",             true,  s => s.FlyDown),
            new BindAction("Turbo (hold)",     true,  s => s.FlyTurbo),
        };
        private const int FLY_SECTION_START = 7;

        // Gamepad navigation items: one per bind row, then HUD Scale, Reset, Cancel, Save.
        private static readonly int NAV_SCALE  = Actions.Length;
        private static readonly int NAV_RESET  = Actions.Length + 1;
        private static readonly int NAV_CANCEL = Actions.Length + 2;
        private static readonly int NAV_SAVE   = Actions.Length + 3;
        private static readonly int NAV_ITEM_COUNT = Actions.Length + 4;

        // Layout, in unscaled units (the whole menu is scaled to the screen via GUI.matrix)
        private const float W          = 640f;
        private const float PAD        = 22f;
        private const float HEADER_H   = 66f;
        private const float COLS_H     = 26f;
        private const float SECTION_H  = 30f;
        private const float ROW_H      = 32f;
        private const float CELL_W     = 170f;
        private const float CELL_H     = 24f;
        private const float SCALE_H    = 42f;
        private const float HINT_H     = 24f;
        private const float BUTTON_H   = 36f;
        private static readonly float H =
            HEADER_H + COLS_H + SECTION_H * 2f + ROW_H * Actions.Length + 12f + SCALE_H + HINT_H + BUTTON_H + PAD;

        private GameObjectFinder _gameObjectFinder;
        private List<MonoBehaviour> _disabledScripts = new List<MonoBehaviour>();

        private bool _isVisible = false;
        private Vector2 _panelPos;
        private bool _needsCentering = true;
        private float _uiScale = 1f;
        private bool _dragging = false;
        private Vector2 _dragOffset;

        private GUIStyle _styleTitle;
        private GUIStyle _styleSubtitle;
        private GUIStyle _styleCaption;
        private GUIStyle _styleCaptionCenter;
        private GUIStyle _styleLabel;
        private GUIStyle _styleChip;
        private GUIStyle _styleButton;
        private bool _stylesReady = false;

        private TrainerSettings _tempSettings;

        // Listening state (-1 = not capturing)
        private int _listeningIndex = -1;
        private InputDeviceType _listeningDevice = InputDeviceType.Keyboard;
        private bool _clickHandledThisFrame = false;

        // Gamepad navigation state
        private int _selectedIndex = 0;
        private bool _wasNavUp    = false;
        private bool _wasNavDown  = false;
        private bool _wasNavLeft  = false;
        private bool _wasNavRight = false;
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

        // Modifier keys may not reach IMGUI as KeyDown events, so held binds poll for them.
        private static readonly KeyCode[] ModifierKeys =
        {
            KeyCode.LeftShift, KeyCode.RightShift, KeyCode.LeftControl,
            KeyCode.RightControl, KeyCode.LeftAlt, KeyCode.RightAlt
        };

        public static bool IsVisibleGlobally = false;
        public bool IsVisible => _isVisible;
        public bool IsListening => _listeningIndex >= 0;
        public Action OnMenuClosed;

        public BindMenuRenderer(GameObjectFinder gameObjectFinder)
        {
            _gameObjectFinder = gameObjectFinder;
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
                FinishListening();
                _needsCentering = true;
            }
        }

        private void InitStyles()
        {
            if (_stylesReady) return;

            _styleTitle         = TrainerTheme.MakeLabel(20, FontStyle.Bold);
            _styleSubtitle      = TrainerTheme.MakeLabel(12);
            _styleCaption       = TrainerTheme.MakeLabel(11, FontStyle.Bold);
            _styleCaptionCenter = TrainerTheme.MakeLabel(11, FontStyle.Bold, TextAnchor.MiddleCenter);
            _styleLabel         = TrainerTheme.MakeLabel(14);
            _styleChip          = TrainerTheme.MakeLabel(12, FontStyle.Bold, TextAnchor.MiddleCenter);
            _styleButton        = TrainerTheme.MakeLabel(13, FontStyle.Bold, TextAnchor.MiddleCenter);
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

            if (_listeningIndex >= 0)
                HandleKeyboardListening();

            // Scale with the resolution (designed at 1080p) and always fit on screen.
            _uiScale = Mathf.Min(Mathf.Clamp(Screen.height / 1080f, 0.75f, 2f),
                                 Screen.height * 0.94f / H, Screen.width * 0.96f / W);
            Matrix4x4 oldMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(_uiScale, _uiScale, 1f));

            float viewW = Screen.width / _uiScale;
            float viewH = Screen.height / _uiScale;
            if (_needsCentering)
            {
                _panelPos = new Vector2((viewW - W) * 0.5f, (viewH - H) * 0.5f);
                _needsCentering = false;
            }

            TrainerTheme.FillRect(new Rect(0f, 0f, viewW, viewH), TrainerTheme.Backdrop);
            if (Event.current.type == EventType.Repaint)
                HandleDrag(viewW, viewH);
            DrawPanel();

            GUI.matrix = oldMatrix;
        }

        // ── Input ───────────────────────────────────────────────────────────

        private void HandleKeyboardListening()
        {
            Event e = Event.current;

            // Use raw Input for escape to guarantee it works even if events are swallowed
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                FinishListening();
                if (e.type != EventType.Repaint && e.type != EventType.Layout) e.Use();
                return;
            }
            if (_listeningDevice != InputDeviceType.Keyboard) return;

            bool held = Actions[_listeningIndex].Held;
            if (held)
            {
                foreach (var mk in ModifierKeys)
                {
                    if (Input.GetKeyDown(mk))
                    {
                        AssignKey(_listeningIndex, mk, KeyCode.None);
                        FinishListening();
                        return;
                    }
                }
            }

            if (e.type != EventType.KeyDown && e.type != EventType.MouseDown) return;

            KeyCode key = KeyCode.None;
            if (e.type == EventType.KeyDown)
            {
                key = e.keyCode;
            }
            else if (e.type == EventType.MouseDown)
            {
                if (e.button == 2) key = KeyCode.Mouse2;
                else if (e.button == 3) key = KeyCode.Mouse3;
                else if (e.button == 4) key = KeyCode.Mouse4;
                else if (e.button == 5) key = KeyCode.Mouse5;
                else if (e.button == 6) key = KeyCode.Mouse6;
            }

            // Ignore None, Escape and the main mouse buttons (they drive the menu itself)
            if (key == KeyCode.None || key == KeyCode.Escape) return;

            bool isModifier = Array.IndexOf(ModifierKeys, key) >= 0;
            if (held)
            {
                AssignKey(_listeningIndex, key, KeyCode.None);
            }
            else
            {
                // Pure modifier keys aren't main keys here; they're caught as the modifier.
                if (isModifier) return;

                KeyCode mod = KeyCode.None;
                if (e.shift || Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) mod = KeyCode.LeftShift;
                else if (e.control || Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) mod = KeyCode.LeftControl;
                else if (e.alt || Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt)) mod = KeyCode.LeftAlt;

                AssignKey(_listeningIndex, key, mod);
            }
            FinishListening();
            e.Use();
        }

        private void HandleGamepadInput(Gamepad gp)
        {
            if (_listeningIndex >= 0)
            {
                // Circle / B cancels capture. The once-per-frame guard prevents this same
                // press from also closing the menu in a later pass of the frame.
                if (gp[GamepadButton.East].wasPressedThisFrame)
                {
                    FinishListening();
                    return;
                }
                if (_listeningDevice != InputDeviceType.Gamepad) return;

                // Combo-aware capture — ANY held button can be a modifier:
                //  - 2 buttons the same frame  → combo (prefer a shoulder/trigger as modifier).
                //  - 1 button while another is still held → combo (held = modifier).
                //  - 1 button alone → deferred; becomes a single bind on release, or the
                //    modifier if a second button arrives while it is held.
                // Held binds (fly movement) take the first button alone, no combos.
                GamepadButton? first = null, second = null;
                foreach (var btn in AllButtons)
                {
                    if (btn == GamepadButton.East) continue; // reserved for cancel
                    if (!gp[btn].wasPressedThisFrame) continue;
                    if (first == null) first = btn;
                    else { second = btn; break; }
                }

                if (first.HasValue && Actions[_listeningIndex].Held)
                {
                    AssignGamepadButton(_listeningIndex, first.Value, null);
                    FinishListening();
                    return;
                }

                if (first.HasValue && second.HasValue)
                {
                    GamepadButton main, mod;
                    if (IsGpModifier(first.Value) && !IsGpModifier(second.Value)) { mod = first.Value; main = second.Value; }
                    else if (IsGpModifier(second.Value) && !IsGpModifier(first.Value)) { mod = second.Value; main = first.Value; }
                    else { mod = first.Value; main = second.Value; }
                    AssignGamepadButton(_listeningIndex, main, mod);
                    FinishListening();
                    return;
                }

                if (first.HasValue)
                {
                    if (_pendingGpModifier.HasValue && _pendingGpModifier.Value != first.Value &&
                        gp[_pendingGpModifier.Value].isPressed)
                    {
                        AssignGamepadButton(_listeningIndex, first.Value, _pendingGpModifier);
                        FinishListening();
                        return;
                    }
                    _pendingGpModifier = first.Value; // defer
                    return;
                }

                // Deferred lone button released without a second button → single bind.
                if (_pendingGpModifier.HasValue && gp[_pendingGpModifier.Value].wasReleasedThisFrame)
                {
                    AssignGamepadButton(_listeningIndex, _pendingGpModifier.Value, null);
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
            if (_selectedIndex == NAV_SCALE)
            {
                if (navLeft  && !_wasNavLeft)  AdjustTempScale(-0.05f);
                if (navRight && !_wasNavRight) AdjustTempScale(+0.05f);
            }
            _wasNavLeft  = navLeft;
            _wasNavRight = navRight;

            if (gp[GamepadButton.South].wasPressedThisFrame)       // A / Cross — activate
                HandleGamepadActivate(_selectedIndex);
            else if (gp[GamepadButton.North].wasPressedThisFrame)  // Y / Triangle — clear pad bind
            {
                if (_selectedIndex < Actions.Length) ClearBind(_selectedIndex, InputDeviceType.Gamepad);
            }
            else if (gp[GamepadButton.East].wasPressedThisFrame)   // B / Circle — close
                CloseMenu();
        }

        private void HandleDrag(float viewW, float viewH)
        {
            Vector2 mouse = MouseGui();
            var header = new Rect(_panelPos.x, _panelPos.y, W, HEADER_H);
            if (!_dragging && Input.GetMouseButtonDown(0) && header.Contains(mouse) && !CloseRect().Contains(mouse))
            {
                _dragging = true;
                _dragOffset = mouse - _panelPos;
            }
            if (_dragging)
            {
                if (Input.GetMouseButton(0)) _panelPos = mouse - _dragOffset;
                else _dragging = false;
            }

            _panelPos.x = Mathf.Clamp(_panelPos.x, 0f, Mathf.Max(0f, viewW - W));
            _panelPos.y = Mathf.Clamp(_panelPos.y, 0f, Mathf.Max(0f, viewH - H));
        }

        private void StartListening(int index, InputDeviceType device)
        {
            _listeningIndex = index;
            _listeningDevice = device;
            _pendingGpModifier = null;
        }

        private void FinishListening()
        {
            _listeningIndex = -1;
            _pendingGpModifier = null;
        }

        private static bool IsGpModifier(GamepadButton btn)
        {
            return System.Array.IndexOf(GpModifiers, btn) >= 0;
        }

        private KeyBind GetBind(int index)
        {
            if (_tempSettings == null || index < 0 || index >= Actions.Length) return null;
            return Actions[index].Get(_tempSettings);
        }

        private void AssignKey(int index, KeyCode main, KeyCode mod)
        {
            var b = GetBind(index);
            if (b == null) return;
            b.MainKey = main;
            b.Modifier = mod;
        }

        private void AssignGamepadButton(int index, GamepadButton main, GamepadButton? mod)
        {
            var b = GetBind(index);
            if (b == null) return;
            b.GamepadBtn = main;
            b.GamepadModifier = mod;
        }

        private void ClearBind(int index, InputDeviceType device)
        {
            var b = GetBind(index);
            if (b == null) return;
            if (device == InputDeviceType.Gamepad)
            {
                b.GamepadBtn = null;
                b.GamepadModifier = null;
            }
            else
            {
                b.MainKey = KeyCode.None;
                b.Modifier = KeyCode.None;
            }
            if (_listeningIndex == index) FinishListening();
        }

        private void HandleGamepadActivate(int index)
        {
            if (index < Actions.Length)
                StartListening(index, InputDeviceType.Gamepad);
            else if (index == NAV_SCALE) // HUD Scale — A increments (use ◄ ► to dec/inc)
                AdjustTempScale(+0.05f);
            else if (index == NAV_RESET)
                ResetTempToDefaults();
            else if (index == NAV_CANCEL)
                CloseMenu();
            else if (index == NAV_SAVE)
                SaveTempSettings();
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
                FlyForward         = src.FlyForward.Clone(),
                FlyBackward        = src.FlyBackward.Clone(),
                FlyLeft            = src.FlyLeft.Clone(),
                FlyRight           = src.FlyRight.Clone(),
                FlyUp              = src.FlyUp.Clone(),
                FlyDown            = src.FlyDown.Clone(),
                FlyTurbo           = src.FlyTurbo.Clone(),
                OverlayScale       = src.OverlayScale,
                HelpBeamSingleplayer = src.HelpBeamSingleplayer,
            };
        }

        private void AdjustTempScale(float delta)
        {
            if (_tempSettings == null) return;
            float next = Mathf.Clamp(_tempSettings.OverlayScale + delta, 0.25f, 2.0f);
            _tempSettings.OverlayScale = Mathf.Round(next * 100f) / 100f;
        }

        // ── Drawing ─────────────────────────────────────────────────────────

        private void DrawPanel()
        {
            float px = _panelPos.x;
            float py = _panelPos.y;
            bool gamepadActive = Gamepad.current != null && InputDeviceTracker.Current == InputDeviceType.Gamepad;
            bool ps = InputDeviceTracker.IsDualShock;

            TrainerTheme.DrawPanel(new Rect(px, py, W, H), TrainerTheme.Panel);

            // ── Header ───────────────────────────────────────────────────
            TrainerTheme.Fill(new Rect(px + PAD, py + 18f, 4f, 30f), TrainerTheme.Accent, true);
            TrainerTheme.Label(new Rect(px + PAD + 14f, py + 12f, 400f, 26f), "Keybinds", _styleTitle, TrainerTheme.Text);
            TrainerTheme.Label(new Rect(px + PAD + 14f, py + 37f, W - PAD * 2f - 60f, 18f),
                "Click a binding and press a key or button  ·  Right-click clears it", _styleSubtitle, TrainerTheme.TextDim);

            if (Button(CloseRect(), "X", false, false, TrainerTheme.Danger))
                CloseMenu();

            TrainerTheme.FillRect(new Rect(px, py + HEADER_H, W, 1f), TrainerTheme.Divider);

            // ── Column headers ───────────────────────────────────────────
            float y = py + HEADER_H + 8f;
            Rect kbCol = new Rect(px + W - PAD - CELL_W * 2f - 10f, y, CELL_W, 18f);
            Rect gpCol = new Rect(px + W - PAD - CELL_W, y, CELL_W, 18f);
            TrainerTheme.Label(new Rect(px + PAD, y, 200f, 18f), "ACTION", _styleCaption, TrainerTheme.TextFaint);
            TrainerTheme.Label(kbCol, "KEYBOARD", _styleCaptionCenter, gamepadActive ? TrainerTheme.TextFaint : TrainerTheme.Accent);
            TrainerTheme.Label(gpCol, "GAMEPAD", _styleCaptionCenter, gamepadActive ? TrainerTheme.Accent : TrainerTheme.TextFaint);
            y = py + HEADER_H + COLS_H;

            // ── Bind rows ────────────────────────────────────────────────
            for (int i = 0; i < Actions.Length; i++)
            {
                if (i == 0 || i == FLY_SECTION_START)
                {
                    TrainerTheme.Label(new Rect(px + PAD, y + 10f, 300f, 18f),
                        i == 0 ? "GENERAL" : "FLY MODE", _styleCaption, i == 0 ? TrainerTheme.Accent : TrainerTheme.Fly);
                    y += SECTION_H;
                }
                DrawBindRow(i, px, y, gamepadActive);
                y += ROW_H;
            }

            // ── HUD scale ────────────────────────────────────────────────
            y += 6f;
            TrainerTheme.FillRect(new Rect(px + PAD, y, W - PAD * 2f, 1f), TrainerTheme.Divider);
            y += 6f;
            var scaleRow = new Rect(px + PAD - 8f, y + 4f, W - PAD * 2f + 16f, SCALE_H - 8f);
            DrawRowBackground(scaleRow, gamepadActive && _selectedIndex == NAV_SCALE);
            TrainerTheme.Label(new Rect(px + PAD, y, 200f, SCALE_H), "HUD scale", _styleLabel, TrainerTheme.Text);
            float sy = y + (SCALE_H - 26f) * 0.5f;
            float sx = px + W - PAD - CELL_W;
            if (Button(new Rect(sx, sy, 34f, 26f), "-", false, false, TrainerTheme.Accent))
                AdjustTempScale(-0.05f);
            TrainerTheme.Label(new Rect(sx + 34f, sy, CELL_W - 68f, 26f), $"{_tempSettings.OverlayScale:F2}x", _styleChip, TrainerTheme.Text);
            if (Button(new Rect(sx + CELL_W - 34f, sy, 34f, 26f), "+", false, false, TrainerTheme.Accent))
                AdjustTempScale(+0.05f);
            y += SCALE_H;

            // ── Hint ─────────────────────────────────────────────────────
            string hint;
            if (gamepadActive)
            {
                string sel = ps ? "Cross" : "A";
                string clear = ps ? "Triangle" : "Y";
                string close = ps ? "Circle" : "B";
                hint = _selectedIndex == NAV_SCALE
                    ? $"◄ ► adjust scale  ·  {close} close"
                    : $"D-Pad navigate  ·  {sel} rebind  ·  {clear} clear  ·  {close} cancel / close";
            }
            else
            {
                hint = "Esc cancels a capture  ·  Changes apply when you press Save";
            }
            TrainerTheme.Label(new Rect(px + PAD, y, W - PAD * 2f, HINT_H), hint, _styleSubtitle, TrainerTheme.TextFaint);
            y += HINT_H;

            // ── Buttons ──────────────────────────────────────────────────
            if (Button(new Rect(px + PAD, y, 150f, BUTTON_H), "Reset defaults", false, gamepadActive && _selectedIndex == NAV_RESET, TrainerTheme.Danger))
                ResetTempToDefaults();
            if (Button(new Rect(px + W - PAD - 250f, y, 110f, BUTTON_H), "Cancel", false, gamepadActive && _selectedIndex == NAV_CANCEL, TrainerTheme.Accent))
                CloseMenu();
            if (Button(new Rect(px + W - PAD - 130f, y, 130f, BUTTON_H), "Save", true, gamepadActive && _selectedIndex == NAV_SAVE, TrainerTheme.Accent))
                SaveTempSettings();
        }

        private void DrawBindRow(int index, float px, float y, bool gamepadActive)
        {
            var row = new Rect(px + PAD - 8f, y + 1f, W - PAD * 2f + 16f, ROW_H - 2f);
            DrawRowBackground(row, gamepadActive && _selectedIndex == index);

            TrainerTheme.Label(new Rect(px + PAD, y, 260f, ROW_H), Actions[index].Label, _styleLabel, TrainerTheme.Text);

            float cy = y + (ROW_H - CELL_H) * 0.5f;
            DrawBindCell(index, InputDeviceType.Keyboard, new Rect(px + W - PAD - CELL_W * 2f - 10f, cy, CELL_W, CELL_H));
            DrawBindCell(index, InputDeviceType.Gamepad,  new Rect(px + W - PAD - CELL_W, cy, CELL_W, CELL_H));
        }

        private void DrawRowBackground(Rect row, bool selected)
        {
            if (selected)
            {
                TrainerTheme.Fill(row, TrainerTheme.AccentSoft, true);
                TrainerTheme.Fill(new Rect(row.x, row.y + 6f, 3f, row.height - 12f), TrainerTheme.Accent, true);
            }
            else if (row.Contains(MouseGui()))
            {
                TrainerTheme.Fill(row, TrainerTheme.Surface, true);
            }
        }

        private void DrawBindCell(int index, InputDeviceType device, Rect r)
        {
            var bind = Actions[index].Get(_tempSettings);
            bool keyboard = device == InputDeviceType.Keyboard;

            if (_listeningIndex == index && _listeningDevice == device)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f);
                Color bg = Color.Lerp(TrainerTheme.WarnSoft, new Color(1f, 0.75f, 0.33f, 0.34f), pulse);
                TrainerTheme.DrawChip(r, keyboard ? "Press a key..." : "Press a button...", _styleChip, bg, TrainerTheme.Warn);
            }
            else
            {
                bool bound = keyboard ? bind.HasKeyboard : bind.HasGamepad;
                string text = !bound ? "Not set"
                    : keyboard ? bind.ToStringKeyboard() : bind.ToStringGamepad(InputDeviceTracker.IsDualShock);
                Color bg = r.Contains(MouseGui()) ? TrainerTheme.ChipHover : TrainerTheme.Chip;
                TrainerTheme.DrawChip(r, text, _styleChip, bg, bound ? TrainerTheme.Text : TrainerTheme.TextFaint);
            }

            if (Clicked(r, 0))
                StartListening(index, device);
            else if (Clicked(r, 1))
                ClearBind(index, device);
        }

        // Rounded button. Primary = filled accent; otherwise a subtle surface that takes
        // the hover color on mouse-over. Selected (gamepad focus) draws an accent ring.
        private bool Button(Rect r, string text, bool primary, bool selected, Color hoverColor)
        {
            bool hover = r.Contains(MouseGui());
            if (selected)
                TrainerTheme.Fill(new Rect(r.x - 2f, r.y - 2f, r.width + 4f, r.height + 4f), TrainerTheme.Accent, true);

            Color bg, fg;
            if (primary)
            {
                bg = hover || selected ? Color.Lerp(TrainerTheme.Accent, Color.white, 0.18f) : TrainerTheme.Accent;
                fg = new Color(0.04f, 0.07f, 0.11f, 1f);
            }
            else
            {
                bg = hover ? new Color(hoverColor.r, hoverColor.g, hoverColor.b, 0.22f) : TrainerTheme.Chip;
                fg = hover ? hoverColor : TrainerTheme.Text;
            }
            TrainerTheme.DrawChip(r, text, _styleButton, bg, fg);
            return Clicked(r, 0);
        }

        private Rect CloseRect() => new Rect(_panelPos.x + W - PAD - 30f, _panelPos.y + 18f, 30f, 30f);

        // Mouse in menu space. Raw Input instead of IMGUI events because the menu's own
        // clicks must not be eaten before the capture logic sees them.
        private Vector2 MouseGui()
        {
            Vector3 m = Input.mousePosition;
            return new Vector2(m.x, Screen.height - m.y) / _uiScale;
        }

        private bool Clicked(Rect r, int button)
        {
            if (_clickHandledThisFrame || Event.current.type != EventType.Repaint) return false;
            if (!Input.GetMouseButtonDown(button) || !r.Contains(MouseGui())) return false;
            _clickHandledThisFrame = true;
            return true;
        }

        private void CloseMenu()
        {
            _isVisible = false;
            IsVisibleGlobally = false;
            _dragging = false;
            FinishListening();

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
    }
}
