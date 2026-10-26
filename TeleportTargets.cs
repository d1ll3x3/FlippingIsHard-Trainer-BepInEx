using System;
using System.Collections.Generic;
using System.Linq;
using EHS;
using EHS.Interactables;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FlippingIsHardTrainer
{
    // One teleport target: where the phone lands (rigidbody position and rotation) and the
    // camera's orbit yaw it lands with.
    public sealed class TeleportTarget
    {
        public readonly string Name;
        public readonly Vector3 Position;
        public readonly Quaternion Rotation;
        public readonly float CameraYaw;

        public TeleportTarget(string name, Vector3 position, Quaternion rotation, float cameraYaw)
        {
            Name = name;
            Position = position;
            Rotation = rotation;
            CameraYaw = cameraYaw;
        }
    }

    // Copied from FIH Ghost's WorldIndex.cs (https://github.com/d1ll3x3/FIH_Ghost), so the
    // Teleports tab lands exactly where that mod's Levels and NPCs tabs do.
    //
    // An area starts where its progress zone starts: each area has ChangeLevelProgress zones
    // (by height or along a spline) that tell the game how far the player got, and their
    // start point is on the path the level is climbed from. A boss area is the exception:
    // landing inside it skips the door that starts the fight (the game then forces a
    // fail-safe boss respawn), so its target, and that of any NPC inside it, is the end of
    // the previous area's path, in front of that door. That point is above the door's roof,
    // so the target is the first floor under it. Every computed target is put on the floor
    // below it, so the phone doesn't land falling. The spots the level data gets wrong are
    // placed by hand (FixedTargets).
    public static class TeleportTargets
    {
        // A zone's path from its start to its end, and the volume its trigger covers.
        private struct Zone
        {
            public int Area;
            public Vector3 Start, StartAhead, End, EndBehind;
            public Bounds Bounds;
            public bool HasBounds;
        }

        // The phone rests this far above the floor it lands on: lying flat it is ~0.31 m
        // thick (its BoundingBox), so its centre sits just above the floor.
        private const float RestHeight = 0.2f;
        // How far above a target the floor search starts, and how far down it looks.
        private const float SearchAbove = 1.5f;
        private const float SearchDepth = 12f;
        // Under a roof the floor can be further down than in the open.
        private const float RoofSearchDepth = 30f;
        // How far in front of an NPC the phone lands.
        private const float NpcDistance = 2.5f;
        // Everything but the local player (layer 6), so the search never hits the phone
        // itself wherever it stands.
        private const int GroundMask = ~(1 << 6);

        public const string StoryMapId = "OfficialMap_TrashlandsStory";

        // The story map's spots the level data gets wrong, placed by hand in the game on
        // 25-oct-2026 with the phone resting where a run of that level starts: rigidbody
        // position and rotation, and the camera's orbit yaw. L10 and Narrator are in front
        // of the boss door, under its roof; landing past it starts the fight's fail-safe.
        private static readonly Dictionary<string, (Vector3 pos, Quaternion rot, float cameraYaw)> FixedTargets =
            new Dictionary<string, (Vector3, Quaternion, float)>
        {
            ["area:2"] = (new Vector3(18.607f, -14.475f, 262.544f), new Quaternion(0f, 0.03078f, 0f, 0.99953f), 4.3f),
            ["area:3"] = (new Vector3(-4.285f, 20.226f, 89.658f), new Quaternion(0f, 0.99721f, 0f, 0.07462f), -5.9f),
            ["area:4"] = (new Vector3(-36.564f, 374.906f, -15.619f), new Quaternion(0f, 0.24265f, 0f, 0.97011f), 24.1f),
            ["area:7"] = (new Vector3(-2.203f, 2288.446f, -39.797f), new Quaternion(0f, 0.99859f, 0f, -0.05301f), 19.9f),
            ["area:8"] = (new Vector3(40.449f, 2663.528f, -170.198f), new Quaternion(-0.00732f, 0.91112f, -0.0125f, 0.41188f), 132f),
            ["area:9"] = (new Vector3(107.616f, 3132.23f, -181.135f), new Quaternion(0f, -0.51351f, 0f, 0.85809f), -67.4f),
            ["area:10"] = (new Vector3(-171.424f, 3511.64f, -79.893f), new Quaternion(0.71287f, 0f, 0.7013f, 0f), -76f),
            ["area:11"] = (new Vector3(-314.231f, 3583.05f, 195.891f), new Quaternion(0f, -0.71351f, 0f, 0.70064f), 140.2f),
            ["npc:Webby"] = (new Vector3(-88.997f, 986.947f, 176.111f), new Quaternion(-0.25796f, 0.28094f, 0.91968f, 0.0933f), -57.7f),
            ["npc:Narrator"] = (new Vector3(-170.635f, 3511.64f, -79.624f), new Quaternion(0.70557f, 0f, 0.70864f, 0f), -87.8f),
        };
        // Interactable NPCs left out of the teleport list (not characters to visit).
        private static readonly string[] HiddenNpcs = { "Microwaver", "Phone" };

        private static TeleportTarget[] _levels = Array.Empty<TeleportTarget>();
        private static TeleportTarget[] _npcs = Array.Empty<TeleportTarget>();
        private static IntPtr _builtFor;

        public static TeleportTarget[] Levels => _levels;
        public static TeleportTarget[] Npcs => _npcs;

        // Checked through its Rigidbody: comparing PlayerRef itself needs FishNet, which the
        // trainer doesn't reference.
        public static bool IsPlayerReady() => PlayerRef.LocalPlayer?.Rb != null;

        // The targets only fit the story map's geometry.
        public static bool IsStoryMap()
        {
            var progress = PlayerRef.LocalPlayer?.GameRefs?.WorldSaveManager?.GetCurrentProgress();
            return progress != null && progress.MapId == StoryMapId;
        }

        /// <summary>
        /// Rebuilds the targets when the level (its LevelsInfoManager) changed, or when forced
        /// (a list built while the level was still spawning its objects could miss some).
        /// </summary>
        public static void Refresh(bool force = false)
        {
            try
            {
                var player = PlayerRef.LocalPlayer;
                var levels = player?.GameRefs?.LevelsInfoManager;
                if (levels == null) return;
                if (!force && levels.Pointer == _builtFor) return;
                _builtFor = levels.Pointer;

                bool story = IsStoryMap();
                var zones = FindZones();
                var infos = levels.LevelsInfo?.TryCast<Il2CppSystem.Collections.Generic.List<LevelInfoSO>>();
                var areas = new List<(int id, string name)>();
                for (int i = 0; infos != null && i < infos.Count; i++)
                    areas.Add((infos[i].Id, CleanAreaName(infos[i].name)));

                var doors = new Dictionary<int, (Vector3 pos, Quaternion rot)>();
                var levelTargets = new List<TeleportTarget>();
                foreach (var (id, name) in areas)
                {
                    var own = zones.Where(z => z.Area == id).ToList();
                    bool isBoss = name.IndexOf("Boss", StringComparison.OrdinalIgnoreCase) >= 0;
                    var before = zones.Where(z => z.Area < id).ToList();
                    bool has = false;
                    Vector3 pos = default;
                    Quaternion rot = Quaternion.identity;
                    float? cameraYaw = null;
                    if (isBoss && before.Count > 0)
                    {
                        // Highest end among the areas below: the end of the climb up to the door.
                        var last = before.OrderByDescending(z => z.End.y).First();
                        doors[id] = (UnderRoof(last.End), Facing(last.EndBehind, last.End));
                        (pos, rot) = doors[id];
                        has = true;
                    }
                    else if (own.Count > 0)
                    {
                        // Lowest start: areas are climbed upwards, so that is where it begins.
                        var first = own.OrderBy(z => z.Start.y).First();
                        pos = OnFloor(first.Start);
                        rot = Facing(first.Start, first.StartAhead);
                        has = true;
                    }
                    if (story && FixedTargets.TryGetValue("area:" + id, out var fixedTarget))
                    {
                        (pos, rot, float yaw) = fixedTarget;
                        cameraYaw = yaw;
                        has = true;
                    }
                    if (has) levelTargets.Add(new TeleportTarget($"{id}. {name}", pos, rot, cameraYaw ?? rot.eulerAngles.y));
                }
                _levels = levelTargets.ToArray();
                _npcs = FindNpcs(zones, doors, story);
                TrainerPlugin.Logger.LogInfo($"Teleports: {_levels.Length} levels, {_npcs.Length} NPCs");
            }
            catch (Exception ex)
            {
                TrainerPlugin.Logger.LogError($"Error building the teleport targets: {ex}");
            }
        }

        // Same steps as the game's own checkpoint load: through its PlayerTeleportController
        // (which keeps the NetworkTransform in sync), velocities cleared, the camera snapped
        // to the new spot and turned to the target's yaw.
        public static bool Teleport(TeleportTarget target)
        {
            try
            {
                var player = PlayerRef.LocalPlayer;
                var teleport = player?.PlayerTeleportController;
                var rb = player?.Rb;
                if (teleport == null || rb == null)
                {
                    TrainerPlugin.Logger.LogWarning($"Could not teleport to {target.Name}: player not ready");
                    return false;
                }
                teleport.TeleportTo(target.Position, target.Rotation);
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;

                player.GameRefs?.CameraManager?.SyncPosAfterTeleport(target.Position);
                var orbital = Orbital(player);
                if (orbital != null)
                {
                    var axis = orbital.HorizontalAxis;
                    axis.Value = Mathf.Repeat(target.CameraYaw + 180f, 360f) - 180f;
                    orbital.HorizontalAxis = axis;
                }
                TrainerPlugin.Logger.LogInfo($"Teleported to {target.Name}");
                return true;
            }
            catch (Exception ex)
            {
                TrainerPlugin.Logger.LogError($"Error teleporting to {target.Name}: {ex}");
                return false;
            }
        }

        /// <summary>The camera's orbit yaw in degrees, or the phone's heading without one.</summary>
        public static float CameraYaw(PlayerRef player)
        {
            var orbital = Orbital(player);
            return orbital != null ? orbital.HorizontalAxis.Value : player.Rb.rotation.eulerAngles.y;
        }

        private static Unity.Cinemachine.CinemachineOrbitalFollow Orbital(PlayerRef player)
            => player.GameRefs?.CameraManager?.MainCinemachineCamera?.GetComponent<Unity.Cinemachine.CinemachineOrbitalFollow>();

        private static List<Zone> FindZones()
        {
            var zones = new List<Zone>();
            foreach (var zone in Object.FindObjectsOfType<ChangeLevelProgressByHeight>(true))
            {
                int id = ZoneArea(zone);
                if (id < 0 || zone.startPosT == null) continue;
                Vector3 start = zone.startPosT.position;
                Vector3 end = zone.finishPosT != null ? zone.finishPosT.position : start;
                zones.Add(WithBounds(zone, new Zone { Area = id, Start = start, StartAhead = end, End = end, EndBehind = start }));
            }
            foreach (var zone in Object.FindObjectsOfType<ChangeLevelProgressBySpline>(true))
            {
                int id = ZoneArea(zone);
                if (id < 0) continue;
                var path = zone.cachedPositions;
                if (path != null && path.Length > 0)
                {
                    int n = path.Length;
                    zones.Add(WithBounds(zone, new Zone
                    {
                        Area = id, Start = path[0], StartAhead = path[Math.Min(5, n - 1)],
                        End = path[n - 1], EndBehind = path[Math.Max(0, n - 6)],
                    }));
                    continue;
                }
                // The zone caches its path lazily; evaluate the spline itself until it has.
                var spline = zone.splineContainer;
                if (spline == null) continue;
                zones.Add(WithBounds(zone, new Zone
                {
                    Area = id, Start = ToVector(spline.EvaluatePosition(0f)), StartAhead = ToVector(spline.EvaluatePosition(0.02f)),
                    End = ToVector(spline.EvaluatePosition(1f)), EndBehind = ToVector(spline.EvaluatePosition(0.98f)),
                }));
            }
            return zones;
        }

        // The volume of the zone's trigger, to tell which area an NPC stands in.
        private static Zone WithBounds(ChangeLevelProgressBase zone, Zone z)
        {
            var detector = zone.triggerDetectorComponent;
            if (detector == null) return z;
            foreach (var col in detector.GetComponentsInChildren<Collider>(true))
            {
                if (!z.HasBounds) z.Bounds = col.bounds;
                else z.Bounds.Encapsulate(col.bounds);
                z.HasBounds = true;
            }
            return z;
        }

        private static int ZoneArea(ChangeLevelProgressBase zone)
        {
            var info = zone.levelInfoHolder?.LevelInfo;
            return info != null ? info.Id : -1;
        }

        private static TeleportTarget[] FindNpcs(List<Zone> zones, Dictionary<int, (Vector3 pos, Quaternion rot)> doors, bool story)
        {
            var npcs = new List<TeleportTarget>();
            foreach (var npc in Object.FindObjectsOfType<InteractableNpc>(true))
            {
                var t = npc.transform;
                string name = CleanNpcName(npc.gameObject.name);
                if (HiddenNpcs.Any(h => string.Equals(h, name, StringComparison.OrdinalIgnoreCase))) continue;

                Vector3 front = t.forward;
                front.y = 0;
                if (front.sqrMagnitude < 0.01f) front = Vector3.forward;
                front.Normalize();
                Vector3 pos = OnFloor(t.position + front * NpcDistance);
                // Facing the NPC, ready to talk.
                Quaternion rot = Quaternion.LookRotation(-front);
                float? cameraYaw = null;
                // Inside a boss area: in front of its door, like the boss itself.
                foreach (var z in zones)
                {
                    if (!z.HasBounds || !z.Bounds.Contains(t.position) || !doors.TryGetValue(z.Area, out var door)) continue;
                    (pos, rot) = door;
                    break;
                }
                if (story && FixedTargets.TryGetValue("npc:" + name, out var fixedTarget))
                {
                    (pos, rot, float yaw) = fixedTarget;
                    cameraYaw = yaw;
                }
                npcs.Add(new TeleportTarget(name, pos, rot, cameraYaw ?? rot.eulerAngles.y));
            }
            // Bottom to top, the order they are met in.
            return npcs.OrderBy(n => n.Position.y).ToArray();
        }

        // The floor under (or just above) a point, with the phone resting on it; the point
        // itself when there is none within reach. Also puts a ghost mode start set while
        // flying on the floor.
        public static Vector3 OnFloor(Vector3 point)
        {
            return Physics.Raycast(point + Vector3.up * SearchAbove, Vector3.down, out RaycastHit hit,
                       SearchAbove + SearchDepth, GroundMask, QueryTriggerInteraction.Ignore)
                ? hit.point + Vector3.up * RestHeight
                : point;
        }

        // The first floor under a roof: the search goes on from just below the first surface
        // it hits. Started inside the roof, it can't hit the roof's underside (colliders are
        // only hit from outside), so the next hit is the floor beneath.
        private static Vector3 UnderRoof(Vector3 point)
        {
            if (!Physics.Raycast(point + Vector3.up * SearchAbove, Vector3.down, out RaycastHit roof,
                    SearchAbove + SearchDepth, GroundMask, QueryTriggerInteraction.Ignore))
                return point;
            if (Physics.Raycast(roof.point + Vector3.down * 0.05f, Vector3.down, out RaycastHit floor,
                    RoofSearchDepth, GroundMask, QueryTriggerInteraction.Ignore))
                return floor.point + Vector3.up * RestHeight;
            return roof.point + Vector3.up * RestHeight;
        }

        private static Quaternion Facing(Vector3 from, Vector3 to)
        {
            Vector3 dir = to - from;
            dir.y = 0;
            return dir.sqrMagnitude > 0.01f ? Quaternion.LookRotation(dir) : Quaternion.identity;
        }

        private static Vector3 ToVector(Unity.Mathematics.float3 v) => new Vector3(v.x, v.y, v.z);

        // "LevelInfo_L5_ToyChest" -> "L5 ToyChest".
        private static string CleanAreaName(string assetName)
        {
            string name = assetName ?? "";
            if (name.StartsWith("LevelInfo_")) name = name.Substring("LevelInfo_".Length);
            return name.Replace('_', ' ');
        }

        // "InteractableNPC_MsElephant(Clone)" -> "MsElephant".
        private static string CleanNpcName(string goName)
        {
            string name = (goName ?? "").Replace("(Clone)", "").Trim();
            const string prefix = "InteractableNPC";
            if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) name = name.Substring(prefix.Length);
            return name.Trim('_', ' ', '-').Replace('_', ' ');
        }
    }
}
