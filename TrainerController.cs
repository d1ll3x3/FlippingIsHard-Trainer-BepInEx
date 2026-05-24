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
        private bool _flyModeActive = false;
        
        // Configuration
        private float _flySpeed = 15.0f;
        private float _flySpeedBoost = 3.0f;
        
        // Component references
        private GameObjectFinder _gameObjectFinder;
        private InputHandler _inputHandler;
        private OverlayRenderer _overlayRenderer;
        
        // Current state for overlay
        private Vector3 _currentPosition = Vector3.zero;
        
        public bool enabled { get; set; }
        
        public void Initialize()
        {
            try
            {
                // Initialize components
                _gameObjectFinder = new GameObjectFinder();
                _inputHandler = new InputHandler();
                _overlayRenderer = new OverlayRenderer();
                
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
                
                // Handle trainer hotkeys
                HandleHotkeys();
                
                // Handle fly mode if active
                if (_flyModeActive)
                {
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
        
        public void OnGUI()
        {
            try
            {
                if (!enabled) return;
                _overlayRenderer.OnGUI();
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
                    playerTransform.position = _savedPosition;
                    playerTransform.rotation = _savedRotation;
                    
                    // Reset velocity if player has Rigidbody
                    ResetPlayerVelocity(playerTransform.gameObject);
                    
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
                var playerTransform = _gameObjectFinder.FindPlayerTransform();
                var cameraTransform = _gameObjectFinder.FindCameraTransform();
                
                if (playerTransform == null || cameraTransform == null)
                    return;
                
                // Calculate movement based on camera direction
                Vector3 movement = CalculateFlyMovement(cameraTransform);
                
                if (movement != Vector3.zero)
                {
                    playerTransform.position += movement;
                    ResetPlayerVelocity(playerTransform.gameObject);
                }
            }
            catch (Exception ex)
            {
                TrainerPlugin.Logger.LogError($"Error in HandleFlyMode: {ex}");
            }
        }
        
        private Vector3 CalculateFlyMovement(Transform cameraTransform)
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
            
            speed *= Time.deltaTime;
            
            // Calculate movement
            Vector3 movement = Vector3.zero;
            
            if (_inputHandler.IsWPressed()) movement += forward * speed;
            if (_inputHandler.IsSPressed()) movement -= forward * speed;
            if (_inputHandler.IsAPressed()) movement -= right * speed;
            if (_inputHandler.IsDPressed()) movement += right * speed;
            if (_inputHandler.IsSpacePressed()) movement.y += speed;
            if (_inputHandler.IsCtrlPressed()) movement.y -= speed;
            
            return movement;
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
                _overlayRenderer.UpdateData(
                    _currentPosition,
                    _hasSavedPosition,
                    _flyModeActive
                );
            }
            catch (Exception ex)
            {
                TrainerPlugin.Logger.LogWarning($"Error updating overlay: {ex.Message}");
            }
        }
    }
}