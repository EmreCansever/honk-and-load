using UnityEditor;

namespace HonkAndLoad.EditorTools
{
    /// <summary>
    /// Unity menüsüne "Honk & Load → Reklam Videosu Kaydet" ekler.
    /// Oyunu video modunda başlatır; kayıt bitince Play modu kendiliğinden kapanır.
    /// Kayıtlar proje klasöründe Recordings/ altına yazılır.
    /// </summary>
    public static class VideoCaptureMenu
    {
        public const string RecordKey = "hal_record_video";

        // Senaryo numaraları GameController.VideoScenario ile aynı
        [MenuItem("Honk & Load/Reklam: Sonsuz – Rekoru Geç")]
        private static void RecordEndlessRecord() => Record(1);

        [MenuItem("Honk & Load/Reklam: Sonsuz – Kaybetme")]
        private static void RecordEndlessFail() => Record(2);

        [MenuItem("Honk & Load/Reklam: Macera")]
        private static void RecordAdventure() => Record(0);

        private static void Record(int scenario)
        {
            if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
            SessionState.SetInt("hal_video_scenario", scenario);
            SessionState.SetBool(RecordKey, true);
            EditorApplication.isPlaying = true;
        }

        [MenuItem("Honk & Load/Ekran Taraması (seçili cihaz)")]
        private static void ScreenSweep()
        {
            if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
            SessionState.SetBool("hal_screen_sweep", true);
            EditorApplication.isPlaying = true;
        }

        [MenuItem("Honk & Load/Video Önizle (Sonsuz – Rekor, kayıtsız)")]
        private static void Preview()
        {
            if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
            SessionState.SetInt("hal_video_scenario", 1);
            SessionState.SetBool(RecordKey + "_preview", true);
            EditorApplication.isPlaying = true;
        }
    }
}
