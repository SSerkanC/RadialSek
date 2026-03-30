using System;
using System.Collections.Generic;
using System.Linq;
using RadialSek.Models;

namespace RadialSek.Services
{
    public static class WeatherSettingsService
    {
        public const string DayNightAuto = "Auto";
        public const string DayNightDay = "Day";
        public const string DayNightNight = "Night";

        private static readonly IReadOnlyList<WeatherPresetOption> PresetOptions = new List<WeatherPresetOption>
        {
            new WeatherPresetOption { Key = "ClearSky", DisplayName = "Acik Hava", Description = "Gunesli veya gece acik hava." },
            new WeatherPresetOption { Key = "MostlyClear", DisplayName = "Az Bulutlu", Description = "Hafif bulut gecisleri." },
            new WeatherPresetOption { Key = "PartlyCloudy", DisplayName = "Parcali Bulutlu", Description = "Belirgin bulut gecisleri." },
            new WeatherPresetOption { Key = "Overcast", DisplayName = "Cok Bulutlu", Description = "Yogun bulut ortusu." },
            new WeatherPresetOption { Key = "Fog", DisplayName = "Sisli", Description = "Sis bantlari ve pus." },
            new WeatherPresetOption { Key = "Drizzle", DisplayName = "Cisenti", Description = "Ince yagmur efekti." },
            new WeatherPresetOption { Key = "FreezingDrizzle", DisplayName = "Donan Cisenti", Description = "Soguk tonlu ince yagis." },
            new WeatherPresetOption { Key = "Rain", DisplayName = "Yagmurlu", Description = "Standart yagmur efekti." },
            new WeatherPresetOption { Key = "FreezingRain", DisplayName = "Donan Yagmur", Description = "Soguk tonlu yagmur efekti." },
            new WeatherPresetOption { Key = "Snow", DisplayName = "Karli", Description = "Kar taneleri." },
            new WeatherPresetOption { Key = "SnowGrains", DisplayName = "Kar Taneleri (Ince)", Description = "Daha ince ve yogun kar parcaciklari." },
            new WeatherPresetOption { Key = "RainShowers", DisplayName = "Sağanak Yagmur", Description = "Daha hizli ve yogun yagmur." },
            new WeatherPresetOption { Key = "SnowShowers", DisplayName = "Kar Sağanagi", Description = "Daha yogun kar gecisi." },
            new WeatherPresetOption { Key = "Thunderstorm", DisplayName = "Firtina", Description = "Yagmur + simsek." },
            new WeatherPresetOption { Key = "ThunderstormHail", DisplayName = "Dolu Firtinasi", Description = "Yagmur + dolu + simsek." }
        };

        private static readonly IReadOnlyList<DayNightModeOption> DayNightOptions = new List<DayNightModeOption>
        {
            new DayNightModeOption { Key = DayNightAuto, DisplayName = "Otomatik", Description = "Canli veri veya saate gore otomatik secim." },
            new DayNightModeOption { Key = DayNightDay, DisplayName = "Gunduz", Description = "Her zaman gunduz gorseli kullanir." },
            new DayNightModeOption { Key = DayNightNight, DisplayName = "Gece", Description = "Her zaman gece gorseli kullanir." }
        };

        public static IReadOnlyList<WeatherPresetOption> GetWeatherPresetOptions() => PresetOptions;

        public static IReadOnlyList<DayNightModeOption> GetDayNightModeOptions() => DayNightOptions;

        public static string ResolveWeatherPresetKey(string? key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return "PartlyCloudy";
            }

            var match = PresetOptions.FirstOrDefault(x => string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase));
            return match?.Key ?? "PartlyCloudy";
        }

        public static string ResolveDayNightModeKey(string? key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return DayNightAuto;
            }

            var match = DayNightOptions.FirstOrDefault(x => string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase));
            return match?.Key ?? DayNightAuto;
        }

        public static double ClampSpeedScale(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0)
            {
                return 1.0;
            }

            return Math.Max(0.4, Math.Min(2.0, value));
        }

        public static double ClampIntensityScale(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0)
            {
                return 1.0;
            }

            return Math.Max(0.4, Math.Min(2.0, value));
        }
    }
}
