using System;
using System.IO;

namespace RadialSek.Services
{
    public static class ApplicationStorageService
    {
        private const string ApplicationFolderName = "Radial Sek";

        public static string GetDataDirectory()
        {
            var directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                ApplicationFolderName);

            Directory.CreateDirectory(directory);
            return directory;
        }

        public static string GetConfigPath()
        {
            return Path.Combine(GetDataDirectory(), "config.json");
        }

        public static string GetDiagnosticLogPath()
        {
            return Path.Combine(GetDataDirectory(), "radial_sek_error.log");
        }

        public static string GetBundledConfigPath()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
        }
    }
}
