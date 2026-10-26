using System;
using UnityEngine;

namespace FlippingIsHardTrainer
{
    public class TrainerController
    {
        // Trainer state
        private bool _hasSavedPosition = false;
        private Vector3 _savedPosition = Vector3.zero;
        private Quaternion _savedRotation = Quaternion.identity;
        private Vector3 _savedLinearVelocity = Vector3.zero;
        private Vector3 _savedAngularVelocity = Vector3.zero;
        private Quaternion _savedCameraRotation = Quaternion.identity;
        private bool _flyModeActive = false;
        private bool _keepVelocityActive = false;
        private bool _keepAngleActive = false;
        private bool _autoRepairActive = false;

        // Fly mode (trainer-owned, position-based so it works on the game's kinematic body)
        private bool _flyWasKinematic = false;
        private bool _flyWasGravity = true;
        private bool _flyWasCollisions = true;
        private float _flySpeed = 18.0f;
        private float _flySpeedBoost = 3.0f;
        private Vector3 _lastFlyVelocity = Vector3.zero; // momentum carried over when exiting fly

        // Component references
        private GameObjectFinder _gameObjectFinder;
        private InputHandler _inputHandler;
        private OverlayRenderer _overlayRenderer;
        private BindMenuRenderer _bindMenuRenderer;
        private GhostMode _ghostMode;

        // Current state for overlay
        private Vector3 _currentPosition = Vector3.zero;

        // Active-device tracking (auto-switch keyboard <-> gamepad)
        private InputDeviceType _lastDevice = InputDeviceType.Keyboard;

        // Window-focus tracking: ignore trainer hotkeys while unfocused (e.g. Steam overlay
        // via Shift+Tab) and for a short grace after regaining focus, so a modifier still
        // "stuck" from the tab combo can't misfire a bind (e.g. Shift+R overwriting the save).
        private bool _wasFocused = true;
        private float _inputGraceUntil = 0f;
        private const float FOCUS_INPUT_GRACE = 0.3f;

        // Player freeze while the bind menu is open (stops movement from any input source)
        private bool _wasMenuVisible = false;
        private Vector3 _frozenPosition = Vector3.zero;
        private Quaternion _frozenRotation = Quaternion.identity;
        private Vector3 _frozenLinVel = Vector3.zero;
        private Vector3 _frozenAngVel = Vector3.zero;
        private bool _frozenKinematic = false;

        public bool enabled { get; set; }
        
        public void Initialize()
        {
            try
            {
                // Initialize components
                TrainerConfig.Load();
                
                _gameObjectFinder = new GameObjectFinder();
                _inputHandler = new InputHandler();
                _overlayRenderer = new OverlayRenderer();
                _overlayRenderer.RefreshKeybinds();
                _ghostMode = new GhostMode(() => _flyModeActive);
                _bindMenuRenderer = new BindMenuRenderer(_gameObjectFinder, _ghostMode);
                _bindMenuRenderer.OnMenuClosed = () => {
                    _overlayRenderer.RefreshKeybinds(_lastDevice);
                };
                
                TrainerPlugin.Logger.LogInfo("TrainerController initialized successfully");
            }
            catch (Exception ex)
            {
                TrainerPlugin.Logger.LogError($"Error initializing TrainerController: {ex}");
                enabled = false;
            }
        }
        
        public void Update()
        {
            try
            {
                if (!enabled) return;

                // Update input handler
                _inputHandler.Update();

                // Track the active device and refresh the HUD when it changes
                InputDeviceTracker.Update();
                if (_lastDevice != InputDeviceTracker.Current)
                {
                    _lastDevice = InputDeviceTracker.Current;
                    _overlayRenderer.RefreshKeybinds(_lastDevice);
                }

                // Disable the game's OWN fly cheat so its Shift+F toggle can never start a
                // second, separate fly (our turbo is Shift, our fly is F, so Shift+F would
                // otherwise trigger the game's native fly).
                DisableNativeFly();

                // Window focus gate: while the window is unfocused (e.g. Steam overlay opened
                // with Shift+Tab) we don't process any trainer hotkeys, and we apply a short
                // grace + edge reset on regaining focus so a modifier still held from the tab
                // combo can't misfire a bind (Shift+R was overwriting the saved position).
                bool focused = Application.isFocused;
                if (focused && !_wasFocused)
                {
                    _inputGraceUntil = Time.unscaledTime + FOCUS_INPUT_GRACE;
                    _inputHandler.ResetEdges();
                }
                _wasFocused = focused;
                bool acceptInput = focused && Time.unscaledTime >= _inputGraceUntil;

                // Open / close bind menu (not while the menu is capturing a bind, or assigning
                // the menu's own key would close it)
                if (acceptInput && !_bindMenuRenderer.IsListening && _inputHandler.IsOpenBindMenuPressed())
                {
                    _bindMenuRenderer.ToggleVisibility();
                }

                // Freeze the player while the menu is open so no input source can move it
                HandleMenuFreeze();

                // A teleport picked in the menu, now that it is closed and the player unfrozen
                var pendingTeleport = _bindMenuRenderer.TakePendingTeleport();
                if (pendingTeleport != null)
                {
                    _ghostMode.CancelAttempt();
                    TeleportTargets.Teleport(pendingTeleport);
                }

                // Same for a ghost picked in the Ghost tab to play or spectate: spectating
                // freezes the phone, which the menu's own freeze would undo.
                _bindMenuRenderer.TakePendingGhostAction()?.Invoke();

                _ghostMode.Update();

                // Handle trainer hotkeys only if menu is not visible and input is accepted
                if (acceptInput && !_bindMenuRenderer.IsVisible)
                {
                    HandleHotkeys();

                    if (_flyModeActive)
                        HandleFlyMode();
                }
                
                // Reducimos la frecuencia de actualización de UI a 1 vez cada varios frames para evitar lag
                if (Time.frameCount % 5 == 0)
                {
                    UpdateCurrentPosition();
                    UpdateOverlay();
                }
            }
            catch (Exception ex)
            {
                TrainerPlugin.Logger.LogError($"Error in TrainerController.Update: {ex}");
            }
        }
        
        public void FixedUpdate()
        {
            try
            {
                if (!enabled) return;
                _ghostMode.FixedUpdate();
            }
            catch (Exception ex)
            {
                TrainerPlugin.Logger.LogError($"Error in TrainerController.FixedUpdate: {ex}");
                _ghostMode.CancelAttempt();
            }
        }

        public void LateUpdate()
        {
            try
            {
                if (!enabled) return;
                _ghostMode.LateUpdate();
            }
            catch (Exception ex)
            {
                TrainerPlugin.Logger.LogError($"Error in TrainerController.LateUpdate: {ex}");
                _ghostMode.StopPlayback();
                _ghostMode.CancelAttempt();
            }
        }

        /// <summary>Called when the game is closing: a hand-made ghost recording is saved.</summary>
        public void Shutdown()
        {
            try
            {
                _ghostMode?.Shutdown();
            }
            catch (Exception ex)
            {
                TrainerPlugin.Logger.LogError($"Error in TrainerController.Shutdown: {ex}");
            }
        }

        public void OnGUI()
        {
            try
            {
                if (!enabled) return;
                _overlayRenderer.OnGUI();
                _bindMenuRenderer?.Draw();
            }
            catch (Exception ex)
            {
                TrainerPlugin.Logger.LogError($"Error in TrainerController.OnGUI: {ex}");
            }
        }
        
        private void HandleMenuFreeze()
        {
            try
            {
                bool menuVisible = _bindMenuRenderer.IsVisible;

                if (menuVisible && !_wasMenuVisible)
                    BeginFreeze();
                else if (!menuVisible && _wasMenuVisible)
                    EndFreeze();

                if (menuVisible)
                    ApplyFreeze();

                _wasMenuVisible = menuVisible;
            }
            catch (Exception ex)
            {
                TrainerPlugin.Logger.LogWarning($"Error in HandleMenuFreeze: {ex.Message}");
            }
        }

        private void BeginFreeze()
        {
            // The attempt's clock would keep running on a frozen phone.
            _ghostMode.CancelAttempt();

            var t = _gameObjectFinder.FindPlayerTransform();
            if (t != null)
            {
                _frozenPosition = t.position;
                _frozenRotation = t.rotation;
            }

            var rb = _gameObjectFinder.GetCachedPlayerRigidbody();
            if (rb != null)
            {
                _frozenPosition = rb.position;
                _frozenRotation = rb.rotation;
                _frozenLinVel = rb.linearVelocity;
                _frozenAngVel = rb.angularVelocity;
                _frozenKinematic = rb.isKinematic;
                rb.isKinematic = true;
            }
        }

        private void ApplyFreeze()
        {
            // Body is kinematic while frozen, so only pin position/rotation.
            // Setting velocity on a kinematic body logs warnings and is unnecessary.
            var rb = _gameObjectFinder.GetCachedPlayerRigidbody();
            if (rb != null)
            {
                rb.position = _frozenPosition;
                rb.rotation = _frozenRotation;
            }

            var t = _gameObjectFinder.FindPlayerTransform();
            if (t != null)
            {
                t.position = _frozenPosition;
                t.rotation = _frozenRotation;
            }
        }

        private void EndFreeze()
        {
            var rb = _gameObjectFinder.GetCachedPlayerRigidbody();
            if (rb != null)
            {
                rb.isKinematic = _frozenKinematic;
                rb.linearVelocity = _frozenLinVel;
                rb.angularVelocity = _frozenAngVel;
            }
        }

        private void HandleHotkeys()
        {
            // Save position (Shift + R)
            if (_inputHandler.IsSavePositionPressed())
            {
                SavePosition();
            }
            
            // Teleport to saved position (R); in ghost mode, back to the ghost start instead
            if (_inputHandler.IsTeleportPressed())
            {
                if (_ghostMode.Active)
                    RestartGhost();
                else
                    TeleportToSavedPosition();
            }
            
            // Toggle fly mode (F)
            if (_inputHandler.IsToggleFlyModePressed())
            {
                ToggleFlyMode();
            }
            
            // Toggle keep velocity (V)
            if (_inputHandler.IsToggleKeepVelocityPressed())
            {
                _keepVelocityActive = !_keepVelocityActive;
                TrainerPlugin.Logger.LogInfo($"Keep Velocity {(_keepVelocityActive ? "activated" : "deactivated")}");
            }
            
            // Toggle keep angle (C)
            if (_inputHandler.IsToggleKeepAnglePressed())
            {
                _keepAngleActive = !_keepAngleActive;
                TrainerPlugin.Logger.LogInfo($"Keep Angle {(_keepAngleActive ? "activated" : "deactivated")}");
            }

            // Toggle auto-repair on teleport (G)
            if (_inputHandler.IsToggleAutoRepairPressed())
            {
                _autoRepairActive = !_autoRepairActive;
                TrainerPlugin.Logger.LogInfo($"Auto-Repair {(_autoRepairActive ? "activated" : "deactivated")}");
            }

            // Toggle help beam in singleplayer (N). Only gates new spawns: a beam that is
            // already up goes away on release, as usual.
            if (_inputHandler.IsToggleHelpBeamPressed())
            {
                TrainerConfig.Settings.HelpBeamSingleplayer = !TrainerConfig.Settings.HelpBeamSingleplayer;
                TrainerPlugin.Logger.LogInfo($"Help Beam SP {(TrainerConfig.Settings.HelpBeamSingleplayer ? "activated" : "deactivated")}");
            }

            // Ghost mode (H). Its other binds only work while it is on, like the HUD only
            // shows them then: set start (Y), set end (P), record (U), play (I), spectate (O).
            // Polled every frame either way, so a press made while off doesn't fire on turning it on.
            if (_inputHandler.IsToggleGhostModePressed())
                _ghostMode.ToggleActive();
            bool setStart = _inputHandler.IsGhostSetStartPressed();
            bool setEnd = _inputHandler.IsGhostSetEndPressed();
            bool record = _inputHandler.IsGhostRecordPressed();
            bool play = _inputHandler.IsGhostPlayPressed();
            bool spectate = _inputHandler.IsGhostSpectatePressed();
            if (_ghostMode.Active)
            {
                if (setStart) _ghostMode.SetStart();
                if (setEnd) _ghostMode.SetEnd();
                if (record) _ghostMode.ToggleRecording();
                if (play) _ghostMode.TogglePlay();
                if (spectate) _ghostMode.ToggleSpectate();
            }
        }

        private void RestartGhost()
        {
            // An attempt starts on the ground, not flying through walls.
            if (_flyModeActive)
                ToggleFlyMode();

            if (_ghostMode.Restart())
                RepairIfEnabled();
        }
        
        private void SavePosition()
        {
            try
            {
                var playerTransform = _gameObjectFinder.FindPlayerTransform();
                if (playerTransform != null)
                {
                    _savedPosition = playerTransform.position;
                    _savedRotation = playerTransform.rotation;
                    
                    var cameraTransform = _gameObjectFinder.FindCameraTransform();
                    if (cameraTransform != null)
                    {
                        _savedCameraRotation = cameraTransform.rotation;
                    }
                    else
                    {
                        _savedCameraRotation = Quaternion.identity;
                    }

                    var rigidbody = _gameObjectFinder.GetCachedPlayerRigidbody();
                    if (rigidbody != null)
                    {
                        _savedLinearVelocity = rigidbody.linearVelocity;
                        _savedAngularVelocity = rigidbody.angularVelocity;
                    }
                    else
                    {
                        _savedLinearVelocity = Vector3.zero;
                        _savedAngularVelocity = Vector3.zero;
                    }

                    _hasSavedPosition = true;
                    
                    TrainerPlugin.Logger.LogInfo($"Position saved: {_savedPosition}");
                    _overlayRenderer.SetPositionSaved(true);
                }
                else
                {
                    TrainerPlugin.Logger.LogWarning("Could not save position: Player not found");
                }
            }
            catch (Exception ex)
            {
                TrainerPlugin.Logger.LogError($"Error saving position: {ex}");
            }
        }
        
        private void TeleportToSavedPosition()
        {
            try
            {
                if (!_hasSavedPosition)
                {
                    TrainerPlugin.Logger.LogWarning("Cannot teleport: No position saved");
                    return;
                }
                
                var playerTransform = _gameObjectFinder.FindPlayerTransform();
                if (playerTransform != null)
                {
                    var rigidbody = _gameObjectFinder.GetCachedPlayerRigidbody();
                    
                    if (rigidbody != null)
                    {
                        // Fix for physics engine: use Rigidbody position directly and force sync
                        rigidbody.position = _savedPosition;
                        rigidbody.rotation = _savedRotation;
                        
                        // We briefly toggle isKinematic to force the physics engine to sync the position immediately
                        bool wasKinematic = rigidbody.isKinematic;
                        rigidbody.isKinematic = true;
                        rigidbody.isKinematic = wasKinematic;
                        
                        if (_keepVelocityActive)
                        {
                            if (_keepAngleActive)
                            {
                                // Restore exact global velocities
                                rigidbody.linearVelocity = _savedLinearVelocity;
                                rigidbody.angularVelocity = _savedAngularVelocity;
                            }
                            else
                            {
                                // Rotate velocity relative to current camera
                                var cameraTransform = _gameObjectFinder.FindCameraTransform();
                                if (cameraTransform != null)
                                {
                                    Quaternion rotationDifference = cameraTransform.rotation * Quaternion.Inverse(_savedCameraRotation);
                                    rigidbody.linearVelocity = rotationDifference * _savedLinearVelocity;
                                    rigidbody.angularVelocity = rotationDifference * _savedAngularVelocity;
                                }
                                else
                                {
                                    // Fallback if no camera found
                                    rigidbody.linearVelocity = _savedLinearVelocity;
                                    rigidbody.angularVelocity = _savedAngularVelocity;
                                }
                            }
                        }
                        else
                        {
                            rigidbody.linearVelocity = Vector3.zero;
                            rigidbody.angularVelocity = Vector3.zero;
                        }
                    }
                    
                    playerTransform.position = _savedPosition;
                    playerTransform.rotation = _savedRotation;

                    RepairIfEnabled();

                    TrainerPlugin.Logger.LogInfo($"Teleported to saved position: {_savedPosition}");
                }
                else
                {
                    TrainerPlugin.Logger.LogWarning("Could not teleport: Player not found");
                }
            }
            catch (Exception ex)
            {
                TrainerPlugin.Logger.LogError($"Error teleporting: {ex}");
            }
        }
        
        // Return the phone repaired when the toggle is on. We call the game's own reassemble
        // RPC unconditionally — if the phone isn't broken the server no-ops.
        // (PhoneContext.IsDestroyed proved unreliable as a gate.)
        private void RepairIfEnabled()
        {
            if (!_autoRepairActive) return;

            var localPlayer = _gameObjectFinder.FindPlayer();
            if (localPlayer != null)
            {
                PhoneRepair.IsPhoneBroken(localPlayer); // logs IsDestroyed for diagnostics
                PhoneRepair.RepairPhone(localPlayer);
            }
        }

        // Hard-disable the game's native fly cheat component so it can never run. Its toggle
        // is Shift+F, which collides with the trainer's fly (F) + turbo (Shift); disabling the
        // component stops its FixedUpdate/HandleFlying entirely, leaving ONLY the trainer's fly.
        private void DisableNativeFly()
        {
            try
            {
                var flyCheat = _gameObjectFinder.GetFlyCheat();
                if (flyCheat != null && flyCheat.enabled)
                {
                    flyCheat.enabled = false;
                    TrainerPlugin.Logger.LogInfo("[FLY] Disabled native EHS.FlyCheat (only trainer fly remains).");
                }
            }
            catch (Exception ex)
            {
                TrainerPlugin.Logger.LogWarning($"[FLY] Error disabling native fly: {ex.Message}");
            }
        }

        private void ToggleFlyMode()
        {
            try
            {
                _flyModeActive = !_flyModeActive;
                _overlayRenderer.SetFlyModeActive(_flyModeActive);

                // Flying through walls doesn't count as an attempt.
                if (_flyModeActive)
                    _ghostMode.CancelAttempt();

                var rb = _gameObjectFinder.GetCachedPlayerRigidbody();
                if (rb != null)
                {
                    if (_flyModeActive)
                    {
                        // Take full manual control: kinematic so the game's physics/prediction
                        // doesn't fight us, no gravity, no collisions (noclip). We move by
                        // position, which is valid on a kinematic body (no velocity warnings).
                        _flyWasKinematic = rb.isKinematic;
                        _flyWasGravity = rb.useGravity;
                        _flyWasCollisions = rb.detectCollisions;

                        rb.isKinematic = true;
                        rb.useGravity = false;
                        rb.detectCollisions = false;
                    }
                    else
                    {
                        rb.useGravity = _flyWasGravity;
                        rb.detectCollisions = _flyWasCollisions;
                        rb.isKinematic = _flyWasKinematic;

                        // Conserve momentum: carry the last fly velocity over. Set it AFTER
                        // restoring the non-kinematic state so it actually applies (and no
                        // "velocity on kinematic body" warning). If you weren't moving when you
                        // exited, _lastFlyVelocity is ~0 and you simply drop.
                        if (!rb.isKinematic)
                        {
                            rb.linearVelocity = _lastFlyVelocity;
                            rb.angularVelocity = Vector3.zero;
                        }
                    }
                }

                TrainerPlugin.Logger.LogInfo($"[FLY] Trainer fly {(_flyModeActive ? "ON" : "OFF")}");
            }
            catch (Exception ex)
            {
                TrainerPlugin.Logger.LogError($"Error toggling fly mode: {ex}");
            }
        }

        private void HandleFlyMode()
        {
            try
            {
                var cameraTransform = _gameObjectFinder.FindCameraTransform();
                var rb = _gameObjectFinder.GetCachedPlayerRigidbody();
                if (cameraTransform == null || rb == null) return;

                Vector3 flyVelocity = CalculateFlyVelocity(cameraTransform);
                _lastFlyVelocity = flyVelocity; // remembered for momentum on exit

                Vector3 displacement = flyVelocity * Time.deltaTime;
                if (displacement == Vector3.zero) return;

                // Move by position (kinematic-safe; setting velocity would be ignored + warn).
                Vector3 target = rb.position + displacement;
                rb.position = target;
                var t = _gameObjectFinder.FindPlayerTransform();
                if (t != null) t.position = target;
            }
            catch (Exception ex)
            {
                TrainerPlugin.Logger.LogError($"Error in HandleFlyMode: {ex}");
            }
        }

        private Vector3 CalculateFlyVelocity(Transform cameraTransform)
        {
            // Horizontal movement relative to where the camera looks.
            Vector3 forward = cameraTransform.forward;
            Vector3 right = cameraTransform.right;
            forward.y = 0;
            right.y = 0;
            if (forward.magnitude > 0.001f) forward.Normalize();
            if (right.magnitude > 0.001f) right.Normalize();

            float speed = _flySpeed;
            if (_inputHandler.IsFlyTurboHeld()) speed *= _flySpeedBoost;

            Vector3 velocity = Vector3.zero;
            if (_inputHandler.IsFlyForwardHeld()) velocity += forward * speed;
            if (_inputHandler.IsFlyBackwardHeld()) velocity -= forward * speed;
            if (_inputHandler.IsFlyLeftHeld()) velocity -= right * speed;
            if (_inputHandler.IsFlyRightHeld()) velocity += right * speed;
            if (_inputHandler.IsFlyUpHeld()) velocity.y += speed;
            if (_inputHandler.IsFlyDownHeld()) velocity.y -= speed;

            return velocity;
        }

        private void UpdateCurrentPosition()
        {
            try
            {
                var playerTransform = _gameObjectFinder.FindPlayerTransform();
                if (playerTransform != null)
                {
                    _currentPosition = playerTransform.position;
                }
            }
            catch (Exception ex)
            {
                TrainerPlugin.Logger.LogWarning($"Error updating position: {ex.Message}");
            }
        }
        
        private void UpdateOverlay()
        {
            try
            {
                float speed = 0f;
                var rigidbody = _gameObjectFinder.GetCachedPlayerRigidbody();
                if (rigidbody != null)
                {
                    speed = rigidbody.linearVelocity.magnitude;
                }

                _overlayRenderer.UpdateData(
                    _currentPosition,
                    speed,
                    _hasSavedPosition,
                    _flyModeActive,
                    _keepVelocityActive,
                    _keepAngleActive,
                    _autoRepairActive,
                    TrainerConfig.Settings.HelpBeamSingleplayer
                );
                _overlayRenderer.UpdateGhost(
                    _ghostMode.Active,
                    _ghostMode.Course.HasStart,
                    _ghostMode.Course.HasEnd,
                    _ghostMode.IsRecording,
                    _ghostMode.IsPlaying && !_ghostMode.Spectating,
                    _ghostMode.Spectating,
                    _ghostMode.StatusText()
                );
            }
            catch (Exception ex)
            {
                TrainerPlugin.Logger.LogWarning($"Error updating overlay: {ex.Message}");
            }
        }
    }
}