using System.Collections.Generic;

namespace RadialSek.Models
{
    public class MenuItemConfig
    {
        public string Label { get; set; } = "";
        public string TargetPath { get; set; } = "";
        public string CustomIconPath { get; set; } = "";
        public string CategorySymbolKey { get; set; } = "";
        public string FixedColor { get; set; } = "";
        public bool IsCategory { get; set; }
        public List<MenuItemConfig> Children { get; set; } = new List<MenuItemConfig>();
    }
}
