/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPFindHasStatusEffectUnit.cs
 * @author hqrse
 * @date 2026/07/21
 * @brief パーティ状況条件 : 状態異常を保持しているユニットがいる
 * =====================================*/

using System;
using AttributeUtility;
using CommandBattleCore;
using UnityEngine;

namespace PPCore
{
    // パーティ状況条件: 指定の状態異常・バフを持つ味方が 1 体でもいるか
    // 「バフが切れている味方がいるなら支援に回る」のような、盤面全体の状況判断に使う
    //
    // 判定のみを行い、対象の絞り込みは行わない
    // 誰を狙うかを決めるのはターゲット検索ノード（PPUnitAISearchNode）の役割で、
    // 条件が候補リストまで書き換えると、判定に使っただけのつもりが対象を変えていた、という事故が起きる
    [Serializable]
    [PPTypeMenuName("パーティ状態/状態異常を所有してるユニットがいる")]
    public class PPFindHasStatusEffectUnit : PPPartyConditionValidator
    {
        // 判定用エフェクトID。空なら ID では絞り込まない
        [Label("エフェクトID")]
        [SerializeField] private string mEffectId = "";

        // 判定用タグ。None なら タグでは絞り込まない
        [Label("エフェクトタグ")]
        [SerializeField] private StatusEffectTag mTags = StatusEffectTag.None;

        // 反転で付与されていないユニットがいるかの判定になる
        [Label("条件を反転する")]
        [SerializeField] private bool mIsInvert = false;

        // 条件に合致する味方が 1 体でもいるかを判定する
        // aSnapShot : 評価対象のパーティ状況スナップショット
        // return : 条件を満たすユニットがいれば true
        public override bool Evaluate(PPPartyAIContext aSnapShot)
        {
            foreach (var unit in aSnapShot.AliveMembers)
            {
                if (HasMatchingEffect(unit) != mIsInvert) return true;
            }
            return false;
        }

        // 条件に合致する状態異常を持っているかを走査する
        // ID とタグはどちらも未指定なら素通しになるため、両方未指定だと付与の有無だけを見る
        // aUnit : 判定対象のユニット
        // return : 合致する状態異常があれば true
        private bool HasMatchingEffect(PPBattleUnit aUnit)
        {
            foreach (var eff in aUnit.ActiveStatusEffects)
            {
                if(eff == null) continue;
                if (!string.IsNullOrEmpty(mEffectId) && eff.EffectId != mEffectId) continue;
                if(mTags != StatusEffectTag.None && (eff.Tags & mTags) == 0) continue;

                return true;
            }
            return false;
        }

        [ContextMenu("説明文を生成")]
        protected override void BuildDescription()
        {
            string target = string.IsNullOrEmpty(mEffectId)
                ? (mTags == StatusEffectTag.None ? "何らかの状態異常" : $"{mTags} の効果")
                : mEffectId;
            mDescription = mIsInvert ? $"{target}が付与されているユニットがいない" : $"{target}が付与されているユニットがいる";
        }
    }
}
