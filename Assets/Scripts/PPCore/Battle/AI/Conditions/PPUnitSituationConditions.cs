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
    // 「相性が良いときだけ大技を切る」といった判断に使う
    // 判定は PPAttributeAffinity に委ね、相性表をここへ持ち込まない
    [Serializable]
    [PPTypeMenuName("戦況/弱点を突ける敵がいる")]
    public sealed class PPUnitCanExploitWeaknessCondition : PPUnitConditionValidator
    {
        // 反転すると「弱点を突ける相手が居ない」の判定になる
        [Label("条件を反転する")]
        [SerializeField] private bool mIsInvert = false;

        // 弱点を突ける敵が居るかを判定する
        // aUnit : 判定対象のユニット
        // aSnapShot : 評価に使うパーティ状況スナップショット
        // return : 条件を満たす場合 true
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
    //
    // 弱点を突ける敵がいるか（PPUnitCanExploitWeaknessCondition）の裏返しにあたる条件で、
    // 「相性の悪い相手が居るうちは守りに回る」「不利な相手から逃げる」といった判断に使う
    // 判定は相性表を持つ PPAttributeAffinity に委ね、ここへ相性の知識を持ち込まない
    [Serializable]
    [PPTypeMenuName("戦況/不利属性の敵がいる")]
    public sealed class PPUnitDisadvantagedEnemyCondition : PPUnitConditionValidator
    {
        // 反転すると「不利な相手が居ない」の判定になる
        [Label("条件を反転する")]
        [SerializeField] private bool mIsInvert = false;

        // 自分を弱点として突ける敵が居るかを判定する
        // aUnit : 判定対象のユニット
        // aSnapShot : 評価に使うパーティ状況スナップショット
        // return : 条件を満たす場合 true
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

    // ユニット条件: このユニットの通常攻撃 1 回で倒しきれる敵がいるか
    // 「とどめを刺せるならスキルを温存して殴る」という判断の入口に使う
    // 見積もりに命中・クリティカルの乱数は含めないため、実際には落としきれないこともある
    [Serializable]
    [PPTypeMenuName("戦況/通常攻撃で倒せる敵がいる")]
    public sealed class PPUnitCanFinishEnemyCondition : PPUnitConditionValidator
    {
        // 反転すると「とどめを刺せる相手が居ない」の判定になる
        [Label("条件を反転する")]
        [SerializeField] private bool mIsInvert = false;

        // 通常攻撃で倒せる敵が居るかを判定する
        // aUnit : 判定対象のユニット
        // aSnapShot : 評価に使うパーティ状況スナップショット
        // return : 条件を満たす場合 true
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
    //
    // 「相手が大技を構えたら防御に回る」「回復を撃たれる前に潰す」といった、
    // 相手の手を読んで動く判断のためのもの
    //
    // 行動はティック終了時にまとめて積まれるため、思考時点ではコマンド列が空になっている
    // そのため判定にはバトルの進行役が握っている実行待ちの行動（IPPPendingActionSource）が要る
    // 供給元が差し込まれていない場合は読む材料が無く、常に不成立になる
    //
    // 敵側の思考が自分より後に走った場合、その手はまだ計画に無い
    // 「必ず読める」ものではなく「読めたときだけ反応する」条件として使うこと
    [Serializable]
    [PPTypeMenuName("戦況/敵の次の行動")]
    public sealed class PPUnitEnemyPendingActionCondition : PPUnitConditionValidator, IPPUnitAISkillFilterOwner
    {
        // スキルで絞り込むか。false なら「何らかの行動が積まれているか」の判定になる
        [Label("スキルで絞り込む")]
        [SerializeField] private bool mIsFilterBySkill = true;
        [Label("対象スキル")]
        [EditCondition(nameof(mIsFilterBySkill), true, false)]
        [SerializeField] private PPUnitAISkillFilter mFilter = new();
        // 反転すると「そういう行動が積まれていない」の判定になる
        [Label("条件を反転する")]
        [SerializeField] private bool mIsInvert = false;

        // 保持しているスキルの絞り込み条件。エディタの診断から参照する
        // 絞り込みを使わない設定のときは、未設定でも問題にならないよう null を返す
        public PPUnitAISkillFilter Filter => mIsFilterBySkill ? mFilter : null;

        // 相手陣営の実行待ちの行動に、条件へ合うものがあるかを判定する
        // aUnit : 判定対象のユニット
        // aSnapShot : 評価に使うパーティ状況スナップショット
        // return : 条件を満たす場合 true
        public override bool Evaluate(PPBattleUnit aUnit, PPPartyAIContext aSnapShot)
        {
            if (aUnit == null) return false;

            bool isFound = HasMatchedPending(aUnit, aSnapShot);
            return isFound != mIsInvert;
        }

        // 設定内容から説明文を組み立てる
        [ContextMenu("説明文を生成")]
        protected override void BuildDescription()
        {
            string body = mIsFilterBySkill
                ? $"敵が {mFilter.ToDisplayString()} のスキルを撃とうとしている"
                : "敵が行動を積んでいる";
            mDescription = mIsInvert ? $"{body} 状態ではない" : body;
        }

        // 相手陣営の実行待ちの行動を走査する
        // aUnit : 判定対象のユニット
        // aSnapShot : 評価に使うパーティ状況スナップショット
        // return : 条件へ合う行動が積まれていれば true
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

    // ユニット条件: 生存している味方が自分 1 体だけか
    //
    // 「独りになったら自己強化して粘る」「味方が居るうちは支援に徹する」といった切り替えに使う
    // 生存数の条件（PPAliveUnitCountCondition）でも数は見られるが、
    // こちらは「残っているのが自分か」まで見るため、
    // 同じツリーを複数ユニットで共有していても本人だけが反応する
    [Serializable]
    [PPTypeMenuName("戦況/自分が最後の1体")]
    public sealed class PPUnitIsLastStandingCondition : PPUnitConditionValidator
    {
        // 反転すると「まだ味方が残っている」の判定になる
        [Label("条件を反転する")]
        [SerializeField] private bool mIsInvert = false;

        // 生存している味方が自分だけかを判定する
        // aUnit : 判定対象のユニット
        // aSnapShot : 評価に使うパーティ状況スナップショット
        // return : 条件を満たす場合 true
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
