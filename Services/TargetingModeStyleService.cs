using RadialSek.Models;

namespace RadialSek.Services
{
    public static class TargetingModeStyleService
    {
        private static readonly TargetingModeStyleOption[] Options =
        {
            new TargetingModeStyleOption
            {
                Key = "LaserLine",
                DisplayName = "Lazer Cizgisi",
                Description = "Merkezden cursora uzanan temiz ve parlak klasik hedefleme cizgisi."
            },
            new TargetingModeStyleOption
            {
                Key = "DottedFlow",
                DisplayName = "Nokta Akisi",
                Description = "Cizgi yerine hedefe dogru dizilen parlak noktalardan olusan akis."
            },
            new TargetingModeStyleOption
            {
                Key = "GlowOrb",
                DisplayName = "Glow Kure",
                Description = "Ince cizgiyle birlikte cursor ucunda parlayan bir hedefleme kuresi gorunur."
            },
            new TargetingModeStyleOption
            {
                Key = "LightCone",
                DisplayName = "Isik Konisi",
                Description = "Merkezden cursor yonune uzanan ince bir isik huzmesi gibi gorunur."
            },
            new TargetingModeStyleOption
            {
                Key = "ParticleTrail",
                DisplayName = "Parcacik Izi",
                Description = "Cursor yonune akan kucuk parcaciklar ile daha enerjik bir hedefleme hissi verir."
            },
            new TargetingModeStyleOption
            {
                Key = "TargetArrow",
                DisplayName = "Hedef Oku",
                Description = "Cursor ucunda yonu gosteren bir ok basi ile daha mekanik bir hedefleme sunar."
            }
        };

        public static System.Collections.Generic.IReadOnlyList<TargetingModeStyleOption> GetOptions()
        {
            return Options;
        }

        public static string ResolveKey(string? key)
        {
            foreach (var option in Options)
            {
                if (string.Equals(option.Key, key, System.StringComparison.OrdinalIgnoreCase))
                {
                    return option.Key;
                }
            }

            return "LaserLine";
        }
    }
}
