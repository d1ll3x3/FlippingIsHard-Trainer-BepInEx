using System;
using EHS.Steam;
using Steamworks;
using UnityEngine;

namespace FlippingIsHardTrainer
{
    internal sealed class TrainerLobbyUsage
    {
        private const string MemberDataKey = "fih_trainer_used_v1";
        private SteamManager _steamManager;
        private CSteamID _pendingLobby;
        private float _nextAttempt;

        public void RecordUse()
        {
            try
            {
                if (!SteamManagerBase.Initialized)
                    return;

                if (_steamManager == null)
                    _steamManager = UnityEngine.Object.FindObjectOfType<SteamManager>();

                if (_steamManager != null && _steamManager.IsInLobby)
                    _pendingLobby = _steamManager.CurrentLobbySteamId;
            }
            catch (Exception ex)
            {
                TrainerPlugin.Logger.LogWarning($"Could not record trainer use for the Steam lobby: {ex.Message}");
            }
        }

        public void Update()
        {
            if (_pendingLobby.m_SteamID == 0 || Time.unscaledTime < _nextAttempt)
                return;

            _nextAttempt = Time.unscaledTime + 1f;
            try
            {
                if (SteamManagerBase.Initialized && _steamManager != null && _steamManager.IsInLobby
                    && _steamManager.CurrentLobbySteamId.m_SteamID == _pendingLobby.m_SteamID)
                {
                    SteamMatchmaking.SetLobbyMemberData(_pendingLobby, MemberDataKey, "1");
                }

                _pendingLobby = default;
            }
            catch (Exception ex)
            {
                TrainerPlugin.Logger.LogWarning($"Could not publish trainer use to the Steam lobby: {ex.Message}");
            }
        }
    }
}
