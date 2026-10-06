using UnityEngine;

namespace HonkAndLoad.Gameplay
{
    /// <summary>Dokunulabilir nesneleri işaretler (sütun kolisi veya raf slotu).</summary>
    public class Tappable : MonoBehaviour
    {
        public enum TapKind { Column, Buffer }
        public TapKind Kind;
        public int Index;
    }
}
