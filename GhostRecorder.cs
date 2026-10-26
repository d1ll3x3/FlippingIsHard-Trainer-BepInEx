using System;
using EHS;
using UnityEngine;

namespace FlippingIsHardTrainer
{
    // Copied from FIH Ghost's GhostRecorder.cs (https://github.com/d1ll3x3/FIH_Ghost),
    // trimmed to the phone's pose: the trainer cuts no area or challenge splits.
    //
    // Records the local phone every physics tick. Sample times are the run clock: the
    // playtime at Start plus the physics time since (Time.fixedTimeAsDouble), which advances
    // exactly one fixedDeltaTime per sample and stops with the game, since FixedUpdate does
    // not run at timeScale 0.
    public sealed class GhostRecorder
    {
        // Further than this in one 20 ms tick is ~500 m/s: a teleport, not movement.
        private const float CutDistance = 10f;

        private GhostRecording _rec;
        private double _timeBase;
        private double _startFixedTime;
        private int _startCheckpoints;

        public bool IsRecording => _rec != null;
        public double Seconds => _rec?.Duration ?? 0;

        public void Start(PlayerRef player)
        {
            var progress = player.GameRefs?.WorldSaveManager?.GetCurrentProgress();
            double playtime = player.PlayerProgressHandler?.Playtime ?? 0;
            _rec = new GhostRecording
            {
                MapId = progress?.MapId ?? "",
                Difficulty = progress?.DifficultyType.ToString() ?? "",
                StartPlaytime = playtime,
                RecordedAtUtc = DateTime.UtcNow,
                WorldGuid = progress?.WorldGuid ?? "",
            };
            _timeBase = playtime;
            _startFixedTime = Time.fixedTimeAsDouble;
            _startCheckpoints = player.GameRefs?.CheckpointsManager?.UsedCheckpoints ?? 0;
            TrainerPlugin.Logger.LogInfo($"[GHOST] Recording started: map '{_rec.MapId}' {_rec.Difficulty}");
        }

        /// <summary>Ends the recording; null if nothing was recorded.</summary>
        public GhostRecording Stop(PlayerRef player)
        {
            var rec = _rec;
            _rec = null;
            if (rec == null) return null;
            var checkpoints = player?.GameRefs?.CheckpointsManager;
            if (checkpoints != null)
                rec.CheckpointsUsed = Math.Max(0, checkpoints.UsedCheckpoints - _startCheckpoints);
            TrainerPlugin.Logger.LogInfo($"[GHOST] Recording stopped: {rec.Samples.Count} samples ({rec.Duration:0.00} s)");
            return rec.Samples.Count > 0 ? rec : null;
        }

        /// <summary>Call once per FixedUpdate while recording.</summary>
        public void Step(PlayerRef player)
        {
            var rb = player.Rb;
            if (rb == null) return;
            double t = _timeBase + (Time.fixedTimeAsDouble - _startFixedTime);

            var samples = _rec.Samples;
            Vector3 pos = rb.position;
            bool cut = samples.Count > 0
                && (pos - samples[samples.Count - 1].Position).sqrMagnitude > CutDistance * CutDistance;
            samples.Add(new GhostSample { Time = t, Position = pos, Rotation = rb.rotation, Cut = cut });
        }
    }
}
