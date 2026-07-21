/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPPartyConditionValidator.cs
 * @author hqrse
 * @date 2026/07/21
 * @brief パーティAI状況条件の基底クラス
 * =====================================*/

using CommandBattleCore;
using UnityEngine;

namespace PPCore
{
    public abstract class PPPartyConditionValidator : ScriptableObject
    {
        [Header("表示")]
        [Label("説明")]
        [TextArea]
        [SerializeField] protected string mDescription;
        
        public string Description => mDescription;
        
        // 現在のスナップショットに対して条件を満たすか判定
        public abstract bool Evaluate(PPPartyAIContext aSnapShot);
        
        protected virtual string GetOpString(PPCompareOp aOp)
            => aOp switch
            {
                PPCompareOp.Equal => "等しい",
                PPCompareOp.NotEqual => "等しくない",
                PPCompareOp.GreaterOrEqual => "以上",
                PPCompareOp.LessOrEqual => "以下",
                PPCompareOp.GreaterThan => "より多い",
                PPCompareOp.LessThan => "未満",
                _ => ""
            };

        protected virtual void BuildString()
        {
        }
        
        protected string GetResourceTypeString(PPResourceType aType) 
            => aType switch
            {
                PPResourceType.Normal => PPResource.TypeNormal,
                PPResourceType.Fire => PPResource.TypeFire,
                PPResourceType.Water => PPResource.TypeWater,
                PPResourceType.Earth => PPResource.TypeEarth,
                PPResourceType.Shine => PPResource.TypeShine,
                PPResourceType.Dark => PPResource.TypeDark,
                _ => ""
            };
    }
}