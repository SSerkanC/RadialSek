using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using RadialSek.Models;

namespace RadialSek.Services
{
    public static class CenterClockFontService
    {
        private static readonly IReadOnlyList<CenterClockFontOption> Options = new List<CenterClockFontOption>
        {
            new CenterClockFontOption
            {
                Key = "ProgramLabel",
                DisplayName = "Segoe UI Semibold (Program Adi)",
                FontFamilyName = "Segoe UI",
                Description = "Program isimlerinde kullandigimiz font."
            },
            new CenterClockFontOption
            {
                Key = "Inter",
                DisplayName = "Inter",
                FontFamilyName = "Inter",
                Description = "Arayuzler icin optimize edilen modern sans serif."
            },
            new CenterClockFontOption
            {
                Key = "Orbitron",
                DisplayName = "Orbitron",
                FontFamilyName = "Orbitron",
                Description = "Daha futuristik ve dijital saat hissi verir."
            },
            new CenterClockFontOption
            {
                Key = "Rajdhani",
                DisplayName = "Rajdhani",
                FontFamilyName = "Rajdhani",
                Description = "Kose yapili, teknik ve ekran odakli bir gorunum sunar."
            },
            new CenterClockFontOption
            {
                Key = "RobotoMono",
                DisplayName = "Roboto Mono",
                FontFamilyName = "Roboto Mono",
                Description = "Monospace oldugu icin rakam hizalamasi nettir."
            }
        };

        public static IReadOnlyList<CenterClockFontOption> GetOptions() => Options;

        public static CenterClockFontOption Resolve(string? key)
        {
            foreach (var option in Options)
            {
                if (string.Equals(option.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    return option;
                }
            }

            return Options.First();
        }

        public static FontFamily CreateFontFamily(string? key)
        {
            var option = Resolve(key);
            foreach (var candidate in GetCandidateFamilyNames(option))
            {
                if (TryResolveInstalledFont(candidate, out var resolved))
                {
                    return resolved;
                }
            }

            return new FontFamily("Segoe UI");
        }

        public static string ResolveKey(string? key)
        {
            return Resolve(key).Key;
        }

        private static IEnumerable<string> GetCandidateFamilyNames(CenterClockFontOption option)
        {
            yield return option.FontFamilyName;

            switch (option.Key)
            {
                case "ProgramLabel":
                    yield return "Segoe UI";
                    break;
                case "Inter":
                    yield return "Calibri";
                    yield return "Arial";
                    break;
                case "Orbitron":
                    yield return "Bahnschrift";
                    yield return "Verdana";
                    break;
                case "Rajdhani":
                    yield return "Tahoma";
                    yield return "Trebuchet MS";
                    break;
                case "RobotoMono":
                    yield return "Cascadia Mono";
                    yield return "Consolas";
                    yield return "Courier New";
                    break;
            }

            yield return "Segoe UI";
        }

        private static bool TryResolveInstalledFont(string familyName, out FontFamily fontFamily)
        {
            var normalizedTarget = NormalizeFontName(familyName);
            var match = Fonts.SystemFontFamilies.FirstOrDefault(f =>
                NormalizeFontName(f.Source) == normalizedTarget);
            if (match != null)
            {
                fontFamily = match;
                return true;
            }

            fontFamily = null!;
            return false;
        }

        private static string NormalizeFontName(string value)
        {
            return new string(value.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        }
    }
}
