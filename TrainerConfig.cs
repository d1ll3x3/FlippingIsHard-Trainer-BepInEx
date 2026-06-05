using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using BepInEx;
using UnityEngine;

namespace FlippingIsHardTrainer
{
    public class KeyBind
    {
        public KeyCode Modifier { get; set; } = KeyCode.None;
        public KeyCode MainKey { get; set; } = KeyCode.None;

        public KeyBind() { }

        public KeyBind(KeyCode mainKey, KeyCode modifier = KeyCode.None)
        {
            MainKey = mainKey;
            Modifier = modifier;
        }

        public override string ToString()
        {
            if (MainKey == KeyCode.None) return "Unbound";
            
            string modStr = Modifier == KeyCode.None ? "" : $"{Modifier} + ";
            return $"{modStr}{MainKey}";
        }

        public KeyBind Clone()
        {
            return new KeyBind(MainKey, Modifier);
        }
    }

    public class TrainerSettings
    {
        public KeyBind SavePosition { get; set; } = new KeyBind(KeyCode.R, KeyCode.LeftShift);
        public KeyBind Teleport { get; set; } = new KeyBind(KeyCode.R);
        public KeyBind ToggleFlyMode { get; set; } = new KeyBind(KeyCode.F);
        public KeyBind ToggleKeepVelocity { get; set; } = new KeyBind(KeyCode.V);
        public KeyBind ToggleKeepAngle { get; set; } = new KeyBind(KeyCode.C);
        public KeyBind OpenBindMenu { get; set; } = new KeyBind(KeyCode.B);
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
                    TrainerPlugin.Logger.LogInfo("Config loaded successfully.");
                }
                else
                {
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
            Settings = new TrainerSettings();
        }
    }
}
