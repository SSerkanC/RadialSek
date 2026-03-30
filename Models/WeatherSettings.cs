namespace RadialSek.Models
{
    public class WeatherSettings
    {
        public bool EnableAnimations { get; set; } = true;
        public bool UseLiveData { get; set; } = true;
        public string ManualPreset { get; set; } = "PartlyCloudy";
        public string DayNightMode { get; set; } = "Auto";
        public double AnimationSpeedScale { get; set; } = 1.0;
        public double AnimationIntensityScale { get; set; } = 1.0;

        public WeatherSettings Clone()
        {
            return new WeatherSettings
            {
                EnableAnimations = EnableAnimations,
                UseLiveData = UseLiveData,
                ManualPreset = ManualPreset,
                DayNightMode = DayNightMode,
                AnimationSpeedScale = AnimationSpeedScale,
                AnimationIntensityScale = AnimationIntensityScale
            };
        }
    }
}
