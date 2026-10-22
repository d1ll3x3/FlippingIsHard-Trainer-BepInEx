using HarmonyLib;

namespace FlippingIsHardTrainer
{
    // Enables the multiplayer-only "help beam" (right-click) in singleplayer.
    //
    // The ONLY solo gate in the game is PlayerHelpBeamV2.CanSpawnNewBeam(bool allowSolo):
    // TickUpdate calls it with allowSolo = false, and then PlayerConnections.IsPlayingSolo()
    // vetoes the spawn. Forcing allowSolo = true lets the game's own beam logic run untouched:
    // hold/release, the contact requirement, the distance/height cancel, breaking the phone
    // and despawning on a break all behave exactly like in a lobby with other players.
    //
    // This replaces the old reimplementation that polled RMB and spawned through reflection.
    // Its "orphan cleanup" destroyed every NetworkedHelpBeam in the scene, which also wiped
    // the beams placed around the map.
    //
    // Safe to Harmony-patch: CanSpawnNewBeam has its own body (not folded with another
    // method) and only takes a bool. In real multiplayer IsPlayingSolo() is already false,
    // so the patch changes nothing there.
    [HarmonyPatch(typeof(EHS.Player.HelpBeam.PlayerHelpBeamV2), "CanSpawnNewBeam")]
    public static class HelpBeamUnlock
    {
        // __0 = allowSolo (positional, so it doesn't depend on interop parameter names).
        private static void Prefix(ref bool __0)
        {
            if (TrainerConfig.Settings.HelpBeamSingleplayer)
                __0 = true;
        }
    }
}
