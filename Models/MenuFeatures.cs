namespace RadialSek.Models
{
    public class MenuFeatures
    {
        public bool StartWithWindows { get; set; }
        public bool EnableOpenAnimation { get; set; } = true;
        public bool ShowHoverLabels { get; set; }
        public bool ShowCategoryLabels { get; set; } = true;
        public bool ShowIconChrome { get; set; }
        public bool EnableGradientRingAnimations { get; set; } = true;
        public bool EnableMonochromeBackdrop { get; set; }
        public bool EnableMenuBackdropBlur { get; set; } = true;
        public bool EnableLightIdleMode { get; set; }
        public int LightIdleDelaySeconds { get; set; } = 20;

        public MenuFeatures Clone()
        {
            return new MenuFeatures
            {
                StartWithWindows = StartWithWindows,
                EnableOpenAnimation = EnableOpenAnimation,
                ShowHoverLabels = ShowHoverLabels,
                ShowCategoryLabels = ShowCategoryLabels,
                ShowIconChrome = ShowIconChrome,
                EnableGradientRingAnimations = EnableGradientRingAnimations,
                EnableMonochromeBackdrop = EnableMonochromeBackdrop,
                EnableMenuBackdropBlur = EnableMenuBackdropBlur,
                EnableLightIdleMode = EnableLightIdleMode,
                LightIdleDelaySeconds = LightIdleDelaySeconds
            };
        }
    }
}
