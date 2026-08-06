/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPResourceGainSkillEffectDefinition.cs
 * @author hqrse
 * @date 2026/08/06
 * @brief リソース追加型スキルエフェクトの定義
 * =====================================*/

using System;
using CommandBattleCore;
using UnityEngine;

namespace PPCore
{
    // 対象が所属するパーティの行動リソースプールへ、指定属性・量のリソースを加算するスキルエフェクト
    // ApplyTarget = 発動者 の場合は aTarget が発動者自身になるため、発動者側のパーティへ加算される
    [Serializable]
    [PPTypeMenuName("リソース追加")]
    public class PPResourceGainSkillEffectDefinition : PPSkillEffectDefinition
    {
        [Label("付与するリソース")]
        [SerializeField] private PPTypeAttribute mType = PPTypeAttribute.Normal;
        [Label("付与量")]
        [SerializeField] private float mAmount = 0f;

        // aSource : スキル発動者
        // aTarget : リソースを付与する対象。所属パーティのリソースプールへ加算する
        // aSourceSkill : この効果を保有するスキル定義
        // aContext : バトルコンテキスト
        public override void Apply(BattleUnit aSource, BattleUnit aTarget, PPSkillDefinition aSourceSkill, BattleContext aContext)
        {
            (aContext.GetParty(aTarget.Side) as PPBattleParty)?.ResourcePool.Add(mType, mAmount);
        }

        public override string BuildString()
            => $"リソース追加：{mType} {mAmount}";
    }
}
