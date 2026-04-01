namespace RadialSek.Models
{
    public class ToolsSettings
    {
        public AlarmToolSettings Alarm { get; set; } = new AlarmToolSettings();
        public StopwatchToolSettings Stopwatch { get; set; } = new StopwatchToolSettings();
        public ShutdownTimerToolSettings ShutdownTimer { get; set; } = new ShutdownTimerToolSettings();

        public ToolsSettings Clone()
        {
            return new ToolsSettings
            {
                Alarm = Alarm?.Clone() ?? new AlarmToolSettings(),
                Stopwatch = Stopwatch?.Clone() ?? new StopwatchToolSettings(),
                ShutdownTimer = ShutdownTimer?.Clone() ?? new ShutdownTimerToolSettings()
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
}
