using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using UnityEngine;

namespace FlippingIsHardTrainer
{
    // Ghost mode's start and end on one map, saved in Ghosts/Courses/<map>.json. Vectors are
    // float arrays: System.Text.Json doesn't see the interop Vector3's fields.
    public sealed class GhostCourse
    {
        public const float DefaultEndSize = 4f;
        public const float MinEndSize = 1f;
        public const float MaxEndSize = 30f;

        // Start: the phone's position and rotation, and the camera's orbit yaw. Null = not set.
        public float[] Start { get; set; }
        public float[] StartRotation { get; set; }
        public float StartCameraYaw { get; set; }
        // End: a cube of EndSize metres around this point. Null = not set.
        public float[] End { get; set; }
        public float EndSize { get; set; } = DefaultEndSize;

        [JsonIgnore] public bool HasStart => Start?.Length == 3 && StartRotation?.Length == 4;
        [JsonIgnore] public bool HasEnd => End?.Length == 3;
        [JsonIgnore] public bool IsReady => HasStart && HasEnd;
        [JsonIgnore] public Vector3 StartPosition => new Vector3(Start[0], Start[1], Start[2]);
        [JsonIgnore] public Quaternion StartRot => new Quaternion(StartRotation[0], StartRotation[1], StartRotation[2], StartRotation[3]);
        [JsonIgnore] public Vector3 EndCenter => new Vector3(End[0], End[1], End[2]);
        [JsonIgnore] public Bounds EndBounds => new Bounds(EndCenter, Vector3.one * EndSize);

        public void SetStart(Vector3 pos, Quaternion rot, float cameraYaw)
        {
            Start = new[] { pos.x, pos.y, pos.z };
            StartRotation = new[] { rot.x, rot.y, rot.z, rot.w };
            StartCameraYaw = cameraYaw;
        }

        public void SetEnd(Vector3 center) => End = new[] { center.x, center.y, center.z };

        private static string PathFor(string mapId) => Path.Combine(GhostLibrary.CoursesFolder, GhostLibrary.SafeName(mapId) + ".json");

        /// <summary>The map's course; an empty one when it has none or it can't be read.</summary>
        public static GhostCourse Load(string mapId)
        {
            string path = PathFor(mapId);
            try
            {
                if (File.Exists(path))
                    return JsonSerializer.Deserialize<GhostCourse>(File.ReadAllText(path)) ?? new GhostCourse();
            }
            catch (Exception ex)
            {
                TrainerPlugin.Logger.LogError($"[GHOST] Could not read {path}: {ex.Message}. Starting with no start or end.");
            }
            return new GhostCourse();
        }

        public void Save(string mapId)
        {
            string path = PathFor(mapId);
            try
            {
                Directory.CreateDirectory(GhostLibrary.CoursesFolder);
                File.WriteAllText(path, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception ex)
            {
                TrainerPlugin.Logger.LogError($"[GHOST] Could not save {path}: {ex.Message}");
            }
        }
    }
}
