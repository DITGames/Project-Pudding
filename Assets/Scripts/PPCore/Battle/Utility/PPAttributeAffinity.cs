/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPAttributeAffinity.cs
 * @author hqrse
 * @date 2026/07/27
 * @brief 属性同士の相性解決
 * =====================================*/
using UnityEngine;

namespace PPCore
{
    public enum PPAffinityResult
    {
        Neutral,
        Weak,
        Resist,
    }
    
    public static class PPAttributeAffinity
    {
        /* 相性解決 */
        public static PPAffinityResult Resolve(PPTypeAttribute aAttackAttribute, PPTypeAttribute aDefendAttribute)
        {
            if(aAttackAttribute == PPTypeAttribute.Normal || aDefendAttribute == PPTypeAttribute.Normal)
                return PPAffinityResult.Neutral;
            if(aAttackAttribute == aDefendAttribute)
                return PPAffinityResult.Neutral;
            
            if(IsShineDarkPair(aAttackAttribute, aDefendAttribute))
                return PPAffinityResult.Weak;
            
            if(Beats(aAttackAttribute, aDefendAttribute))
                return PPAffinityResult.Weak;
            if(Beats(aDefendAttribute, aAttackAttribute))
                return PPAffinityResult.Resist;
            
            return PPAffinityResult.Neutral;
        }
        
        // 光と闇の相互関係か
        private static bool IsShineDarkPair(PPTypeAttribute aX, PPTypeAttribute aY)
            => (aX == PPTypeAttribute.Shine && aY == PPTypeAttribute.Dark)
            || (aX == PPTypeAttribute.Dark && aY == PPTypeAttribute.Shine);
        
        // 三すくみ関係か
        private static bool Beats(PPTypeAttribute aX, PPTypeAttribute aY)
            => (aX == PPTypeAttribute.Fire && aY == PPTypeAttribute.Earth)
            || (aX == PPTypeAttribute.Earth && aY == PPTypeAttribute.Water)
            || (aX == PPTypeAttribute.Water && aY == PPTypeAttribute.Fire);
    }
}