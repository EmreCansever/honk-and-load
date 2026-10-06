using HonkAndLoad.Gameplay;
using UnityEngine;

namespace HonkAndLoad.Core
{
    /// <summary>
    /// Sahnede GameController yoksa otomatik oluşturur. Böylece boş bir sahnede
    /// Play'e basmak oyunu başlatmaya yeter.
    /// </summary>
    public static class GameBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            if (Object.FindAnyObjectByType<GameController>() != null) return;
            new GameObject("HonkAndLoad").AddComponent<GameController>();
        }
    }
}
