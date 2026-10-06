using UnityEngine;

namespace HonkAndLoad.Core
{
    /// <summary>Koli ve kamyon renkleri. En fazla 8 renk (tasarım dokümanı, bölüm 6).</summary>
    public static class Palette
    {
        private static readonly Color[] CrateColors =
        {
            new Color(0.95f, 0.30f, 0.30f), // kırmızı
            new Color(0.25f, 0.55f, 0.95f), // mavi
            new Color(1.00f, 0.80f, 0.20f), // sarı
            new Color(0.30f, 0.80f, 0.40f), // yeşil
            new Color(0.70f, 0.40f, 0.90f), // mor
            new Color(1.00f, 0.55f, 0.15f), // turuncu
            new Color(0.30f, 0.85f, 0.85f), // turkuaz
            new Color(0.95f, 0.50f, 0.75f), // pembe
        };

        public static Color Crate(int index) => CrateColors[((index % CrateColors.Length) + CrateColors.Length) % CrateColors.Length];

        public static Color Truck(int index) => Color.Lerp(Crate(index), Color.black, 0.25f);

        public static readonly Color Ground = new Color(0.78f, 0.79f, 0.80f);
        public static readonly Color Slot = new Color(0.74f, 0.77f, 0.83f);
        public static readonly Color Background = new Color(0.55f, 0.75f, 0.95f);
        public static readonly Color Asphalt = new Color(0.32f, 0.34f, 0.38f);
        public static readonly Color Warning = new Color(1.0f, 0.82f, 0.15f);
        public static readonly Color Shelf = new Color(0.55f, 0.6f, 0.68f);
        public static readonly Color Lane = new Color(0.86f, 0.87f, 0.88f);
    }
}
