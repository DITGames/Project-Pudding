/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPStatusApplySkillEffectDefinition.cs
 * @author hqrse
 * @date 2026/08/06
 * @brief StatusEffect付与型スキルエフェクトの定義
 * =====================================*/

using System;
using CommandBattleCore;
using UnityEngine;

namespace PPCore
{
    // 内部にインラインで持つ PPEffectDefinition から StatusEffect を生成し、対象に付与するスキルエフェクト
    // [PPTypeMenuName] を持たないため、トップレベルの型選択ツリーには直接出てこない
    // StatusEffect の葉（毒・パラメータ変動）をツリーから直接選んだ際に、PPSkillEffectPickerPopup がこのクラスでラップして生成する
    [Serializable]
    public class PPStatusApplySkillEffectDefinition : PPSkillEffectDefinition
    {
        [Label("エフェクト")]
        [SerializeReference]
        private PPEffectDefinition mEffect;

        // SerializeReference のデシリアライズ用に残す
        public PPStatusApplySkillEffectDefinition() { }

        // aEffect : あらかじめ設定する StatusEffect。ツリーピッカーで StatusEffect の葉を直接選んだ際に使う
        public PPStatusApplySkillEffectDefinition(PPEffectDefinition aEffect)
        {
            mEffect = aEffect;
        }

        // aSource : スキル発動者
        // aTarget : StatusEffect を付与する対象
        // aSourceSkill : この効果を保有するスキル定義
        // aContext : バトルコンテキスト
        public override void Apply(BattleUnit aSource, BattleUnit aTarget, PPSkillDefinition aSourceSkill, BattleContext aContext)
        {
            if (mEffect == null) return;
            aTarget.AddStatusEffect(mEffect.CreateRuntimeStatusEffect(aSource, aTarget, aContext), aContext);
        }

        public override string BuildString()
            => mEffect != null ? $"StatusEffect付与：{mEffect.BuildString()}" : "StatusEffect付与（未設定）";
    }
}
