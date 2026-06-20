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
        private bool _showOverlay = true;

        // Coordinate caching
        private Vector3? _cachedCoords = null;
        private string _cachedHeightStr = "HEIGHT: 0.0 M";
        private string _cachedCoordsStr = "XYZ: 0.0, 0.0, 0.0";
        private float _lastSpeed = -1f;
        private string _cachedSpeedStr = "SPEED: 0.0 M/S";

        // Keybind caching (to avoid GC allocations in OnGUI)
        private string _strSave;
        private string _strTelSave;
        private string _strTelReady;
        private string _strFly;
        private string _strVelOn;
        private string _strVelOff;
        private string _strAngOn;
        private string _strAngOff;
        private string _strRepairOn;
        private string _strRepairOff;
        private string _strMenu;
        private string _strFlyControls = "     WASD / Space / Ctrl  +  Shift=Turbo";

        // Base layout constants (scaled at draw time)
        private const float BASE_CTRL_W     = 420f;
        private const float BASE_CTRL_H     = 260f;
        private const float BASE_CTRL_H_FLY = 308f;
        private const float BASE_COORD_W    = 240f;
        private const float BASE_COORD_H    = 92f;
        private const float BASE_PAD        = 20f;

        private float Scale => Mathf.Clamp(TrainerConfig.Settings.OverlayScale, 0.25f, 2.0f);
        private float _cachedScale = -1f;

        // Colors
        private readonly Color _bgColor    = new Color(0.08f, 0.08f, 0.08f, 0.85f);
        private readonly Color _borderColor = new Color(0.4f,  0.6f,  1.0f,  1.0f);
        private readonly Color _headerColor = new Color(0.0f,  0.8f,  1.0f,  1.0f);
        private readonly Color _savedColor  = new Color(0.2f,  1.0f,  0.4f,  1.0f);
        private readonly Color _flyColor    = new Color(0.0f,  1.0f,  1.0f,  1.0f);
        private readonly Color _dimColor    = new Color(0.85f, 0.85f, 0.85f, 1.0f);
        private readonly Color _ctrlColor   = new Color(1.0f,  0.7f,  0.4f,  1.0f);
        private readonly Color _dangerColor = new Color(0.8f,  0.3f,  0.3f,  1.0f);

        // Styles — rebuilt when scale changes
        private GUIStyle _styleHeader;
        private GUIStyle _styleText;
        private bool _stylesReady = false;

        public void UpdateData(Vector3 pos, float speed, bool hasSaved, bool flyActive, bool keepVelocity, bool keepAngle, bool autoRepair)
        {
            if (_cachedCoords == null || Vector3.Distance(_currentPosition, pos) > 0.05f)
            {
                _currentPosition = pos;
                _cachedHeightStr = $"HEIGHT: {_currentPosition.y:F1} M";
                _cachedCoordsStr = $"XYZ: {_currentPosition.x:F1}, {_currentPosition.y:F1}, {_currentPosition.z:F1}";
                _cachedCoords = pos;
            }

            if (Mathf.Abs(_lastSpeed - speed) > 0.1f)
            {
                _lastSpeed = speed;
                _cachedSpeedStr = $"SPEED: {speed:F1} M/S";
            }

            _hasSavedPosition = hasSaved;
            _flyModeActive = flyActive;
            _keepVelocityActive = keepVelocity;
            _keepAngleActive = keepAngle;
            _autoRepairActive = autoRepair;
            _showOverlay = Application.isFocused;
        }

        public void SetPositionSaved(bool saved) => _hasSavedPosition = saved;
        public void SetFlyModeActive(bool active) => _flyModeActive = active;

        public void RefreshKeybinds(InputDeviceType device = InputDeviceType.Keyboard)
        {
            bool ps = InputDeviceTracker.IsDualShock;
            string saveStr = TrainerConfig.Settings.SavePosition.ToString(device, ps).PadRight(10);
            string telStr = TrainerConfig.Settings.Teleport.ToString(device, ps).PadRight(10);
            string flyStr = TrainerConfig.Settings.ToggleFlyMode.ToString(device, ps).PadRight(10);
            string velStr = TrainerConfig.Settings.ToggleKeepVelocity.ToString(device, ps).PadRight(10);
            string angStr = TrainerConfig.Settings.ToggleKeepAngle.ToString(device, ps).PadRight(10);
            string repStr = TrainerConfig.Settings.ToggleAutoRepair.ToString(device, ps).PadRight(10);
            string menuStr = TrainerConfig.Settings.OpenBindMenu.ToString(device, ps).PadRight(10);

            _strSave = $"  {saveStr}:  Save position";
            _strTelSave = $"  {telStr}:  Teleport (Save first)";
            _strTelReady = $"  {telStr}:  Teleport (Ready)";
            _strFly = $"  {flyStr}:  Toggle Fly Mode";
            _strVelOn = $"  {velStr}:  Keep Velocity [ON]";
            _strVelOff = $"  {velStr}:  Keep Velocity [OFF]";
            _strAngOn = $"  {angStr}:  Keep Angle [ON]";
            _strAngOff = $"  {angStr}:  Keep Angle [OFF]";
            _strRepairOn = $"  {repStr}:  Auto-Repair [ON]";
            _strRepairOff = $"  {repStr}:  Auto-Repair [OFF]";
            _strMenu = $"  {menuStr}:  Open Bind Menu";

            // Fly-mode control hint, switched by active device.
            if (device == InputDeviceType.Gamepad)
                _strFlyControls = ps
                    ? "     L-Stick / R2 / L2  +  Circle=Turbo"
                    : "     L-Stick / RT / LT  +  B=Turbo";
            else
                _strFlyControls = "     WASD / Space / Ctrl  +  Shift=Turbo";
        }

        public void OnGUI()
        {
            if (!_showOverlay) return;

            EnsureStyles();
            DrawControls();
            DrawCoords();
        }

        // ── Styles ──────────────────────────────────────────────────────────

        private void EnsureStyles()
        {
            float scale = Scale;
            if (_stylesReady && Mathf.Approximately(_cachedScale, scale)) return;
            _cachedScale = scale;

            _styleHeader = new GUIStyle();
            _styleHeader.fontSize = Mathf.RoundToInt(22 * scale);
            _styleHeader.fontStyle = FontStyle.Bold;
            _styleHeader.normal.textColor = _headerColor;
            _styleHeader.alignment = TextAnchor.UpperLeft;

            _styleText = new GUIStyle();
            _styleText.fontSize = Mathf.RoundToInt(18 * scale);
            _styleText.fontStyle = FontStyle.Bold;
            _styleText.normal.textColor = Color.white;
            _styleText.alignment = TextAnchor.UpperLeft;

            _stylesReady = true;
        }

        // ── Controls overlay (bottom-left) ──────────────────────────────────

        private void DrawControls()
        {
            float sc = Scale;
            float h = (_flyModeActive ? BASE_CTRL_H_FLY : BASE_CTRL_H) * sc;
            float x = BASE_PAD;
            float y = Screen.height - h - BASE_PAD;
            float w = BASE_CTRL_W * sc;

            DrawBox(x, y, w, h);

            float lw   = (BASE_CTRL_W - 20f) * sc;
            float cx   = x + 10f * sc;
            float cy   = y + 12f * sc;
            float lineH   = 24f * sc;
            float headerH = 28f * sc;

            // Header
            _styleHeader.normal.textColor = _headerColor;
            GUI.Label(new Rect(cx, cy, lw, headerH), "  FLIPPING IS HARD TRAINER", _styleHeader);
            cy += headerH;

            // Fly mode status
            if (_flyModeActive)
            {
                _styleText.normal.textColor = _flyColor;
                GUI.Label(new Rect(cx, cy, lw, lineH), "  \u00bb FLY MODE ACTIVE", _styleText);
                cy += lineH;
                _styleText.normal.textColor = _dimColor;
                GUI.Label(new Rect(cx, cy, lw, lineH), _strFlyControls, _styleText);
                cy += lineH;
            }

            _styleText.normal.textColor = Color.white;
            GUI.Label(new Rect(cx, cy, lw, lineH), _strSave, _styleText);
            cy += lineH;

            _styleText.normal.textColor = _hasSavedPosition ? _savedColor : _dimColor;
            GUI.Label(new Rect(cx, cy, lw, lineH), _hasSavedPosition ? _strTelReady : _strTelSave, _styleText);
            cy += lineH;

            _styleText.normal.textColor = _ctrlColor;
            GUI.Label(new Rect(cx, cy, lw, lineH), _strFly, _styleText);
            cy += lineH;

            _styleText.normal.textColor = _keepVelocityActive ? _savedColor : _dimColor;
            GUI.Label(new Rect(cx, cy, lw, lineH), _keepVelocityActive ? _strVelOn : _strVelOff, _styleText);
            cy += lineH;

            _styleText.normal.textColor = _keepAngleActive ? _savedColor : _dimColor;
            GUI.Label(new Rect(cx, cy, lw, lineH), _keepAngleActive ? _strAngOn : _strAngOff, _styleText);
            cy += lineH;

            _styleText.normal.textColor = _autoRepairActive ? _savedColor : _dimColor;
            GUI.Label(new Rect(cx, cy, lw, lineH), _autoRepairActive ? _strRepairOn : _strRepairOff, _styleText);
            cy += lineH;

            _styleText.normal.textColor = _dimColor;
            GUI.Label(new Rect(cx, cy, lw, lineH), _strMenu, _styleText);
        }

        // ── Coordinates overlay (top-right) ─────────────────────────────────

        private void DrawCoords()
        {
            float sc = Scale;
            float cw = BASE_COORD_W * sc;
            float ch = BASE_COORD_H * sc;
            float x  = Screen.width - cw - BASE_PAD;
            float y  = BASE_PAD;

            DrawBox(x, y, cw, ch);

            float cx    = x + 12f * sc;
            float cy    = y + 10f * sc;
            float labelH = 24f * sc;

            _styleText.normal.textColor = Color.white;
            GUI.Label(new Rect(cx, cy, cw - 24f * sc, labelH), _cachedSpeedStr, _styleText);
            cy += labelH;
            GUI.Label(new Rect(cx, cy, cw - 24f * sc, labelH), _cachedHeightStr, _styleText);
            cy += labelH;
            GUI.Label(new Rect(cx, cy, cw - 24f * sc, labelH), _cachedCoordsStr, _styleText);
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private void DrawBox(float x, float y, float w, float h)
        {
            Color orig = GUI.color;

            GUI.color = _bgColor;
            GUI.Box(new Rect(x, y, w, h), GUIContent.none);

            float b = 2f;
            GUI.color = _borderColor;
            GUI.Box(new Rect(x,         y,         w, b), GUIContent.none); // top
            GUI.Box(new Rect(x,         y + h - b, w, b), GUIContent.none); // bottom
            GUI.Box(new Rect(x,         y,         b, h), GUIContent.none); // left
            GUI.Box(new Rect(x + w - b, y,         b, h), GUIContent.none); // right

            GUI.color = orig;
        }
    }
}
