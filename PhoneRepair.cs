using System;
using UnityEngine;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace FlippingIsHardTrainer
{
    // Repairs the local player's broken phone — gameplay state, not just visuals.
    //
    // The phone destruction is driven by EHS.DestructionStateMachine (a FiniteStateMachine
    // with EHS.StateNormal / EHS.StateDestroyed). While broken the OWNER runs StateDestroyed
    // locally and only auto-exits from its own TickUpdate, so the networked reassemble RPC
    // does NOT repair the owner. To truly repair we force the FSM back to StateNormal, whose
    // Enter() re-arms movement/abilities.
    //
    // We also fire the visual SetToNormal and the networked reassemble RPC so the phone looks
    // right and other clients see the repair too. Game types are reached via reflection over
    // the referenced Assembly-CSharp.dll (EHS.PlayerRef derives from FishNet's NetworkBehaviour,
    // which we don't reference, so reflection avoids a compile-time dependency). EHS.StateNormal
    // derives from Il2CppSystem.Object, so it can be strongly typed and constructed directly.
    public static class PhoneRepair
    {
        // Cached per local player. Unity's overloaded == makes a destroyed component
        // compare as null, so these self-invalidate on scene change / respawn.
        private static EHS.DestroyedPhoneEffectHandler _handler;
        private static MonoBehaviour _playerNetworked;
        private static MonoBehaviour _playerRef;

        private static void Resolve(GameObject localPlayer)
        {
            if (localPlayer == null) return;
            var root = localPlayer.transform.root != null
                ? localPlayer.transform.root.gameObject
                : localPlayer;

            if (_handler == null)
                _handler = root.GetComponentInChildren<EHS.DestroyedPhoneEffectHandler>(true);

            if (_playerNetworked == null || _playerRef == null)
            {
                foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (mb == null) continue;
                    var t = mb.GetIl2CppType();
                    if (t == null) continue;
                    if (_playerNetworked == null && t.Name == "PlayerNetworked") _playerNetworked = mb;
                    if (_playerRef == null && t.Name == "PlayerRef") _playerRef = mb;
                }
            }
        }

        public static bool IsPhoneBroken(GameObject localPlayer)
        {
            try
            {
                Resolve(localPlayer);
                if (_handler != null)
                {
                    var dv = _handler.destroyedVisual;
                    bool destroyedActive = dv != null && dv.activeInHierarchy;
                    return destroyedActive;
                }
            }
            catch (Exception ex)
            {
                TrainerPlugin.Logger.LogWarning($"[Repair] IsPhoneBroken error: {ex.Message}");
            }
            return false;
        }

        public static void RepairPhone(GameObject localPlayer)
        {
            try
            {
                Resolve(localPlayer);

                // 1) THE REAL REPAIR: force the owner's destruction FSM back to Normal so
                //    movement/abilities are restored.
                ForceStateNormal();

                // 2) Local visual reassemble (in case StateNormal.Enter doesn't repaint).
                if (_handler != null)
                {
                    try { _handler.SetToNormal(false); }
                    catch (Exception ex) { TrainerPlugin.Logger.LogWarning($"[Repair] SetToNormal failed: {ex.Message}"); }
                }

                // 3) Networked reassemble so other clients see the repair too.
                SendReassembleRpc();
            }
            catch (Exception ex)
            {
                TrainerPlugin.Logger.LogError($"[Repair] RepairPhone error: {ex}");
            }
        }

        private static void ForceStateNormal()
        {
            if (_playerRef == null)
            {
                TrainerPlugin.Logger.LogWarning("[Repair] PlayerRef not found; cannot reach FSM.");
                return;
            }
            try
            {
                var fsmObj = _playerRef.GetIl2CppType().GetProperty("DestructionStateMachine")
                    ?.GetGetMethod()?.Invoke(_playerRef, null);
                if (fsmObj == null)
                {
                    TrainerPlugin.Logger.LogWarning("[Repair] DestructionStateMachine null.");
                    return;
                }

                var fsmType = fsmObj.GetIl2CppType();

                string stateName = null;
                try
                {
                    var n = fsmType.GetProperty("CurrentStateName")?.GetGetMethod()?.Invoke(fsmObj, null);
                    if (n != null) stateName = n.ToString();
                }
                catch { }
                TrainerPlugin.Logger.LogInfo($"[Repair] FSM current state: {stateName ?? "?"}");

                // Only force a transition if we're actually in the destroyed state — re-entering
                // Normal every teleport would needlessly reset movement.
                if (stateName != null && stateName.IndexOf("Destroy", StringComparison.OrdinalIgnoreCase) < 0)
                    return;

                var setState = fsmType.GetMethod("SetState");
                if (setState == null)
                {
                    TrainerPlugin.Logger.LogWarning("[Repair] FSM.SetState not found.");
                    return;
                }

                var normal = new EHS.StateNormal();
                var args = new Il2CppReferenceArray<Il2CppSystem.Object>(1);
                args[0] = normal;
                setState.Invoke(fsmObj, args);
                TrainerPlugin.Logger.LogInfo("[Repair] FSM forced to StateNormal.");
            }
            catch (Exception ex)
            {
                TrainerPlugin.Logger.LogError($"[Repair] ForceStateNormal error: {ex}");
            }
        }

        private static void SendReassembleRpc()
        {
            if (_playerNetworked == null) return;
            try
            {
                var method = _playerNetworked.GetIl2CppType().GetMethod("RequestPhoneReassembleServerRpc");
                if (method == null) return;
                var args = new Il2CppReferenceArray<Il2CppSystem.Object>(1);
                args[0] = null; // NetworkConnection — server fills it
                method.Invoke(_playerNetworked, args);
            }
            catch (Exception ex)
            {
                TrainerPlugin.Logger.LogWarning($"[Repair] reassemble RPC failed: {ex.Message}");
            }
        }
    }
}
