using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace FlippingIsHardTrainer
{
    // Enables the multiplayer-only "help beam" (right-click) in singleplayer, replicating
    // the base-game rules:
    //   - Hold RMB to keep the beam up; releasing despawns it.
    //   - It can only be summoned while touching something (ground OR wall, via the game's
    //     own EHS.GroundContact). Holding RMB mid-air spawns it the moment you land.
    //     "Touching" is validated against the last contact point still being next to the
    //     player, because IsTouching() lingers a few frames after a spring-pad launch —
    //     that stale window was spawning phantom beams mid-flight.
    //   - Moving away from the beam (past PlayerSettingsSO.HelpBeamDistanceToCancel /
    //     HelpBeamHeightToCancel) despawns it and breaks the phone per the game's own
    //     HelpBeamDestructPhoneOnDespawn / HelpBeamMinDurationForDestruct settings.
    //   - Breaking the phone (springs, crashes...) kills the beam immediately. "Broken" is
    //     read from the destruction FSM (StateDestroyed), the game's authoritative signal —
    //     the destroyedVisual lags several frames when breaking mid-air.
    //
    // The game's own input path (EHS.PlayerInputHandler.OnInputHelpBeam) hard-blocks solo
    // play with an INLINED multiplayer check (Harmony-spoofing it was tried and the check
    // never fired — it no longer exists as a call in the native binary), so the input layer
    // is driven here and the spawn goes through the game's own TrySpawnHelpBeamClient
    // (which works in SP — verified, spawn level 0) with RPC fallbacks, via IL2CPP
    // reflection (same pattern as PhoneRepair).
    public static class HelpBeamUnlock
    {
        private const float SPAWN_CONFIRM_DELAY = 0.35f;
        private const float BREAK_CONFIRM_DELAY = 0.7f;
        private const int MAX_SPAWN_LEVEL = 2;
        private const int MAX_BREAK_LEVEL = 2;

        private const Il2CppSystem.Reflection.BindingFlags ALL_FLAGS =
            Il2CppSystem.Reflection.BindingFlags.Instance |
            Il2CppSystem.Reflection.BindingFlags.Static |
            Il2CppSystem.Reflection.BindingFlags.Public |
            Il2CppSystem.Reflection.BindingFlags.NonPublic;

        // Cached per local player. Unity's overloaded == makes destroyed components compare
        // as null, so these self-invalidate on scene change / respawn.
        private static MonoBehaviour _beam;            // PlayerHelpBeamV2
        private static MonoBehaviour _playerNetworked; // PlayerNetworked (phone destroy RPC)
        private static MonoBehaviour _playerRef;       // PlayerRef (destruction FSM)
        private static EHS.GroundContact _ground;      // contact check (ground or wall)

        private static Il2CppSystem.Reflection.MethodInfo _isMultiplayerClient;
        private static bool _mpLookupFailed;

        // Vanilla thresholds, read once from the game's PlayerSettingsSO.
        private static bool _settingsLoaded;
        private static float _distToCancel = 10f;
        private static float _heightToCancel = 8f;
        private static bool _destructOnDespawn = true;
        private static float _minDurationForDestruct = 0f;

        // Spawn escalation state
        private static int _pendingSpawnLevel = -1;
        private static float _pendingSpawnCheckAt;

        // Active-beam state
        private static Vector3 _spawnPos;
        private static Il2CppSystem.Object _spawnPosBoxed; // reused for RPC args
        private static float _spawnTime;
        private static bool _wasActive;

        // Break (phone destruct) escalation state
        private static int _pendingBreakLevel = -1;
        private static float _pendingBreakCheckAt;
        private static GameObject _player;

        // Set after a break or a fully-failed spawn: the button must be released and
        // pressed again before another spawn.
        private static bool _requireRepress;

        // Despawn throttle + deferred orphan cleanup. When the phone breaks with the beam
        // up, the SP host clears the player's beam reference WITHOUT despawning the object
        // ("No active help beam to despawn"), leaving permanent orphan beams — so shortly
        // after every despawn request any beam objects still around get destroyed.
        private static float _despawnRequestedAt = -999f;
        private static float _cleanupAt = -1f;

        public static void Update(GameObject localPlayer)
        {
            try
            {
                if (localPlayer == null) return;
                _player = localPlayer;

                // In real multiplayer the game handles the beam natively — stay out of the way.
                if (IsMultiplayer())
                {
                    ResetTransientState();
                    return;
                }

                Resolve(localPlayer);
                if (_beam == null) return;
                LoadVanillaSettings();

                if (_pendingBreakLevel >= 0 && Time.unscaledTime >= _pendingBreakCheckAt)
                    ConfirmPendingBreak();

                if (_pendingSpawnLevel >= 0 && Time.unscaledTime >= _pendingSpawnCheckAt)
                    ConfirmPendingSpawn();

                if (_cleanupAt > 0f && Time.unscaledTime >= _cleanupAt)
                {
                    _cleanupAt = -1f;
                    CleanupOrphanBeams();
                }

                var mouse = Mouse.current;
                if (mouse == null) return;
                bool held = mouse.rightButton.isPressed;
                bool active = IsBeamActive();

                // The beam vanished without us requesting it (e.g. a break cleared it
                // server-side): schedule the orphan cleanup. Whether a new beam may spawn
                // is governed by the usual rules below (FSM + sustained contact).
                if (_wasActive && !active && Time.unscaledTime - _despawnRequestedAt > 1f)
                {
                    _pendingSpawnLevel = -1;
                    _despawnRequestedAt = Time.unscaledTime;
                    _cleanupAt = Time.unscaledTime + 0.6f;
                }
                _wasActive = active;

                if (!held) _requireRepress = false;

                // Phone broke while the beam is up (spring pads, crashes...): kill the beam
                // right away, like vanilla does in multiplayer.
                if (active && IsPhoneBrokenNow(localPlayer))
                {
                    _requireRepress = true;
                    _pendingSpawnLevel = -1;
                    // Log only when a despawn request will actually go out (this branch
                    // re-enters every frame until the syncvar clears).
                    if (Time.unscaledTime - _despawnRequestedAt >= 0.75f)
                        TrainerPlugin.Logger.LogInfo("[Beam] Phone broke with the beam up — despawning it.");
                    Despawn();
                    return;
                }

                // While held with no beam up: spawn as soon as we have real support.
                if (held && !active && _pendingSpawnLevel < 0 && _pendingBreakLevel < 0 && !_requireRepress)
                {
                    if (HasSupport(localPlayer) && !IsPhoneBrokenNow(localPlayer))
                        TrySpawnLevel(WorkingSpawnLevel >= 0 ? WorkingSpawnLevel : 0);
                    else if (mouse.rightButton.wasPressedThisFrame)
                        TrainerPlugin.Logger.LogInfo("[Beam] No support yet — the beam will spawn on contact while you keep holding.");
                    return;
                }

                // Release (or any frame without the button held): the beam disappears.
                if ((active || _pendingSpawnLevel >= 0) && !held && _pendingBreakLevel < 0)
                {
                    _pendingSpawnLevel = -1;
                    Despawn();
                    return;
                }

                // Moving away while the beam is up: despawn + break the phone, like vanilla.
                if (active && _pendingBreakLevel < 0
                    && Time.unscaledTime - _despawnRequestedAt > 1f // don't re-fire while a despawn is in flight
                    && IsBeyondCancelDistance(localPlayer))
                    BreakAndDespawn(localPlayer);
            }
            catch (Exception ex)
            {
                TrainerPlugin.Logger.LogWarning($"[Beam] Update error: {ex.Message}");
            }
        }

        // Called when the feature is toggled off so an active beam doesn't linger.
        public static void Deactivate()
        {
            try
            {
                if (_beam != null && IsBeamActive()) Despawn();
            }
            catch { }
            ResetTransientState();
        }

        private static void ResetTransientState()
        {
            _pendingSpawnLevel = -1;
            _pendingBreakLevel = -1;
        }

        // ── Resolution / detection ──────────────────────────────────────────

        private static void Resolve(GameObject localPlayer)
        {
            if (_beam != null && _playerNetworked != null && _playerRef != null && _ground != null) return;
            var root = localPlayer.transform.root != null
                ? localPlayer.transform.root.gameObject
                : localPlayer;

            if (_ground == null)
                _ground = root.GetComponentInChildren<EHS.GroundContact>(true);

            if (_beam == null || _playerNetworked == null || _playerRef == null)
            {
                foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (mb == null) continue;
                    var t = mb.GetIl2CppType();
                    if (t == null) continue;
                    if (_beam == null && t.Name == "PlayerHelpBeamV2") _beam = mb;
                    if (_playerNetworked == null && t.Name == "PlayerNetworked") _playerNetworked = mb;
                    if (_playerRef == null && t.Name == "PlayerRef") _playerRef = mb;
                }
            }
        }

        private static void LoadVanillaSettings()
        {
            if (_settingsLoaded) return;
            _settingsLoaded = true; // one attempt is enough; keep defaults on failure
            try
            {
                var all = Resources.FindObjectsOfTypeAll(Il2CppType.Of<EHS.PlayerSettingsSO>());
                if (all != null && all.Length > 0)
                {
                    var so = all[0].TryCast<EHS.PlayerSettingsSO>();
                    if (so != null)
                    {
                        _distToCancel = so.HelpBeamDistanceToCancel;
                        _heightToCancel = so.HelpBeamHeightToCancel;
                        _destructOnDespawn = so.HelpBeamDestructPhoneOnDespawn;
                        _minDurationForDestruct = so.HelpBeamMinDurationForDestruct;
                        TrainerPlugin.Logger.LogInfo(
                            $"[Beam] Vanilla settings: distToCancel={_distToCancel}, heightToCancel={_heightToCancel}, " +
                            $"destructOnDespawn={_destructOnDespawn}, minDurationForDestruct={_minDurationForDestruct}");
                        return;
                    }
                }
                TrainerPlugin.Logger.LogWarning("[Beam] PlayerSettingsSO not found; using default cancel thresholds.");
            }
            catch (Exception ex)
            {
                TrainerPlugin.Logger.LogWarning($"[Beam] Reading PlayerSettingsSO failed ({ex.Message}); using defaults.");
            }
        }

        // True only when the game itself reports a multiplayer session. If the static
        // PlayerConnections.IsMultiplayerClient() can't be reached, fall back to counting
        // tagged players (>1 = multiplayer).
        private static bool IsMultiplayer()
        {
            try
            {
                if (_isMultiplayerClient == null && !_mpLookupFailed)
                {
                    var t = Il2CppSystem.Type.GetType("EHS.Network.PlayerConnections, Assembly-CSharp");
                    _isMultiplayerClient = t?.GetMethod("IsMultiplayerClient", ALL_FLAGS);
                    if (_isMultiplayerClient == null)
                    {
                        _mpLookupFailed = true;
                        TrainerPlugin.Logger.LogWarning("[Beam] PlayerConnections.IsMultiplayerClient not found; using player-count fallback.");
                    }
                }
                if (_isMultiplayerClient != null)
                {
                    var res = _isMultiplayerClient.Invoke(null, null);
                    if (res != null) return res.Unbox<bool>();
                }
            }
            catch (Exception ex)
            {
                _mpLookupFailed = true;
                _isMultiplayerClient = null;
                TrainerPlugin.Logger.LogWarning($"[Beam] IsMultiplayerClient failed: {ex.Message}");
            }

            try { return GameObject.FindGameObjectsWithTag("Player").Length > 1; }
            catch { return false; }
        }

        // The base game's authoritative "broken" signal is the destruction FSM (the owner
        // enters StateDestroyed the instant the break happens — see PhoneRepair). The
        // destroyedVisual lags a few frames when breaking mid-air, so it is only a fallback.
        private static bool IsPhoneBrokenNow(GameObject localPlayer)
        {
            try
            {
                var fsmObj = _playerRef?.GetIl2CppType().GetProperty("DestructionStateMachine")
                    ?.GetGetMethod()?.Invoke(_playerRef, null);
                var name = fsmObj?.GetIl2CppType().GetProperty("CurrentStateName")
                    ?.GetGetMethod()?.Invoke(fsmObj, null);
                if (name != null)
                    return name.ToString().IndexOf("Destroy", StringComparison.OrdinalIgnoreCase) >= 0;
            }
            catch { }
            return PhoneRepair.IsPhoneBroken(localPlayer);
        }

        // Real support = the game says we're touching AND the last contact point is still
        // right next to us. IsTouching() can linger a few frames after a spring-pad launch;
        // by then the contact point is already meters behind, so this kills the phantom
        // mid-air spawn while still allowing spawns on the pad itself.
        private static bool HasSupport(GameObject localPlayer)
        {
            try
            {
                if (_ground == null) return true; // if the contact check breaks, don't block the feature
                if (!_ground.IsTouching()) return false;
                Vector3 contact = _ground.LastAverageContactPoint;
                return (localPlayer.transform.position - contact).sqrMagnitude <= 4f; // within 2 m
            }
            catch { return true; }
        }

        private static bool IsBeamActive()
        {
            try
            {
                var getter = _beam.GetIl2CppType().GetProperty("IsHelpBeamActive")?.GetGetMethod();
                var res = getter?.Invoke(_beam, null);
                return res != null && res.Unbox<bool>();
            }
            catch { return false; }
        }

        private static bool IsBeyondCancelDistance(GameObject localPlayer)
        {
            Vector3 beamPos = GetBeamWorldPos();
            Vector3 p = localPlayer.transform.position;
            float horizontal = Vector2.Distance(new Vector2(p.x, p.z), new Vector2(beamPos.x, beamPos.z));
            float height = Mathf.Abs(p.y - beamPos.y);
            return horizontal > _distToCancel || height > _heightToCancel;
        }

        private static Vector3 GetBeamWorldPos()
        {
            // Prefer the live networked beam object; fall back to the captured spawn point.
            try
            {
                var getter = _beam.GetIl2CppType().GetProperty("activeHelpBeamSyncVar")?.GetGetMethod();
                var obj = getter?.Invoke(_beam, null);
                var comp = obj?.TryCast<Component>();
                if (comp != null) return comp.transform.position;
            }
            catch { }
            return _spawnPos;
        }

        // ── Spawn ───────────────────────────────────────────────────────────

        private static int WorkingSpawnLevel => TrainerConfig.Settings.HelpBeamSpawnLevel;

        private static void TrySpawnLevel(int level)
        {
            try
            {
                var type = _beam.GetIl2CppType();
                var posObj = type.GetMethod("GetBeamSpawnPos", ALL_FLAGS)?.Invoke(_beam, null);
                if (posObj != null)
                {
                    _spawnPosBoxed = posObj;
                    try { _spawnPos = posObj.Unbox<Vector3>(); }
                    catch { _spawnPos = _player != null ? _player.transform.position : Vector3.zero; }
                }
                else
                {
                    _spawnPosBoxed = null;
                    _spawnPos = _player != null ? _player.transform.position : Vector3.zero;
                }

                if (level == 0)
                {
                    type.GetMethod("TrySpawnHelpBeamClient", ALL_FLAGS)?.Invoke(_beam, null);
                }
                else
                {
                    if (_spawnPosBoxed == null)
                    {
                        TrainerPlugin.Logger.LogWarning("[Beam] GetBeamSpawnPos unavailable; cannot spawn.");
                        _pendingSpawnLevel = -1;
                        return;
                    }
                    string name = level == 1 ? "RequestHelpBeamSpawnServerRpc" : "SpawnTestHelpBeamServerRpc";
                    var args = new Il2CppReferenceArray<Il2CppSystem.Object>(1);
                    args[0] = _spawnPosBoxed;
                    type.GetMethod(name, ALL_FLAGS)?.Invoke(_beam, args);
                }

                _spawnTime = Time.unscaledTime;
                _pendingSpawnLevel = level;
                _pendingSpawnCheckAt = Time.unscaledTime + SPAWN_CONFIRM_DELAY;
            }
            catch (Exception ex)
            {
                TrainerPlugin.Logger.LogWarning($"[Beam] Spawn level {level} failed: {ex.Message}");
                if (level < MAX_SPAWN_LEVEL) TrySpawnLevel(level + 1);
                else _pendingSpawnLevel = -1;
            }
        }

        private static void ConfirmPendingSpawn()
        {
            int level = _pendingSpawnLevel;
            _pendingSpawnLevel = -1;

            if (_beam == null) return;
            if (IsBeamActive())
            {
                if (WorkingSpawnLevel != level)
                {
                    TrainerConfig.Settings.HelpBeamSpawnLevel = level;
                    TrainerConfig.Save(); // remember across sessions so future spawns are instant
                    TrainerPlugin.Logger.LogInfo($"[Beam] Spawn level {level} works; remembered.");
                }
                return;
            }

            if (level < MAX_SPAWN_LEVEL)
            {
                TrySpawnLevel(level + 1);
            }
            else
            {
                _requireRepress = true;
                TrainerPlugin.Logger.LogWarning("[Beam] All spawn paths failed (no beam appeared).");
            }
        }

        // ── Despawn / break ─────────────────────────────────────────────────

        private static void Despawn()
        {
            // Throttle: the syncvar takes a moment to clear, so callers can re-enter for a
            // few frames — one request (plus its deferred cleanup) is enough.
            if (Time.unscaledTime - _despawnRequestedAt < 0.75f) return;
            _despawnRequestedAt = Time.unscaledTime;
            _cleanupAt = Time.unscaledTime + 0.6f;
            try
            {
                _beam.GetIl2CppType().GetMethod("RequestHelpBeamDespawnServerRpc", ALL_FLAGS)?.Invoke(_beam, null);
            }
            catch (Exception ex)
            {
                TrainerPlugin.Logger.LogWarning($"[Beam] Despawn failed: {ex.Message}");
            }
        }

        // Destroys beam objects that survived a despawn request (the server had already
        // dropped its reference to them, so the RPC can no longer reach them).
        private static void CleanupOrphanBeams()
        {
            try
            {
                // A fresh spawn is underway/newer than the despawn request — don't touch it.
                if (_pendingSpawnLevel >= 0 || _spawnTime > _despawnRequestedAt) return;

                int n = 0;
                foreach (var mb in UnityEngine.Object.FindObjectsOfType<MonoBehaviour>())
                {
                    if (mb == null) continue;
                    var t = mb.GetIl2CppType();
                    if (t != null && t.Name == "NetworkedHelpBeam")
                    {
                        // Destroy the whole prefab (the component can sit on a child);
                        // killing only the child left the beam root + syncvar alive, which
                        // produced a "zombie" beam the server could no longer despawn.
                        var rootGo = mb.transform.root != null ? mb.transform.root.gameObject : mb.gameObject;
                        UnityEngine.Object.Destroy(rootGo);
                        n++;
                    }
                }
                if (n > 0)
                    TrainerPlugin.Logger.LogInfo($"[Beam] Cleaned up {n} orphan beam object(s).");
            }
            catch (Exception ex)
            {
                TrainerPlugin.Logger.LogWarning($"[Beam] Orphan cleanup failed: {ex.Message}");
            }
        }

        private static void BreakAndDespawn(GameObject localPlayer)
        {
            float duration = Time.unscaledTime - _spawnTime;
            // Vanilla rule: abandoning your beam EARLY breaks you (minDurationForDestruct is
            // how long the beam must have been up before leaving it becomes safe).
            bool destruct = _destructOnDespawn && duration < _minDurationForDestruct;
            TrainerPlugin.Logger.LogInfo($"[Beam] Player moved away (beam up {duration:F1}s) — despawning{(destruct ? " + breaking phone" : "")}.");

            _requireRepress = true; // no instant respawn while the button is still held

            if (!destruct)
            {
                Despawn();
                return;
            }
            TryBreakLevel(0);
        }

        private static void TryBreakLevel(int level)
        {
            try
            {
                if (level == 0)
                {
                    // Vanilla client path: despawn + let the game run its destruct logic.
                    // m_value is readonly in this Il2CppInterop version; write via pointer.
                    Il2CppSystem.Boolean il2cppTrue = default;
                    unsafe { *(bool*)&il2cppTrue = true; }
                    var args = new Il2CppReferenceArray<Il2CppSystem.Object>(1);
                    args[0] = il2cppTrue.BoxIl2CppObject();
                    _beam.GetIl2CppType().GetMethod("RequestHelpBeamDespawnClient", ALL_FLAGS)?.Invoke(_beam, args);
                }
                else if (level == 1)
                {
                    // Networked phone destroy (mirror of the reassemble RPC PhoneRepair uses).
                    Despawn();
                    if (_playerNetworked != null && _spawnPosBoxed != null)
                    {
                        var args = new Il2CppReferenceArray<Il2CppSystem.Object>(2);
                        args[0] = _spawnPosBoxed;
                        args[1] = null; // NetworkConnection — server fills it
                        _playerNetworked.GetIl2CppType().GetMethod("RequestPhoneDestroyServerRpc", ALL_FLAGS)
                            ?.Invoke(_playerNetworked, args);
                    }
                }
                else
                {
                    // Last resort: force the owner's destruction FSM to StateDestroyed
                    // (inverse of PhoneRepair.ForceStateNormal).
                    Despawn();
                    ForceStateDestroyed();
                }

                _pendingBreakLevel = level;
                _pendingBreakCheckAt = Time.unscaledTime + BREAK_CONFIRM_DELAY;
            }
            catch (Exception ex)
            {
                TrainerPlugin.Logger.LogWarning($"[Beam] Break level {level} failed: {ex.Message}");
                if (level < MAX_BREAK_LEVEL) TryBreakLevel(level + 1);
                else _pendingBreakLevel = -1;
            }
        }

        private static void ConfirmPendingBreak()
        {
            int level = _pendingBreakLevel;
            _pendingBreakLevel = -1;

            if (_player != null && PhoneRepair.IsPhoneBroken(_player))
            {
                TrainerPlugin.Logger.LogInfo($"[Beam] Phone broken via break level {level}.");
                return;
            }

            if (level < MAX_BREAK_LEVEL)
                TryBreakLevel(level + 1);
            else
                TrainerPlugin.Logger.LogWarning("[Beam] Could not break the phone (all paths tried).");
        }

        private static void ForceStateDestroyed()
        {
            if (_playerRef == null) return;
            try
            {
                var fsmObj = _playerRef.GetIl2CppType().GetProperty("DestructionStateMachine")
                    ?.GetGetMethod()?.Invoke(_playerRef, null);
                if (fsmObj == null) return;

                var setState = fsmObj.GetIl2CppType().GetMethod("SetState");
                if (setState == null) return;

                var destroyed = new EHS.StateDestroyed();
                var args = new Il2CppReferenceArray<Il2CppSystem.Object>(1);
                args[0] = destroyed;
                setState.Invoke(fsmObj, args);
                TrainerPlugin.Logger.LogInfo("[Beam] FSM forced to StateDestroyed.");
            }
            catch (Exception ex)
            {
                TrainerPlugin.Logger.LogWarning($"[Beam] ForceStateDestroyed error: {ex.Message}");
            }
        }
    }
}
