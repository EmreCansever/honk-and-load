using UnityEngine;

namespace HonkAndLoad.Core
{
    /// <summary>
    /// Başka bir nesnenin StopAllCoroutines çağrısından etkilenmeyen coroutine'ler için boş taşıyıcı.
    /// </summary>
    public class CoroutineHost : MonoBehaviour
    {
        public static CoroutineHost Create(string name)
        {
            var go = new GameObject(name);
            return go.AddComponent<CoroutineHost>();
        }
    }
}
