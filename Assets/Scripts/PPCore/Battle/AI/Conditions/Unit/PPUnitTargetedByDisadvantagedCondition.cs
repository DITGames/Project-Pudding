/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPUnitTargetedByDisadvantagedCondition.cs
 * @author hqrse
 * @date 2026/08/27
 * @brief ユニット条件 : 不利属性の敵に狙われている
 * =====================================*/

using System;
using AttributeUtility;
using UnityEngine;

namespace PPCore
{
    // ユニット条件: 直近に自分へダメージを与えた敵が、自分に対して有利な属性か
    //
    // 「不利属性の敵がいる」との違いは、実際に自分が殴られているかどうかを見る点
    // 相性の悪い相手が居ても狙われていなければ動かない、という書き分けができる
    //
    // 判定にはバトル中の見聞き（PPUnitAIBlackboard）が要る
    // 思考ルーチンへバトルの参照が渡っていない場合は記録が無く、常に不成立になる
    // 反射ダメージのように発生元を持たないダメージも加害者の記録に残らないため、判定に効かない
    [Serializable]
    [PPTypeMenuName("戦況/不利属性の敵に狙われている")]
    public sealed class PPUnitTargetedByDisadvantagedCondition : PPUnitConditionValidator
    {
        // 直近の被弾をさかのぼって見るティック数。0 なら経過を問わない
        [Label("さかのぼるティック数")]
        [SerializeField] private int mWithinTicks = 1;
        // 反転すると「不利属性の敵に狙われていない」の判定になる
        [Label("条件を反転する")]
        [SerializeField] private bool mIsInvert = false;

        // 直近の加害者が自分に対して有利な属性かを判定する
        // aUnit : 判定対象のユニット
        // aSnapShot : 評価に使うパーティ状況スナップショット
        // return : 条件を満たす場合 true
        public override bool Evaluate(PPBattleUnit aUnit, PPPartyAIContext aSnapShot)
        {
            if (aUnit == null) return false;

            bool isTargeted = IsTargetedByDisadvantaged(aUnit, aSnapShot);
            return isTargeted != mIsInvert;
        }

        // 設定内容から説明文を組み立てる
        [ContextMenu("説明文を生成")]
        protected override void BuildDescription()
        {
            string range = mWithinTicks > 0 ? $"直近{mWithinTicks}ティック以内に" : "";
            string body = $"{range}不利属性の敵に狙われている";
            mDescription = mIsInvert ? body.Replace("狙われている", "狙われていない") : body;
        }

        // 直近の加害者が不利属性かを調べる
        // aUnit : 判定対象のユニット
        // aSnapShot : 評価に使うパーティ状況スナップショット
        // return : 不利属性の敵に狙われていれば true
        private bool IsTargetedByDisadvantaged(PPBattleUnit aUnit, PPPartyAIContext aSnapShot)
        {
            var blackboard = aSnapShot.GetBlackboard(aUnit);
            var attacker = blackboard?.LastAttacker;
            if (attacker == null || !attacker.IsAlive) return false;

            // 期間指定がある場合は、その範囲内の被弾だけを見る
            if (mWithinTicks > 0 && !blackboard.IsDamagedWithin(aSnapShot.Context.TurnCount, mWithinTicks))
                return false;

            return PPAttributeAffinity.Resolve(attacker.TypeAttribute, aUnit.TypeAttribute) == PPAffinityResult.Weak;
        }
    }
}
