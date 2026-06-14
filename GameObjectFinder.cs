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

        // Cached references to the game's own fly cheat and its hotkey handler.
        private EHS.FlyCheat _cachedFlyCheat;
        private global::CheatsHotKeyHandlerMono _cachedCheatsHandler;

        // The game's native fly cheat component (FlyCheat.SetExternalFlyEnabled / IsFlying).
        public EHS.FlyCheat GetFlyCheat()
        {
            if (_cachedFlyCheat != null)
                return _cachedFlyCheat;
            try
            {
                _cachedFlyCheat = UnityEngine.Object.FindObjectOfType<EHS.FlyCheat>();
            }
            catch (Exception)
            {
                // Silent catch
            }
            return _cachedFlyCheat;
        }

        // The game's cheat hotkey handler. We disable it so the game doesn't toggle fly on
        // its own keys (the trainer becomes the sole controller of the fly cheat).
        public global::CheatsHotKeyHandlerMono GetCheatsHotKeyHandler()
        {
            if (_cachedCheatsHandler != null)
                return _cachedCheatsHandler;
            try
            {
                _cachedCheatsHandler = UnityEngine.Object.FindObjectOfType<global::CheatsHotKeyHandlerMono>();
            }
            catch (Exception)
            {
                // Silent catch
            }
            return _cachedCheatsHandler;
        }
        
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
        
        public void ClearCache()
        {
            _cachedPlayer = null;
            _cachedPlayerRigidbody = null;
            _cachedCamera = null;
            _cachedFlyCheat = null;
            _cachedCheatsHandler = null;
        }
    }
}