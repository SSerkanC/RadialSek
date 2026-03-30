using System.Collections.Generic;
using System.Linq;
using RadialSek.Models;

namespace RadialSek.Services
{
    public static class MenuItemCloneService
    {
        public static MenuItemConfig Clone(MenuItemConfig item)
        {
            return new MenuItemConfig
            {
                Label = item.Label,
                TargetPath = item.TargetPath,
                CustomIconPath = item.CustomIconPath,
                CategorySymbolKey = item.CategorySymbolKey,
                FixedColor = item.FixedColor,
                IsCategory = item.IsCategory,
                Children = item.Children.Select(Clone).ToList()
            };
        }

        public static List<MenuItemConfig> CloneMany(IEnumerable<MenuItemConfig> items)
        {
            return items.Select(Clone).ToList();
        }
    }
}
