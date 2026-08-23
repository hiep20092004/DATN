using System.Collections.Generic;
using UnityEngine;

namespace BorderSpawnModule
{
    [CreateAssetMenu(fileName = "BorderRuleConfig", menuName = "Data/Border Rule Config")]
    public class BorderRuleConfig : ScriptableObject
    {
        [SerializeField] List<BorderRuleEntry> borderRules = new List<BorderRuleEntry>();

        public List<BorderRuleEntry> BorderRules => borderRules;
    }
}
