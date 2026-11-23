/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPBattleCommands.cs
 * @author hqrse
 * @date 2026/06/13
 * @brief バトルコマンド（基底・攻撃・アイテム・入れ替え・逃走）
 * =====================================*/

using System;
using System.Collections.Generic;
using UnityEngine;
using CustomConsole;

namespace PPCore
{
    // バトル中の 1 行動を表すコマンドの基底クラス。行動者（Source）と対象の決め方（TargetResolver）を持つ
    // 効果は派生クラスの Execute が実装し、ActionQueue に積まれて BattleManager が順に実行する
    public abstract class BattleCommandBase
    {
        /* 状態 */

        // このコマンドを実行するユニット
        public BattleUnit Source { get; }
        // 対象を決定するリゾルバ。実行時に BattleContext.ResolveTargets へ渡す
        public ITargetResolver TargetResolver { get; }
        // リアクション（反撃など）として生成されたコマンドなら、その深さと発生元。通常のコマンドなら null
        // BattleManager.DispatchReactions がキューへ積む際に設定する
        public ReactionDamageInfo ReactionInfo { get; protected internal set; }

        /* プロパティ */

        // 実行優先度。優先度付きキューへ拡張する際の差し込み口で、既定では未使用
        public virtual int Priority => 0;
        // リアクションの連鎖の深さ。通常のコマンドなら 0
        public int ReactionDepth => ReactionInfo?.Depth ?? 0;
        // リアクション（反撃など）として生成されたコマンドなら true。連鎖抑止の判定に使う
        public bool IsReaction => ReactionInfo != null;
        // 攻撃にあたる行動なら true
        // StatusEffectConsumeTrigger.Attacked の判定に使う。何を攻撃と見なすかはゲーム側の裁量のため派生で上書きする
        public virtual bool IsAttack => false;

        /* 生成 */

        // aSource : コマンドを実行するユニット
        // aTargetResolver : 対象を決定するリゾルバ
        protected BattleCommandBase(BattleUnit aSource, ITargetResolver aTargetResolver)
        {
            Source = aSource;
            TargetResolver = aTargetResolver;
        }

        /* 操作 */

        // コマンドの効果を実行する。派生クラスで具体的な処理（攻撃・アイテム使用など）を実装する
        // aContext : 実行時のバトルコンテキスト
        public abstract void Execute(BattleContext aContext);
    }

    // 通常攻撃コマンド。解決した全対象に対し、命中・クリティカル判定を経てダメージを与える
    // ダメージ計算式は DamageFormula として差し替え可能な static デリゲートにしてある
    public class AttackCommand : BattleCommandBase
    {
        /* static 変数 */

        // 通常攻撃のダメージ計算式（攻撃側, 防御側）。既定は「攻撃力 - 防御力 × 0.5」で最低 1 ダメージ
        // static なので差し替えるとゲーム全体に効く
        public static Func<BattleUnit, BattleUnit, float> DamageFormula { get; set; } =
            (src, tgt) =>
                Mathf.Max(1f, src.Parameters.Attack.CurrentValue - tgt.Parameters.Defense.CurrentValue * 0.5f);

        /* プロパティ */

        // 通常攻撃は常に攻撃として扱う
        public override bool IsAttack => true;

        /* 生成 */

        // aSource : 攻撃するユニット
        // aResolver : 対象を決めるリゾルバ
        public AttackCommand(BattleUnit aSource, ITargetResolver aResolver) : base(aSource, aResolver) {}

        /* 操作 */

        // 対象ごとにダメージ情報を組み立て、命中判定 → クリティカル補正 → 適用の順に処理する
        // ミスの場合もダメージ量 0 で適用まで通し、外れた結果を購読側へ届ける
        public override void Execute(BattleContext aContext)
        {
            foreach (var target in aContext.ResolveTargets(Source, TargetResolver))
            {
                float raw = DamageFormula(Source, target);
                var info = new DamageInfo(Source, target, raw, new AttackDamageReason(this));

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

                target.ApplyDamage(info, aContext);
            }
        }
    }

    // アイテムの効果本体。アイテム定義の型は決めていないため、
    // 使用時の振る舞いだけをこのインターフェースで受け取る
    public interface IItemEffect
    {
        // アイテムを使用する
        void Use(BattleUnit aSource, System.Collections.Generic.List<BattleUnit> aTargets, BattleContext aContext);
    }

    // アイテム使用コマンド。効果の中身は IItemEffect へ完全に委譲する
    public class ItemCommand : BattleCommandBase
    {
        // 使用するアイテムの効果
        public IItemEffect Item { get; }

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
        public override void Execute(BattleContext aContext)
        {
            if ((Source.CurrentRestrictions & ActionRestriction.CannotEscape) != 0) return;
            if (EscapeFormula(Source, aContext))
                aContext.EscapeRequested = true;
        }
    }

    // 通常攻撃コマンド。属性相性を含むダメージ計算（PPDamageUtility）を使う
    public class PPAttackCommand : AttackCommand
    {
        /* 生成 */

        // aSource : 攻撃するユニット
        // aResolver : 対象を決めるリゾルバ
        public PPAttackCommand(PPBattleUnit aSource, ITargetResolver aResolver)
            : base(aSource, aResolver)
        {
        }

        /* 操作 */

        // 対象ごとにダメージを算出して適用する
        public override void Execute(BattleContext aContext)
        {
            if (Source is not PPBattleUnit)
            {
                CustomConsoleLog.Warning("Battle", $"{Source.DisplayName}の通常攻撃: 発動者がPPBattleUnitではありません。");
                return;
            }

            List<PPDamageInfo> damages = new();
            var sourceAttribute = PPDamageUtility.ResolveAttribute(Source);

            foreach (var target in aContext.ResolveTargets(Source, TargetResolver))
            {
                float raw = PPDamageUtility.ResolveAttackDamage(Source, target);
                var damageInfo = PPDamageUtility.CreateDamageInfo(Source, target, raw, sourceAttribute,
                    new AttackDamageReason(this), aContext);
                damages.Add(damageInfo);
            }

            foreach (var damageInfo in damages) damageInfo.Target?.ApplyDamage(damageInfo, aContext);
        }
    }
}
