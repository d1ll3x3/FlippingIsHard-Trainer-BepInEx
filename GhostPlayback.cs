using EHS;
using UnityEngine;

namespace FlippingIsHardTrainer
{
    // Copied from FIH Ghost's GhostPlayback.cs (https://github.com/d1ll3x3/FIH_Ghost),
    // trimmed to its relative clock: the trainer has no full-run ghost synced to playtime.
    //
    // Plays a recording on a ghost body from its first sample, starting now, on game time
    // (Time.timeAsDouble), the timeline the physics samples were taken on. It freezes in the
    // pause menu like the player does.
    public sealed class GhostPlayback
    {
        private readonly GhostBody _body;
        private GhostRecording _rec;
        // Ghost time = Time.timeAsDouble + _offset.
        private double _offset;
        private int _index;

        public GhostPlayback(Color color) => _body = new GhostBody(color);

        public bool IsPlaying => _rec != null;
        public Transform Body => _body.Root;
        // Seconds into the recording.
        public double Seconds => IsPlaying ? Time.timeAsDouble + _offset - _rec.StartTime : 0;
        public double Duration => _rec?.Duration ?? 0;

        public bool Start(GhostRecording rec, PlayerRef player)
        {
            Stop();
            if (rec.Samples.Count == 0 || !_body.Create(player)) return false;
            _rec = rec;
            _offset = rec.StartTime - Time.timeAsDouble;
            _index = 0;
            Apply(Time.timeAsDouble + _offset);
            return true;
        }

        public void Stop()
        {
            _body.Destroy();
            _rec = null;
        }

        /// <summary>Call once per frame, after the game moved the player (LateUpdate).</summary>
        public void Update()
        {
            if (_rec == null) return;
            if (!_body.IsAlive)
            {
                TrainerPlugin.Logger.LogInfo("[GHOST] Body destroyed with the scene; playback stopped.");
                Stop();
                return;
            }
            double t = Time.timeAsDouble + _offset;
            if (t > _rec.EndTime)
            {
                TrainerPlugin.Logger.LogInfo($"[GHOST] Playback finished ({_rec.Duration:0.00} s).");
                Stop();
                return;
            }
            Apply(t);
        }

        private void Apply(double t)
        {
            var samples = _rec.Samples;
            // Frame to frame the clock only moves forward a little; anything else (a clock
            // that went back) gets a binary search.
            if (samples[_index].Time > t || (_index + 8 < samples.Count && samples[_index + 8].Time <= t))
                _index = IndexAtOrBefore(t);
            while (_index + 1 < samples.Count && samples[_index + 1].Time <= t) _index++;

            var a = samples[_index];
            // Hold the last pose before a teleport instead of sliding through the level.
            if (t <= a.Time || _index + 1 >= samples.Count || samples[_index + 1].Cut)
            {
                _body.SetPose(a.Position, a.Rotation);
                return;
            }
            var b = samples[_index + 1];
            float f = (float)((t - a.Time) / (b.Time - a.Time));
            _body.SetPose(Vector3.Lerp(a.Position, b.Position, f), Quaternion.Slerp(a.Rotation, b.Rotation, f));
        }

        private int IndexAtOrBefore(double t)
        {
            var samples = _rec.Samples;
            int lo = 0, hi = samples.Count - 1;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) / 2;
                if (samples[mid].Time <= t) lo = mid;
                else hi = mid - 1;
            }
            return lo;
        }
    }
}
