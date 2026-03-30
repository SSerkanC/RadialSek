using System.Collections.Generic;

namespace RadialSek.Models
{
    public class MenuConfig
    {
        public string MenuStyle { get; set; } = "Style1";
        public string Theme { get; set; } = "Crimson";
        public string OpenAnimationStyle { get; set; } = "SoftRise";
        public string TargetingModeStyle { get; set; } = "LaserLine";
        public string CategoryStripStyle { get; set; } = "GlassBeam";
        public string CategoryStripFont { get; set; } = "Segoe";
        public string CenterClockFont { get; set; } = "ProgramLabel";
        public double CategoryStripOpacity { get; set; } = 0.98;
        public double CategoryStripFontOpacity { get; set; } = 1.0;
        public string CategoryStripFontColor { get; set; } = "#FAFCFF";
        public double InnerGradientRingThicknessScale { get; set; } = 1.0;
        public double OuterGradientRingThicknessScale { get; set; } = 1.0;
        public double MenuBackdropBlurSizeScale { get; set; } = 1.0;
        public double MenuBackdropBlurStrengthScale { get; set; } = 1.0;
        public ActivationShortcut TargetingShortcut { get; set; } = ActivationShortcut.CreateTargetingModeDefault();
        public MenuFeatures Features { get; set; } = new MenuFeatures();
        public WeatherSettings Weather { get; set; } = new WeatherSettings();
        public List<ActivationShortcut> Shortcuts { get; set; } = new List<ActivationShortcut>
        {
            ActivationShortcut.CreateOpenMenuDefault()
        };
        public AudioSettings Audio { get; set; } = new AudioSettings();
        public List<MenuPageConfig> Pages { get; set; } = new List<MenuPageConfig>
        {
            new MenuPageConfig { Title = "1" }
        };
        public List<MenuItemConfig> Items { get; set; } = new List<MenuItemConfig>();
        public List<MenuItemConfig> Page2Items { get; set; } = new List<MenuItemConfig>();
    }
}
