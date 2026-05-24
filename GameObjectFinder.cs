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
        
        // Aumentamos la duración de la caché para reducir drásticamente las búsquedas
        private const float CACHE_DURATION = 15f; 
        
        // Tiempo de espera (cooldown) si la búsqueda falla, para no inundar el log ni causar lag
        private float _playerSearchCooldown = 0f;
        private float _cameraSearchCooldown = 0f;
        private const float SEARCH_COOLDOWN_DURATION = 2f;
        
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
                
            // Check cooldown si ha fallado recientemente
            if (Time.time < _playerSearchCooldown)
                return null;
            
            // TrainerPlugin.Logger.LogInfo("Searching for player GameObject..."); // Comentado para evitar spam en log
            
            // Method 1: Try to find by tag
            try
            {
                _cachedPlayer = GameObject.FindWithTag("Player");
                if (_cachedPlayer != null)
                {
                    _lastPlayerFindTime = Time.time;
                    // TrainerPlugin.Logger.LogInfo($"Found player by tag: {_cachedPlayer.name}");
                    return _cachedPlayer;
                }
            }
            catch (Exception)
            {
                // Silent catch
            }
            
            // Si llegamos aquí, no lo encontró, aplicamos cooldown de 2 segundos antes de volver a buscar
            _playerSearchCooldown = Time.time + SEARCH_COOLDOWN_DURATION;
            _cachedPlayer = null;
            return null;
        }
        
        public GameObject FindCamera()
        {
            // Return cached camera if still valid
            if (_cachedCamera != null && Time.time - _lastCameraFindTime < CACHE_DURATION)
                return _cachedCamera;
                
            // Check cooldown si ha fallado recientemente
            if (Time.time < _cameraSearchCooldown)
                return null;
            
            // TrainerPlugin.Logger.LogInfo("Searching for camera GameObject..."); // Comentado para evitar spam en log
            
            // Method 1: Try to find by tag
            try
            {
                _cachedCamera = GameObject.FindWithTag("MainCamera");
                if (_cachedCamera != null)
                {
                    _lastCameraFindTime = Time.time;
                    // TrainerPlugin.Logger.LogInfo($"Found camera by tag: {_cachedCamera.name}");
                    return _cachedCamera;
                }
            }
            catch (Exception)
            {
                // Silent catch
            }
            
            // Si llegamos aquí, no lo encontró, aplicamos cooldown de 2 segundos antes de volver a buscar
            _cameraSearchCooldown = Time.time + SEARCH_COOLDOWN_DURATION;
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