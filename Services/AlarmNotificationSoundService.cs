using System;
using System.IO;
using System.Windows.Media;

namespace RadialSek.Services
{
    public static class AlarmNotificationSoundService
    {
        private static readonly object SyncRoot = new object();
        private static MediaPlayer? _player;

        public static bool TryPlay(string? customSoundPath, double volume)
        {
            var resolvedPath = ResolveSoundPath(customSoundPath);
            if (string.IsNullOrWhiteSpace(resolvedPath))
            {
                return false;
            }

            var normalizedVolume = ClampVolume(volume);
            if (normalizedVolume <= 0.001)
            {
                return false;
            }

            lock (SyncRoot)
            {
                try
                {
                    _player ??= new MediaPlayer();
                    _player.Open(new Uri(resolvedPath, UriKind.Absolute));
                    _player.Volume = normalizedVolume;
                    _player.Position = TimeSpan.Zero;
                    _player.Play();
                    return true;
                }
                catch
                {
                    return false;
                }
            }
        }

        public static void Stop()
        {
            lock (SyncRoot)
            {
                if (_player == null)
                {
                    return;
                }

                try
                {
                    _player.Stop();
                }
                catch
                {
                }
            }
        }

        private static string? ResolveSoundPath(string? inputPath)
        {
            if (string.IsNullOrWhiteSpace(inputPath))
            {
                return null;
            }

            var trimmed = inputPath.Trim().Trim('"');
            if (trimmed.Length == 0)
            {
                return null;
            }

            try
            {
                if (Path.IsPathRooted(trimmed) && File.Exists(trimmed))
                {
                    return Path.GetFullPath(trimmed);
                }

                var fromBaseDirectory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, trimmed));
                if (File.Exists(fromBaseDirectory))
                {
                    return fromBaseDirectory;
                }

                var fromCurrentDirectory = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, trimmed));
                if (File.Exists(fromCurrentDirectory))
                {
                    return fromCurrentDirectory;
                }
            }
            catch
            {
            }

            return null;
        }

        private static double ClampVolume(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                return 0.9;
            }

            return Math.Max(0.0, Math.Min(1.0, value));
        }
    }
}
