/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file StandardCommands.cs
 * @author hqrse
 * @date 2026/06/13
 * @brief 基本のコマンド実装
 * =====================================*/
using System;
using UnityEngine;

namespace CommandBattleCore
{
    /// <summary>
    /// 通常攻撃コマンド。解決した全対象に対し、命中・クリティカル判定を経てダメージを与える。
    /// <para>
    /// ダメージ計算式は <see cref="DamageFormula"/> として差し替え可能な static デリゲートにしてある。
    /// </para>
    /// </summary>
    public class AttackCommand : BattleCommandBase
    {
        /// <summary>
        /// 通常攻撃のダメージ計算式（攻撃側, 防御側）。
        /// 既定は「攻撃力 - 防御力 × 0.5」で、最低 1 ダメージを保証する。
        /// static なので差し替えるとゲーム全体に効く。
        /// </summary>
        public static Func<BattleUnit, BattleUnit, float> DamageFormula { get; set; } =
            (src, tgt) =>
                Mathf.Max(1f, src.Parameters.Attack.CurrentValue - tgt.Parameters.Defense.CurrentValue * 0.5f);

        /// <param name="aSource">攻撃するユニット。</param>
        /// <param name="aResolver">対象を決めるリゾルバ。</param>
        public AttackCommand(BattleUnit aSource, ITargetResolver aResolver) : base(aSource, aResolver) {}

        /// <summary>
        /// 対象ごとにダメージ情報を組み立て、命中判定 → クリティカル補正 → 適用の順に処理する。
        /// ミスの場合はダメージ量を 0 に落としたうえで適用まで通し、
        /// 「外れた」という結果を購読側へ届ける。
        /// </summary>
        /// <param name="aContext">実行時のバトルコンテキスト。</param>
        public override void Execute(BattleContext aContext)
        {
            foreach (var target in aContext.ResolveTargets(Source, TargetResolver))
            {
                float raw = DamageFormula(Source, target);
                var info = new DamageInfo(Source, target, raw, this);

                var hit = aContext.ResolveHit(Source, target, info);

                if (hit.mResult == HitResult.Miss)
                {
                    info.IsMiss = true;
                    info.Amount = 0f;
                }
                if (hit.mCriticalInfo.IsCritical)
                {
                    info.IsCritical = true;
                    info.Amount *= hit.mCriticalInfo.CriticalMultiplier;
                }

                target.ApplyDamage(info);
            }
        }
    }

    /// <summary>
    /// スキル使用コマンド。
    /// <para>
    /// コア層はリソースという概念を持たないため、ここではコストの消費を行わない。
    /// リソース消費が必要な場合は <see cref="PPSkillCommand"/> のように派生側で実装する。
    /// </para>
    /// </summary>
    public class SkillCommand : BattleCommandBase
    {
        /// <summary>使用するスキル。</summary>
        public BattleSkill Skill { get; }

        /// <param name="aSource">スキルを使用するユニット。</param>
        /// <param name="aSkill">使用するスキル。</param>
        /// <param name="aResolverOverride">対象を明示指定する場合のリゾルバ。null ならスキル既定を使う。</param>
        public SkillCommand(BattleUnit aSource, BattleSkill aSkill, ITargetResolver aResolverOverride = null)
            : base(aSource, aResolverOverride ?? aSkill.DefaultTargetResolver)
        {
            Skill = aSkill;
        }

        /// <summary>
        /// 対象解決 → 発動可否検証 → スキル実行 → 使用記録の順に処理する。
        /// 対象不在または発動条件を満たさない場合は理由付きで通知して中止する。
        /// </summary>
        /// <remarks>
        /// 対象は冒頭で 1 回だけ解決し、その結果をそのままスキルへ渡す。
        /// リゾルバを再度直接呼ぶと <see cref="BattleContext.ResolveTargets"/> を経由せず
        /// <see cref="ITargetFilter"/> が適用されないため、解決は 1 回に統一すること。
        /// </remarks>
        /// <param name="aContext">実行時のバトルコンテキスト。</param>
        public override void Execute(BattleContext aContext)
        {
            // 先にターゲット解決
            var targets = aContext.ResolveTargets(Source, TargetResolver);
            if (targets.Count == 0)
            {
                aContext.NotifyCastFailed(Source, Skill, CastFailReason.InvalidTarget);
                return;
            }

            // 発動可能?
            var validation = aContext.Rules.CastValidator.Validate(Source, Skill, aContext);
            if (!validation.CanCast)
            {
                aContext.NotifyCastFailed(Source, Skill, validation.Reason);
                return;
            }

            // プロジェクトに合わせてコストの消費

            // スキル実行
            Skill.Execute(Source, targets, aContext);
            Skill.NotifyUsed();
        }
    }

    /// <summary>
    /// アイテムの効果本体。アイテム定義はコア層では型を決めないため、
    /// 使用時の振る舞いだけをこのインターフェースで受け取る。
    /// </summary>
    public interface IItemEffect
    {
        /// <summary>
        /// アイテムを使用する。
        /// </summary>
        /// <param name="aSource">使用者。</param>
        /// <param name="aTargets">解決済みの対象リスト。</param>
        /// <param name="aContext">バトルコンテキスト。</param>
        void Use(BattleUnit aSource, System.Collections.Generic.List<BattleUnit> aTargets, BattleContext aContext);
    }

    /// <summary>
    /// アイテム使用コマンド。効果の中身は <see cref="IItemEffect"/> へ完全に委譲する。
    /// </summary>
    public class ItemCommand : BattleCommandBase
    {
        /// <summary>使用するアイテムの効果。</summary>
        public IItemEffect Item { get; }

        /// <param name="aSource">アイテムを使用するユニット。</param>
        /// <param name="aItem">使用するアイテムの効果。</param>
        /// <param name="aResolver">対象を決めるリゾルバ。</param>
        public ItemCommand(BattleUnit aSource, IItemEffect aItem, ITargetResolver aResolver) : base(aSource, aResolver)
            => Item = aItem;

        /// <summary>対象を解決してアイテム効果を実行する。</summary>
        /// <param name="aContext">実行時のバトルコンテキスト。</param>
        public override void Execute(BattleContext aContext)
            => Item.Use(Source, TargetResolver.Resolve(Source, aContext), aContext);
    }

    /// <summary>
    /// メンバー入れ替えコマンド。アクティブのユニットを控えのユニットと交代させる。
    /// </summary>
    public class SwapCommand : BattleCommandBase
    {
        /// <summary>参戦させる控えのユニット。</summary>
        public BattleUnit ReserveUnit { get; }

        /// <param name="aOutUnit">退場させるアクティブメンバー。コマンドの実行主体でもある。</param>
        /// <param name="aInUnit">参戦させる控えメンバー。</param>
        public SwapCommand(BattleUnit aOutUnit, BattleUnit aInUnit) : base(aOutUnit, new SelfResolver())
        {
            ReserveUnit = aInUnit;
        }

        /// <summary>
        /// 入れ替えを実行する。入れ替え不可の状態異常が掛かっている場合は何もしない。
        /// </summary>
        /// <param name="aContext">実行時のバトルコンテキスト。</param>
        public override void Execute(BattleContext aContext)
        {
            if ((Source.CurrentRestrictions & ActionRestriction.CannotSwap) != 0) return;
            aContext.GetParty(Source.Side).SwapMember(Source, ReserveUnit);
        }
    }

    /// <summary>
    /// 逃走コマンド。成功するとコンテキストに逃走フラグを立て、勝敗判定側が戦闘を終了させる。
    /// </summary>
    public class EscapeCommand : BattleCommandBase
    {
        /// <summary>
        /// 逃走成功判定の式（逃走ユニット, コンテキスト）。既定は必ず成功。
        /// static なので差し替えるとゲーム全体に効く。
        /// </summary>
        public static Func<BattleUnit, BattleContext, bool> EscapeFormula { get; set; } = (_, _) => true;

        /// <param name="aSource">逃走するユニット。</param>
        public EscapeCommand(BattleUnit aSource) : base(aSource, new SelfResolver()){}

        /// <summary>
        /// 逃走を試みる。逃走不可の状態異常が掛かっている場合、
        /// および判定に失敗した場合はフラグを立てず何も起こらない。
        /// </summary>
        /// <param name="aContext">実行時のバトルコンテキスト。</param>
        public override void Execute(BattleContext aContext)
        {
            if ((Source.CurrentRestrictions & ActionRestriction.CannotEscape) != 0) return;
            if (EscapeFormula(Source, aContext))
                aContext.EscapeRequested = true;
        }
    }
}
