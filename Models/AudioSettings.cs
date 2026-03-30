namespace RadialSek.Models
{
    public class AudioSettings
    {
        public bool EnableSounds { get; set; } = true;
        public bool SilentMode { get; set; }
        public double MasterVolume { get; set; } = 0.72;
        public double UiVolume { get; set; } = 0.86;
        public double HoverVolume { get; set; } = 0.78;
        public double NotificationVolume { get; set; } = 0.82;

        public AudioSettings Clone()
        {
            return new AudioSettings
            {
                EnableSounds = EnableSounds,
                SilentMode = SilentMode,
                MasterVolume = MasterVolume,
                UiVolume = UiVolume,
                HoverVolume = HoverVolume,
                NotificationVolume = NotificationVolume
            };
        }
    }
}
