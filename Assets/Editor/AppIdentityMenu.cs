using UnityEditor;
using UnityEngine;

namespace HonkAndLoad.EditorTools
{
    /// <summary>
    /// "Honk & Load → Uygulama İkonu ve Açılış Ekranı" menüsü: Assets/Art/AppIcon içindeki
    /// görselleri Player Settings'e uygular (Android uyarlanabilir/yuvarlak/eski ikon,
    /// varsayılan ikon, açılış ekranı logosu ve arka plan rengi).
    /// Görseller kodla üretildi; kaynak: Store/README.md.
    /// </summary>
    public static class AppIdentityMenu
    {
        private const string Dir = "Assets/Art/AppIcon/";
        private static readonly Color SplashBackground = new Color32(0xFF, 0xA8, 0x3A, 0xFF);

        [MenuItem("Honk & Load/Uygulama İkonu ve Açılış Ekranı")]
        public static void Apply()
        {
            Texture2D master = Load("icon_master_1024.png", false);
            Texture2D bg = Load("adaptive_bg_432.png", false);
            Texture2D fg = Load("adaptive_fg_432.png", false);
            Texture2D round = Load("icon_round_432.png", false);
            Texture2D legacy = Load("icon_legacy_432.png", false);
            Sprite logo = LoadSprite("splash_logo.png");

            if (master == null || bg == null || fg == null || round == null || legacy == null || logo == null)
            {
                Debug.LogError("[Honk & Load] İkon görselleri bulunamadı: " + Dir);
                return;
            }

            // Tüm platformlar için varsayılan ikon
            PlayerSettings.SetIcons(UnityEditor.Build.NamedBuildTarget.Unknown, new[] { master }, IconKind.Any);

            // Android ikonları (Android modülü kurulu değilse bu adım atlanır)
            int androidKinds = 0;
#pragma warning disable 618
            PlatformIconKind[] kinds = PlayerSettings.GetSupportedIconKindsForPlatform(BuildTargetGroup.Android);
            foreach (PlatformIconKind kind in kinds)
            {
                string name = kind.ToString();
                PlatformIcon[] icons = PlayerSettings.GetPlatformIcons(BuildTargetGroup.Android, kind);
                foreach (PlatformIcon icon in icons)
                {
                    if (name.Contains("Adaptive")) icon.SetTextures(bg, fg);
                    else if (name.Contains("Round")) icon.SetTexture(round);
                    else icon.SetTexture(legacy);
                }
                PlayerSettings.SetPlatformIcons(BuildTargetGroup.Android, kind, icons);
                androidKinds++;
            }
#pragma warning restore 618

            // Açılış ekranı: turuncu zemin + kamyon ve oyun adı, Unity logosu yok
            PlayerSettings.SplashScreen.show = true;
            PlayerSettings.SplashScreen.showUnityLogo = false;
            PlayerSettings.SplashScreen.backgroundColor = SplashBackground;
            PlayerSettings.SplashScreen.drawMode = PlayerSettings.SplashScreen.DrawMode.AllSequential;
            PlayerSettings.SplashScreen.animationMode = PlayerSettings.SplashScreen.AnimationMode.Dolly;
            PlayerSettings.SplashScreen.logos = new[] { PlayerSettings.SplashScreenLogo.Create(1.6f, logo) };

            AssetDatabase.SaveAssets();
            Debug.Log($"[Honk & Load] İkon ve açılış ekranı uygulandı (Android ikon türü: {androidKinds}).");
        }

        private static Texture2D Load(string file, bool sprite)
        {
            string path = Dir + file;
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return null;
            bool changed = false;
            TextureImporterType type = sprite ? TextureImporterType.Sprite : TextureImporterType.Default;
            if (importer.textureType != type) { importer.textureType = type; changed = true; }
            if (importer.mipmapEnabled) { importer.mipmapEnabled = false; changed = true; }
            if (importer.textureCompression != TextureImporterCompression.Uncompressed)
            {
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                changed = true;
            }
            if (!importer.alphaIsTransparency) { importer.alphaIsTransparency = true; changed = true; }
            if (changed) importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static Sprite LoadSprite(string file)
        {
            if (Load(file, true) == null) return null;
            return AssetDatabase.LoadAssetAtPath<Sprite>(Dir + file);
        }
    }
}
