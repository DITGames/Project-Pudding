/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPUnitStatusEffectCondition.cs
 * @author hqrse
 * @date 2026/08/11
 * @brief ユニット条件 : 状態異常・バフの付与状況
 * =====================================*/

using System;
using CommandBattleCore;
using UnityEngine;

namespace PPCore
{
    // ユニット条件: 指定の状態異常・バフが付与されているか
    // 「バフを付けてから大技を撃つ」戦術で、バフのステップが達成済みかを判定するのが主な用途
    // エフェクト ID とタグの 2 軸で絞り込み、どちらも未指定なら「何か 1 つでも付与されているか」になる
    [Serializable]
    [PPTypeMenuName("ユニット状態/状態異常の付与")]
    public sealed class PPUnitStatusEffectCondition : PPUnitConditionValidator
    {
        // 判定するエフェクト ID。空なら ID では絞り込まない
        [Label("エフェクトID")]
        [SerializeField] private string mEffectId = "";
        // 判定するタグ。None なら タグでは絞り込まない
        [Label("エフェクトタグ")]
        [SerializeField] private StatusEffectTag mTags = StatusEffectTag.None;
        // 反転すると「付与されていない」の判定になる
        [Label("条件を反転する")]
        [SerializeField] private bool mIsInvert = false;

        // 指定の状態異常が付与されているかを判定する
        // aUnit : 判定対象のユニット
        // aSnapShot : 評価に使うパーティ状況スナップショット
        // return : 条件を満たす場合 true
        public override bool Evaluate(PPBattleUnit aUnit, PPPartyAIContext aSnapShot)
            => aUnit != null && HasMatchingEffect(aUnit) != mIsInvert;

        // 条件に合致する状態異常を持っているかを走査する
        // ID とタグはどちらも未指定なら素通しになるため、両方未指定だと付与の有無だけを見る
        // aUnit : 判定対象のユニット
        // return : 合致する状態異常があれば true
        private bool HasMatchingEffect(PPBattleUnit aUnit)
        {
            foreach (var effect in aUnit.ActiveStatusEffects)
            {
                if (effect == null) continue;
                if (!string.IsNullOrEmpty(mEffectId) && effect.EffectId != mEffectId) continue;
                if (mTags != StatusEffectTag.None && (effect.Tags & mTags) == 0) continue;

                return true;
            }
            return false;
        }

        // 設定内容から説明文を組み立てる
        [ContextMenu("説明文を生成")]
        protected override void BuildDescription()
        {
            string target = string.IsNullOrEmpty(mEffectId)
                ? (mTags == StatusEffectTag.None ? "何らかの状態異常" : $"{mTags} の効果")
                : mEffectId;
            mDescription = mIsInvert ? $"{target} が付与されていない" : $"{target} が付与されている";
        }
    }
}
