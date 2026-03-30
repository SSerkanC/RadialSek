using System.Windows.Media;

namespace RadialSek.Models
{
    public class ThemePalette
    {
        public string Key { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public Color SegmentColor { get; set; }
        public Color SegmentActiveColor { get; set; }
        public Color SegmentStrokeColor { get; set; }
        public Color CenterColor { get; set; }
        public Color CenterBorderColor { get; set; }
        public Color IconColor { get; set; }
        public Color IconActiveColor { get; set; }
        public Color IconBorderColor { get; set; }
        public Color IconActiveBorderColor { get; set; }
        public Color ShadowColor { get; set; }
        public Color TitleColor { get; set; }
        public Color SubtitleColor { get; set; }
    }
}
