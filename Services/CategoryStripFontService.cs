using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Media;
using RadialSek.Models;

namespace RadialSek.Services
{
    public static class CategoryStripFontService
    {
        private static readonly IReadOnlyList<CategoryStripFontOption> Options = new List<CategoryStripFontOption>
        {
            new CategoryStripFontOption { Key = "SuperFoods", DisplayName = "Super Foods", FontFamilyName = "Super Foods", FontFileName = "Super Foods.ttf", Description = "Yuvarlak ve dikkat ceken baslik fontu." },
            new CategoryStripFontOption { Key = "ShareDong", DisplayName = "Share Dong", FontFamilyName = "Share Dong", FontFileName = "Share Dong.ttf", Description = "Daha karakterli ve eglenceli gorunum." },
            new CategoryStripFontOption { Key = "Grobold", DisplayName = "Grobold", FontFamilyName = "Grobold", Description = "Kalin ve guclu bir logo hissi." },
            new CategoryStripFontOption { Key = "SuperWonder", DisplayName = "Super Wonder", FontFamilyName = "Super Wonder", Description = "Yumusak ama vurucu baslik stili." },
            new CategoryStripFontOption { Key = "AlmondMocca", DisplayName = "Almond Mocca", FontFamilyName = "Almond Mocca", Description = "Tatli, dolgun ve okunakli bir gorunum." },
            new CategoryStripFontOption { Key = "CuteDino", DisplayName = "Cute Dino", FontFamilyName = "Cute Dino", Description = "Daha oyuncu ve yumusak bir his." },
            new CategoryStripFontOption { Key = "Thesead", DisplayName = "Thesead", FontFamilyName = "Thesead", Description = "Daha ozgun ve sert bir baslik tarzi." },
            new CategoryStripFontOption { Key = "Bahnschrift", DisplayName = "Bahnschrift SemiBold", FontFamilyName = "Bahnschrift", Description = "Temiz ve modern sistem fontu." },
            new CategoryStripFontOption { Key = "Segoe", DisplayName = "Segoe UI Semibold", FontFamilyName = "Segoe UI", Description = "Guvenli ve okunakli varsayilan secenek." }
        };

        public static IReadOnlyList<CategoryStripFontOption> GetOptions() => Options;

        public static CategoryStripFontOption Resolve(string? key)
        {
            foreach (var option in Options)
            {
                if (string.Equals(option.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    return option;
                }
            }

            return Options.FirstOrDefault(o => string.Equals(o.Key, "Segoe", StringComparison.OrdinalIgnoreCase))
                ?? Options[0];
        }

        public static FontFamily CreateFontFamily(string? key)
        {
            var option = Resolve(key);

            if (TryResolveInstalledFont(option.FontFamilyName, out var installed))
            {
                return installed;
            }

            var fileBackedFont = TryResolveFileBackedFont(option);
            if (fileBackedFont != null)
            {
                return fileBackedFont;
            }

            if (TryResolveInstalledFont("Bahnschrift", out var bahnschrift))
            {
                return bahnschrift;
            }

            if (TryResolveInstalledFont("Segoe UI", out var segoe))
            {
                return segoe;
            }

            return new FontFamily("Segoe UI");
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

        private static FontFamily? TryResolveFileBackedFont(CategoryStripFontOption option)
        {
            foreach (var directory in GetFontDirectories())
            {
                foreach (var fileName in GetCandidateFileNames(option))
                {
                    var fullPath = Path.Combine(directory, fileName);
                    if (!File.Exists(fullPath))
                    {
                        continue;
                    }

                    var folderUri = new Uri(Path.GetFullPath(directory) + Path.DirectorySeparatorChar, UriKind.Absolute);
                    return new FontFamily(folderUri, "./#" + option.FontFamilyName);
                }
            }

            return null;
        }

        private static IEnumerable<string> GetCandidateFileNames(CategoryStripFontOption option)
        {
            if (!string.IsNullOrWhiteSpace(option.FontFileName))
            {
                yield return option.FontFileName;
            }

            yield return option.FontFamilyName + ".ttf";
            yield return option.FontFamilyName + ".otf";
            yield return option.DisplayName + ".ttf";
            yield return option.DisplayName + ".otf";
        }

        private static IEnumerable<string> GetFontDirectories()
        {
            var windowsFonts = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Fonts");
            if (Directory.Exists(windowsFonts))
            {
                yield return windowsFonts;
            }

            var userFonts = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Microsoft",
                "Windows",
                "Fonts");
            if (Directory.Exists(userFonts))
            {
                yield return userFonts;
            }
        }

        private static string NormalizeFontName(string value)
        {
            return new string(value.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        }
    }
}
