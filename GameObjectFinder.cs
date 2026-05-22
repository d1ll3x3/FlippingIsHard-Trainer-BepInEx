using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FlippingIsHardTrainer
{
    public class GameObjectFinder
    {
        // Cached references
        private GameObject _cachedPlayer;
        private GameObject _cachedCamera;
        private float _lastPlayerFindTime = 0f;
        private float _lastCameraFindTime = 0f;
        private const float CACHE_DURATION = 5f;
        
        public Transform FindPlayerTransform()
        {
            var player = FindPlayer();
            return player?.transform;
        }
        
        public Transform FindCameraTransform()
        {
            var camera = FindCamera();
            return camera?.transform;
        }
        
        public GameObject FindPlayer()
        {
            // Return cached player if still valid
            if (_cachedPlayer != null && Time.time - _lastPlayerFindTime < CACHE_DURATION)
                return _cachedPlayer;
            
            TrainerPlugin.Logger.LogInfo("Searching for player GameObject...");
            
            // Method 1: Try to find by tag
            try
            {
                _cachedPlayer = GameObject.FindWithTag("Player");
                if (_cachedPlayer != null)
                {
                    _lastPlayerFindTime = Time.time;
                    TrainerPlugin.Logger.LogInfo($"Found player by tag: {_cachedPlayer.name}");
                    return _cachedPlayer;
                }
            }
            catch (Exception ex)
            {
                TrainerPlugin.Logger.LogWarning($"Error finding player by tag: {ex.Message}");
            }
            
            // Method 2: Try common player object names
            string[] playerNames = {
                "Flippy", "Player", "Phone", "PlayerPhone",
                "RetroPhone", "CellPhone", "Nokia", "BrickPhone",
                "player", "PLAYER", "MainPlayer", "Character",
                "Hero", "MainCharacter", "PlayerCharacter"
            };
            
            foreach (var name in playerNames)
            {
                try
                {
                    _cachedPlayer = GameObject.Find(name);
                    if (_cachedPlayer != null)
                    {
                        _lastPlayerFindTime = Time.time;
                        TrainerPlugin.Logger.LogInfo($"Found player by name '{name}': {_cachedPlayer.name}");
                        return _cachedPlayer;
                    }
                }
                catch (Exception ex)
                {
                    TrainerPlugin.Logger.LogWarning($"Error finding player by name '{name}': {ex.Message}");
                }
            }
            
            // Method 3: Search all GameObjects in active scenes
            try
            {
                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    Scene scene = SceneManager.GetSceneAt(i);
                    if (!scene.isLoaded) continue;
                    
                    GameObject[] rootObjects = scene.GetRootGameObjects();
                    TrainerPlugin.Logger.LogInfo($"Scene '{scene.name}' has {rootObjects.Length} root objects");
                    
                    foreach (GameObject root in rootObjects)
                    {
                        // Check root
                        if (IsPlayerObject(root))
                        {
                            _cachedPlayer = root;
                            _lastPlayerFindTime = Time.time;
                            TrainerPlugin.Logger.LogInfo($"Found player in scene '{scene.name}': {root.name}");
                            return _cachedPlayer;
                        }
                        
                        // Check children recursively
                        _cachedPlayer = SearchChildrenForPlayer(root.transform);
                        if (_cachedPlayer != null)
                        {
                            _lastPlayerFindTime = Time.time;
                            TrainerPlugin.Logger.LogInfo($"Found player in scene '{scene.name}': {_cachedPlayer.name}");
                            return _cachedPlayer;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                TrainerPlugin.Logger.LogWarning($"Error searching scenes for player: {ex.Message}");
            }
            
            TrainerPlugin.Logger.LogWarning("Player not found");
            _cachedPlayer = null;
            return null;
        }
        
        public GameObject FindCamera()
        {
            // Return cached camera if still valid
            if (_cachedCamera != null && Time.time - _lastCameraFindTime < CACHE_DURATION)
                return _cachedCamera;
            
            TrainerPlugin.Logger.LogInfo("Searching for camera GameObject...");
            
            // Method 1: Try to find by tag
            try
            {
                _cachedCamera = GameObject.FindWithTag("MainCamera");
                if (_cachedCamera != null)
                {
                    _lastCameraFindTime = Time.time;
                    TrainerPlugin.Logger.LogInfo($"Found camera by tag: {_cachedCamera.name}");
                    return _cachedCamera;
                }
            }
            catch (Exception ex)
            {
                TrainerPlugin.Logger.LogWarning($"Error finding camera by tag: {ex.Message}");
            }
            
            // Method 2: Try common camera object names
            string[] cameraNames = {
                "Main Camera", "Camera", "PlayerCamera",
                "ThirdPersonCamera", "FollowCamera", "CinemachineCamera",
                "MainCamera", "main camera", "camera", "Main",
                "CameraController", "CameraRig"
            };
            
            foreach (var name in cameraNames)
            {
                try
                {
                    _cachedCamera = GameObject.Find(name);
                    if (_cachedCamera != null)
                    {
                        _lastCameraFindTime = Time.time;
                        TrainerPlugin.Logger.LogInfo($"Found camera by name '{name}': {_cachedCamera.name}");
                        return _cachedCamera;
                    }
                }
                catch (Exception ex)
                {
                    TrainerPlugin.Logger.LogWarning($"Error finding camera by name '{name}': {ex.Message}");
                }
            }
            
            // Method 3: Search all GameObjects in active scenes for Camera component
            try
            {
                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    Scene scene = SceneManager.GetSceneAt(i);
                    if (!scene.isLoaded) continue;
                    
                    GameObject[] rootObjects = scene.GetRootGameObjects();
                    foreach (GameObject root in rootObjects)
                    {
                        // Check root
                        if (root.GetComponent<Camera>() != null)
                        {
                            _cachedCamera = root;
                            _lastCameraFindTime = Time.time;
                            TrainerPlugin.Logger.LogInfo($"Found camera in scene '{scene.name}': {root.name}");
                            return _cachedCamera;
                        }
                        
                        // Check children
                        _cachedCamera = SearchChildrenForCamera(root.transform);
                        if (_cachedCamera != null)
                        {
                            _lastCameraFindTime = Time.time;
                            TrainerPlugin.Logger.LogInfo($"Found camera in scene '{scene.name}': {_cachedCamera.name}");
                            return _cachedCamera;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                TrainerPlugin.Logger.LogWarning($"Error searching scenes for camera: {ex.Message}");
            }
            
            TrainerPlugin.Logger.LogWarning("Camera not found");
            _cachedCamera = null;
            return null;
        }
        
        private GameObject SearchChildrenForPlayer(Transform parent)
        {
            try
            {
                for (int i = 0; i < parent.childCount; i++)
                {
                    Transform child = parent.GetChild(i);
                    if (child == null) continue;
                    
                    if (IsPlayerObject(child.gameObject))
                        return child.gameObject;
                    
                    // Recurse
                    GameObject found = SearchChildrenForPlayer(child);
                    if (found != null)
                        return found;
                }
            }
            catch (Exception ex)
            {
                TrainerPlugin.Logger.LogWarning($"Error searching children for player: {ex.Message}");
            }
            
            return null;
        }
        
        private GameObject SearchChildrenForCamera(Transform parent)
        {
            try
            {
                for (int i = 0; i < parent.childCount; i++)
                {
                    Transform child = parent.GetChild(i);
                    if (child == null) continue;
                    
                    if (child.GetComponent<Camera>() != null)
                        return child.gameObject;
                    
                    // Recurse
                    GameObject found = SearchChildrenForCamera(child);
                    if (found != null)
                        return found;
                }
            }
            catch (Exception ex)
            {
                TrainerPlugin.Logger.LogWarning($"Error searching children for camera: {ex.Message}");
            }
            
            return null;
        }
        
        private bool IsPlayerObject(GameObject go)
        {
            if (go == null) return false;
            
            string name = go.name.ToLower();
            
            // Check name
            if (name.Contains("player") || name.Contains("flippy") || name.Contains("phone") ||
                name.Contains("character") || name.Contains("hero") || name.Contains("main"))
                return true;
            
            // Check for Rigidbody (player usually has one)
            try
            {
                if (go.GetComponent<Rigidbody>() != null)
                {
                    // Additional check: player usually has a collider too
                    if (go.GetComponent<Collider>() != null)
                        return true;
                }
            }
            catch { }
            
            return false;
        }
        
        public void ClearCache()
        {
            _cachedPlayer = null;
            _cachedCamera = null;
            _lastPlayerFindTime = 0f;
            _lastCameraFindTime = 0f;
        }
    }
}