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

        [MenuItem("Honk & Load/Reklam Videosu Kaydet")]
        private static void Record()
        {
            if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
            SessionState.SetBool(RecordKey, true);
            EditorApplication.isPlaying = true;
        }

        [MenuItem("Honk & Load/Video Modunu Kayıtsız Önizle")]
        private static void Preview()
        {
            if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
            SessionState.SetBool(RecordKey + "_preview", true);
            EditorApplication.isPlaying = true;
        }
    }
}
