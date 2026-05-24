using UnityEngine;

namespace FlippingIsHardTrainer
{
    public class OverlayRenderer
    {
        // Overlay data
        private Vector3 _currentPosition = Vector3.zero;
        private bool _hasSavedPosition = false;
        private bool _flyModeActive = false;
        private bool _showOverlay = true;

        // Layout constants
        private const int CTRL_W = 390;
        private const int CTRL_H = 164;
        private const int CTRL_H_FLY = 212;
        private const int COORD_W = 240;
        private const int COORD_H = 70;
        private const int PAD = 20;

        // Colors
        private readonly Color _bgColor    = new Color(0.08f, 0.08f, 0.08f, 0.85f);
        private readonly Color _borderColor = new Color(0.4f,  0.6f,  1.0f,  1.0f);
        private readonly Color _headerColor = new Color(0.0f,  0.8f,  1.0f,  1.0f);
        private readonly Color _savedColor  = new Color(0.2f,  1.0f,  0.4f,  1.0f);
        private readonly Color _flyColor    = new Color(0.0f,  1.0f,  1.0f,  1.0f);
        private readonly Color _dimColor    = new Color(0.7f,  0.7f,  0.7f,  1.0f);
        private readonly Color _ctrlColor   = new Color(1.0f,  0.7f,  0.4f,  1.0f);
        private readonly Color _dangerColor = new Color(0.8f,  0.3f,  0.3f,  1.0f);

        // Styles — created lazily inside OnGUI
        private GUIStyle _styleHeader;
        private GUIStyle _styleText;
        private bool _stylesReady = false;

        public void UpdateData(Vector3 pos, bool hasSaved, bool flyActive)
        {
            _currentPosition = pos;
            _hasSavedPosition = hasSaved;
            _flyModeActive = flyActive;
            _showOverlay = Application.isFocused;
        }

        public void SetPositionSaved(bool saved) => _hasSavedPosition = saved;
        public void SetFlyModeActive(bool active) => _flyModeActive = active;

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
            if (_stylesReady) return;

            // In IL2CPP interop, GUIStyle() default constructor works fine.
            // We copy from GUI.skin.label using the Pointer property.
            _styleHeader = new GUIStyle();
            _styleHeader.fontSize = 22;
            _styleHeader.fontStyle = FontStyle.Bold;
            _styleHeader.normal.textColor = _headerColor;
            _styleHeader.alignment = TextAnchor.UpperLeft;

            _styleText = new GUIStyle();
            _styleText.fontSize = 18;
            _styleText.fontStyle = FontStyle.Bold;
            _styleText.normal.textColor = Color.white;
            _styleText.alignment = TextAnchor.UpperLeft;

            _stylesReady = true;
        }

        // ── Controls overlay (bottom-left) ──────────────────────────────────

        private void DrawControls()
        {
            float h = _flyModeActive ? CTRL_H_FLY : CTRL_H;
            float x = PAD;
            float y = Screen.height - h - PAD;

            DrawBox(x, y, CTRL_W, h);

            float cx = x + 10;
            float cy = y + 12;

            // Header
            _styleHeader.normal.textColor = _headerColor;
            GUI.Label(new Rect(cx, cy, CTRL_W - 20, 28), "  FLIPPING IS HARD TRAINER", _styleHeader);
            cy += 28;

            // Fly mode status
            if (_flyModeActive)
            {
                _styleText.normal.textColor = _flyColor;
                GUI.Label(new Rect(cx, cy, CTRL_W - 20, 24), "  \u00bb FLY MODE ACTIVE", _styleText);
                cy += 24;
                _styleText.normal.textColor = _dimColor;
                GUI.Label(new Rect(cx, cy, CTRL_W - 20, 24), "     WASD / Space / Ctrl  +  Shift=Turbo", _styleText);
                cy += 24;
            }

            // Shift+R
            _styleText.normal.textColor = Color.white;
            GUI.Label(new Rect(cx, cy, CTRL_W - 20, 24), "  Shift+R   :  Save position", _styleText);
            cy += 24;

            // R
            _styleText.normal.textColor = _hasSavedPosition ? _savedColor : _dimColor;
            string teleportLabel = _hasSavedPosition ? "  R         :  Teleport (Ready)" : "  R         :  Teleport (Save first)";
            GUI.Label(new Rect(cx, cy, CTRL_W - 20, 24), teleportLabel, _styleText);
            cy += 24;

            // F
            _styleText.normal.textColor = _ctrlColor;
            GUI.Label(new Rect(cx, cy, CTRL_W - 20, 24), "  F         :  Toggle Fly Mode", _styleText);
        }

        // ── Coordinates overlay (top-right) ─────────────────────────────────

        private void DrawCoords()
        {
            float x = Screen.width - COORD_W - PAD;
            float y = PAD;

            DrawBox(x, y, COORD_W, COORD_H);

            float cx = x + 12;
            float cy = y + 10;

            _styleText.normal.textColor = Color.white;
            GUI.Label(new Rect(cx, cy, COORD_W - 24, 22),
                $"HEIGHT: {_currentPosition.y:F1} M", _styleText);
            cy += 26;
            GUI.Label(new Rect(cx, cy, COORD_W - 24, 22),
                $"XYZ: {_currentPosition.x:F1}, {_currentPosition.y:F1}, {_currentPosition.z:F1}", _styleText);
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
