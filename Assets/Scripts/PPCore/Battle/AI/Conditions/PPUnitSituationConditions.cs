/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPUnitSituationConditions.cs
 * @author hqrse
 * @date 2026/08/24
 * @brief ユニット条件（戦況）
 * =====================================*/

using System;
using UnityEngine;
using AttributeUtility;

namespace PPCore
{
    // ユニット条件: このユニットの属性で弱点を突ける敵がいるか
    // 相性の判定は PPAttributeAffinity に委ねる
    [Serializable]
    [PPTypeMenuName("戦況/弱点を突ける敵がいる")]
    public sealed class PPUnitCanExploitWeaknessCondition : PPUnitConditionValidator
    {
        // 反転すると「弱点を突ける相手が居ない」の判定になる
        [Label("条件を反転する")]
        [SerializeField] private bool mIsInvert = false;

        // 弱点を突ける敵が居るかを判定する
        public override bool Evaluate(PPBattleUnit aUnit, PPPartyAIContext aSnapShot)
        {
            if (aUnit == null) return false;

            bool isFound = false;
            foreach (var enemy in aSnapShot.AliveEnemies)
            {
                if (PPAttributeAffinity.Resolve(aUnit.TypeAttribute, enemy.TypeAttribute) != PPAffinityResult.Weak)
                    continue;

                isFound = true;
                break;
            }
            return isFound != mIsInvert;
        }

        // 設定内容から説明文を組み立てる
        [ContextMenu("説明文を生成")]
        protected override void BuildDescription()
            => mDescription = mIsInvert ? "弱点を突ける敵がいない" : "弱点を突ける敵がいる";
    }

    // ユニット条件: 自分に対して有利な属性を持つ敵が生存しているか
    // 相性の判定は PPAttributeAffinity に委ねる
    [Serializable]
    [PPTypeMenuName("戦況/不利属性の敵がいる")]
    public sealed class PPUnitDisadvantagedEnemyCondition : PPUnitConditionValidator
    {
        // 反転すると「不利な相手が居ない」の判定になる
        [Label("条件を反転する")]
        [SerializeField] private bool mIsInvert = false;

        // 自分を弱点として突ける敵が居るかを判定する
        public override bool Evaluate(PPBattleUnit aUnit, PPPartyAIContext aSnapShot)
        {
            if (aUnit == null) return false;

            bool isFound = false;
            foreach (var enemy in aSnapShot.AliveEnemies)
            {
                // 攻撃側が敵、防御側が自分。敵から見て自分が弱点なら不利な相手
                if (PPAttributeAffinity.Resolve(enemy.TypeAttribute, aUnit.TypeAttribute) != PPAffinityResult.Weak)
                    continue;

                isFound = true;
                break;
            }
            return isFound != mIsInvert;
        }

        // 設定内容から説明文を組み立てる
        [ContextMenu("説明文を生成")]
        protected override void BuildDescription()
            => mDescription = mIsInvert ? "不利属性の敵がいない" : "不利属性の敵がいる";
    }

    // ユニット条件: 直近に自分へダメージを与えた敵が、自分に対して有利な属性か
    // PPUnitAIBlackboard の記録が無い場合や、発生元を持たないダメージでは成立しない
    [Serializable]
    [PPTypeMenuName("戦況/不利属性の敵に狙われている")]
    public sealed class PPUnitTargetedByDisadvantagedCondition : PPUnitConditionValidator
    {
        /* インスペクタ設定 */

        // 直近の被弾をさかのぼって見るティック数。0 なら経過を問わない
        [Label("さかのぼるティック数")]
        [SerializeField] private int mWithinTicks = 1;
        // 反転すると「不利属性の敵に狙われていない」の判定になる
        [Label("条件を反転する")]
        [SerializeField] private bool mIsInvert = false;

        /* 評価・判定 */

        // 直近の加害者が自分に対して有利な属性かを判定する
        public override bool Evaluate(PPBattleUnit aUnit, PPPartyAIContext aSnapShot)
        {
            if (aUnit == null) return false;

            bool isTargeted = IsTargetedByDisadvantaged(aUnit, aSnapShot);
            return isTargeted != mIsInvert;
        }

        /* 問い合わせ */

        // 直近の加害者が不利属性かを調べる
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

        /* 説明文 */

        // 設定内容から説明文を組み立てる
        [ContextMenu("説明文を生成")]
        protected override void BuildDescription()
        {
            string range = mWithinTicks > 0 ? $"直近{mWithinTicks}ティック以内に" : "";
            string body = $"{range}不利属性の敵に狙われている";
            mDescription = mIsInvert ? body.Replace("狙われている", "狙われていない") : body;
        }
    }

    // ユニット条件: このユニットの通常攻撃 1 回で倒しきれる敵がいるか
    // 見積もりに命中・クリティカルの乱数は含めない
    [Serializable]
    [PPTypeMenuName("戦況/通常攻撃で倒せる敵がいる")]
    public sealed class PPUnitCanFinishEnemyCondition : PPUnitConditionValidator
    {
        // 反転すると「とどめを刺せる相手が居ない」の判定になる
        [Label("条件を反転する")]
        [SerializeField] private bool mIsInvert = false;

        // 通常攻撃で倒せる敵が居るかを判定する
        public override bool Evaluate(PPBattleUnit aUnit, PPPartyAIContext aSnapShot)
        {
            if (aUnit == null) return false;

            bool isFound = false;
            foreach (var enemy in aSnapShot.AliveEnemies)
            {
                if (PPDamageUtility.ResolveAttackDamage(aUnit, enemy) < enemy.Parameters.Hp.Current) continue;

                isFound = true;
                break;
            }
            return isFound != mIsInvert;
        }

        // 設定内容から説明文を組み立てる
        [ContextMenu("説明文を生成")]
        protected override void BuildDescription()
            => mDescription = mIsInvert ? "通常攻撃で倒せる敵がいない" : "通常攻撃で倒せる敵がいる";
    }

    // ユニット条件: 実行待ちの行動に、指定条件に合う敵の行動が積まれているか
    // IPPPendingActionSource が無い場合は不成立。敵の思考が後に走ると読めないため、読めたときだけ反応する条件として使う
    [Serializable]
    [PPTypeMenuName("戦況/敵の次の行動")]
    public sealed class PPUnitEnemyPendingActionCondition : PPUnitConditionValidator, IPPUnitAISkillFilterOwner
    {
        /* インスペクタ設定 */

        // スキルで絞り込むか。false なら「何らかの行動が積まれているか」の判定になる
        [Label("スキルで絞り込む")]
        [SerializeField] private bool mIsFilterBySkill = true;
        [Label("対象スキル")]
        [EditCondition(nameof(mIsFilterBySkill), true, false)]
        [SerializeField] private PPUnitAISkillFilter mFilter = new();
        // 反転すると「そういう行動が積まれていない」の判定になる
        [Label("条件を反転する")]
        [SerializeField] private bool mIsInvert = false;

        /* プロパティ */

        // 保持しているスキルの絞り込み条件。エディタの診断から参照する
        // 絞り込みを使わない設定のときは、未設定でも問題にならないよう null を返す
        public PPUnitAISkillFilter Filter => mIsFilterBySkill ? mFilter : null;

        /* 評価・判定 */

        // 相手陣営の実行待ちの行動に、条件へ合うものがあるかを判定する
        public override bool Evaluate(PPBattleUnit aUnit, PPPartyAIContext aSnapShot)
        {
            if (aUnit == null) return false;

            bool isFound = HasMatchedPending(aUnit, aSnapShot);
            return isFound != mIsInvert;
        }

        /* 問い合わせ */

        // 相手陣営の実行待ちの行動を走査する
        private bool HasMatchedPending(PPBattleUnit aUnit, PPPartyAIContext aSnapShot)
        {
            var source = aSnapShot.PendingSource;
            if (source == null) return false;

            var opponent = aUnit.Side == BattleSide.Ally ? BattleSide.Enemy : BattleSide.Ally;
            foreach (var pending in source.EnumeratePending(opponent))
            {
                if (!mIsFilterBySkill) return true;

                // 通常攻撃のようにスキル定義を持たない行動は、スキルでの絞り込みには掛からない
                var definition = (pending.Command as SkillCommand)?.Skill?.SourceDefinition as PPSkillDefinition;
                if (mFilter.IsMatch(definition)) return true;
            }
            return false;
        }

        /* 説明文 */

        // 設定内容から説明文を組み立てる
        [ContextMenu("説明文を生成")]
        protected override void BuildDescription()
        {
            string body = mIsFilterBySkill
                ? $"敵が {mFilter.ToDisplayString()} のスキルを撃とうとしている"
                : "敵が行動を積んでいる";
            mDescription = mIsInvert ? $"{body} 状態ではない" : body;
        }
    }

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

    // ユニット条件: 直前に指定した出来事が起きたか（「殴られたら反撃する」など）
    // PPUnitAIBlackboard の記録が無い場合は常に不成立になる
    [Serializable]
    [PPTypeMenuName("戦況/直前の出来事")]
    public sealed class PPUnitRecentEventCondition : PPUnitConditionValidator
    {
        /* インスペクタ設定 */

        [Label("出来事")]
        [SerializeField] private PPUnitRecentEventKind mKind = PPUnitRecentEventKind.Damaged;
        // さかのぼって見るティック数。0 を指定すると「バトル中に一度でも起きたか」になる
        [Label("さかのぼるティック数")]
        [SerializeField] private int mWithinTicks = 1;
        // 反転すると「起きていない」の判定になる
        [Label("条件を反転する")]
        [SerializeField] private bool mIsInvert = false;

        /* 評価・判定 */

        // 指定した出来事が直前に起きたかを判定する
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

        /* 説明文 */

        // 設定内容から説明文を組み立てる
        [ContextMenu("説明文を生成")]
        protected override void BuildDescription()
        {
            string kind = mKind == PPUnitRecentEventKind.Damaged ? "攻撃を受けた" : "味方が倒された";
            string range = mWithinTicks > 0 ? $"直近{mWithinTicks}ティック以内に" : "バトル中に一度でも";
            mDescription = mIsInvert ? $"{range}{kind}ことがない" : $"{range}{kind}";
        }
    }

    // ユニット条件: 生存している味方が自分 1 体だけか
    // 残っているのが自分かまで見るため、ツリーを共有していても本人だけが反応する
    [Serializable]
    [PPTypeMenuName("戦況/自分が最後の1体")]
    public sealed class PPUnitIsLastStandingCondition : PPUnitConditionValidator
    {
        // 反転すると「まだ味方が残っている」の判定になる
        [Label("条件を反転する")]
        [SerializeField] private bool mIsInvert = false;

        // 生存している味方が自分だけかを判定する
        public override bool Evaluate(PPBattleUnit aUnit, PPPartyAIContext aSnapShot)
        {
            if (aUnit == null) return false;

            var members = aSnapShot.AliveMembers;
            bool isLast = members.Count == 1 && members[0] == aUnit;
            return isLast != mIsInvert;
        }

        // 設定内容から説明文を組み立てる
        [ContextMenu("説明文を生成")]
        protected override void BuildDescription()
            => mDescription = mIsInvert ? "自分以外にも味方が生存している" : "生存している味方が自分だけ";
    }
}
