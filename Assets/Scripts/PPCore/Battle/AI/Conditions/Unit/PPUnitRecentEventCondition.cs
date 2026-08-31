/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPUnitRecentEventCondition.cs
 * @author hqrse
 * @date 2026/08/27
 * @brief ユニット条件 : 直前に起きた出来事
 * =====================================*/

using System;
using AttributeUtility;
using UnityEngine;

namespace PPCore
{
    // 直前に起きた出来事の種類
    public enum PPUnitRecentEventKind
    {
        // 自分がダメージを受けた
        [InspectorName("攻撃を受けた")]
        Damaged = 0,
        // 味方が倒された
        [InspectorName("味方が倒された")]
        AllyDefeated = 1,
    }

    // ユニット条件: 直前に指定した出来事が起きたか
    //
    // 「殴られたら反撃する」「味方が落ちたら回復に回る」といった、
    // 現在の盤面だけを見ていても書けない判断のためのもの
    //
    // 判定にはバトル中の見聞き（PPUnitAIBlackboard）が要る
    // 思考ルーチンへバトルの参照が渡っていない場合は記録が無く、常に不成立になる
    [Serializable]
    [PPTypeMenuName("戦況/直前の出来事")]
    public sealed class PPUnitRecentEventCondition : PPUnitConditionValidator
    {
        [Label("出来事")]
        [SerializeField] private PPUnitRecentEventKind mKind = PPUnitRecentEventKind.Damaged;
        // さかのぼって見るティック数。0 を指定すると「バトル中に一度でも起きたか」になる
        [Label("さかのぼるティック数")]
        [SerializeField] private int mWithinTicks = 1;
        // 反転すると「起きていない」の判定になる
        [Label("条件を反転する")]
        [SerializeField] private bool mIsInvert = false;

        // 指定した出来事が直前に起きたかを判定する
        // aUnit : 判定対象のユニット
        // aSnapShot : 評価に使うパーティ状況スナップショット
        // return : 条件を満たす場合 true
        public override bool Evaluate(PPBattleUnit aUnit, PPPartyAIContext aSnapShot)
        {
            if (aUnit == null) return false;

            var blackboard = aSnapShot.GetBlackboard(aUnit);
            if (blackboard == null) return mIsInvert;

            // 期間指定が無い場合は、経過を問わず一度でも起きていれば成立させる
            int turnCount = aSnapShot.Context.TurnCount;
            int within = mWithinTicks > 0 ? mWithinTicks : int.MaxValue;
            bool isHappened = mKind switch
            {
                PPUnitRecentEventKind.Damaged => blackboard.IsDamagedWithin(turnCount, within),
                PPUnitRecentEventKind.AllyDefeated => blackboard.IsAllyDefeatedWithin(turnCount, within),
                _ => false,
            };
            return isHappened != mIsInvert;
        }

        // 設定内容から説明文を組み立てる
        [ContextMenu("説明文を生成")]
        protected override void BuildDescription()
        {
            string kind = mKind == PPUnitRecentEventKind.Damaged ? "攻撃を受けた" : "味方が倒された";
            string range = mWithinTicks > 0 ? $"直近{mWithinTicks}ティック以内に" : "バトル中に一度でも";
            mDescription = mIsInvert ? $"{range}{kind}ことがない" : $"{range}{kind}";
        }
    }
}
