using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using BepInEx;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace FlippingIsHardTrainer
{
    public class KeyBind
    {
        // Keyboard binding
        public KeyCode Modifier { get; set; } = KeyCode.None;
        public KeyCode MainKey { get; set; } = KeyCode.None;
        // Gamepad binding (GamepadBtn = main, GamepadModifier = optional 2-button combo)
        public GamepadButton? GamepadBtn { get; set; } = null;
        public GamepadButton? GamepadModifier { get; set; } = null;

        [JsonIgnore] public bool HasKeyboard => MainKey != KeyCode.None;
        [JsonIgnore] public bool HasGamepad => GamepadBtn.HasValue;

        public KeyBind() { }

        public KeyBind(KeyCode mainKey, KeyCode modifier = KeyCode.None)
        {
            MainKey = mainKey;
            Modifier = modifier;
        }

        public override string ToString()
        {
            // Default representation prefers keyboard for logs/back-compat.
            return HasKeyboard ? ToStringKeyboard() : ToStringGamepad(false);
        }

        public string ToString(InputDeviceType device, bool ps)
        {
            return device == InputDeviceType.Gamepad ? ToStringGamepad(ps) : ToStringKeyboard();
        }

        public string ToStringKeyboard()
        {
            if (MainKey == KeyCode.None) return "Unbound";

            string modStr = Modifier == KeyCode.None ? "" : $"{KeyName(Modifier)} + ";
            return $"{modStr}{KeyName(MainKey)}";
        }

        // Short display names; KeyCode.ToString() gives "LeftShift", "Alpha1", "Mouse3"...
        public static string KeyName(KeyCode key)
        {
            return key switch
            {
                KeyCode.LeftShift or KeyCode.RightShift     => "Shift",
                KeyCode.LeftControl or KeyCode.RightControl => "Ctrl",
                KeyCode.LeftAlt or KeyCode.RightAlt         => "Alt",
                KeyCode.Return                              => "Enter",
                KeyCode.Escape                              => "Esc",
                KeyCode.Mouse2                              => "Mouse Mid",
                KeyCode.Mouse3                              => "Mouse 4",
                KeyCode.Mouse4                              => "Mouse 5",
                KeyCode.Mouse5                              => "Mouse 6",
                KeyCode.Mouse6                              => "Mouse 7",
                >= KeyCode.Alpha0 and <= KeyCode.Alpha9     => ((int)key - (int)KeyCode.Alpha0).ToString(),
                >= KeyCode.Keypad0 and <= KeyCode.Keypad9   => $"Num {(int)key - (int)KeyCode.Keypad0}",
                _                                           => key.ToString(),
            };
        }

        public string ToStringGamepad(bool ps)
        {
            if (!GamepadBtn.HasValue) return "Unbound";

            string modStr = GamepadModifier.HasValue ? $"{GamepadButtonToString(GamepadModifier.Value, ps)} + " : "";
            return $"{modStr}{GamepadButtonToString(GamepadBtn.Value, ps)}";
        }

        public KeyBind Clone()
        {
            return new KeyBind(MainKey, Modifier) { GamepadBtn = GamepadBtn, GamepadModifier = GamepadModifier };
        }

        public static string GamepadButtonToString(GamepadButton btn, bool ps)
        {
            return btn switch
            {
                GamepadButton.South         => ps ? "Cross"    : "A",
                GamepadButton.East          => ps ? "Circle"   : "B",
                GamepadButton.North         => ps ? "Triangle" : "Y",
                GamepadButton.West          => ps ? "Square"   : "X",
                GamepadButton.LeftShoulder  => ps ? "L1"       : "LB",
                GamepadButton.RightShoulder => ps ? "R1"       : "RB",
                GamepadButton.LeftTrigger   => ps ? "L2"       : "LT",
                GamepadButton.RightTrigger  => ps ? "R2"       : "RT",
                GamepadButton.Start         => ps ? "Options"  : "Start",
                GamepadButton.Select        => ps ? "Share"    : "Select",
                GamepadButton.LeftStick     => ps ? "L3"       : "L3",
                GamepadButton.RightStick    => ps ? "R3"       : "R3",
                GamepadButton.DpadUp        => "D-Up",
                GamepadButton.DpadDown      => "D-Down",
                GamepadButton.DpadLeft      => "D-Left",
                GamepadButton.DpadRight     => "D-Right",
                _                           => btn.ToString(),
            };
        }
    }

    public class TrainerSettings
    {
        public const int CURRENT_VERSION = 4;

        // No initializer on purpose: an old JSON without this field deserializes to 0,
        // which triggers the migration below.
        public int Version { get; set; }

        // Save Position: LB + X (Xbox) / L1 + Square (PS)
        public KeyBind SavePosition { get; set; } = new KeyBind(KeyCode.R, KeyCode.LeftShift)
            { GamepadModifier = GamepadButton.LeftShoulder, GamepadBtn = GamepadButton.West };
        public KeyBind Teleport { get; set; } = new KeyBind(KeyCode.R)
            { GamepadBtn = GamepadButton.West };
        public KeyBind ToggleFlyMode { get; set; } = new KeyBind(KeyCode.F)
            { GamepadBtn = GamepadButton.LeftStick };
        public KeyBind ToggleKeepVelocity { get; set; } = new KeyBind(KeyCode.V)
            { GamepadBtn = GamepadButton.DpadLeft };
        public KeyBind ToggleKeepAngle { get; set; } = new KeyBind(KeyCode.C)
            { GamepadBtn = GamepadButton.DpadRight };
        public KeyBind ToggleAutoRepair { get; set; } = new KeyBind(KeyCode.G)
            { GamepadBtn = GamepadButton.DpadUp };
        public KeyBind ToggleHelpBeam { get; set; } = new KeyBind(KeyCode.N)
            { GamepadBtn = GamepadButton.DpadDown };
        public KeyBind OpenBindMenu { get; set; } = new KeyBind(KeyCode.B)
            { GamepadModifier = GamepadButton.LeftShoulder, GamepadBtn = GamepadButton.RightShoulder };

        // Ghost mode. Keys clear of FIH Ghost's J K L, so both mods can run together; on a
        // gamepad, LB combos like Save. Missing in old configs -> initializers apply, so no
        // version bump.
        public KeyBind ToggleGhostMode { get; set; } = new KeyBind(KeyCode.H)
            { GamepadModifier = GamepadButton.LeftShoulder, GamepadBtn = GamepadButton.North };
        public KeyBind GhostSetStart { get; set; } = new KeyBind(KeyCode.Y)
            { GamepadModifier = GamepadButton.LeftShoulder, GamepadBtn = GamepadButton.DpadLeft };
        public KeyBind GhostSetEnd { get; set; } = new KeyBind(KeyCode.P)
            { GamepadModifier = GamepadButton.LeftShoulder, GamepadBtn = GamepadButton.East };
        public KeyBind GhostRecord { get; set; } = new KeyBind(KeyCode.U)
            { GamepadModifier = GamepadButton.LeftShoulder, GamepadBtn = GamepadButton.DpadUp };
        public KeyBind GhostPlay { get; set; } = new KeyBind(KeyCode.I)
            { GamepadModifier = GamepadButton.LeftShoulder, GamepadBtn = GamepadButton.DpadRight };
        public KeyBind GhostSpectate { get; set; } = new KeyBind(KeyCode.O)
            { GamepadModifier = GamepadButton.LeftShoulder, GamepadBtn = GamepadButton.DpadDown };

        // Fly mode movement (held, not toggled). The gamepad left stick always moves as
        // well, so the directions ship without a gamepad button.
        // Missing in old configs -> initializers apply, so no version bump (which would
        // wipe the user's existing binds).
        public KeyBind FlyForward { get; set; } = new KeyBind(KeyCode.W);
        public KeyBind FlyBackward { get; set; } = new KeyBind(KeyCode.S);
        public KeyBind FlyLeft { get; set; } = new KeyBind(KeyCode.A);
        public KeyBind FlyRight { get; set; } = new KeyBind(KeyCode.D);
        public KeyBind FlyUp { get; set; } = new KeyBind(KeyCode.Space)
            { GamepadBtn = GamepadButton.RightTrigger };
        public KeyBind FlyDown { get; set; } = new KeyBind(KeyCode.LeftControl)
            { GamepadBtn = GamepadButton.LeftTrigger };
        public KeyBind FlyTurbo { get; set; } = new KeyBind(KeyCode.LeftShift)
            { GamepadBtn = GamepadButton.East };

        public float OverlayScale { get; set; } = 1.0f;
        // Enable the game's multiplayer-only help beam (right-click) in singleplayer.
        // Missing in old configs -> initializer keeps it ON, so no version bump needed.
        public bool HelpBeamSingleplayer { get; set; } = true;
    }

    public static class TrainerConfig
    {
        private static string ConfigFilePath => Path.Combine(Paths.ConfigPath, "com.flippingishard.trainer.json");
        
        public static TrainerSettings Settings { get; set; } = new TrainerSettings();

        public static void Load()
        {
            try
            {
                if (File.Exists(ConfigFilePath))
                {
                    string json = File.ReadAllText(ConfigFilePath);
                    var options = new JsonSerializerOptions { Converters = { new JsonStringEnumConverter() } };
                    Settings = JsonSerializer.Deserialize<TrainerSettings>(json, options) ?? new TrainerSettings();

                    // Migrate outdated configs (e.g. pre-gamepad-defaults) by regenerating
                    // to the current defaults so new gamepad binds (LB+RB, etc.) take effect.
                    if (Settings.Version < TrainerSettings.CURRENT_VERSION)
                    {
                        Settings = new TrainerSettings { Version = TrainerSettings.CURRENT_VERSION };
                        Save();
                        TrainerPlugin.Logger.LogInfo($"Config migrated to v{TrainerSettings.CURRENT_VERSION}.");
                    }
                    else
                    {
                        TrainerPlugin.Logger.LogInfo("Config loaded successfully.");
                    }
                }
                else
                {
                    Settings = new TrainerSettings { Version = TrainerSettings.CURRENT_VERSION };
                    Save(); // Create default config file
                }
            }
            catch (Exception ex)
            {
                TrainerPlugin.Logger.LogError($"Error loading config: {ex.Message}. Using defaults.");
                Settings = new TrainerSettings();
            }
        }

        public static void Save()
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };
                string json = JsonSerializer.Serialize(Settings, options);
                File.WriteAllText(ConfigFilePath, json);
                TrainerPlugin.Logger.LogInfo("Config saved successfully.");
            }
            catch (Exception ex)
            {
                TrainerPlugin.Logger.LogError($"Error saving config: {ex.Message}");
            }
        }

        public static void ResetToDefaults()
        {
            Settings = new TrainerSettings { Version = TrainerSettings.CURRENT_VERSION };
        }
    }
}
