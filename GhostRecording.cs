using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace FlippingIsHardTrainer
{
    public struct GhostSample
    {
        // Run clock: the playtime when the recording began plus the physics time since, so
        // it advances one fixedDeltaTime per sample.
        public double Time;
        public Vector3 Position;
        public Quaternion Rotation;
        // The phone jumped here (teleport, checkpoint, respawn): playback must not
        // interpolate from the previous sample to this one.
        public bool Cut;
    }

    // Copied from FIH Ghost's GhostRecording.cs (https://github.com/d1ll3x3/FIH_Ghost) and
    // kept on its file format, so ghosts move between both mods with Export / Import. The
    // trainer writes no category or markers (those are FIH Ghost's PB splits); it skips
    // them when reading that mod's files.
    //
    // File (.fihghost, little endian): "FIHG" version:i32, then mapId:str difficulty:str
    // startPlaytime:f64 recordedAtUtcTicks:i64 checkpointsUsed:i32, since v2
    // category:u8 categoryId:str finalTime:f64 worldGuid:str,
    // samples:i32 × (time:f64 pos:f32×3 rot:f32×4 cut:u8),
    // markers:i32 × (time:f64 kind:u8 id:str value:f64). str = BinaryWriter string.
    public sealed class GhostRecording
    {
        public const string Extension = ".fihghost";
        private const int FormatVersion = 2;
        private static readonly byte[] Magic = Encoding.ASCII.GetBytes("FIHG");
        // Bytes per sample as Save writes it: time f64, pos 3×f32, rot 4×f32, cut u8.
        private const int SampleSize = 8 + 3 * 4 + 4 * 4 + 1;
        // FIH Ghost's category for a recording that isn't one of its PB splits.
        private const byte FreeCategory = 0;

        public string MapId = "";
        public string Difficulty = "";
        public double StartPlaytime;
        public DateTime RecordedAtUtc;
        public int CheckpointsUsed;
        // Ghost mode attempts: the time from the first input to the end zone. 0 otherwise.
        public double FinalTime;
        public string WorldGuid = "";
        public readonly List<GhostSample> Samples = new List<GhostSample>();

        public double StartTime => Samples.Count == 0 ? 0 : Samples[0].Time;
        public double EndTime => Samples.Count == 0 ? 0 : Samples[Samples.Count - 1].Time;
        public double Duration => EndTime - StartTime;

        public void Save(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            // Written next to the target and swapped in, so a crash mid-write never leaves a
            // truncated PB behind.
            string temp = path + ".tmp";
            using (var writer = new BinaryWriter(File.Create(temp)))
            {
                writer.Write(Magic);
                writer.Write(FormatVersion);
                writer.Write(MapId ?? "");
                writer.Write(Difficulty ?? "");
                writer.Write(StartPlaytime);
                writer.Write(RecordedAtUtc.Ticks);
                writer.Write(CheckpointsUsed);
                writer.Write(FreeCategory);
                writer.Write("");   // category id
                writer.Write(FinalTime);
                writer.Write(WorldGuid ?? "");

                writer.Write(Samples.Count);
                foreach (var s in Samples)
                {
                    writer.Write(s.Time);
                    writer.Write(s.Position.x);
                    writer.Write(s.Position.y);
                    writer.Write(s.Position.z);
                    writer.Write(s.Rotation.x);
                    writer.Write(s.Rotation.y);
                    writer.Write(s.Rotation.z);
                    writer.Write(s.Rotation.w);
                    writer.Write(s.Cut);
                }

                writer.Write(0);   // markers
            }
            File.Move(temp, path, true);
        }

        /// <summary>Reads a .fihghost file; throws InvalidDataException if it is not one.</summary>
        public static GhostRecording Load(string path)
        {
            using var reader = new BinaryReader(File.OpenRead(path));
            var rec = ReadHeader(reader, path);

            int samples = reader.ReadInt32();
            rec.Samples.Capacity = samples;
            for (int i = 0; i < samples; i++)
            {
                rec.Samples.Add(new GhostSample
                {
                    Time = reader.ReadDouble(),
                    Position = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()),
                    Rotation = new Quaternion(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()),
                    Cut = reader.ReadBoolean(),
                });
            }
            // The markers that follow are FIH Ghost's split events; playback doesn't use them.
            return rec;
        }

        /// <summary>
        /// The header and duration only, for listing files without reading every sample:
        /// samples have a fixed size, so the first and last times are a seek away. The
        /// returned recording has no samples.
        /// </summary>
        public static GhostRecording LoadSummary(string path, out double duration)
        {
            using var reader = new BinaryReader(File.OpenRead(path));
            var rec = ReadHeader(reader, path);
            int samples = reader.ReadInt32();
            duration = 0;
            if (samples > 1)
            {
                long first = reader.BaseStream.Position;
                double start = reader.ReadDouble();
                reader.BaseStream.Seek(first + (long)(samples - 1) * SampleSize, SeekOrigin.Begin);
                duration = reader.ReadDouble() - start;
            }
            return rec;
        }

        private static GhostRecording ReadHeader(BinaryReader reader, string path)
        {
            byte[] magic = reader.ReadBytes(Magic.Length);
            if (magic.Length != Magic.Length || Encoding.ASCII.GetString(magic) != "FIHG")
                throw new InvalidDataException($"'{Path.GetFileName(path)}' is not a ghost file");
            int version = reader.ReadInt32();
            if (version < 1 || version > FormatVersion)
                throw new InvalidDataException($"'{Path.GetFileName(path)}' has format v{version}, this trainer reads up to v{FormatVersion}");

            var rec = new GhostRecording
            {
                MapId = reader.ReadString(),
                Difficulty = reader.ReadString(),
                StartPlaytime = reader.ReadDouble(),
                RecordedAtUtc = new DateTime(reader.ReadInt64(), DateTimeKind.Utc),
                CheckpointsUsed = reader.ReadInt32(),
            };
            // v1 files are FIH Ghost's hand-made recordings from before categories existed.
            if (version >= 2)
            {
                reader.ReadByte();     // category
                reader.ReadString();   // category id
                rec.FinalTime = reader.ReadDouble();
                rec.WorldGuid = reader.ReadString();
            }
            return rec;
        }
    }
}
