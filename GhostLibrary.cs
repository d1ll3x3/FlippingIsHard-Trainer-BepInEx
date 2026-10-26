using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;

namespace FlippingIsHardTrainer
{
    // Adapted from FIH Ghost's GhostLibrary.cs (https://github.com/d1ll3x3/FIH_Ghost).
    //
    // Every ghost the Ghost tab lists, from the Ghosts folder next to the trainer's dll:
    // the ghost mode PB of each map (Ghosts/PB), the trainer's own recordings (Ghosts) and
    // imported files (Ghosts/Imported).
    public sealed class GhostLibrary
    {
        public enum Source { Pb, Recorded, Imported }

        public sealed class Entry
        {
            public string Path;
            public string Name;
            public Source Source;
            public string Title;
            // Header fields only (GhostRecording.LoadSummary): no samples.
            public GhostRecording Header;
            public double Duration;

            // The attempt time for ghost mode attempts, else the recording's length.
            public double Time => Header.FinalTime > 0 ? Header.FinalTime : Duration;
        }

        public static string Root { get; } = System.IO.Path.Combine(
            System.IO.Path.GetDirectoryName(typeof(TrainerPlugin).Assembly.Location) ?? Paths.ConfigPath, "Ghosts");
        public static string PbFolder => System.IO.Path.Combine(Root, "PB");
        public static string ImportedFolder => System.IO.Path.Combine(Root, "Imported");
        public static string CoursesFolder => System.IO.Path.Combine(Root, "Courses");

        private List<Entry> _entries = new List<Entry>();

        public IReadOnlyList<Entry> Entries => _entries;
        public Entry Latest => _entries.Where(e => e.Source == Source.Recorded).OrderByDescending(e => e.Header.RecordedAtUtc).FirstOrDefault();

        public static string PbPath(string mapId) => System.IO.Path.Combine(PbFolder, SafeName(mapId) + GhostRecording.Extension);

        public Entry Find(string path) => path == null ? null : _entries.FirstOrDefault(e => e.Path == path);

        public Entry Pb(string mapId) => Find(PbPath(mapId));

        /// <summary>Rescans the folders: PBs first, then recordings and imports, newest first.</summary>
        public void Refresh()
        {
            var entries = new List<Entry>();
            Scan(PbFolder, Source.Pb, entries);
            Scan(Root, Source.Recorded, entries);
            Scan(ImportedFolder, Source.Imported, entries);
            _entries = entries
                .OrderBy(e => e.Source)
                .ThenByDescending(e => e.Header.RecordedAtUtc)
                .ToList();
        }

        // A map id as a file name: the story map's id is already one, a custom map's may not be.
        public static string SafeName(string mapId)
        {
            string name = string.IsNullOrEmpty(mapId) ? "unknown" : mapId;
            foreach (char c in System.IO.Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            return name;
        }

        private static void Scan(string dir, Source source, List<Entry> into)
        {
            if (!Directory.Exists(dir)) return;
            foreach (var file in Directory.GetFiles(dir, "*" + GhostRecording.Extension, SearchOption.TopDirectoryOnly))
            {
                try
                {
                    var header = GhostRecording.LoadSummary(file, out double duration);
                    var entry = new Entry
                    {
                        Path = file,
                        Name = System.IO.Path.GetFileName(file),
                        Source = source,
                        Header = header,
                        Duration = duration,
                    };
                    entry.Title = TitleOf(entry);
                    into.Add(entry);
                }
                catch (Exception ex)
                {
                    TrainerPlugin.Logger.LogWarning($"[GHOST] Skipping {file}: {ex.Message}");
                }
            }
        }

        private static string TitleOf(Entry e)
        {
            string stem = System.IO.Path.GetFileNameWithoutExtension(e.Name);
            return e.Source switch
            {
                Source.Pb => "PB · " + e.Header.MapId.Replace("OfficialMap_", ""),
                Source.Imported => "Imported · " + stem,
                _ => stem,
            };
        }
    }
}
