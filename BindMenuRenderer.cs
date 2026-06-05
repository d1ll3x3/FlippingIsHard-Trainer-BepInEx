using System;
using System.Collections.Generic;
using UnityEngine;

namespace FlippingIsHardTrainer
{
    public class BindMenuRenderer
    {
        private GameObjectFinder _gameObjectFinder;
        private List<MonoBehaviour> _disabledScripts = new List<MonoBehaviour>();

        private bool _isVisible = false;
        private Rect _windowRect = new Rect(Screen.width / 2 - 250, Screen.height / 2 - 200, 500, 400);

        private GUIStyle _titleStyle;
        private bool _stylesReady = false;

        private Color _defaultBgColor;

        private TrainerSettings _tempSettings;
        
        // Listening state
        private string _listeningAction = null;
        private bool _clickHandledThisFrame = false;
        private GUI.WindowFunction _windowDelegate;

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
                
                // Clone settings for editing
                _tempSettings = new TrainerSettings
                {
                    SavePosition = TrainerConfig.Settings.SavePosition.Clone(),
                    Teleport = TrainerConfig.Settings.Teleport.Clone(),
                    ToggleFlyMode = TrainerConfig.Settings.ToggleFlyMode.Clone(),
                    ToggleKeepVelocity = TrainerConfig.Settings.ToggleKeepVelocity.Clone(),
                    ToggleKeepAngle = TrainerConfig.Settings.ToggleKeepAngle.Clone(),
                    OpenBindMenu = TrainerConfig.Settings.OpenBindMenu.Clone()
                };
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

            // Handle Listening input
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

        private void AssignKey(string action, KeyCode main, KeyCode mod)
        {
            if (_tempSettings == null) return;
            switch (action)
            {
                case "Save": _tempSettings.SavePosition = new KeyBind(main, mod); break;
                case "Teleport": _tempSettings.Teleport = new KeyBind(main, mod); break;
                case "Fly": _tempSettings.ToggleFlyMode = new KeyBind(main, mod); break;
                case "Vel": _tempSettings.ToggleKeepVelocity = new KeyBind(main, mod); break;
                case "Angle": _tempSettings.ToggleKeepAngle = new KeyBind(main, mod); break;
                case "Menu": _tempSettings.OpenBindMenu = new KeyBind(main, mod); break;
            }
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

            DrawBindRow(20, ref cy, "Save Position", "Save", _tempSettings.SavePosition);
            DrawBindRow(20, ref cy, "Teleport", "Teleport", _tempSettings.Teleport);
            DrawBindRow(20, ref cy, "Toggle Fly Mode", "Fly", _tempSettings.ToggleFlyMode);
            DrawBindRow(20, ref cy, "Toggle Keep Velocity", "Vel", _tempSettings.ToggleKeepVelocity);
            DrawBindRow(20, ref cy, "Toggle Keep Angle", "Angle", _tempSettings.ToggleKeepAngle);
            DrawBindRow(20, ref cy, "Open Bind Menu", "Menu", _tempSettings.OpenBindMenu);

            cy = _windowRect.height - 50; // Bottom row
            
            if (CustomButton(new Rect(20, cy, 140, 30), "Reset Defaults"))
            {
                TrainerConfig.ResetToDefaults();
                
                // Re-clone settings for editing so visuals update immediately without closing menu
                _tempSettings = new TrainerSettings
                {
                    SavePosition = TrainerConfig.Settings.SavePosition,
                    Teleport = TrainerConfig.Settings.Teleport,
                    ToggleFlyMode = TrainerConfig.Settings.ToggleFlyMode,
                    ToggleKeepVelocity = TrainerConfig.Settings.ToggleKeepVelocity,
                    ToggleKeepAngle = TrainerConfig.Settings.ToggleKeepAngle,
                    OpenBindMenu = TrainerConfig.Settings.OpenBindMenu
                };
            }
            if (CustomButton(new Rect(180, cy, 140, 30), "Cancel"))
            {
                CloseMenu();
            }
            if (CustomButton(new Rect(340, cy, 140, 30), "SAVE"))
            {
                TrainerConfig.Settings = _tempSettings;
                TrainerConfig.Save();
                CloseMenu();
            }

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

        private void DrawBindRow(float x, ref float y, string label, string actionId, KeyBind currentBind)
        {
            GUI.Label(new Rect(x, y, 200, 25), label);
            
            string btnText = _listeningAction == actionId ? "[ Press any key (Esc to cancel) ]" : $"[ {currentBind} ]";
            
            if (_listeningAction == actionId)
            {
                GUI.color = Color.green;
            }
            
            if (CustomButton(new Rect(x + 180, y, 280, 25), btnText))
            {
                _listeningAction = actionId;
            }
            
            // Restore default color
            GUI.color = Color.white;
            
            y += 35;
        }
    }
}
