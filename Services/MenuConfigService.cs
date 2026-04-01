using System;
using System.IO;
using System.Text.Json;
using RadialSek.Models;

namespace RadialSek.Services
{
    public class MenuConfigService
    {
        private readonly string _configPath;
        private readonly string _bundledConfigPath;

        public MenuConfigService()
        {
            _configPath = ApplicationStorageService.GetConfigPath();
            _bundledConfigPath = ApplicationStorageService.GetBundledConfigPath();
            EnsureConfigExists();
        }

        public MenuConfig LoadConfig()
        {
            var json = File.ReadAllText(_configPath);
            var config = JsonSerializer.Deserialize<MenuConfig>(json) ?? new MenuConfig();
            if (string.IsNullOrWhiteSpace(config.MenuStyle))
            {
                config.MenuStyle = "Style1";
            }
            if (string.IsNullOrWhiteSpace(config.Theme))
            {
                config.Theme = "Crimson";
            }
            config.OpenAnimationStyle = MenuOpenAnimationService.ResolveKey(config.OpenAnimationStyle);
            config.TargetingModeStyle = TargetingModeStyleService.ResolveKey(config.TargetingModeStyle);
            config.CategoryStripStyle = CategoryStripStyleService.ResolveKey(config.CategoryStripStyle);
            config.CategoryStripFont = CategoryStripFontService.Resolve(config.CategoryStripFont).Key;
            config.CenterClockFont = CenterClockFontService.ResolveKey(config.CenterClockFont);
            config.CategoryStripOpacity = Math.Max(0.15, Math.Min(1.0, config.CategoryStripOpacity <= 0 ? 0.98 : config.CategoryStripOpacity));
            config.CategoryStripFontOpacity = Math.Max(0.15, Math.Min(1.0, config.CategoryStripFontOpacity <= 0 ? 1.0 : config.CategoryStripFontOpacity));
            config.InnerGradientRingThicknessScale = Math.Max(0.4, Math.Min(2.5, config.InnerGradientRingThicknessScale <= 0 ? 1.0 : config.InnerGradientRingThicknessScale));
            config.OuterGradientRingThicknessScale = Math.Max(0.4, Math.Min(2.5, config.OuterGradientRingThicknessScale <= 0 ? 1.0 : config.OuterGradientRingThicknessScale));
            config.MenuBackdropBlurSizeScale = ClampScale(config.MenuBackdropBlurSizeScale, 1.0, 0.6, 2.5);
            config.MenuBackdropBlurStrengthScale = ClampScale(config.MenuBackdropBlurStrengthScale, 1.0, 0.4, 2.5);
            if (string.IsNullOrWhiteSpace(config.CategoryStripFontColor))
            {
                config.CategoryStripFontColor = "#FAFCFF";
            }
            config.TargetingShortcut ??= ActivationShortcut.CreateTargetingModeDefault();
            if (string.IsNullOrWhiteSpace(config.TargetingShortcut.Trigger))
            {
                config.TargetingShortcut = ActivationShortcut.CreateTargetingModeDefault();
            }
            config.Features ??= new MenuFeatures();
            if (!json.Contains("\"EnableGradientRingAnimations\"", StringComparison.OrdinalIgnoreCase))
            {
                config.Features.EnableGradientRingAnimations = true;
            }
            if (!json.Contains("\"EnableMenuBackdropBlur\"", StringComparison.OrdinalIgnoreCase))
            {
                config.Features.EnableMenuBackdropBlur = true;
            }
            config.Features.EnableOpenAnimation = !string.Equals(config.OpenAnimationStyle, "None", StringComparison.OrdinalIgnoreCase);
            config.Weather ??= new WeatherSettings();
            config.Weather.ManualPreset = WeatherSettingsService.ResolveWeatherPresetKey(config.Weather.ManualPreset);
            config.Weather.DayNightMode = WeatherSettingsService.ResolveDayNightModeKey(config.Weather.DayNightMode);
            config.Weather.AnimationSpeedScale = WeatherSettingsService.ClampSpeedScale(config.Weather.AnimationSpeedScale);
            config.Weather.AnimationIntensityScale = WeatherSettingsService.ClampIntensityScale(config.Weather.AnimationIntensityScale);
            config.Audio ??= new AudioSettings();
            config.Audio.MasterVolume = ClampUnitVolume(config.Audio.MasterVolume, 0.72);
            config.Audio.UiVolume = ClampUnitVolume(config.Audio.UiVolume, 0.86);
            config.Audio.HoverVolume = ClampUnitVolume(config.Audio.HoverVolume, 0.78);
            config.Audio.NotificationVolume = ClampUnitVolume(config.Audio.NotificationVolume, 0.82);
            config.Tools ??= new ToolsSettings();
            config.Tools.Alarm ??= new AlarmToolSettings();
            config.Tools.Stopwatch ??= new StopwatchToolSettings();
            config.Tools.ShutdownTimer ??= new ShutdownTimerToolSettings();
            config.Tools.Alarm.DueNotificationSoundPath ??= string.Empty;
            config.Tools.Alarm.DueNotificationSoundVolume = ClampUnitVolume(config.Tools.Alarm.DueNotificationSoundVolume, 0.9);
            if (config.Shortcuts == null || config.Shortcuts.Count == 0)
            {
                config.Shortcuts = CreateDefaultShortcuts();
            }
            else
            {
                EnsureShortcutDefaults(config.Shortcuts);
            }
            if (config.Pages == null || config.Pages.Count == 0)
            {
                config.Pages = new System.Collections.Generic.List<MenuPageConfig>
                {
                    new MenuPageConfig
                    {
                        Title = "1",
                        Items = config.Items ?? new System.Collections.Generic.List<MenuItemConfig>()
                    }
                };

                if (config.Page2Items != null && config.Page2Items.Count > 0)
                {
                    config.Pages.Add(new MenuPageConfig
                    {
                        Title = "2",
                        Items = config.Page2Items
                    });
                }
            }
            else
            {
                foreach (var page in config.Pages)
                {
                    page.Items ??= new System.Collections.Generic.List<MenuItemConfig>();
                    if (string.IsNullOrWhiteSpace(page.Title))
                    {
                        page.Title = "1";
                    }
                }
            }
            return config;
        }

        public void SaveConfig(MenuConfig config)
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            var tempPath = _configPath + ".tmp";
            File.WriteAllText(tempPath, JsonSerializer.Serialize(config, options));
            File.Move(tempPath, _configPath, true);
        }

        private void EnsureConfigExists()
        {
            if (File.Exists(_configPath))
            {
                return;
            }

            if (File.Exists(_bundledConfigPath))
            {
                File.Copy(_bundledConfigPath, _configPath, true);
                return;
            }

            var defaultConfig = new MenuConfig
            {
                MenuStyle = "Style1",
                Theme = "Crimson",
                OpenAnimationStyle = "SoftRise",
                TargetingModeStyle = "LaserLine",
                CategoryStripStyle = "GlassBeam",
                CategoryStripFont = "Segoe",
                CenterClockFont = "ProgramLabel",
                CategoryStripOpacity = 0.98,
                CategoryStripFontOpacity = 1.0,
                CategoryStripFontColor = "#FAFCFF",
                InnerGradientRingThicknessScale = 1.0,
                OuterGradientRingThicknessScale = 1.0,
                MenuBackdropBlurSizeScale = 1.0,
                MenuBackdropBlurStrengthScale = 1.0,
                TargetingShortcut = ActivationShortcut.CreateTargetingModeDefault(),
                Items =
                {
                    new MenuItemConfig { Label = "Explorer", TargetPath = "explorer.exe" },
                    new MenuItemConfig { Label = "Notepad", TargetPath = "notepad.exe" },
                    new MenuItemConfig { Label = "Calculator", TargetPath = "calc.exe" },
                    new MenuItemConfig { Label = "Paint", TargetPath = "mspaint.exe" },
                    new MenuItemConfig { Label = "Task Manager", TargetPath = "taskmgr.exe" },
                    new MenuItemConfig { Label = "Cmd", TargetPath = "cmd.exe" }
                }
            };

            SaveConfig(defaultConfig);
        }

        private static System.Collections.Generic.List<ActivationShortcut> CreateDefaultShortcuts()
        {
            return new System.Collections.Generic.List<ActivationShortcut>
            {
                new ActivationShortcut
                {
                    ShortcutId = ActivationShortcut.OpenMenuShortcutId
                },
                ActivationShortcut.CreateToggleProgramDefault()
            };
        }

        private static void EnsureShortcutDefaults(System.Collections.Generic.IList<ActivationShortcut> shortcuts)
        {
            var hasOpenShortcut = false;
            var hasToggleShortcut = false;

            foreach (var shortcut in shortcuts)
            {
                if (string.IsNullOrWhiteSpace(shortcut.ShortcutId))
                {
                    shortcut.ShortcutId = ActivationShortcut.OpenMenuShortcutId;
                }

                if (shortcut.ShortcutId == ActivationShortcut.OpenMenuShortcutId)
                {
                    hasOpenShortcut = true;
                }
                else if (shortcut.ShortcutId == ActivationShortcut.ToggleProgramShortcutId)
                {
                    hasToggleShortcut = true;
                }
            }

            if (!hasOpenShortcut)
            {
                shortcuts.Add(new ActivationShortcut
                {
                    ShortcutId = ActivationShortcut.OpenMenuShortcutId
                });
            }

            if (!hasToggleShortcut)
            {
                shortcuts.Add(ActivationShortcut.CreateToggleProgramDefault());
            }
        }

        private static double ClampUnitVolume(double value, double fallback)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                return fallback;
            }

            return Math.Max(0, Math.Min(1.0, value));
        }

        private static double ClampScale(double value, double fallback, double min, double max)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0)
            {
                return fallback;
            }

            return Math.Max(min, Math.Min(max, value));
        }
    }
}
