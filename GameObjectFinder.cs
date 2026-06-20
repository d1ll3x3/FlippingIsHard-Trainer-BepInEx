using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Il2CppInterop.Runtime;

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
            // Return cached player if still valid. Unity overloads == so a destroyed
            // object (scene change / respawn) compares as null, auto-invalidating the cache.
            if (_cachedPlayer != null)
                return _cachedPlayer;

            // Check cooldown si ha fallado recientemente
            if (Time.time < _playerSearchCooldown)
                return null;

            try
            {
                // Multiplayer: there can be several "Player"-tagged objects (one per client).
                // We must control ONLY the local player; touching a remote player's rigidbody
                // does nothing useful (no network authority — the server overwrites it).
                var local = FindLocalPlayer();
                if (local != null)
                {
                    _cachedPlayer = local;
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

        // Returns the LOCAL player GameObject. In singleplayer this is just the single
        // tagged player; in multiplayer it picks the one this client owns.
        private GameObject FindLocalPlayer()
        {
            GameObject[] players = null;
            try { players = GameObject.FindGameObjectsWithTag("Player"); }
            catch { }

            // Fallback: no tagged players (different game version) → scan for PlayerNetworked.
            if (players == null || players.Length == 0)
            {
                var list = new List<GameObject>();
                try
                {
                    var all = UnityEngine.Object.FindObjectsOfType<MonoBehaviour>();
                    foreach (var mb in all)
                    {
                        if (mb != null && mb.GetIl2CppType() != null
                            && mb.GetIl2CppType().Name == "PlayerNetworked")
                            list.Add(mb.gameObject);
                    }
                }
                catch { }
                players = list.ToArray();
            }

            if (players == null || players.Length == 0)
                return null;

            // Singleplayer fast path: only one player, it's us.
            if (players.Length == 1)
                return players[0];

            // Multiplayer: find the one we own.
            foreach (var p in players)
            {
                if (IsLocalPlayer(p)) return p;
            }

            // Last-resort fallback: the player closest to the active camera is almost
            // always the local one (the camera is parented to / follows the local player).
            if (Camera.main != null)
            {
                GameObject best = null;
                float bestDist = float.MaxValue;
                var camPos = Camera.main.transform.position;
                foreach (var p in players)
                {
                    if (p == null) continue;
                    float d = Vector3.Distance(p.transform.position, camPos);
                    if (d < bestDist) { bestDist = d; best = p; }
                }
                if (best != null && bestDist <= 10f) return best;
            }

            return null;
        }

        // A player is "local" if it owns the active camera or its network component
        // reports IsOwner == true.
        private bool IsLocalPlayer(GameObject p)
        {
            if (p == null) return false;
            try
            {
                var cam = p.GetComponentInChildren<Camera>(false);
                if (cam != null && cam.isActiveAndEnabled) return true;

                var mbs = p.GetComponents<MonoBehaviour>();
                foreach (var mb in mbs)
                {
                    if (mb == null) continue;
                    var typeObj = mb.GetIl2CppType();
                    if (typeObj == null) continue;

                    var isOwnerProp = typeObj.GetProperty("IsOwner");
                    if (isOwnerProp == null) continue;

                    var method = isOwnerProp.GetGetMethod();
                    if (method == null) continue;

                    var res = method.Invoke(mb, null);
                    if (res != null && res.Unbox<bool>()) return true;
                }
            }
            catch { }
            return false;
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