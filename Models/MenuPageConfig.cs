using System.Collections.Generic;

namespace RadialSek.Models
{
    public class MenuPageConfig
    {
        public string Title { get; set; } = "1";
        public List<MenuItemConfig> Items { get; set; } = new List<MenuItemConfig>();
    }
}
