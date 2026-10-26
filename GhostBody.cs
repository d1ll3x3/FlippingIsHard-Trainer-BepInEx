using EHS;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace FlippingIsHardTrainer
{
    // Copied from FIH Ghost's GhostBody.cs (https://github.com/d1ll3x3/FIH_Ghost), so the
    // trainer's ghosts look like that mod's.
    //
    // The visible ghost: a copy of the local player's PlayerVisual reduced to the bare
    // phone (casing and screen, no cosmetics), with every script, collider and rigidbody
    // removed and one translucent material on every renderer. Layout of PlayerVisual:
    // FIH Ghost's NOTAS_FASE1.md, "El jugador".
    internal sealed class GhostBody
    {
        // Children that only matter for the real phone: the hidden hitbox mesh, the broken
        // phone's insides, FMOD sounds and the camera shake.
        private static readonly string[] StrippedChildren =
        {
            "BoundingBox", "Visuals_DestroyedOnly",
            "AutoAlignVibrationSound", "DestroySound", "CameraShakeOnDestroy",
        };

        // Skin slots kept per container: the phone itself. Every other slot (charm,
        // effects, stickers, accessories, and any category a game update adds) is a
        // cosmetic and goes.
        private static readonly (string container, string kept)[] PhoneSlots =
        {
            ("Visuals_Always", "Face"),
            ("Visuals_Regular", "Cover"),
        };

        // Sprites/Default is transparent out of the box; URP/Unlit would need transparent
        // keywords the build may have stripped. Both ship with the game (F11 dump).
        private static readonly string[] ShaderCandidates = { "Sprites/Default", "Universal Render Pipeline/Unlit" };

        private readonly Color _color;
        private GameObject _root;
        private Material _material;

        public GhostBody(Color color) => _color = color;

        public bool IsAlive => _root != null;
        public Transform Root => _root != null ? _root.transform : null;

        /// <summary>Builds the ghost from the player's current visuals. False if it can't.</summary>
        public bool Create(PlayerRef player)
        {
            Destroy();
            var visual = player.PlayerVisualHandler?.PlayerVisualT;
            if (visual == null)
            {
                TrainerPlugin.Logger.LogWarning("[GHOST] The player has no PlayerVisual yet.");
                return false;
            }
            if (!EnsureMaterial()) return false;

            // Cloned under an inactive parent the copy never becomes active, so none of the
            // cloned game scripts runs Awake/OnEnable before it is stripped.
            var holder = new GameObject("TrainerGhost_Holder");
            holder.SetActive(false);
            try
            {
                var clone = Object.Instantiate(visual.gameObject, holder.transform);
                clone.name = "TrainerGhost";
                // Destroyed with the holder below if it can't be made inert.
                if (!Strip(clone)) return false;
                ApplyMaterial(clone);
                clone.transform.SetParent(null, false);
                _root = clone;
            }
            finally
            {
                Object.Destroy(holder);
            }
            TrainerPlugin.Logger.LogInfo("[GHOST] Body created.");
            return true;
        }

        public void SetPose(Vector3 position, Quaternion rotation)
        {
            _root.transform.SetPositionAndRotation(position, rotation);
        }

        public void Destroy()
        {
            if (_root != null) Object.Destroy(_root);
            _root = null;
        }

        private static bool Strip(GameObject clone)
        {
            var t = clone.transform;
            foreach (var path in StrippedChildren)
            {
                var child = t.Find(path);
                if (child != null) Object.DestroyImmediate(child.gameObject);
            }
            foreach (var (container, kept) in PhoneSlots)
            {
                var parent = t.Find(container);
                if (parent == null) continue;
                for (int i = parent.childCount - 1; i >= 0; i--)
                {
                    var slot = parent.GetChild(i);
                    if (slot.name != kept) Object.DestroyImmediate(slot.gameObject);
                }
            }

            // Scripts first: some require a collider or rigidbody and would block removing it.
            // '&', not '&&': every type is still removed after one fails.
            bool clean = RemoveAll<MonoBehaviour>(clone) & RemoveAll<Joint>(clone)
                         & RemoveAll<Collider>(clone) & RemoveAll<Rigidbody>(clone);

            // Off the player's layer, so nothing that raycasts or filters for the player
            // (camera, triggers) mistakes the ghost for it.
            foreach (var child in clone.GetComponentsInChildren<Transform>(true))
                child.gameObject.layer = 0;
            return clean;
        }

        // DestroyImmediate refuses a component another one still [RequireComponent]s; that
        // one goes in the same pass, so a later pass can remove the first. Anything left
        // after the passes would wake up with the ghost, so it is reported as a failure.
        private static bool RemoveAll<T>(GameObject clone) where T : Component
        {
            for (int pass = 0; pass < 4; pass++)
            {
                var left = clone.GetComponentsInChildren<T>(true);
                if (left.Length == 0) return true;
                foreach (var c in left) Object.DestroyImmediate(c);
            }
            var stuck = clone.GetComponentsInChildren<T>(true);
            if (stuck.Length == 0) return true;
            TrainerPlugin.Logger.LogError($"[GHOST] Could not remove {stuck.Length} {typeof(T).Name}(s), first: " +
                                        $"{stuck[0].GetIl2CppType().FullName} on '{stuck[0].name}'");
            return false;
        }

        private void ApplyMaterial(GameObject clone)
        {
            foreach (var renderer in clone.GetComponentsInChildren<Renderer>(true))
            {
                var shared = renderer.sharedMaterials;
                // Inverted-hull outline meshes would render as a solid shell with a
                // double-sided transparent material.
                bool outline = false;
                foreach (var m in shared)
                    if (m != null && m.shader != null && m.shader.name.Contains("Outline")) outline = true;
                if (outline)
                {
                    renderer.enabled = false;
                    continue;
                }

                var mats = new Material[shared.Length];
                for (int i = 0; i < mats.Length; i++) mats[i] = _material;
                renderer.sharedMaterials = mats;
                // MaterialHandlerOptimized leaves a property block on the casing's renderers
                // (not the screen's) and the clone keeps it, overriding the ghost material:
                // only the screen showed. Cleared the way the game clears it.
                renderer.SetPropertyBlock(null);
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }

        private bool EnsureMaterial()
        {
            if (_material != null) return true;
            foreach (var name in ShaderCandidates)
            {
                var shader = Shader.Find(name);
                if (shader == null) continue;
                _material = new Material(shader) { name = "TrainerGhost_Material", color = _color };
                // Draw after the level's opaque geometry, like any transparent object.
                _material.renderQueue = (int)RenderQueue.Transparent;
                TrainerPlugin.Logger.LogInfo($"[GHOST] Material shader: {name}");
                return true;
            }
            TrainerPlugin.Logger.LogError("[GHOST] No usable shader for the ghost material.");
            return false;
        }
    }
}
