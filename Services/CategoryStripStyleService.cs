using System;
using System.Collections.Generic;
using RadialSek.Models;

namespace RadialSek.Services
{
    public static class CategoryStripStyleService
    {
        private static readonly IReadOnlyList<CategoryStripStyleOption> Options = new List<CategoryStripStyleOption>
        {
            new CategoryStripStyleOption { Key = "GlassBeam", DisplayName = "Cam Şerit", Description = "Yumuşak, parlak ve cam hissi veren modern görünüm." },
            new CategoryStripStyleOption { Key = "GradientBeam", DisplayName = "Gradient Şerit", Description = "Önceki yoğun gradient tasarımı." },
            new CategoryStripStyleOption { Key = "NeonRail", DisplayName = "Neon Ray", Description = "Keskin ışık hattı ve canlı neon vurgu." },
            new CategoryStripStyleOption { Key = "AuroraRibbon", DisplayName = "Aurora Kurdele", Description = "Renklerin yumuşakça aktığı aurora tarzı şerit." },
            new CategoryStripStyleOption { Key = "CarbonPulse", DisplayName = "Karbon Pulse", Description = "Daha teknik, katmanlı ve güçlü kontrastlı görünüm." },
            new CategoryStripStyleOption { Key = "CrystalTag", DisplayName = "Kristal Etiket", Description = "Şeffaf etiket ve parlak kenar geçişleri." },
            new CategoryStripStyleOption { Key = "LiquidArc", DisplayName = "Sıvı Ark", Description = "Akışkan hissi veren daha yumuşak ve dolgun şerit." }
        };

        public static IReadOnlyList<CategoryStripStyleOption> GetOptions() => Options;

        public static string ResolveKey(string? key)
        {
            foreach (var option in Options)
            {
                if (string.Equals(option.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    return option.Key;
                }
            }

            return "GlassBeam";
        }
    }
}
