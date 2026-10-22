using UnityEngine;

namespace FlippingIsHardTrainer
{
    public class OverlayRenderer
    {
        // Overlay data
        private Vector3 _currentPosition = Vector3.zero;
        private bool _hasSavedPosition = false;
        private bool _flyModeActive = false;
        private bool _keepVelocityActive = false;
        private bool _keepAngleActive = false;
        private bool _autoRepairActive = false;
        private bool _helpBeamSpActive = false;
        private bool _showOverlay = true;

        // Telemetry caching
        private Vector3? _cachedCoords = null;
        private string _cachedHeightStr = "0.0 m";
        private string _cachedCoordsStr = "0.0, 0.0, 0.0";
        private float _lastSpeed = -1f;
        private string _cachedSpeedStr = "0.0 m/s";

        // Keybind caching (to avoid GC allocations in OnGUI)
        private string _keySave;
        private string _keyTeleport;
        private string _keyFly;
        private string _keyVel;
        private string _keyAngle;
        private string _keyRepair;
        private string _keyBeam;
        private string _keyMenu;
        private string _strFlyControls = "";
        private bool _layoutDirty = true;

        // Base layout (multiplied by the HUD scale at draw time)
        private const float BASE_PAD      = 14f;
        private const float BASE_MARGIN   = 20f;
        private const float BASE_HEADER_H = 24f;
        private const float BASE_ROW_H    = 25f;
        private const float BASE_CHIP_H   = 20f;
        private const float BASE_LABEL_W  = 150f;
        private const float BASE_PILL_W   = 52f;
        private const float BASE_FLY_H    = 46f;
        private const float BASE_STATS_W  = 220f;

        private float Scale => Mathf.Clamp(TrainerConfig.Settings.OverlayScale, 0.25f, 2.0f);
        private float _cachedScale = -1f;
        private float _chipW;

        // Styles — rebuilt when scale changes
        private GUIStyle _styleTitle;
        private GUIStyle _styleLabel;
        private GUIStyle _styleChip;
        private GUIStyle _stylePill;
        private GUIStyle _styleCaption;
        private GUIStyle _styleValue;
        private GUIStyle _styleSmall;
        private GUIStyle _styleSmallRight;
        private bool _stylesReady = false;

        public void UpdateData(Vector3 pos, float speed, bool hasSaved, bool flyActive, bool keepVelocity, bool keepAngle, bool autoRepair, bool helpBeamSp)
        {
            if (_cachedCoords == null || Vector3.Distance(_currentPosition, pos) > 0.05f)
            {
                _currentPosition = pos;
                _cachedHeightStr = $"{_currentPosition.y:F1} m";
                _cachedCoordsStr = $"{_currentPosition.x:F1}, {_currentPosition.y:F1}, {_currentPosition.z:F1}";
                _cachedCoords = pos;
            }

            if (Mathf.Abs(_lastSpeed - speed) > 0.1f)
            {
                _lastSpeed = speed;
                _cachedSpeedStr = $"{speed:F1} m/s";
            }

            _hasSavedPosition = hasSaved;
            _flyModeActive = flyActive;
            _keepVelocityActive = keepVelocity;
            _keepAngleActive = keepAngle;
            _autoRepairActive = autoRepair;
            _helpBeamSpActive = helpBeamSp;
            _showOverlay = Application.isFocused;
        }

        public void SetPositionSaved(bool saved) => _hasSavedPosition = saved;
        public void SetFlyModeActive(bool active) => _flyModeActive = active;

        public void RefreshKeybinds(InputDeviceType device = InputDeviceType.Keyboard)
        {
            var s = TrainerConfig.Settings;
            bool ps = InputDeviceTracker.IsDualShock;
            _keySave     = s.SavePosition.ToString(device, ps);
            _keyTeleport = s.Teleport.ToString(device, ps);
            _keyFly      = s.ToggleFlyMode.ToString(device, ps);
            _keyVel      = s.ToggleKeepVelocity.ToString(device, ps);
            _keyAngle    = s.ToggleKeepAngle.ToString(device, ps);
            _keyRepair   = s.ToggleAutoRepair.ToString(device, ps);
            // Toggle bind shown; the beam itself is used with the game's right-click.
            _keyBeam     = s.ToggleHelpBeam.ToString(device, ps);
            _keyMenu     = s.OpenBindMenu.ToString(device, ps);

            // Fly-mode control hint, built from the current binds of the active device.
            string move = device == InputDeviceType.Gamepad
                ? "L-Stick"
                : $"{s.FlyForward.ToStringKeyboard()} {s.FlyLeft.ToStringKeyboard()} {s.FlyBackward.ToStringKeyboard()} {s.FlyRight.ToStringKeyboard()}";
            _strFlyControls = $"{move} move  ·  {s.FlyUp.ToString(device, ps)} up  ·  " +
                              $"{s.FlyDown.ToString(device, ps)} down  ·  {s.FlyTurbo.ToString(device, ps)} turbo";

            _layoutDirty = true;
        }

        public void OnGUI()
        {
            if (!_showOverlay) return;

            EnsureStyles();
            DrawControls();
            DrawStats();
        }

        // ── Styles ──────────────────────────────────────────────────────────

        private void EnsureStyles()
        {
            float scale = Scale;
            if (!_stylesReady || !Mathf.Approximately(_cachedScale, scale))
            {
                _cachedScale = scale;
                _styleTitle      = TrainerTheme.MakeLabel(Mathf.RoundToInt(14 * scale), FontStyle.Bold);
                _styleLabel      = TrainerTheme.MakeLabel(Mathf.RoundToInt(13 * scale));
                _styleChip       = TrainerTheme.MakeLabel(Mathf.RoundToInt(11 * scale), FontStyle.Bold, TextAnchor.MiddleCenter);
                _stylePill       = TrainerTheme.MakeLabel(Mathf.RoundToInt(10 * scale), FontStyle.Bold, TextAnchor.MiddleCenter);
                _styleCaption    = TrainerTheme.MakeLabel(Mathf.RoundToInt(10 * scale), FontStyle.Bold);
                _styleValue      = TrainerTheme.MakeLabel(Mathf.RoundToInt(15 * scale), FontStyle.Bold, TextAnchor.MiddleRight);
                _styleSmall      = TrainerTheme.MakeLabel(Mathf.RoundToInt(11 * scale));
                _styleSmallRight = TrainerTheme.MakeLabel(Mathf.RoundToInt(12 * scale), FontStyle.Normal, TextAnchor.MiddleRight);
                _stylesReady = true;
                _layoutDirty = true;
            }

            if (_layoutDirty)
            {
                // Size the key column to the longest bind (gamepad combos are wide).
                float widest = 0f;
                foreach (var k in new[] { _keySave, _keyTeleport, _keyFly, _keyVel, _keyAngle, _keyRepair, _keyBeam, _keyMenu })
                    widest = Mathf.Max(widest, _styleChip.CalcSize(new GUIContent(k ?? "")).x);
                _chipW = Mathf.Clamp(widest + 18f * scale, 64f * scale, 170f * scale);
                _layoutDirty = false;
            }
        }

        // ── Controls card (bottom-left) ─────────────────────────────────────

        private void DrawControls()
        {
            float sc = Scale;
            float pad = BASE_PAD * sc;
            float rowH = BASE_ROW_H * sc;
            float chipH = BASE_CHIP_H * sc;
            float w = pad * 2f + _chipW + 10f * sc + BASE_LABEL_W * sc + BASE_PILL_W * sc;
            float h = pad * 2f + BASE_HEADER_H * sc + 8f * sc + rowH * 7f + 17f * sc + chipH;
            if (_flyModeActive) h += (BASE_FLY_H + 8f) * sc;

            float x = BASE_MARGIN;
            float y = Screen.height - h - BASE_MARGIN;
            TrainerTheme.DrawPanel(new Rect(x, y, w, h), TrainerTheme.Panel);

            float cx = x + pad;
            float cy = y + pad;
            float innerW = w - pad * 2f;

            // Header: accent bar + title
            TrainerTheme.Fill(new Rect(cx, cy + 4f * sc, 4f * sc, BASE_HEADER_H * sc - 8f * sc), TrainerTheme.Accent, true);
            TrainerTheme.Label(new Rect(cx + 12f * sc, cy, innerW, BASE_HEADER_H * sc), "FLIPPING IS HARD TRAINER", _styleTitle, TrainerTheme.Text);
            cy += BASE_HEADER_H * sc + 8f * sc;

            if (_flyModeActive)
            {
                var fly = new Rect(cx, cy, innerW, BASE_FLY_H * sc);
                TrainerTheme.Fill(fly, TrainerTheme.FlySoft, true);
                TrainerTheme.Label(new Rect(fly.x + 10f * sc, fly.y + 5f * sc, fly.width - 20f * sc, 18f * sc), "FLY MODE ACTIVE", _styleCaption, TrainerTheme.Fly);
                TrainerTheme.Label(new Rect(fly.x + 10f * sc, fly.y + 22f * sc, fly.width - 20f * sc, 18f * sc), _strFlyControls, _styleSmall, TrainerTheme.TextDim);
                cy += (BASE_FLY_H + 8f) * sc;
            }

            DrawRow(cx, ref cy, innerW, _keySave, "Save position", null, false, TrainerTheme.On);
            DrawRow(cx, ref cy, innerW, _keyTeleport, "Teleport", _hasSavedPosition ? "READY" : "EMPTY", _hasSavedPosition, TrainerTheme.On);
            DrawRow(cx, ref cy, innerW, _keyFly, "Fly mode", _flyModeActive ? "ON" : "OFF", _flyModeActive, TrainerTheme.Fly);
            DrawRow(cx, ref cy, innerW, _keyVel, "Keep velocity", _keepVelocityActive ? "ON" : "OFF", _keepVelocityActive, TrainerTheme.On);
            DrawRow(cx, ref cy, innerW, _keyAngle, "Keep angle", _keepAngleActive ? "ON" : "OFF", _keepAngleActive, TrainerTheme.On);
            DrawRow(cx, ref cy, innerW, _keyRepair, "Auto-repair", _autoRepairActive ? "ON" : "OFF", _autoRepairActive, TrainerTheme.On);
            DrawRow(cx, ref cy, innerW, _keyBeam, "Help beam (solo)", _helpBeamSpActive ? "ON" : "OFF", _helpBeamSpActive, TrainerTheme.On);

            // Footer: menu shortcut
            cy += 8f * sc;
            TrainerTheme.FillRect(new Rect(cx, cy, innerW, 1f), TrainerTheme.Divider);
            cy += 8f * sc;
            TrainerTheme.DrawChip(new Rect(cx, cy, _chipW, chipH), _keyMenu, _styleChip, TrainerTheme.Chip, TrainerTheme.TextDim);
            TrainerTheme.Label(new Rect(cx + _chipW + 10f * sc, cy, BASE_LABEL_W * sc, chipH), "Keybinds & settings", _styleLabel, TrainerTheme.TextDim);
        }

        // One control: keycap, label and an optional status pill on the right.
        private void DrawRow(float x, ref float y, float w, string key, string label, string pill, bool on, Color onColor)
        {
            float sc = Scale;
            float chipH = BASE_CHIP_H * sc;
            float rowH = BASE_ROW_H * sc;

            TrainerTheme.DrawChip(new Rect(x, y + (rowH - chipH) * 0.5f, _chipW, chipH), key, _styleChip, TrainerTheme.Chip, TrainerTheme.Text);
            TrainerTheme.Label(new Rect(x + _chipW + 10f * sc, y, BASE_LABEL_W * sc, rowH), label, _styleLabel,
                pill == null || on ? TrainerTheme.Text : TrainerTheme.TextDim);

            if (pill != null)
            {
                float pw = (BASE_PILL_W - 8f) * sc;
                float ph = 18f * sc;
                var r = new Rect(x + w - pw, y + (rowH - ph) * 0.5f, pw, ph);
                Color bg = on ? new Color(onColor.r, onColor.g, onColor.b, 0.16f) : TrainerTheme.Surface;
                TrainerTheme.DrawChip(r, pill, _stylePill, bg, on ? onColor : TrainerTheme.TextFaint);
            }

            y += rowH;
        }

        // ── Telemetry card (top-right) ──────────────────────────────────────

        private void DrawStats()
        {
            float sc = Scale;
            float pad = 12f * sc;
            float rowH = 24f * sc;
            float w = BASE_STATS_W * sc;
            float h = pad * 2f + rowH * 3f;
            float x = Screen.width - w - BASE_MARGIN;
            float y = BASE_MARGIN;

            TrainerTheme.DrawPanel(new Rect(x, y, w, h), TrainerTheme.Panel);

            float cx = x + pad;
            float cy = y + pad;
            float innerW = w - pad * 2f;

            DrawStat(cx, ref cy, innerW, rowH, "SPEED", _cachedSpeedStr, _styleValue, TrainerTheme.Accent);
            DrawStat(cx, ref cy, innerW, rowH, "HEIGHT", _cachedHeightStr, _styleValue, TrainerTheme.Text);
            DrawStat(cx, ref cy, innerW, rowH, "XYZ", _cachedCoordsStr, _styleSmallRight, TrainerTheme.TextDim);
        }

        private void DrawStat(float x, ref float y, float w, float h, string caption, string value, GUIStyle valueStyle, Color valueColor)
        {
            TrainerTheme.Label(new Rect(x, y, w, h), caption, _styleCaption, TrainerTheme.TextFaint);
            TrainerTheme.Label(new Rect(x, y, w, h), value, valueStyle, valueColor);
            y += h;
        }
    }
}
