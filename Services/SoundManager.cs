using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Media;
using System.Text;
using RadialSek.Models;

namespace RadialSek.Services
{
    public enum SoundCue
    {
        UiClick,
        UiSelect,
        UiHover,
        UiTabSwitch,
        UiToggleOn,
        UiToggleOff,
        UiWindowOpen,
        UiWindowClose,
        MenuOpen,
        MenuClose,
        MenuLaunch,
        MenuDrop,
        Success,
        Warning,
        Error,
        Notification,
        ShortcutCaptured
    }

    public sealed class SoundManager
    {
        private enum SoundBus
        {
            Ui,
            Hover,
            Notification
        }

        private enum WaveShape
        {
            Sine,
            SoftPulse,
            SoftBell,
            NoiseTap
        }

        private readonly struct SoundSegment
        {
            public SoundSegment(
                double durationMs,
                double startFrequency,
                double endFrequency,
                double gain,
                WaveShape shape,
                double attackMs,
                double releaseMs)
            {
                DurationMs = durationMs;
                StartFrequency = startFrequency;
                EndFrequency = endFrequency;
                Gain = gain;
                Shape = shape;
                AttackMs = attackMs;
                ReleaseMs = releaseMs;
            }

            public double DurationMs { get; }
            public double StartFrequency { get; }
            public double EndFrequency { get; }
            public double Gain { get; }
            public WaveShape Shape { get; }
            public double AttackMs { get; }
            public double ReleaseMs { get; }
        }

        private sealed class SoundDefinition
        {
            public SoundDefinition(SoundBus bus, double gain, TimeSpan cooldown, params SoundSegment[] segments)
            {
                Bus = bus;
                Gain = gain;
                Cooldown = cooldown;
                Segments = segments;
            }

            public SoundBus Bus { get; }
            public double Gain { get; }
            public TimeSpan Cooldown { get; }
            public IReadOnlyList<SoundSegment> Segments { get; }
        }

        private sealed class PreparedClip
        {
            public PreparedClip(MemoryStream stream, SoundPlayer player)
            {
                Stream = stream;
                Player = player;
            }

            public MemoryStream Stream { get; }
            public SoundPlayer Player { get; }
            public object SyncRoot { get; } = new object();
        }

        private const int SampleRate = 44100;
        private const int VolumeBucketSteps = 20;
        private static readonly TimeSpan GlobalMinInterval = TimeSpan.FromMilliseconds(16);
        private const string HoverSoundFileName = "radialsek_menu_hover_sound.wav";
        private readonly object _stateLock = new object();
        private readonly Dictionary<SoundCue, SoundDefinition> _definitions = CreateDefinitions();
        private readonly Dictionary<SoundCue, DateTime> _lastPlayedAtUtc = new Dictionary<SoundCue, DateTime>();
        private readonly ConcurrentDictionary<(SoundCue Cue, int Bucket), PreparedClip> _preparedClipCache = new ConcurrentDictionary<(SoundCue Cue, int Bucket), PreparedClip>();
        private readonly ConcurrentDictionary<(string Path, int Bucket), PreparedClip> _preparedHoverClipCache = new ConcurrentDictionary<(string Path, int Bucket), PreparedClip>();
        private AudioSettings _settings = new AudioSettings();
        private DateTime _lastGlobalPlayAtUtc = DateTime.MinValue;
        private string? _hoverSoundPath;
        private bool _hoverSoundInitialized;

        private SoundManager()
        {
        }

        public static SoundManager Instance { get; } = new SoundManager();

        public void ApplyConfig(MenuConfig? config)
        {
            ApplySettings(config?.Audio);
        }

        public void ApplySettings(AudioSettings? settings)
        {
            lock (_stateLock)
            {
                _settings = NormalizeSettings(settings);
            }
        }

        public AudioSettings GetSettingsSnapshot()
        {
            lock (_stateLock)
            {
                return _settings.Clone();
            }
        }

        public void Play(SoundCue cue)
        {
            if (!_definitions.TryGetValue(cue, out var definition))
            {
                return;
            }

            AudioSettings snapshot;
            lock (_stateLock)
            {
                snapshot = _settings;
            }

            if (!snapshot.EnableSounds || snapshot.SilentMode)
            {
                return;
            }

            var busVolume = definition.Bus switch
            {
                SoundBus.Notification => snapshot.NotificationVolume,
                SoundBus.Hover => snapshot.HoverVolume,
                _ => snapshot.UiVolume
            };
            var gain = ClampUnit(snapshot.MasterVolume * busVolume * definition.Gain, 0.0);
            if (gain <= 0.001)
            {
                return;
            }

            var nowUtc = DateTime.UtcNow;
            lock (_stateLock)
            {
                if (_lastPlayedAtUtc.TryGetValue(cue, out var lastAtUtc) &&
                    nowUtc - lastAtUtc < definition.Cooldown)
                {
                    return;
                }

                if (nowUtc - _lastGlobalPlayAtUtc < GlobalMinInterval)
                {
                    return;
                }

                _lastPlayedAtUtc[cue] = nowUtc;
                _lastGlobalPlayAtUtc = nowUtc;
            }

            var bucket = Math.Clamp((int)Math.Round(gain * VolumeBucketSteps), 0, VolumeBucketSteps);
            PreparedClip? clip;
            if (cue == SoundCue.UiHover)
            {
                clip = GetPreparedHoverClip(bucket);
            }
            else
            {
                clip = _preparedClipCache.GetOrAdd((cue, bucket), key => CreatePreparedClip(key.Cue, _definitions[key.Cue], key.Bucket));
            }

            if (clip == null)
            {
                return;
            }

            try
            {
                lock (clip.SyncRoot)
                {
                    clip.Stream.Position = 0;
                    clip.Player.Play();
                }
            }
            catch
            {
            }
        }

        private static PreparedClip CreatePreparedClip(SoundCue cue, SoundDefinition definition, int bucket)
        {
            var normalizedBucketGain = bucket / (double)VolumeBucketSteps;
            var waveBytes = SynthesizeWaveBytes(definition, normalizedBucketGain);
            if (cue == SoundCue.MenuDrop)
            {
                ApplyWaveTailFadeOut(waveBytes);
            }

            var stream = new MemoryStream(waveBytes, writable: false);
            var player = new SoundPlayer(stream);

            try
            {
                player.Load();
            }
            catch
            {
            }

            return new PreparedClip(stream, player);
        }

        private PreparedClip? GetPreparedHoverClip(int bucket)
        {
            EnsureHoverSoundInitialized();
            string? hoverSoundPath;
            lock (_stateLock)
            {
                hoverSoundPath = _hoverSoundPath;
            }

            if (string.IsNullOrWhiteSpace(hoverSoundPath))
            {
                return null;
            }

            var key = (Path: hoverSoundPath, Bucket: bucket);
            if (_preparedHoverClipCache.TryGetValue(key, out var cachedClip))
            {
                return cachedClip;
            }

            var created = CreatePreparedClipFromWaveFile(hoverSoundPath, bucket);
            if (created == null)
            {
                return null;
            }

            return _preparedHoverClipCache.GetOrAdd(key, created);
        }

        private void EnsureHoverSoundInitialized()
        {
            if (_hoverSoundInitialized)
            {
                return;
            }

            lock (_stateLock)
            {
                if (_hoverSoundInitialized)
                {
                    return;
                }

                _hoverSoundPath = ResolveHoverSoundPath();
                _hoverSoundInitialized = true;
            }
        }

        private static PreparedClip? CreatePreparedClipFromWaveFile(string path, int bucket)
        {
            try
            {
                if (!File.Exists(path))
                {
                    return null;
                }

                var sourceBytes = File.ReadAllBytes(path);
                if (sourceBytes.Length == 0)
                {
                    return null;
                }

                var normalizedGain = bucket / (double)VolumeBucketSteps;
                var waveBytes = normalizedGain >= 0.999
                    ? sourceBytes
                    : ScaleWaveBytes(sourceBytes, normalizedGain);
                ApplyWaveTailFadeOut(waveBytes);
                var stream = new MemoryStream(waveBytes, writable: false);
                var player = new SoundPlayer(stream);

                player.Load();
                return new PreparedClip(stream, player);
            }
            catch
            {
                return null;
            }
        }

        private static byte[] ScaleWaveBytes(byte[] sourceBytes, double gain)
        {
            if (gain <= 0.001)
            {
                gain = 0.0;
            }

            if (!TryGetWaveDataRegion(sourceBytes, out var audioFormat, out var bitsPerSample, out var dataOffset, out var dataLength))
            {
                return sourceBytes;
            }

            if (audioFormat != 1)
            {
                return sourceBytes;
            }

            var targetBytes = new byte[sourceBytes.Length];
            Buffer.BlockCopy(sourceBytes, 0, targetBytes, 0, sourceBytes.Length);

            if (bitsPerSample == 16)
            {
                var usableLength = dataLength - (dataLength % 2);
                for (var index = 0; index < usableLength; index += 2)
                {
                    var byteIndex = dataOffset + index;
                    var sample = (short)(targetBytes[byteIndex] | (targetBytes[byteIndex + 1] << 8));
                    var scaled = (int)Math.Round(sample * gain);
                    scaled = Math.Max(short.MinValue, Math.Min(short.MaxValue, scaled));
                    targetBytes[byteIndex] = (byte)(scaled & 0xFF);
                    targetBytes[byteIndex + 1] = (byte)((scaled >> 8) & 0xFF);
                }

                return targetBytes;
            }

            if (bitsPerSample == 8)
            {
                for (var index = 0; index < dataLength; index++)
                {
                    var byteIndex = dataOffset + index;
                    var centered = targetBytes[byteIndex] - 128.0;
                    var scaled = (int)Math.Round((centered * gain) + 128.0);
                    targetBytes[byteIndex] = (byte)Math.Max(0, Math.Min(255, scaled));
                }

                return targetBytes;
            }

            return sourceBytes;
        }

        private static void ApplyWaveTailFadeOut(byte[] waveBytes)
        {
            if (!TryGetWaveDataRegion(waveBytes, out var audioFormat, out var bitsPerSample, out var dataOffset, out var dataLength))
            {
                return;
            }

            if (audioFormat != 1 || dataLength <= 0)
            {
                return;
            }

            if (bitsPerSample == 16)
            {
                var usableLength = dataLength - (dataLength % 2);
                var sampleCount = usableLength / 2;
                var fadeSamples = Math.Min(sampleCount, 192);
                if (fadeSamples <= 0)
                {
                    return;
                }

                var fadeStart = sampleCount - fadeSamples;
                for (var sampleIndex = fadeStart; sampleIndex < sampleCount; sampleIndex++)
                {
                    var byteIndex = dataOffset + (sampleIndex * 2);
                    var sample = (short)(waveBytes[byteIndex] | (waveBytes[byteIndex + 1] << 8));
                    var gain = (sampleCount - sampleIndex) / (double)fadeSamples;
                    var scaled = (int)Math.Round(sample * gain);
                    scaled = Math.Max(short.MinValue, Math.Min(short.MaxValue, scaled));
                    waveBytes[byteIndex] = (byte)(scaled & 0xFF);
                    waveBytes[byteIndex + 1] = (byte)((scaled >> 8) & 0xFF);
                }

                return;
            }

            if (bitsPerSample == 8)
            {
                var fadeSamples = Math.Min(dataLength, 192);
                if (fadeSamples <= 0)
                {
                    return;
                }

                var fadeStart = dataLength - fadeSamples;
                for (var sampleIndex = fadeStart; sampleIndex < dataLength; sampleIndex++)
                {
                    var byteIndex = dataOffset + sampleIndex;
                    var centered = waveBytes[byteIndex] - 128.0;
                    var gain = (dataLength - sampleIndex) / (double)fadeSamples;
                    var scaled = (int)Math.Round((centered * gain) + 128.0);
                    waveBytes[byteIndex] = (byte)Math.Max(0, Math.Min(255, scaled));
                }
            }
        }

        private static bool TryGetWaveDataRegion(
            byte[] waveBytes,
            out ushort audioFormat,
            out ushort bitsPerSample,
            out int dataOffset,
            out int dataLength)
        {
            audioFormat = 0;
            bitsPerSample = 0;
            dataOffset = 0;
            dataLength = 0;

            if (waveBytes.Length < 12)
            {
                return false;
            }

            if (!IsChunkId(waveBytes, 0, "RIFF") || !IsChunkId(waveBytes, 8, "WAVE"))
            {
                return false;
            }

            var chunkOffset = 12;
            var hasFormatChunk = false;
            var hasDataChunk = false;
            while (chunkOffset + 8 <= waveBytes.Length)
            {
                var chunkSize = BitConverter.ToInt32(waveBytes, chunkOffset + 4);
                if (chunkSize < 0)
                {
                    break;
                }

                var chunkDataOffset = chunkOffset + 8;
                if (chunkDataOffset > waveBytes.Length)
                {
                    break;
                }

                var chunkDataEnd = chunkDataOffset + chunkSize;
                if (chunkDataEnd > waveBytes.Length)
                {
                    break;
                }

                if (IsChunkId(waveBytes, chunkOffset, "fmt "))
                {
                    if (chunkSize >= 16)
                    {
                        audioFormat = BitConverter.ToUInt16(waveBytes, chunkDataOffset + 0);
                        bitsPerSample = BitConverter.ToUInt16(waveBytes, chunkDataOffset + 14);
                        hasFormatChunk = true;
                    }
                }
                else if (IsChunkId(waveBytes, chunkOffset, "data"))
                {
                    dataOffset = chunkDataOffset;
                    dataLength = chunkSize;
                    hasDataChunk = true;
                }

                chunkOffset = chunkDataEnd + (chunkSize % 2);
            }

            return hasFormatChunk &&
                   hasDataChunk &&
                   dataLength > 0 &&
                   dataOffset >= 0 &&
                   dataOffset + dataLength <= waveBytes.Length;
        }

        private static bool IsChunkId(byte[] bytes, int offset, string expectedId)
        {
            if (offset < 0 || offset + 4 > bytes.Length)
            {
                return false;
            }

            var actual = Encoding.ASCII.GetString(bytes, offset, 4);
            return string.Equals(actual, expectedId, StringComparison.Ordinal);
        }

        private static string? ResolveHoverSoundPath()
        {
            var candidateFolders = new List<string>();
            TryAddCandidateFolder(candidateFolders, Path.Combine(AppContext.BaseDirectory, "RadialSek_sounds"));
            TryAddCandidateFolder(candidateFolders, Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "RadialSek_sounds"));
            TryAddCandidateFolder(candidateFolders, Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "RadialSek_sounds"));
            TryAddCandidateFolder(candidateFolders, Path.Combine(Environment.CurrentDirectory, "RadialSek_sounds"));

            foreach (var folder in candidateFolders)
            {
                if (!Directory.Exists(folder))
                {
                    continue;
                }

                var candidate = Path.Combine(folder, HoverSoundFileName);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }

        private static void TryAddCandidateFolder(ICollection<string> folders, string path)
        {
            try
            {
                var normalized = Path.GetFullPath(path);
                foreach (var existing in folders)
                {
                    if (string.Equals(existing, normalized, StringComparison.OrdinalIgnoreCase))
                    {
                        return;
                    }
                }

                folders.Add(normalized);
            }
            catch
            {
            }
        }

        private static byte[] SynthesizeWaveBytes(SoundDefinition definition, double normalizedGain)
        {
            var totalSamples = 0;
            foreach (var segment in definition.Segments)
            {
                totalSamples += GetSegmentSampleCount(segment.DurationMs);
            }

            if (totalSamples <= 0)
            {
                totalSamples = 1;
            }

            var pcmData = new short[totalSamples];
            var sampleCursor = 0;
            var absoluteSampleIndex = 0;
            foreach (var segment in definition.Segments)
            {
                var sampleCount = GetSegmentSampleCount(segment.DurationMs);
                if (sampleCount <= 0)
                {
                    continue;
                }

                var attackSamples = Math.Max(0, (int)Math.Round(segment.AttackMs * SampleRate / 1000.0));
                var releaseSamples = Math.Max(0, (int)Math.Round(segment.ReleaseMs * SampleRate / 1000.0));
                var phase = 0.0;
                for (var i = 0; i < sampleCount; i++)
                {
                    var t = sampleCount > 1
                        ? i / (double)(sampleCount - 1)
                        : 0.0;
                    var frequency = segment.StartFrequency + ((segment.EndFrequency - segment.StartFrequency) * t);
                    phase += 2.0 * Math.PI * frequency / SampleRate;
                    var waveformValue = SampleWave(segment.Shape, phase, absoluteSampleIndex);
                    var envelope = BuildEnvelope(i, sampleCount, attackSamples, releaseSamples);
                    var sample = waveformValue * envelope * segment.Gain * normalizedGain;
                    sample = Math.Max(-1.0, Math.Min(1.0, sample));
                    pcmData[sampleCursor++] = (short)Math.Round(sample * short.MaxValue);
                    absoluteSampleIndex++;
                }
            }

            return EncodeWavePcm16(pcmData);
        }

        private static int GetSegmentSampleCount(double durationMs)
        {
            if (durationMs <= 0)
            {
                return 0;
            }

            return Math.Max(1, (int)Math.Round(durationMs * SampleRate / 1000.0));
        }

        private static double BuildEnvelope(int sampleIndex, int sampleCount, int attackSamples, int releaseSamples)
        {
            var envelope = 1.0;

            if (attackSamples > 0 && sampleIndex < attackSamples)
            {
                envelope *= sampleIndex / (double)attackSamples;
            }

            if (releaseSamples > 0 && sampleIndex >= sampleCount - releaseSamples)
            {
                var tailSamples = sampleCount - sampleIndex;
                envelope *= Math.Max(0.0, Math.Min(1.0, tailSamples / (double)releaseSamples));
            }

            return envelope;
        }

        private static double SampleWave(WaveShape shape, double phase, int sampleIndex)
        {
            var fundamental = Math.Sin(phase);
            return shape switch
            {
                WaveShape.SoftPulse => (fundamental * 0.74) +
                                       (Math.Sin(phase * 2.0) * 0.20) +
                                       (Math.Sin(phase * 3.0) * 0.06),
                WaveShape.SoftBell => (fundamental * 0.66) +
                                      (Math.Sin(phase * 2.0) * 0.22) +
                                      (Math.Sin(phase * 3.0) * 0.12),
                WaveShape.NoiseTap => (fundamental * 0.38) +
                                      (PseudoNoise(sampleIndex) * 0.62),
                _ => fundamental
            };
        }

        private static double PseudoNoise(int sampleIndex)
        {
            var x = unchecked((uint)(sampleIndex * 747796405u + 2891336453u));
            x = unchecked(((x >> (int)((x >> 28) + 4)) ^ x) * 277803737u);
            x ^= x >> 22;
            return (x / (double)uint.MaxValue) * 2.0 - 1.0;
        }

        private static byte[] EncodeWavePcm16(short[] samples)
        {
            var dataLength = samples.Length * sizeof(short);
            var wave = new byte[44 + dataLength];
            using var stream = new MemoryStream(wave);
            using var writer = new BinaryWriter(stream);

            writer.Write(new[] { (byte)'R', (byte)'I', (byte)'F', (byte)'F' });
            writer.Write(36 + dataLength);
            writer.Write(new[] { (byte)'W', (byte)'A', (byte)'V', (byte)'E' });
            writer.Write(new[] { (byte)'f', (byte)'m', (byte)'t', (byte)' ' });
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)1);
            writer.Write(SampleRate);
            writer.Write(SampleRate * sizeof(short));
            writer.Write((short)sizeof(short));
            writer.Write((short)16);
            writer.Write(new[] { (byte)'d', (byte)'a', (byte)'t', (byte)'a' });
            writer.Write(dataLength);
            foreach (var sample in samples)
            {
                writer.Write(sample);
            }

            return wave;
        }

        private static AudioSettings NormalizeSettings(AudioSettings? settings)
        {
            var snapshot = settings?.Clone() ?? new AudioSettings();
            snapshot.MasterVolume = ClampUnit(snapshot.MasterVolume, 0.72);
            snapshot.UiVolume = ClampUnit(snapshot.UiVolume, 0.86);
            snapshot.HoverVolume = ClampUnit(snapshot.HoverVolume, 0.78);
            snapshot.NotificationVolume = ClampUnit(snapshot.NotificationVolume, 0.82);
            return snapshot;
        }

        private static double ClampUnit(double value, double fallback)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                return fallback;
            }

            return Math.Max(0.0, Math.Min(1.0, value));
        }

        private static Dictionary<SoundCue, SoundDefinition> CreateDefinitions()
        {
            return new Dictionary<SoundCue, SoundDefinition>
            {
                [SoundCue.UiClick] = new SoundDefinition(
                    SoundBus.Ui,
                    gain: 0.76,
                    cooldown: TimeSpan.FromMilliseconds(46),
                    new SoundSegment(40, 980, 730, 0.60, WaveShape.SoftPulse, 1.2, 24)),

                [SoundCue.UiSelect] = new SoundDefinition(
                    SoundBus.Ui,
                    gain: 0.74,
                    cooldown: TimeSpan.FromMilliseconds(74),
                    new SoundSegment(54, 620, 830, 0.60, WaveShape.SoftBell, 2.0, 32)),

                [SoundCue.UiHover] = new SoundDefinition(
                    SoundBus.Hover,
                    gain: 1.0,
                    cooldown: TimeSpan.FromMilliseconds(120),
                    new SoundSegment(20, 700, 700, 0.0, WaveShape.Sine, 0, 0)),

                [SoundCue.UiTabSwitch] = new SoundDefinition(
                    SoundBus.Ui,
                    gain: 0.75,
                    cooldown: TimeSpan.FromMilliseconds(95),
                    new SoundSegment(36, 520, 660, 0.52, WaveShape.SoftPulse, 1.5, 22),
                    new SoundSegment(44, 740, 940, 0.48, WaveShape.SoftBell, 1.5, 26)),

                [SoundCue.UiToggleOn] = new SoundDefinition(
                    SoundBus.Ui,
                    gain: 0.80,
                    cooldown: TimeSpan.FromMilliseconds(90),
                    new SoundSegment(42, 470, 640, 0.52, WaveShape.SoftPulse, 1.5, 24),
                    new SoundSegment(52, 720, 980, 0.45, WaveShape.SoftBell, 1.8, 30)),

                [SoundCue.UiToggleOff] = new SoundDefinition(
                    SoundBus.Ui,
                    gain: 0.76,
                    cooldown: TimeSpan.FromMilliseconds(90),
                    new SoundSegment(48, 880, 620, 0.54, WaveShape.SoftBell, 1.5, 28),
                    new SoundSegment(46, 540, 360, 0.42, WaveShape.SoftPulse, 1.2, 30)),

                [SoundCue.UiWindowOpen] = new SoundDefinition(
                    SoundBus.Ui,
                    gain: 0.74,
                    cooldown: TimeSpan.FromMilliseconds(180),
                    new SoundSegment(95, 320, 640, 0.56, WaveShape.SoftBell, 2.5, 52)),

                [SoundCue.UiWindowClose] = new SoundDefinition(
                    SoundBus.Ui,
                    gain: 0.72,
                    cooldown: TimeSpan.FromMilliseconds(180),
                    new SoundSegment(84, 680, 320, 0.52, WaveShape.SoftBell, 1.8, 50)),

                [SoundCue.MenuOpen] = new SoundDefinition(
                    SoundBus.Ui,
                    gain: 0.78,
                    cooldown: TimeSpan.FromMilliseconds(120),
                    new SoundSegment(78, 280, 610, 0.60, WaveShape.SoftPulse, 2.0, 42)),

                [SoundCue.MenuClose] = new SoundDefinition(
                    SoundBus.Ui,
                    gain: 0.74,
                    cooldown: TimeSpan.FromMilliseconds(120),
                    new SoundSegment(70, 620, 280, 0.58, WaveShape.SoftPulse, 1.6, 40)),

                [SoundCue.MenuLaunch] = new SoundDefinition(
                    SoundBus.Ui,
                    gain: 0.82,
                    cooldown: TimeSpan.FromMilliseconds(70),
                    new SoundSegment(62, 640, 980, 0.60, WaveShape.SoftBell, 1.4, 34)),

                [SoundCue.MenuDrop] = new SoundDefinition(
                    SoundBus.Ui,
                    gain: 0.77,
                    cooldown: TimeSpan.FromMilliseconds(180),
                    new SoundSegment(54, 360, 240, 0.56, WaveShape.NoiseTap, 1.2, 24),
                    new SoundSegment(40, 620, 540, 0.32, WaveShape.SoftPulse, 1.2, 26)),

                [SoundCue.Success] = new SoundDefinition(
                    SoundBus.Notification,
                    gain: 0.84,
                    cooldown: TimeSpan.FromMilliseconds(160),
                    new SoundSegment(52, 560, 740, 0.56, WaveShape.SoftBell, 1.8, 28),
                    new SoundSegment(64, 780, 1120, 0.52, WaveShape.SoftBell, 2.0, 36)),

                [SoundCue.Warning] = new SoundDefinition(
                    SoundBus.Notification,
                    gain: 0.78,
                    cooldown: TimeSpan.FromMilliseconds(180),
                    new SoundSegment(82, 470, 360, 0.60, WaveShape.SoftPulse, 1.4, 46)),

                [SoundCue.Error] = new SoundDefinition(
                    SoundBus.Notification,
                    gain: 0.82,
                    cooldown: TimeSpan.FromMilliseconds(180),
                    new SoundSegment(52, 430, 320, 0.58, WaveShape.NoiseTap, 1.2, 28),
                    new SoundSegment(58, 320, 220, 0.46, WaveShape.SoftPulse, 1.2, 36)),

                [SoundCue.Notification] = new SoundDefinition(
                    SoundBus.Notification,
                    gain: 0.82,
                    cooldown: TimeSpan.FromMilliseconds(180),
                    new SoundSegment(78, 520, 770, 0.56, WaveShape.SoftBell, 2.0, 42),
                    new SoundSegment(58, 1040, 900, 0.34, WaveShape.SoftBell, 1.2, 38)),

                [SoundCue.ShortcutCaptured] = new SoundDefinition(
                    SoundBus.Ui,
                    gain: 0.76,
                    cooldown: TimeSpan.FromMilliseconds(130),
                    new SoundSegment(48, 760, 1030, 0.56, WaveShape.SoftBell, 1.4, 28))
            };
        }
    }
}
