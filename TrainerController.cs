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
        private bool _wasUsingGravity = true;
        private bool _wasDetectingCollisions = true;
        
        // Configuration
        private float _flySpeed = 15.0f;
        private float _flySpeedBoost = 3.0f;
        
        // Component references
        private GameObjectFinder _gameObjectFinder;
        private InputHandler _inputHandler;
        private OverlayRenderer _overlayRenderer;
        private BindMenuRenderer _bindMenuRenderer;
        
        // Current state for overlay
        private Vector3 _currentPosition = Vector3.zero;
        
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
                _bindMenuRenderer = new BindMenuRenderer(_gameObjectFinder);
                
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

                // Open bind menu
                if (_inputHandler.IsOpenBindMenuPressed())
                {
                    _bindMenuRenderer.ToggleVisibility();
                }
                
                // Handle trainer hotkeys only if menu is not visible
                if (!_bindMenuRenderer.IsVisible)
                {
                    HandleHotkeys();
                    
                    // Handle fly mode if active
                    if (_flyModeActive)
                    {
                        HandleFlyMode();
                    }
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
        
        private void HandleHotkeys()
        {
            // Save position (Shift + R)
            if (_inputHandler.IsSavePositionPressed())
            {
                SavePosition();
            }
            
            // Teleport to saved position (R)
            if (_inputHandler.IsTeleportPressed())
            {
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
        
        private void ToggleFlyMode()
        {
            try
            {
                _flyModeActive = !_flyModeActive;
                _overlayRenderer.SetFlyModeActive(_flyModeActive);
                
                var rigidbody = _gameObjectFinder.GetCachedPlayerRigidbody();
                if (rigidbody != null)
                {
                    if (_flyModeActive)
                    {
                        _wasUsingGravity = rigidbody.useGravity;
                        _wasDetectingCollisions = rigidbody.detectCollisions;
                        
                        rigidbody.useGravity = false;
                        rigidbody.detectCollisions = false;
                        rigidbody.linearVelocity = Vector3.zero;
                        rigidbody.angularVelocity = Vector3.zero;
                    }
                    else
                    {
                        rigidbody.useGravity = _wasUsingGravity;
                        rigidbody.detectCollisions = _wasDetectingCollisions;
                        
                        // Reseteamos inercia al salir del modo vuelo
                        rigidbody.linearVelocity = Vector3.zero;
                        rigidbody.angularVelocity = Vector3.zero;
                    }
                }
                
                TrainerPlugin.Logger.LogInfo($"Fly mode {(_flyModeActive ? "activated" : "deactivated")}");
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
                var rigidbody = _gameObjectFinder.GetCachedPlayerRigidbody();
                
                if (cameraTransform == null || rigidbody == null)
                    return;
                
                // Calculate target velocity based on input
                Vector3 targetVelocity = CalculateFlyVelocity(cameraTransform);
                
                // Apply velocity directly (smooth physical integration & noclip)
                rigidbody.linearVelocity = targetVelocity;
                rigidbody.angularVelocity = Vector3.zero;
            }
            catch (Exception ex)
            {
                TrainerPlugin.Logger.LogError($"Error in HandleFlyMode: {ex}");
            }
        }
        
        private Vector3 CalculateFlyVelocity(Transform cameraTransform)
        {
            // Get camera forward and right vectors (horizontal plane only)
            Vector3 forward = cameraTransform.forward;
            Vector3 right = cameraTransform.right;
            forward.y = 0;
            right.y = 0;
            
            if (forward.magnitude > 0.001f) forward.Normalize();
            if (right.magnitude > 0.001f) right.Normalize();
            
            // Calculate speed
            float speed = _flySpeed;
            if (_inputHandler.IsShiftPressed())
                speed *= _flySpeedBoost;
            
            // Calculate velocity vector
            Vector3 velocity = Vector3.zero;
            
            if (_inputHandler.IsWPressed()) velocity += forward * speed;
            if (_inputHandler.IsSPressed()) velocity -= forward * speed;
            if (_inputHandler.IsAPressed()) velocity -= right * speed;
            if (_inputHandler.IsDPressed()) velocity += right * speed;
            if (_inputHandler.IsSpacePressed()) velocity.y += speed;
            if (_inputHandler.IsCtrlPressed()) velocity.y -= speed;
            
            return velocity;
        }
        
        private void ResetPlayerVelocity(GameObject player)
        {
            try
            {
                // Try to get Rigidbody component
                var rigidbody = player.GetComponent<Rigidbody>();
                if (rigidbody != null)
                {
                    rigidbody.linearVelocity = Vector3.zero;
                    rigidbody.angularVelocity = Vector3.zero;
                }
            }
            catch (Exception ex)
            {
                TrainerPlugin.Logger.LogWarning($"Error resetting velocity: {ex.Message}");
            }
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
                    _keepAngleActive
                );
            }
            catch (Exception ex)
            {
                TrainerPlugin.Logger.LogWarning($"Error updating overlay: {ex.Message}");
            }
        }
    }
}