using System;
using UnityEngine;

namespace FlippingIsHardTrainer
{
    // Copied from FIH Ghost's LineBox.cs (https://github.com/d1ll3x3/FIH_Ghost), itself the
    // Map Editor's LineBox trimmed to the box and the line.
    //
    // World-space wireframe drawn with a LineRenderer, for ghost mode's start marker and
    // end zone. No custom shaders: tries a list of always-included shaders and
    // stays invisible if none exist.
    internal sealed class LineBox
    {
        private static readonly string[] ShaderCandidates =
        {
            "Sprites/Default",
            "Universal Render Pipeline/Unlit",
            "UI/Default",
        };

        private readonly string _name;
        private GameObject _go;
        private LineRenderer _lr;
        private bool _initFailed;

        public LineBox(string name) => _name = name;

        // Single line strip visiting all 12 edges of the box (16 points).
        public void ShowBox(Vector3 center, Vector3 size, Color color)
        {
            Vector3 h = size * 0.5f;
            Vector3 b0 = center + new Vector3(-h.x, -h.y, -h.z), b1 = center + new Vector3(h.x, -h.y, -h.z);
            Vector3 b2 = center + new Vector3(h.x, -h.y, h.z), b3 = center + new Vector3(-h.x, -h.y, h.z);
            Vector3 t0 = center + new Vector3(-h.x, h.y, -h.z), t1 = center + new Vector3(h.x, h.y, -h.z);
            Vector3 t2 = center + new Vector3(h.x, h.y, h.z), t3 = center + new Vector3(-h.x, h.y, h.z);
            Show(new[]
            {
                b0, b1, b2, b3, b0,      // bottom loop
                t0, t1, b1, t1,          // up + edge to b1 and back
                t2, b2, t2,
                t3, b3, t3,
                t0,                      // close top loop
            }, color, 0.06f);
        }

        public void ShowLine(Vector3 a, Vector3 b, Color color) => Show(new[] { a, b }, color, 0.08f);

        public void Hide()
        {
            if (_go != null && _go.activeSelf) _go.SetActive(false);
        }

        private void Show(Vector3[] path, Color color, float width)
        {
            if (!EnsureCreated()) return;
            _lr.positionCount = path.Length;
            for (int i = 0; i < path.Length; i++) _lr.SetPosition(i, path[i]);
            _lr.startWidth = _lr.endWidth = width;
            _lr.startColor = _lr.endColor = color;
            var mat = _lr.material;
            if (mat != null)
            {
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
                else if (mat.HasProperty("_Color")) mat.color = color;
            }
            if (!_go.activeSelf) _go.SetActive(true);
        }

        private bool EnsureCreated()
        {
            // Create lazily because shader lookup before the game scene is initialized can
            // incorrectly report that an otherwise available built-in shader is missing.
            if (_go != null) return true;
            if (_initFailed) return false;
            Shader shader = null;
            foreach (var s in ShaderCandidates)
            {
                shader = Shader.Find(s);
                if (shader != null) break;
            }
            if (shader == null)
            {
                TrainerPlugin.Logger.LogWarning("[GHOST] No usable shader found; start and end markers disabled.");
                _initFailed = true;
                return false;
            }
            try
            {
                // Survives scene reloads; the owner hides and reuses it.
                _go = new GameObject(_name);
                UnityEngine.Object.DontDestroyOnLoad(_go);
                _lr = _go.AddComponent<LineRenderer>();
                _lr.useWorldSpace = true;
                _lr.material = new Material(shader);
                _lr.positionCount = 0;
                _lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _lr.receiveShadows = false;
                return true;
            }
            catch (Exception ex)
            {
                TrainerPlugin.Logger.LogWarning($"[GHOST] Marker init failed: {ex.Message}");
                _initFailed = true;
                return false;
            }
        }
    }
}
