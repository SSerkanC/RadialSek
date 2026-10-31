using RadialSek.Services;

namespace RadialSek.Models
{
    public class ToolsSettings
    {
        public AlarmToolSettings Alarm { get; set; } = new AlarmToolSettings();
        public StopwatchToolSettings Stopwatch { get; set; } = new StopwatchToolSettings();
        public ShutdownTimerToolSettings ShutdownTimer { get; set; } = new ShutdownTimerToolSettings();
        public SideMenuToolSettings SideMenu { get; set; } = new SideMenuToolSettings();

        public ToolsSettings Clone()
        {
            return new ToolsSettings
            {
                Alarm = Alarm?.Clone() ?? new AlarmToolSettings(),
                Stopwatch = Stopwatch?.Clone() ?? new StopwatchToolSettings(),
                ShutdownTimer = ShutdownTimer?.Clone() ?? new ShutdownTimerToolSettings(),
                SideMenu = SideMenu?.Clone() ?? new SideMenuToolSettings()
            };
        }
    }

    public class AlarmToolSettings
    {
        public bool EnableAlarmTool { get; set; } = true;
        public bool EnableDueNotificationSound { get; set; } = true;
        public string DueNotificationSoundPath { get; set; } = string.Empty;
        public double DueNotificationSoundVolume { get; set; } = 0.9;

        public AlarmToolSettings Clone()
        {
            return new AlarmToolSettings
            {
                EnableAlarmTool = EnableAlarmTool,
                EnableDueNotificationSound = EnableDueNotificationSound,
                DueNotificationSoundPath = DueNotificationSoundPath ?? string.Empty,
                DueNotificationSoundVolume = DueNotificationSoundVolume
            };
        }
    }

    public class StopwatchToolSettings
    {
        public bool EnableStopwatchTool { get; set; } = true;
        public bool KeepRunningInBackground { get; set; } = true;

        public StopwatchToolSettings Clone()
        {
            return new StopwatchToolSettings
            {
                EnableStopwatchTool = EnableStopwatchTool,
                KeepRunningInBackground = KeepRunningInBackground
            };
        }
    }

    public class ShutdownTimerToolSettings
    {
        public bool EnableShutdownTimerTool { get; set; } = true;
        public bool ShowCenterCountdown { get; set; } = true;

        public ShutdownTimerToolSettings Clone()
        {
            return new ShutdownTimerToolSettings
            {
                EnableShutdownTimerTool = EnableShutdownTimerTool,
                ShowCenterCountdown = ShowCenterCountdown
            };
        }
    }

    public class SideMenuTabConfig
    {
        public string Id { get; set; } = System.Guid.NewGuid().ToString("N");
        public string Name { get; set; } = "Sekme 1";
        public string? Color { get; set; }
        public System.Collections.Generic.List<MenuItemConfig> Items { get; set; } = new System.Collections.Generic.List<MenuItemConfig>();

        public SideMenuTabConfig Clone()
        {
            var clonedItems = new System.Collections.Generic.List<MenuItemConfig>();
            if (Items != null)
            {
                foreach (var item in Items)
                {
                    clonedItems.Add(new MenuItemConfig
                    {
                        Label = item.Label ?? string.Empty,
                        TargetPath = item.TargetPath ?? string.Empty,
                        CustomIconPath = item.CustomIconPath ?? string.Empty,
                        CategorySymbolKey = item.CategorySymbolKey ?? string.Empty,
                        FixedColor = item.FixedColor ?? string.Empty,
                        IsCategory = item.IsCategory,
                        Children = MenuItemCloneService.CloneMany(item.Children ?? new System.Collections.Generic.List<MenuItemConfig>())
                    });
                }
            }

            return new SideMenuTabConfig
            {
                Id = Id ?? System.Guid.NewGuid().ToString("N"),
                Name = Name ?? "Sekme",
                Color = Color,
                Items = clonedItems
            };
        }
    }

    public class SideMenuToolSettings
    {
        public bool EnableSideMenu { get; set; } = false;
        public bool ShowIndicatorLine { get; set; } = true;
        public bool IsCompact { get; set; } = false;
        public bool ShowWeatherCard { get; set; } = true;
        public string ActiveTabId { get; set; } = string.Empty;
        public System.Collections.Generic.List<SideMenuTabConfig> Tabs { get; set; } = new System.Collections.Generic.List<SideMenuTabConfig>();
        public System.Collections.Generic.List<MenuItemConfig> Items { get; set; } = new System.Collections.Generic.List<MenuItemConfig>();

        public SideMenuToolSettings Clone()
        {
            var clonedTabs = new System.Collections.Generic.List<SideMenuTabConfig>();
            if (Tabs != null)
            {
                foreach (var t in Tabs)
                {
                    clonedTabs.Add(t.Clone());
                }
            }

            var clonedItems = new System.Collections.Generic.List<MenuItemConfig>();
            if (Items != null)
            {
                foreach (var item in Items)
                {
                    clonedItems.Add(new MenuItemConfig
                    {
                        Label = item.Label ?? string.Empty,
                        TargetPath = item.TargetPath ?? string.Empty,
                        CustomIconPath = item.CustomIconPath ?? string.Empty,
                        CategorySymbolKey = item.CategorySymbolKey ?? string.Empty,
                        FixedColor = item.FixedColor ?? string.Empty,
                        IsCategory = item.IsCategory,
                        Children = MenuItemCloneService.CloneMany(item.Children ?? new System.Collections.Generic.List<MenuItemConfig>())
                    });
                }
            }

            return new SideMenuToolSettings
            {
                EnableSideMenu = EnableSideMenu,
                ShowIndicatorLine = ShowIndicatorLine,
                IsCompact = IsCompact,
                ShowWeatherCard = ShowWeatherCard,
                ActiveTabId = ActiveTabId ?? string.Empty,
                Tabs = clonedTabs,
                Items = clonedItems
            };
        }
    }
}
