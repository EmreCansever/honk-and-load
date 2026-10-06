using UnityEngine;

namespace HonkAndLoad.Level
{
    /// <summary>
    /// Önce Resources/Levels/level_XXX.json dosyasını arar (elle tasarlanmış bölümler),
    /// yoksa LevelGenerator ile üretir.
    /// </summary>
    public static class LevelLoader
    {
        public static LevelData Load(int levelNumber)
        {
            var asset = Resources.Load<TextAsset>($"Levels/level_{levelNumber:000}");
            if (asset != null)
            {
                LevelData data = JsonUtility.FromJson<LevelData>(asset.text);
                if (data != null && data.Validate(out string error)) return data;
                Debug.LogWarning($"[HonkAndLoad] level_{levelNumber:000}.json geçersiz: {error}. Otomatik bölüm üretiliyor.");
            }
            return LevelGenerator.Generate(levelNumber);
        }
    }
}
