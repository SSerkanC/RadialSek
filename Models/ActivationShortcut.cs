namespace RadialSek.Models
{
    public class ActivationShortcut
    {
        public const string OpenMenuShortcutId = "OpenMenu";
        public const string ToggleProgramShortcutId = "ToggleProgram";
        public const string TargetingModeShortcutId = "TargetingMode";
        public const string TriggerMiddleMouse = "MiddleMouse";
        public const string TriggerMiddleMouseDouble = "MiddleMouseDouble";
        public const string TriggerRightMouse = "RightMouse";
        public const string TriggerXButton1 = "XButton1";
        public const string TriggerXButton2 = "XButton2";

        public bool Ctrl { get; set; }
        public bool Alt { get; set; } = true;
        public bool Shift { get; set; }
        public bool Win { get; set; }
        public string Trigger { get; set; } = TriggerMiddleMouse;
        public string ShortcutId { get; set; } = OpenMenuShortcutId;

        public static ActivationShortcut CreateOpenMenuDefault()
        {
            return new ActivationShortcut
            {
                ShortcutId = OpenMenuShortcutId
            };
        }

        public static ActivationShortcut CreateToggleProgramDefault()
        {
            return new ActivationShortcut
            {
                ShortcutId = ToggleProgramShortcutId,
                Trigger = "F10",
                Alt = false
            };
        }

        public static ActivationShortcut CreateTargetingModeDefault()
        {
            return new ActivationShortcut
            {
                ShortcutId = TargetingModeShortcutId,
                Trigger = "Alt",
                Alt = false
            };
        }

        public ActivationShortcut Clone()
        {
            return new ActivationShortcut
            {
                Ctrl = Ctrl,
                Alt = Alt,
                Shift = Shift,
                Win = Win,
                Trigger = Trigger,
                ShortcutId = ShortcutId
            };
        }

        public string GetDisplayText()
        {
            var text = "";
            if (Ctrl) text += "Ctrl + ";
            if (Alt) text += "Alt + ";
            if (Shift) text += "Shift + ";
            if (Win) text += "Win + ";
            return text + FormatTriggerDisplayName(Trigger);
        }

        public override string ToString()
        {
            return GetDisplayText();
        }

        private static string FormatTriggerDisplayName(string trigger)
        {
            return trigger switch
            {
                TriggerMiddleMouseDouble => "MiddleMouse x2",
                _ => trigger
            };
        }
    }
}
