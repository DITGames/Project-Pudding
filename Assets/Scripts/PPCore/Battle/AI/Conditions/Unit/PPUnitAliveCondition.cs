/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPUnitAliveCondition.cs
 * @author hqrse
 * @date 2026/08/11
 * @brief ユニット条件 : 行動可能かどうか
 * =====================================*/

using System;
using CommandBattleCore;
using UnityEngine;
using AttributeUtility;

namespace PPCore
{
    // ユニット条件: 生存していて行動制限も掛かっていないか
    // 実行者の抽出時に戦術側で既に同じ判定を通しているため、
    // 主な用途は「達成済み判定条件」で対象の生存を確かめるケースになる
    [Serializable]
    [PPTypeMenuName("ユニット状態/行動可能")]
    public sealed class PPUnitAliveCondition : PPUnitConditionValidator
    {
        // 生存だけでなく麻痺などの行動不可も見るか
        [Label("行動制限も見る")]
        [SerializeField] private bool mIsCheckRestriction = true;
        // 反転すると「行動できない」の判定になる
        [Label("条件を反転する")]
        [SerializeField] private bool mIsInvert = false;

        // ユニットが行動可能かを判定する
        // aUnit : 判定対象のユニット
        // aSnapShot : 評価に使うパーティ状況スナップショット
        // return : 条件を満たす場合 true
        public override bool Evaluate(PPBattleUnit aUnit, PPPartyAIContext aSnapShot)
        {
            bool canAct = aUnit != null && aUnit.IsAlive;
            if (canAct && mIsCheckRestriction)
            {
                canAct = (aUnit.CurrentRestrictions & ActionRestriction.CannotAct) == 0;
            }
            return canAct != mIsInvert;
        }

        // 設定内容から説明文を組み立てる
        [ContextMenu("説明文を生成")]
        protected override void BuildDescription()
        {
            string target = mIsCheckRestriction ? "行動できる" : "生存している";
            mDescription = mIsInvert ? target.Replace("できる", "できない").Replace("している", "していない") : target;
        }
    }
}
