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
    // 通常攻撃コマンド。解決した全対象に対し、命中・クリティカル判定を経てダメージを与える
    // ダメージ計算式は DamageFormula として差し替え可能な static デリゲートにしてある
    public class AttackCommand : BattleCommandBase
    {
        // 通常攻撃のダメージ計算式（攻撃側, 防御側）
        // 既定は「攻撃力 - 防御力 × 0.5」で、最低 1 ダメージを保証する
        // static なので差し替えるとゲーム全体に効く
        public static Func<BattleUnit, BattleUnit, float> DamageFormula { get; set; } =
            (src, tgt) =>
                Mathf.Max(1f, src.Parameters.Attack.CurrentValue - tgt.Parameters.Defense.CurrentValue * 0.5f);

        // aSource : 攻撃するユニット
        // aResolver : 対象を決めるリゾルバ
        public AttackCommand(BattleUnit aSource, ITargetResolver aResolver) : base(aSource, aResolver) {}

        // 対象ごとにダメージ情報を組み立て、命中判定 → クリティカル補正 → 適用の順に処理する
        // ミスの場合はダメージ量を 0 に落としたうえで適用まで通し、
        // 「外れた」という結果を購読側へ届ける
        // aContext : 実行時のバトルコンテキスト
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

    // スキル使用コマンド
    public class SkillCommand : BattleCommandBase
    {
        // 使用するスキル
        public BattleSkill Skill { get; }

        // aSource : スキルを使用するユニット
        // aSkill : 使用するスキル
        // aResolverOverride : 対象を明示指定する場合のリゾルバ。null ならスキル既定を使う
        public SkillCommand(BattleUnit aSource, BattleSkill aSkill, ITargetResolver aResolverOverride = null)
            : base(aSource, aResolverOverride ?? aSkill.DefaultTargetResolver)
        {
            Skill = aSkill;
        }

        // 対象解決 → 発動可否検証 → スキル実行 → 使用記録の順に処理する
        // 対象不在または発動条件を満たさない場合は理由付きで通知して中止する
        // 対象は冒頭で 1 回だけ解決し、その結果をそのままスキルへ渡す
        // リゾルバを再度直接呼ぶと BattleContext.ResolveTargets を経由せず
        // ITargetFilter が適用されないため、解決は 1 回に統一すること
        // aContext : 実行時のバトルコンテキスト
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

            // スキル実行
            Skill.Execute(Source, targets, aContext);
            Skill.NotifyUsed();
        }
    }

    // アイテムの効果本体。アイテム定義はコア層では型を決めないため、
    // 使用時の振る舞いだけをこのインターフェースで受け取る
    public interface IItemEffect
    {
        // アイテムを使用する
        // aSource : 使用者
        // aTargets : 解決済みの対象リスト
        // aContext : バトルコンテキスト
        void Use(BattleUnit aSource, System.Collections.Generic.List<BattleUnit> aTargets, BattleContext aContext);
    }

    // アイテム使用コマンド。効果の中身は IItemEffect へ完全に委譲する
    public class ItemCommand : BattleCommandBase
    {
        // 使用するアイテムの効果
        public IItemEffect Item { get; }

        // aSource : アイテムを使用するユニット
        // aItem : 使用するアイテムの効果
        // aResolver : 対象を決めるリゾルバ
        public ItemCommand(BattleUnit aSource, IItemEffect aItem, ITargetResolver aResolver) : base(aSource, aResolver)
            => Item = aItem;

        // 対象を解決してアイテム効果を実行する
        // aContext : 実行時のバトルコンテキスト
        public override void Execute(BattleContext aContext)
            => Item.Use(Source, TargetResolver.Resolve(Source, aContext), aContext);
    }

    // メンバー入れ替えコマンド。アクティブのユニットを控えのユニットと交代させる
    public class SwapCommand : BattleCommandBase
    {
        // 参戦させる控えのユニット
        public BattleUnit ReserveUnit { get; }

        // aOutUnit : 退場させるアクティブメンバー。コマンドの実行主体でもある
        // aInUnit : 参戦させる控えメンバー
        public SwapCommand(BattleUnit aOutUnit, BattleUnit aInUnit) : base(aOutUnit, new SelfResolver())
        {
            ReserveUnit = aInUnit;
        }

        // 入れ替えを実行する。入れ替え不可の状態異常が掛かっている場合は何もしない
        // aContext : 実行時のバトルコンテキスト
        public override void Execute(BattleContext aContext)
        {
            if ((Source.CurrentRestrictions & ActionRestriction.CannotSwap) != 0) return;
            aContext.GetParty(Source.Side).SwapMember(Source, ReserveUnit);
        }
    }

    // 逃走コマンド。成功するとコンテキストに逃走フラグを立て、勝敗判定側が戦闘を終了させる
    public class EscapeCommand : BattleCommandBase
    {
        // 逃走成功判定の式（逃走ユニット, コンテキスト）。既定は必ず成功
        // static なので差し替えるとゲーム全体に効く
        public static Func<BattleUnit, BattleContext, bool> EscapeFormula { get; set; } = (_, _) => true;

        // aSource : 逃走するユニット
        public EscapeCommand(BattleUnit aSource) : base(aSource, new SelfResolver()){}

        // 逃走を試みる。逃走不可の状態異常が掛かっている場合、
        // および判定に失敗した場合はフラグを立てず何も起こらない
        // aContext : 実行時のバトルコンテキスト
        public override void Execute(BattleContext aContext)
        {
            if ((Source.CurrentRestrictions & ActionRestriction.CannotEscape) != 0) return;
            if (EscapeFormula(Source, aContext))
                aContext.EscapeRequested = true;
        }
    }
}
