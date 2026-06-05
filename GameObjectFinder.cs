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
        private Rigidbody _cachedPlayerRigidbody;
        private GameObject _cachedCamera;
        
        // Tiempo de espera (cooldown) si la búsqueda falla, para no inundar el log ni causar lag
        private float _playerSearchCooldown = 0f;
        private float _cameraSearchCooldown = 0f;
        private const float SEARCH_COOLDOWN_DURATION = 2f;
        
        public Rigidbody GetCachedPlayerRigidbody() => _cachedPlayerRigidbody;
        
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
            if (_cachedPlayer != null)
                return _cachedPlayer;
                
            // Check cooldown si ha fallado recientemente
            if (Time.time < _playerSearchCooldown)
                return null;
            
            // Method 1: Try to find by tag
            try
            {
                _cachedPlayer = GameObject.FindWithTag("Player");
                if (_cachedPlayer != null)
                {
                    _cachedPlayerRigidbody = _cachedPlayer.GetComponent<Rigidbody>();
                    return _cachedPlayer;
                }
            }
            catch (Exception)
            {
                // Silent catch
            }
            
            // Si llegamos aquí, no lo encontró, aplicamos cooldown de 2 segundos antes de volver a buscar
            _playerSearchCooldown = Time.time + SEARCH_COOLDOWN_DURATION;
            _cachedPlayerRigidbody = null;
            return null;
        }
        
        public GameObject FindCamera()
        {
            // Return cached camera if still valid
            if (_cachedCamera != null)
                return _cachedCamera;
                
            // Check cooldown si ha fallado recientemente
            if (Time.time < _cameraSearchCooldown)
                return null;
            
            // Method 1: Try to find by tag
            try
            {
                _cachedCamera = GameObject.FindWithTag("MainCamera");
                if (_cachedCamera != null)
                {
                    return _cachedCamera;
                }
            }
            catch (Exception)
            {
                // Silent catch
            }
            
            // Si llegamos aquí, no lo encontró, aplicamos cooldown de 2 segundos antes de volver a buscar
            _cameraSearchCooldown = Time.time + SEARCH_COOLDOWN_DURATION;
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
            _cachedPlayerRigidbody = null;
            _cachedCamera = null;
        }
    }
}