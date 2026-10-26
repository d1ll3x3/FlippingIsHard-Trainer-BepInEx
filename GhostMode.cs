using System;
using System.Collections.Concurrent;
using System.IO;
using EHS;
using UnityEngine;

namespace FlippingIsHardTrainer
{
    // Ghost mode, adapted from FIH Ghost's practice mode and manual recordings
    // (https://github.com/d1ll3x3/FIH_Ghost).
    //
    // Each map has a start and an end zone, set with their binds. While ghost mode
    // is on, the Teleport bind restarts at the start instead of the saved position: the
    // attempt is recorded from the first input (as the game's own timer does) until the
    // phone enters the end zone, saved, and kept as the map's PB when it is the best one.
    // The chosen ghost (the PB unless another one is picked in the tab) runs next to you from
    // that same moment. Besides that: a hand-made recording, watching a ghost next to the
    // phone or with the camera following it, and export / import of .fihghost files.
    public sealed class GhostMode
    {
        public enum AttemptState { None, Armed, Running }

        // How long a message stays on the HUD (unscaled seconds).
        public const float MessageSeconds = 6f;
        private static readonly Color PlayColor = new Color(0.9f, 0.9f, 0.95f, 0.3f);
        private static readonly Color RaceColor = new Color(0.8f, 0.55f, 1f, 0.35f);
        private static readonly Color StartColor = new Color(0.3f, 1f, 0.45f, 1f);
        private static readonly Color EndColor = new Color(1f, 0.8f, 0.25f, 1f);
        private const float StartMarkerHeight = 3f;

        // Bits of EHS.PlayerButtons.ButtonType that move the phone (FIH Ghost's GameAccess):
        // jump, and secondary (auto-align on the ground, spin boost in the air).
        private const int JumpBit = 4;
        private const int SecondaryBit = 65536;

        private readonly Func<bool> _isFlying;
        // Shared by the hand-made recording and the attempts: only one of them runs at a time.
        private readonly GhostRecorder _recorder = new GhostRecorder();
        private readonly GhostPlayback _play = new GhostPlayback(PlayColor);
        private readonly GhostPlayback _race = new GhostPlayback(RaceColor);
        private readonly LineBox _startMarker = new LineBox("TrainerGhost_Start");
        private readonly LineBox _endMarker = new LineBox("TrainerGhost_End");
        // File dialogs answer on their own thread; the answer is run here, on the main one.
        private readonly ConcurrentQueue<Action> _mainThread = new ConcurrentQueue<Action>();

        // The map _course belongs to; null until a level is loaded.
        private string _mapId;
        private GhostCourse _course = new GhostCourse();
        // Every input released since the restart: the key that restarted must not also start
        // the timer.
        private bool _released;
        // The phone is frozen while the camera follows a ghost; these put it back.
        private IntPtr _spectatePlayer;
        private RigidbodyConstraints _savedConstraints;

        public readonly GhostLibrary Library = new GhostLibrary();

        public GhostMode(Func<bool> isFlying)
        {
            _isFlying = isFlying;
            Library.Refresh();
        }

        public bool Active { get; private set; }
        public AttemptState Attempt { get; private set; }
        public bool IsRecording => _recorder.IsRecording && Attempt != AttemptState.Running;
        public bool IsPlaying => _play.IsPlaying;
        public bool Spectating { get; private set; }
        public GhostCourse Course => _course;
        public string PlayingName { get; private set; }
        // The ghost picked in the Ghost tab; null leaves the default (TargetEntry).
        public string SelectedPath { get; set; }
        // The file the last import created, for the Ghost tab to select it.
        public string LastImportedPath { get; set; }
        // Set by the menu while its Ghost tab is open, so the start and end show there too.
        public bool TabOpen { get; set; }
        public string Message { get; private set; } = "";
        public float MessageTime { get; private set; } = float.NegativeInfinity;
        public bool HasRecentMessage => Time.unscaledTime - MessageTime < MessageSeconds;

        /// <summary>Call every frame.</summary>
        public void Update()
        {
            while (_mainThread.TryDequeue(out var action)) action();
            var player = Player();
            if (player is null)
            {
                OnPlayerLost();
                return;
            }
            SyncMap(player);
            DrawMarkers();
        }

        /// <summary>Call every FixedUpdate.</summary>
        public void FixedUpdate()
        {
            if (!_recorder.IsRecording && Attempt != AttemptState.Armed) return;
            // Without a player, the next Update ends what was running.
            var player = Player();
            if (player is null) return;
            if (Attempt == AttemptState.Armed)
            {
                bool input = HasMoveInput(player);
                if (!_released) _released = !input;
                else if (input) BeginTiming(player);
            }
            if (!_recorder.IsRecording) return;
            _recorder.Step(player);
            if (Attempt == AttemptState.Running && _course.HasEnd && _course.EndBounds.Contains(player.Rb.position))
                Finish(player);
        }

        /// <summary>Call every LateUpdate, after the game moved the player.</summary>
        public void LateUpdate()
        {
            _race.Update();
            _play.Update();
            if (!_play.IsPlaying)
            {
                PlayingName = null;
                if (Spectating) StopSpectating();
            }
        }

        /// <summary>Called when the game is closing: keeps a hand-made recording.</summary>
        public void Shutdown()
        {
            if (IsRecording) StopRecording(Player());
        }

        // --- ghost mode and attempts ----------------------------------------------------

        public void ToggleActive()
        {
            Active = !Active;
            if (!Active)
            {
                // Its binds and HUD rows go away with it, so nothing of it keeps running unseen.
                CancelAttempt();
                if (IsRecording) StopRecording(Player());
                if (_play.IsPlaying)
                {
                    _play.Stop();
                    PlayingName = null;
                    if (Spectating) StopSpectating();
                }
                Notify($"Ghost mode off: {BindText(TrainerConfig.Settings.Teleport)} goes to your saved position");
                return;
            }
            Notify(_course.IsReady
                ? $"Ghost mode on: {BindText(TrainerConfig.Settings.Teleport)} restarts at the start"
                : $"Ghost mode on: {CourseHint()}");
        }

        // How to set the start and the end, with the current binds.
        private static string CourseHint()
            => $"{BindText(TrainerConfig.Settings.GhostSetStart)} sets the start, {BindText(TrainerConfig.Settings.GhostSetEnd)} the end";

        /// <summary>
        /// The Teleport bind in ghost mode: back to the start for a new attempt, timed from the
        /// first input. False when there is no course or the phone can't be teleported.
        /// </summary>
        public bool Restart()
        {
            var player = ReadyPlayer();
            if (player is null) return false;
            if (!_course.IsReady)
            {
                Notify($"No start and end yet: {CourseHint()}", true);
                return false;
            }
            if (Spectating) StopPlayback();
            if (IsRecording) StopRecording(player);
            CancelAttempt();
            if (!TeleportTargets.Teleport(new TeleportTarget("ghost start", _course.StartPosition, _course.StartRot, _course.StartCameraYaw)))
            {
                Notify("The player can't be teleported right now", true);
                return false;
            }
            Attempt = AttemptState.Armed;
            _released = false;
            return true;
        }

        /// <summary>Drops the attempt in progress, if any (fly, a teleport, the menu, another map).</summary>
        public void CancelAttempt()
        {
            if (Attempt == AttemptState.Running) _recorder.Stop(null);
            _race.Stop();
            Attempt = AttemptState.None;
        }

        private void BeginTiming(PlayerRef player)
        {
            _recorder.Start(player);
            Attempt = AttemptState.Running;
            var target = TargetEntry();
            var rec = target != null ? LoadOrNotify(target) : null;
            if (rec != null && !_race.Start(rec, player))
                Notify("Could not build the ghost (see the log)", true);
        }

        private void Finish(PlayerRef player)
        {
            var rec = _recorder.Stop(player);
            Attempt = AttemptState.None;
            if (rec == null || rec.Samples.Count < 2) return;
            double time = rec.Duration;
            rec.FinalTime = time;
            double? previous = Library.Pb(_mapId)?.Time;
            bool improved = !previous.HasValue || time < previous.Value;
            string t = FormatTime(time);
            string again = $"{BindText(TrainerConfig.Settings.Teleport)}: again";
            try
            {
                rec.Save(Path.Combine(GhostLibrary.Root, $"attempt_{DateTime.Now:yyyyMMdd_HHmmss}{GhostRecording.Extension}"));
                if (improved) rec.Save(GhostLibrary.PbPath(_mapId));

                if (!improved) Notify($"{t} (PB {FormatTime(previous.Value)})   {again}");
                else if (previous.HasValue) Notify($"New PB! {t} (-{FormatTime(previous.Value - time)})   {again}");
                else Notify($"First PB: {t}   {again}");
            }
            catch (Exception ex)
            {
                TrainerPlugin.Logger.LogError($"[GHOST] Could not save the attempt: {ex}");
                Notify($"Attempt {t}, but it could not be saved: {ex.Message}", true);
            }
            Library.Refresh();
        }

        // --- start and end --------------------------------------------------------------

        /// <summary>The phone's pose and the camera yaw become the start.</summary>
        public void SetStart()
        {
            var player = ReadyPlayer();
            if (player is null) return;
            var rb = player.Rb;
            // Placed while flying, the start would hang in the air: put it on the floor below.
            Vector3 pos = _isFlying() ? TeleportTargets.OnFloor(rb.position) : rb.position;
            _course.SetStart(pos, rb.rotation, TeleportTargets.CameraYaw(player));
            CourseChanged("Start set");
        }

        /// <summary>The end zone is centred on the phone.</summary>
        public void SetEnd()
        {
            var player = ReadyPlayer();
            if (player is null) return;
            _course.SetEnd(player.Rb.position);
            CourseChanged("End set");
        }

        public void ResizeEnd(float delta)
        {
            if (!_course.HasEnd)
            {
                Notify("Set an end first", true);
                return;
            }
            _course.EndSize = Mathf.Clamp(_course.EndSize + delta, GhostCourse.MinEndSize, GhostCourse.MaxEndSize);
            _course.Save(_mapId);
        }

        // A PB timed on the old start or end isn't one on the new course: it stays in the list
        // as a recording, and the next finished attempt is the first PB.
        private void CourseChanged(string what)
        {
            CancelAttempt();
            _course.Save(_mapId);
            string pb = GhostLibrary.PbPath(_mapId);
            if (!File.Exists(pb))
            {
                Notify(what);
                return;
            }
            string kept = Path.Combine(GhostLibrary.Root, $"old_pb_{DateTime.Now:yyyyMMdd_HHmmss}{GhostRecording.Extension}");
            try
            {
                File.Move(pb, kept);
                Notify($"{what}: the previous PB is kept as {Path.GetFileName(kept)}");
            }
            catch (Exception ex)
            {
                TrainerPlugin.Logger.LogError($"[GHOST] Could not move the old PB {pb}: {ex}");
                Notify($"{what}, but the old PB could not be moved: {ex.Message}", true);
            }
            Library.Refresh();
        }

        // --- hand-made recording --------------------------------------------------------

        public void ToggleRecording()
        {
            if (IsRecording)
            {
                StopRecording(Player());
                return;
            }
            var player = ReadyPlayer();
            if (player is null) return;
            CancelAttempt();
            _recorder.Start(player);
            Notify("Recording");
        }

        private void StopRecording(PlayerRef player)
        {
            var rec = _recorder.Stop(player);
            if (rec == null)
            {
                Notify("Recording stopped: nothing was recorded");
                return;
            }
            string name = $"ghost_{DateTime.Now:yyyyMMdd_HHmmss}{GhostRecording.Extension}";
            string path = Path.Combine(GhostLibrary.Root, name);
            try
            {
                rec.Save(path);
                TrainerPlugin.Logger.LogInfo($"[GHOST] Saved {path}");
                Notify($"Saved {name} ({FormatTime(rec.Duration)})");
            }
            catch (Exception ex)
            {
                TrainerPlugin.Logger.LogError($"[GHOST] Could not save {path}: {ex}");
                Notify($"Could not save the ghost: {ex.Message}", true);
            }
            Library.Refresh();
        }

        // --- watching ghosts --------------------------------------------------------------

        /// <summary>
        /// The ghost the binds and the attempts use: the one picked in the Ghost tab, else
        /// this map's PB, else the newest recording.
        /// </summary>
        public GhostLibrary.Entry TargetEntry()
            => Library.Find(SelectedPath) ?? (_mapId != null ? Library.Pb(_mapId) : null) ?? Library.Latest;

        /// <summary>Play bind: stops the ghost being watched, or plays the chosen one.</summary>
        public void TogglePlay()
        {
            if (_play.IsPlaying) StopPlayback();
            else Play(TargetEntry());
        }

        /// <summary>Spectate bind: stops spectating, or spectates the chosen ghost.</summary>
        public void ToggleSpectate()
        {
            if (Spectating) StopPlayback();
            else Spectate(TargetEntry());
        }

        /// <summary>Plays the ghost where it was recorded, from its start, next to the player.</summary>
        public bool Play(GhostLibrary.Entry entry)
        {
            var player = ReadyPlayer();
            if (player is null) return false;
            if (entry == null)
            {
                Notify($"No ghosts yet: record one with {BindText(TrainerConfig.Settings.GhostRecord)}", true);
                return false;
            }
            if (Spectating) StopSpectating();
            var rec = LoadOrNotify(entry);
            if (rec == null) return false;
            if (!_play.Start(rec, player))
            {
                Notify("Could not build the ghost (see the log)", true);
                return false;
            }
            PlayingName = entry.Title;
            Notify($"Playing {entry.Title}");
            return true;
        }

        /// <summary>
        /// Plays the ghost with the game camera following it instead of the phone, which stays
        /// frozen where it is until the ghost ends or is stopped.
        /// </summary>
        public void Spectate(GhostLibrary.Entry entry)
        {
            if (!Play(entry)) return;
            // The phone is frozen, so an attempt can't go on.
            CancelAttempt();
            var player = Player();
            var cam = player.GameRefs?.CameraManager;
            var body = _play.Body;
            if (cam == null || body == null)
            {
                Notify("Spectating is not available here; the ghost plays next to you", true);
                return;
            }
            cam.SetTrackingObject(body);
            cam.SyncPosAfterTeleport(body.position);
            var rb = player.Rb;
            _spectatePlayer = rb.Pointer;
            _savedConstraints = rb.constraints;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            // Frozen by constraints, not made kinematic: the game keeps writing velocities to
            // it every tick, which a kinematic body rejects with a warning each time.
            rb.constraints = RigidbodyConstraints.FreezeAll;
            Spectating = true;
            Notify($"Spectating {entry.Title}");
        }

        public void StopPlayback()
        {
            if (!_play.IsPlaying) return;
            _play.Stop();
            PlayingName = null;
            if (Spectating) StopSpectating();
            Notify("Ghost stopped");
        }

        private void StopSpectating()
        {
            Spectating = false;
            var player = Player();
            // After a scene change the frozen phone and its camera are gone with it.
            if (player is null || player.Rb.Pointer != _spectatePlayer) return;
            var cam = player.GameRefs?.CameraManager;
            if (cam != null && player.CameraTargetT != null)
            {
                cam.SetTrackingObject(player.CameraTargetT);
                cam.SyncPosAfterTeleport(player.Rb.position);
            }
            player.Rb.constraints = _savedConstraints;
        }

        private GhostRecording LoadOrNotify(GhostLibrary.Entry entry)
        {
            try
            {
                return GhostRecording.Load(entry.Path);
            }
            catch (Exception ex)
            {
                TrainerPlugin.Logger.LogError($"[GHOST] Could not load {entry.Path}: {ex}");
                Notify($"Could not load {entry.Name}: {ex.Message}", true);
                return null;
            }
        }

        // --- export / import --------------------------------------------------------------

        public void Export(GhostLibrary.Entry entry)
        {
            NativeFileDialog.SaveGhostFile(ExportName(entry), (path, error) => _mainThread.Enqueue(() =>
            {
                if (error != null)
                {
                    Notify($"Export failed: {error}", true);
                    return;
                }
                if (path == null) return;   // cancelled
                try
                {
                    File.Copy(entry.Path, path, true);
                    TrainerPlugin.Logger.LogInfo($"[GHOST] Exported {entry.Path} to {path}");
                    Notify($"Exported to {path}");
                }
                catch (Exception ex)
                {
                    TrainerPlugin.Logger.LogError($"[GHOST] Could not export to {path}: {ex}");
                    Notify($"Could not export: {ex.Message}", true);
                }
            }));
        }

        public void Import()
        {
            NativeFileDialog.OpenGhostFile((path, error) => _mainThread.Enqueue(() =>
            {
                if (error != null)
                {
                    Notify($"Import failed: {error}", true);
                    return;
                }
                if (path == null) return;   // cancelled
                try
                {
                    // Read before copying: a file that isn't a ghost never enters the library.
                    GhostRecording.LoadSummary(path, out _);
                    string dir = GhostLibrary.ImportedFolder;
                    Directory.CreateDirectory(dir);
                    string stem = Path.GetFileNameWithoutExtension(path);
                    string dest = Path.Combine(dir, stem + GhostRecording.Extension);
                    for (int n = 2; File.Exists(dest); n++)
                        dest = Path.Combine(dir, $"{stem} ({n}){GhostRecording.Extension}");
                    File.Copy(path, dest);
                    Library.Refresh();
                    LastImportedPath = dest;
                    TrainerPlugin.Logger.LogInfo($"[GHOST] Imported {path} as {dest}");
                    Notify($"Imported {Path.GetFileName(dest)}");
                }
                catch (Exception ex)
                {
                    TrainerPlugin.Logger.LogError($"[GHOST] Could not import {path}: {ex}");
                    Notify($"Could not import: {ex.Message}", true);
                }
            }));
        }

        private static string ExportName(GhostLibrary.Entry entry)
        {
            if (entry.Source != GhostLibrary.Source.Pb) return entry.Name;
            var h = entry.Header;
            string time = FormatTime(entry.Time).Replace(':', '-').Replace('.', '-');
            return GhostLibrary.SafeName($"{h.MapId}_{h.Difficulty}_PB_{time}").Replace(' ', '_') + GhostRecording.Extension;
        }

        // --- HUD text -------------------------------------------------------------------

        /// <summary>
        /// One line for the HUD: the attempt, the last message, the recording or the ghost
        /// being watched; null when there is nothing to say.
        /// </summary>
        public string StatusText()
        {
            if (Attempt == AttemptState.Running) return $"Attempt {FormatTime(_recorder.Seconds)}  ·  {PbText()}";
            if (HasRecentMessage) return Message;
            if (Attempt == AttemptState.Armed) return $"Move to start the timer  ·  {PbText()}";
            if (IsRecording) return $"Recording {FormatTime(_recorder.Seconds)}";
            if (_play.IsPlaying)
                return $"{(Spectating ? "Spectating" : "Ghost")}: {PlayingName}  {FormatTime(_play.Seconds)} / {FormatTime(_play.Duration)}";
            if (Active && !_course.IsReady) return CourseHint();
            return null;
        }

        /// <summary>This map's ghost mode PB, as "PB 0:12.34" or "no PB yet".</summary>
        public string PbText()
        {
            var pb = _mapId != null ? Library.Pb(_mapId) : null;
            return pb != null ? $"PB {FormatTime(pb.Time)}" : "no PB yet";
        }

        public static string FormatTime(double seconds)
        {
            var t = TimeSpan.FromSeconds(Math.Max(0, seconds));
            return t.TotalHours >= 1 ? $"{(int)t.TotalHours}:{t:mm\\:ss\\.ff}" : $"{t:m\\:ss\\.ff}";
        }

        public void Notify(string message, bool warning = false)
        {
            Message = message;
            MessageTime = Time.unscaledTime;
            if (warning) TrainerPlugin.Logger.LogWarning($"[GHOST] {message}");
            else TrainerPlugin.Logger.LogInfo($"[GHOST] {message}");
        }

        // --- helpers --------------------------------------------------------------------

        // The local player once the game finished setting it up, else null.
        private static PlayerRef Player()
        {
            var player = PlayerRef.LocalPlayer;
            return player is not null && player.IsFullyInitialized && player.Rb != null ? player : null;
        }

        private PlayerRef ReadyPlayer()
        {
            var player = Player();
            if (player is null)
            {
                Notify("Load into a level first", true);
                return null;
            }
            SyncMap(player);
            return player;
        }

        // A new map (another save, a custom map) brings its own start and end.
        private void SyncMap(PlayerRef player)
        {
            string map = player.GameRefs?.WorldSaveManager?.GetCurrentProgress()?.MapId ?? "";
            if (map == _mapId) return;
            CancelAttempt();
            _mapId = map;
            _course = GhostCourse.Load(map);
        }

        // The scene went away with the player: the attempt is lost, a hand-made recording is kept.
        private void OnPlayerLost()
        {
            if (IsRecording) StopRecording(null);
            CancelAttempt();
            _startMarker.Hide();
            _endMarker.Hide();
        }

        private void DrawMarkers()
        {
            bool show = Active || TabOpen;
            if (show && _course.HasStart)
                _startMarker.ShowLine(_course.StartPosition, _course.StartPosition + Vector3.up * StartMarkerHeight, StartColor);
            else _startMarker.Hide();
            if (show && _course.HasEnd)
                _endMarker.ShowBox(_course.EndCenter, Vector3.one * _course.EndSize, EndColor);
            else _endMarker.Hide();
        }

        /// <summary>
        /// Whether the player is giving the phone input: stick, jump or secondary, read from
        /// the rawData the game reads its input from. Turning the camera doesn't count.
        /// </summary>
        private static bool HasMoveInput(PlayerRef player)
        {
            var handler = player.InputHandler;
            if (handler == null) return false;
            var data = handler.rawData;
            return data.moveInputSBytes.X != 0 || data.moveInputSBytes.Y != 0
                   || (data.Buttons.HeldMask & (JumpBit | SecondaryBit)) != 0;
        }

        private static string BindText(KeyBind bind) => bind.ToString(InputDeviceTracker.Current, InputDeviceTracker.IsDualShock);
    }
}
