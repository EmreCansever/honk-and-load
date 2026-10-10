using UnityEngine;

namespace HonkAndLoad.Gameplay
{
    /// <summary>Kolinin rengini ve soluk gösterilip gösterilmediğini tutar.</summary>
    public class CrateInfo : MonoBehaviour
    {
        public int Color;
        public bool Dimmed;
        /// <summary>Gizli koli: öne gelene kadar gri ve soru işaretli.</summary>
        public bool Hidden;
        /// <summary>Şu an gizli görünümde mi.</summary>
        public bool ShowingHidden;
    }
}
