using System;
using System.Collections.Generic;
using System.Windows.Media;
using RadialSek.Models;

namespace RadialSek.Services
{
    public static class ThemePaletteService
    {
        private static readonly ThemePalette[] Palettes =
        {
            new ThemePalette
            {
                Key = "Crimson",
                DisplayName = "Crimson Night",
                SegmentColor = Color.FromArgb(225, 24, 24, 24),
                SegmentActiveColor = Color.FromArgb(245, 140, 24, 24),
                SegmentStrokeColor = Color.FromArgb(150, 240, 240, 240),
                CenterColor = Color.FromArgb(210, 28, 28, 30),
                CenterBorderColor = Color.FromArgb(90, 255, 255, 255),
                IconColor = Color.FromArgb(170, 34, 34, 36),
                IconActiveColor = Color.FromArgb(210, 110, 24, 24),
                IconBorderColor = Color.FromArgb(160, 255, 255, 255),
                IconActiveBorderColor = Color.FromArgb(220, 255, 232, 180),
                ShadowColor = Color.FromArgb(90, 0, 0, 0),
                TitleColor = Colors.White,
                SubtitleColor = Color.FromArgb(220, 225, 225, 225)
            },
            new ThemePalette
            {
                Key = "Apex",
                DisplayName = "Apex Glass",
                SegmentColor = Color.FromArgb(168, 58, 58, 62),
                SegmentActiveColor = Color.FromArgb(238, 238, 238, 238),
                SegmentStrokeColor = Color.FromArgb(185, 250, 250, 250),
                CenterColor = Color.FromArgb(135, 18, 21, 26),
                CenterBorderColor = Color.FromArgb(130, 245, 245, 245),
                IconColor = Color.FromArgb(0, 0, 0, 0),
                IconActiveColor = Color.FromArgb(72, 255, 255, 255),
                IconBorderColor = Color.FromArgb(0, 0, 0, 0),
                IconActiveBorderColor = Color.FromArgb(110, 255, 255, 255),
                ShadowColor = Color.FromArgb(90, 0, 0, 0),
                TitleColor = Colors.White,
                SubtitleColor = Color.FromArgb(220, 242, 242, 242)
            },
            new ThemePalette
            {
                Key = "Slate",
                DisplayName = "Slate Steel",
                SegmentColor = Color.FromArgb(228, 24, 32, 42),
                SegmentActiveColor = Color.FromArgb(245, 49, 117, 184),
                SegmentStrokeColor = Color.FromArgb(145, 220, 228, 236),
                CenterColor = Color.FromArgb(218, 22, 36, 48),
                CenterBorderColor = Color.FromArgb(110, 196, 220, 239),
                IconColor = Color.FromArgb(172, 28, 42, 54),
                IconActiveColor = Color.FromArgb(220, 38, 108, 173),
                IconBorderColor = Color.FromArgb(170, 208, 220, 232),
                IconActiveBorderColor = Color.FromArgb(220, 233, 244, 255),
                ShadowColor = Color.FromArgb(70, 0, 5, 15),
                TitleColor = Colors.White,
                SubtitleColor = Color.FromArgb(220, 224, 235, 245)
            },
            new ThemePalette
            {
                Key = "Orbit",
                DisplayName = "Orbit Blue",
                SegmentColor = Color.FromArgb(225, 26, 144, 228),
                SegmentActiveColor = Color.FromArgb(238, 140, 205, 255),
                SegmentStrokeColor = Color.FromArgb(210, 240, 248, 255),
                CenterColor = Color.FromArgb(232, 248, 251, 255),
                CenterBorderColor = Color.FromArgb(180, 31, 143, 229),
                IconColor = Color.FromArgb(0, 0, 0, 0),
                IconActiveColor = Color.FromArgb(72, 31, 143, 229),
                IconBorderColor = Color.FromArgb(0, 0, 0, 0),
                IconActiveBorderColor = Color.FromArgb(165, 31, 143, 229),
                ShadowColor = Color.FromArgb(50, 0, 10, 30),
                TitleColor = Color.FromArgb(255, 35, 56, 86),
                SubtitleColor = Color.FromArgb(230, 54, 82, 123)
            },
            new ThemePalette
            {
                Key = "Ivory",
                DisplayName = "Ivory Paper",
                SegmentColor = Color.FromArgb(230, 40, 36, 32),
                SegmentActiveColor = Color.FromArgb(245, 176, 117, 45),
                SegmentStrokeColor = Color.FromArgb(165, 250, 240, 224),
                CenterColor = Color.FromArgb(224, 56, 49, 42),
                CenterBorderColor = Color.FromArgb(110, 252, 235, 208),
                IconColor = Color.FromArgb(180, 74, 62, 49),
                IconActiveColor = Color.FromArgb(224, 161, 101, 35),
                IconBorderColor = Color.FromArgb(160, 245, 227, 197),
                IconActiveBorderColor = Color.FromArgb(225, 255, 243, 218),
                ShadowColor = Color.FromArgb(60, 40, 20, 0),
                TitleColor = Colors.White,
                SubtitleColor = Color.FromArgb(226, 246, 233, 214)
            },
            new ThemePalette
            {
                Key = "Amber",
                DisplayName = "Amber Drive",
                SegmentColor = Color.FromArgb(236, 22, 22, 28),
                SegmentActiveColor = Color.FromArgb(245, 255, 193, 38),
                SegmentStrokeColor = Color.FromArgb(145, 92, 92, 98),
                CenterColor = Color.FromArgb(240, 255, 193, 38),
                CenterBorderColor = Color.FromArgb(155, 22, 22, 28),
                IconColor = Color.FromArgb(0, 0, 0, 0),
                IconActiveColor = Color.FromArgb(0, 0, 0, 0),
                IconBorderColor = Color.FromArgb(0, 0, 0, 0),
                IconActiveBorderColor = Color.FromArgb(0, 0, 0, 0),
                ShadowColor = Color.FromArgb(70, 20, 10, 0),
                TitleColor = Color.FromArgb(255, 45, 36, 14),
                SubtitleColor = Color.FromArgb(228, 65, 52, 20)
            },
            new ThemePalette
            {
                Key = "Graphite",
                DisplayName = "Graphite Minimal",
                SegmentColor = Color.FromArgb(232, 31, 33, 36),
                SegmentActiveColor = Color.FromArgb(240, 88, 92, 101),
                SegmentStrokeColor = Color.FromArgb(150, 109, 113, 120),
                CenterColor = Color.FromArgb(240, 17, 19, 22),
                CenterBorderColor = Color.FromArgb(100, 112, 116, 124),
                IconColor = Color.FromArgb(0, 0, 0, 0),
                IconActiveColor = Color.FromArgb(48, 255, 255, 255),
                IconBorderColor = Color.FromArgb(0, 0, 0, 0),
                IconActiveBorderColor = Color.FromArgb(85, 255, 255, 255),
                ShadowColor = Color.FromArgb(120, 0, 0, 0),
                TitleColor = Colors.White,
                SubtitleColor = Color.FromArgb(220, 216, 218, 223)
            }
        };

        private static readonly IReadOnlyDictionary<string, ThemePalette> PaletteMap = BuildPaletteMap();

        public static IReadOnlyList<ThemePalette> GetPalettes()
        {
            return Palettes;
        }

        public static ThemePalette Resolve(string? key)
        {
            if (!string.IsNullOrWhiteSpace(key) && PaletteMap.TryGetValue(key, out var palette))
            {
                return palette;
            }

            return Palettes[0];
        }

        private static IReadOnlyDictionary<string, ThemePalette> BuildPaletteMap()
        {
            var map = new Dictionary<string, ThemePalette>(StringComparer.OrdinalIgnoreCase);
            foreach (var palette in Palettes)
            {
                map[palette.Key] = palette;
            }

            return map;
        }
    }
}
