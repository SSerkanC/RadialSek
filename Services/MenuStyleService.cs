using System.Collections.Generic;
using RadialSek.Models;

namespace RadialSek.Services
{
    public static class MenuStyleService
    {
        private static readonly MenuStyleOption[] Options =
        {
            new MenuStyleOption
            {
                Key = "Style1",
                DisplayName = "Stil 1 - Arena Wheel",
                Description = "Bosluklu, oyun menusu karakteri daha belirgin halka."
            },
            new MenuStyleOption
            {
                Key = "Style2",
                DisplayName = "Stil 2 - Seam Ring",
                Description = "Bitisik dilimli, daha temiz ve kompakt halka tasarimi."
            },
            new MenuStyleOption
            {
                Key = "Style3",
                DisplayName = "Stil 3 - Command Halo",
                Description = "Ince halka, modern HUD hissi ve hafif isik vurgusu."
            },
            new MenuStyleOption
            {
                Key = "Style4",
                DisplayName = "Stil 4 - Tactical Wheel",
                Description = "Daha kalin, dramatik ve bilgi odakli taktik menu."
            },
            new MenuStyleOption
            {
                Key = "Style5",
                DisplayName = "Stil 5 - Minimal Glyph",
                Description = "Cok sade, ince cizgili ve dikkat dagitmayan sembol agirlikli stil."
            },
            new MenuStyleOption
            {
                Key = "Style6",
                DisplayName = "Stil 6 - Arc Launcher",
                Description = "Tam daire yerine yay formunda, daha hafif ve hizli gorunen yerlesim."
            },
            new MenuStyleOption
            {
                Key = "Style7",
                DisplayName = "Stil 7 - Neon Orbit",
                Description = "Dilimsiz, neon halkali ikon yoresi ve merkezde sade kapatma dugmesi."
            }
        };

        public static IReadOnlyList<MenuStyleOption> GetOptions()
        {
            return Options;
        }
    }
}
