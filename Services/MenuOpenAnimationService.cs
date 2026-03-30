using RadialSek.Models;

namespace RadialSek.Services
{
    public static class MenuOpenAnimationService
    {
        private static readonly MenuOpenAnimationOption[] Options =
        {
            new MenuOpenAnimationOption
            {
                Key = "None",
                DisplayName = "Kapali",
                Description = "Radial menu herhangi bir acilis animasyonu olmadan aninda gorunur."
            },
            new MenuOpenAnimationOption
            {
                Key = "SoftRise",
                DisplayName = "Yumusak Yukselis",
                Description = "Mevcut radial menu acilis animasyonu. Hafif buyuyerek ve yukselerek acilir."
            },
            new MenuOpenAnimationOption
            {
                Key = "SoftFade",
                DisplayName = "Sade Solma",
                Description = "Daha sakin bir acilis. Menu yumusak sekilde gorunur hale gelir."
            },
            new MenuOpenAnimationOption
            {
                Key = "CenterUnfold",
                DisplayName = "Merkezden Acilis",
                Description = "Ana daire once belirir, sonra dilimler merkezden disari acilarak yerlerine oturur."
            },
            new MenuOpenAnimationOption
            {
                Key = "OdakKaskadi",
                DisplayName = "Odak Kaskadi",
                Description = "Ana daire 1 saniyede fade-in olur, ardindan dilimler merkezden sirali sekilde yerine akar."
            },
            new MenuOpenAnimationOption
            {
                Key = "VelvetCurtain",
                DisplayName = "Kadife Perde",
                Description = "Dilimler yumusakca genisleyerek perde gibi acilir, ikonlar sonradan parlayarak belirir."
            },
            new MenuOpenAnimationOption
            {
                Key = "NovaBloom",
                DisplayName = "Nova Patlamasi",
                Description = "Merkez yumusak bir pulse ile belirir, dilimler parlak bir bloom hissiyle disari yayilir."
            },
            new MenuOpenAnimationOption
            {
                Key = "ArcCascade",
                DisplayName = "Yay Kademesi",
                Description = "Dilimler saat yonunde kademeli olarak akip yerine oturur; daha teatrik bir acilis sunar."
            },
            new MenuOpenAnimationOption
            {
                Key = "MeteorDrop",
                DisplayName = "Meteor Dususu",
                Description = "Dilimler yukaridan hizla inip hafif seker, ikonlar daha gec gelerek guclu bir giris etkisi verir."
            }
        };

        public static System.Collections.Generic.IReadOnlyList<MenuOpenAnimationOption> GetOptions()
        {
            return Options;
        }

        public static string ResolveKey(string? key)
        {
            foreach (var option in Options)
            {
                if (string.Equals(option.Key, key, System.StringComparison.OrdinalIgnoreCase))
                {
                    return option.Key;
                }
            }

            return "SoftRise";
        }
    }
}
