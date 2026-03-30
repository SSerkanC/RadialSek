using System;
using System.Diagnostics;
using System.IO;

namespace RadialSek.Services
{
    public class LauncherService
    {
        public void Launch(string targetPath)
        {
            if (string.IsNullOrWhiteSpace(targetPath))
            {
                return;
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = targetPath,
                UseShellExecute = true,
                WorkingDirectory = ResolveWorkingDirectory(targetPath)
            };

            Process.Start(startInfo);
        }

        private static string ResolveWorkingDirectory(string targetPath)
        {
            try
            {
                if (File.Exists(targetPath))
                {
                    return Path.GetDirectoryName(targetPath) ?? AppDomain.CurrentDomain.BaseDirectory;
                }

                if (Directory.Exists(targetPath))
                {
                    return targetPath;
                }
            }
            catch
            {
            }

            return AppDomain.CurrentDomain.BaseDirectory;
        }
    }
}
