using Microsoft.Win32;

namespace RadialSek.Services
{
    public sealed class StartupLaunchService
    {
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "RadialSekLauncher";

        public void Apply(bool enabled, string executablePath)
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
                ?? Registry.CurrentUser.CreateSubKey(RunKeyPath);

            if (enabled)
            {
                key?.SetValue(ValueName, "\"" + executablePath + "\"");
            }
            else
            {
                key?.DeleteValue(ValueName, throwOnMissingValue: false);
            }
        }
    }
}
