using EHS;
using UnityEngine;

namespace FlippingIsHardTrainer
{
    // Fly, teleports and the rest are advantages, so no achievement or achievement stat can be
    // earned while the trainer is loaded. The game has the switch for it: Crowd Control runs
    // (a save's useStreamerEffects) block achievements through AchievementsCore.SetBlockingState,
    // with an allowlist. Blocking with no allowlist (null sets) makes TryUnlock, TrySetProgress
    // and the stat setters refuse every id.
    //
    // AchievementsManager.ApplyBlockingState recomputes the state from the save whenever a
    // world is loaded, restarted or joined, which lifts the block, so it is checked every
    // frame and put back. Polled instead of Harmony-patched: no game method is detoured.
    public static class AchievementBlocker
    {
        // How often to look for the AchievementsManager while it doesn't exist yet.
        private const float SearchInterval = 1f;

        private static AchievementsManager _manager;
        private static float _nextSearch;
        private static bool _loggedBlock;

        public static void Update()
        {
            if (_manager == null)
            {
                if (Time.unscaledTime < _nextSearch) return;
                _nextSearch = Time.unscaledTime + SearchInterval;
                _manager = Object.FindObjectOfType<AchievementsManager>();
                if (_manager == null) return;
            }

            var core = _manager.Core;
            if (core == null) return;
            if (core.IsBlockingActive && core.allowedWhileBlocked == null && core.allowedStatsWhileBlocked == null)
                return;

            core.SetBlockingState(true, null, null);
            if (!_loggedBlock)
            {
                TrainerPlugin.Logger.LogInfo("Achievements blocked: they can't be earned while the trainer is loaded.");
                _loggedBlock = true;
            }
        }
    }
}
