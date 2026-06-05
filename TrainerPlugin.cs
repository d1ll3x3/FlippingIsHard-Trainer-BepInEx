using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Il2CppInterop.Runtime.Injection;
using UnityEngine;
using System;

namespace FlippingIsHardTrainer
{
    [BepInPlugin("com.flippingishard.trainer", "Flipping is Hard Trainer", "1.0.0")]
    public class TrainerPlugin : BasePlugin
    {
        internal static ManualLogSource Logger { get; private set; }

        public override void Load()
        {
            Logger = Log;
            Logger.LogInfo("Flipping is Hard Trainer plugin loaded!");
            Logger.LogInfo("Controls: Shift+R (Save), R (Teleport), F (Fly Mode)");

            try
            {
                var harmony = new Harmony("com.flippingishard.trainer");
                harmony.PatchAll();

                // Register our custom MonoBehaviour with IL2CPP before using AddComponent
                ClassInjector.RegisterTypeInIl2Cpp<TrainerBehaviour>();

                var go = new GameObject("FlippingIsHardTrainer");
                GameObject.DontDestroyOnLoad(go);
                go.AddComponent<TrainerBehaviour>();

                Logger.LogInfo("Trainer behaviour attached successfully");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error loading trainer: {ex}");
            }
        }
    }

    /// <summary>
    /// Main MonoBehaviour for the trainer.
    /// Must have the IntPtr constructor for IL2CPP interop.
    /// </summary>
    public class TrainerBehaviour : MonoBehaviour
    {
        // Required by IL2CPP interop
        public TrainerBehaviour(IntPtr ptr) : base(ptr) { }

        private TrainerController _controller;
        private GameObjectFinder _finder;
        private bool _initialized = false;
        private float _timer = 0f;
        private float _nextAttempt = 2f;   // first attempt after 2s
        private const float RETRY_INTERVAL = 3f;

        void Awake()
        {
            _finder = new GameObjectFinder();
            TrainerPlugin.Logger.LogInfo("TrainerBehaviour awake, waiting for game scene...");
        }

        void Update()
        {
            if (_initialized)
            {
                _controller?.Update();
                return;
            }

            _timer += Time.deltaTime;
            if (_timer >= _nextAttempt)
            {
                _timer = 0f;
                _nextAttempt = RETRY_INTERVAL;
                TryInitialize();
            }
        }

        void OnGUI()
        {
            if (_initialized)
                _controller?.OnGUI();
        }

        private void TryInitialize()
        {
            try
            {
                var player = _finder.FindPlayer();
                var camera = _finder.FindCamera();

                if (player != null && camera != null)
                {
                    TrainerPlugin.Logger.LogInfo($"Found player: {player.name}");
                    TrainerPlugin.Logger.LogInfo($"Found camera: {camera.name}");

                    _controller = new TrainerController();
                    _controller.enabled = true;
                    _controller.Initialize();

                    _initialized = true;
                    TrainerPlugin.Logger.LogInfo("Trainer initialized successfully!");
                }
                else
                {
                    if (player == null) TrainerPlugin.Logger.LogInfo("Player not found yet, retrying...");
                    if (camera == null) TrainerPlugin.Logger.LogInfo("Camera not found yet, retrying...");
                }
            }
            catch (Exception ex)
            {
                TrainerPlugin.Logger.LogError($"Error during initialization: {ex}");
            }
        }
    }
}
