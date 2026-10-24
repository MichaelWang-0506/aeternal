using UnityEngine;

namespace Aeternal.Status
{
    [CreateAssetMenu(menuName = "Aeternal/Status/StatusEffect")]
    public class StatusEffect : ScriptableObject
    {
        public string id;
        public string displayName;
        [TextArea] public string description;
        public int defaultDuration = 1;
        // Add fields for specific behavior (tick damage, modifiers, etc.)
    }
}
