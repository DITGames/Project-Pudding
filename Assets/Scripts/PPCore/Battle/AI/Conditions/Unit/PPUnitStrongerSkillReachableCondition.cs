/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPUnitStrongerSkillReachableCondition.cs
 * @author hqrse
 * @date 2026/08/24
 * @brief ユニット条件 : 数ティック待てば今より強いスキルが撃てる
 * =====================================*/

using System;
using AttributeUtility;
using UnityEngine;

namespace PPCore
{
    // ユニット条件: 指定ティック数だけ待てば、今撃てるものより強いスキルへ手が届くか
    //
    // 見積もりは PPUnitGaugeForecast に委ねる
    // 手持ちのコインゲージで通常攻撃を何回撃てるかを数え、
    // その回数ぶんのスキルゲージ回復で上位スキルのコストへ届くかを見る
    // 「今は弱いスキルを撃たずに通常攻撃で溜める」という判断の根拠になる
    //
    // 今撃てるスキルが 1 つも無い場合も、上位スキルへ届くなら成立する（溜める価値があるため）
    [Serializable]
    [PPTypeMenuName("スキル/待てば強いスキルが撃てる")]
    public sealed class PPUnitStrongerSkillReachableCondition : PPUnitConditionValidator
    {
        [Label("対象スキル")]
        [SerializeField] private PPUnitAISkillFilter mFilter = new();
        [Label("待てるティック数")]
        [SerializeField] private int mAllowedWaitTicks = 3;
        // この差より AI スコアが上でなければ「強い」とみなさない
        // わずかな差のために手を止めるのを防ぐ
        [Label("必要なAIスコア差")]
        [SerializeField] private float mRequiredScoreGain = 0.01f;
        // 反転すると「待っても届かない」の判定になる
        [Label("条件を反転する")]
        [SerializeField] private bool mIsInvert = false;

        // 待てば今より強いスキルへ届くかを判定する
        // aUnit : 判定対象のユニット
        // aSnapShot : 評価に使うパーティ状況スナップショット
        // return : 条件を満たす場合 true
        public override bool Evaluate(PPBattleUnit aUnit, PPPartyAIContext aSnapShot)
        {
            if (aUnit == null) return false;

            return IsReachable(aUnit, aSnapShot) != mIsInvert;
        }

        // 待機で手が届く上位スキルがあるかを調べる
        // aUnit : 判定対象のユニット
        // aSnapShot : 評価に使うパーティ状況スナップショット
        // return : 届く上位スキルがあれば true
        private bool IsReachable(PPBattleUnit aUnit, PPPartyAIContext aSnapShot)
        {
            var rules = aSnapShot.Context.Rules as PPBattleRules;

            // 今撃てる中で最も強いものを基準にする。1 つも撃てないなら基準スコアは持たない
            var (_, current) = PPUnitAISkillQuery.SelectCastable(aUnit, mFilter,
                PPUnitAISkillSelectRule.HighestAIScore, aSnapShot.Context, aSnapShot.Ledger);
            float currentScore = current?.AIScore ?? float.NegativeInfinity;

            foreach (var (skill, definition) in PPUnitAISkillQuery.Enumerate(aUnit, mFilter))
            {
                // 今のゲージで足りているものは「待つ対象」ではない
                if (PPGaugeUtility.CanPay(aUnit.ExtraParameters.SkillGauge, definition.SkillGaugeCost)) continue;
                // クールダウンや使用回数で撃てないものは、ゲージが溜まっても撃てないので除く
                if (!skill.IsReady) continue;
                // 待つ価値があるだけ強くなければ見送る
                if (definition.AIScore - currentScore < mRequiredScoreGain) continue;

                int ticks = PPUnitGaugeForecast.EstimateTicksToReach(aUnit, rules, definition.SkillGaugeCost,
                    Mathf.Max(1, mAllowedWaitTicks));
                if (ticks >= 0) return true;
            }
            return false;
        }

        // 設定内容から説明文を組み立てる
        [ContextMenu("説明文を生成")]
        protected override void BuildDescription()
        {
            string filter = mFilter.ToDisplayString();
            mDescription = mIsInvert
                ? $"{mAllowedWaitTicks}ティック待っても {filter} の強いスキルに届かない"
                : $"{mAllowedWaitTicks}ティック待てば {filter} の強いスキルが撃てる";
        }
    }
}
