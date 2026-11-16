/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPBattleCommands.cs
 * @author hqrse
 * @date 2026/06/13
 * @brief バトルコマンド（基底・攻撃・スキル・アイテム・入れ替え・逃走）
 * =====================================*/

using System;
using System.Collections.Generic;
using UnityEngine;
using CustomConsole;

namespace PPCore
{
    // バトル中の 1 行動を表すコマンドの基底クラス
    // 「誰が」（Source）「誰を狙って」（TargetResolver）行動するかだけを持ち、
    // 実際の効果は派生クラスの Execute が実装する
    // 生成されたコマンドは ActionQueue に積まれ、BattleManager が順に実行する
    public abstract class BattleCommandBase
    {
        // このコマンドを実行するユニット
        public BattleUnit Source { get; }
        // 対象を決定するリゾルバ。実行時に BattleContext.ResolveTargets へ渡す
        public ITargetResolver TargetResolver { get; }
        // 実行優先度。優先度付きキューへ拡張する際の差し込み口で、既定では未使用
        public virtual int Priority => 0;
        // リアクション（反撃など）として生成されたコマンドなら、その深さと発生元。通常のコマンドなら null
        // BattleManager.DispatchReactions がキューへ積む際に設定する
        public ReactionDamageInfo ReactionInfo { get; protected internal set; }
        // リアクションの連鎖の深さ。通常のコマンドなら 0
        public int ReactionDepth => ReactionInfo?.Depth ?? 0;
        // リアクション（反撃など）として生成されたコマンドなら true。連鎖抑止の判定に使う
        public bool IsReaction => ReactionInfo != null;
        // 攻撃にあたる行動なら true
        // StatusEffectConsumeTrigger.Attacked の判定に使う。何を攻撃と見なすかはゲーム側の裁量のため派生で上書きする
        public virtual bool IsAttack => false;

        // aSource : コマンドを実行するユニット
        // aTargetResolver : 対象を決定するリゾルバ
        protected BattleCommandBase(BattleUnit aSource, ITargetResolver aTargetResolver)
        {
            Source = aSource;
            TargetResolver = aTargetResolver;
        }

        // コマンドの効果を実行する。派生クラスで具体的な処理（スキル発動・アイテム使用など）を実装する
        // aContext : 実行時のバトルコンテキスト
        public abstract void Execute(BattleContext aContext);
    }

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

        // 通常攻撃は常に攻撃として扱う
        public override bool IsAttack => true;

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

    // ゲージ消費を伴うコマンドであることを示すインターフェース
    // UI がコマンド実行前に必要量を表示する際、コマンドの具象型を問わずコストを引けるようにする
    public interface IPPBattleCommand
    {
        // このコマンドの実行に必要なコインゲージ量
        public float AttackCost { get; }
    }

    // コインゲージを消費する通常攻撃コマンド
    // 基底の AttackCommand との違いは 3 点
    // 1. 属性相性を含む本作のダメージ計算（PPDamageUtility）を使う
    // 2. 実行時に発動者自身のコインゲージを消費し、支払えなければ攻撃しない
    // 3. 攻撃が成立した場合、ルールで定めた量だけ発動者のスキルゲージを回復する
    // ダメージは先に全対象分を計算しておき、コストを支払えた場合にのみまとめて適用する
    // 支払い失敗時に一部の対象だけダメージが入る状態を避けるための順序
    public class PPAttackCommand : AttackCommand, IPPBattleCommand
    {
        // この攻撃に必要なコインゲージ量。生成時点の攻撃コストで固定される
        public float AttackCost {get; private set;}

        // aSource : 攻撃するユニット
        // aResolver : 対象を決めるリゾルバ
        public PPAttackCommand(PPBattleUnit aSource, ITargetResolver aResolver)
            : base(aSource, aResolver)
        {
            // バフ・デバフ込みでの攻撃コストを適用(時間経過でバフ切れたときに消費できず失敗する可能性がありそう)
            AttackCost = aSource.ExtraParameters.Get(PPParameterSet.ParameterIdAttackCost).CurrentValue;
        }

        // 対象ごとにダメージを算出したうえで、コインゲージを消費できた場合のみ適用する
        // 発動可否は AI 側でも確認されるが、キューに積んでから実行するまでに残量が変わりうるため、
        // ここでも支払いの成否を見る
        // aContext : 実行時のバトルコンテキスト
        public override void Execute(BattleContext aContext)
        {
            if (Source is not PPBattleUnit ppSource)
            {
                CustomConsoleLog.Warning("Battle", $"{Source.DisplayName}の通常攻撃: 発動者がPPBattleUnitではありません。");
                return;
            }

            List<PPDamageInfo> damages = new();
            var sourceAttribute = PPDamageUtility.ResolveAttribute(Source);

            foreach (var target in aContext.ResolveTargets(Source, TargetResolver))
            {
                float raw = PPDamageUtility.ResolveAttackDamage(Source, target);
                var damageInfo = PPDamageUtility.CreateDamageInfo(Source, target, raw, PPSkillCategory.Physical, sourceAttribute,
                    new AttackDamageReason(this), aContext);
                damages.Add(damageInfo);
            }

            // AI・UI 側でも実行可能かチェックされるが念のためコスト消費ができた場合のみ攻撃実行
            if (!PPGaugeUtility.TryPay(ppSource.ExtraParameters.CoinGauge, AttackCost))
            {
                CustomConsoleLog.Warning("Battle", $"{Source.DisplayName}の通常攻撃はコインゲージ不足のため中止されました。");
                return;
            }

            foreach (var damageInfo in damages) damageInfo.Target?.ApplyDamage(damageInfo, aContext);

            RecoverSkillGauge(ppSource, aContext);
        }

        // 通常攻撃の成立に伴うスキルゲージ回復を行う
        // 回復量は拡張ルール側にしか無いため、差し込まれている場合のみ回復する
        // aSource : 攻撃したユニット
        // aContext : 実行時のバトルコンテキスト
        protected virtual void RecoverSkillGauge(PPBattleUnit aSource, BattleContext aContext)
        {
            if (aContext.Rules is not PPBattleRules rules) return;
            if (rules.NormalAttackSkillGaugeRecover <= 0f) return;

            aSource.ExtraParameters.SkillGauge.Recover(rules.NormalAttackSkillGaugeRecover);
            CustomConsoleLog.Verbose("Resource",
                $"{aSource.DisplayName}のスキルゲージが通常攻撃で{rules.NormalAttackSkillGaugeRecover:0.##}回復しました（残量{aSource.ExtraParameters.SkillGauge.Current:0.##}）。");
        }
    }

    // 発動者自身のスキルゲージ消費を伴うスキルコマンド
    // 基底の SkillCommand との違いは、実行前に PPSkillDefinition.SkillGaugeCost を
    // 発動者のスキルゲージから支払う点
    // 対象不在・発動条件不成立・ゲージ不足のいずれかで発動を中止し、
    // それぞれ理由付きで BattleContext.NotifyCastFailed を通知する
    public class PPSkillCommand : SkillCommand
    {
        // 攻撃系統のスキルのみ「攻撃時」のスタック消費のきっかけとして扱う
        // 支援・回復は敵にダメージを与える行動ではないため攻撃に含めない
        public override bool IsAttack
            => Skill?.SourceDefinition is PPSkillDefinition definition && definition.Group == PPSkillGroup.Attack;

        // aSource : スキルを使用するユニット
        // aSkill : 使用するスキル
        // aResolverOverride : 対象を明示指定する場合のリゾルバ。null ならスキル既定を使う
        public PPSkillCommand(BattleUnit aSource, BattleSkill aSkill, ITargetResolver aResolverOverride = null)
            : base(aSource, aSkill, aResolverOverride)
        {
        }

        // スキルを発動する
        // 対象解決 → 発動可否検証 → スキルゲージ支払い → スキル効果実行 → 使用記録、の順に進み、
        // いずれかの段階で失敗した場合はゲージを消費せずに中止する
        // aContext : 実行時のバトルコンテキスト
        public override void Execute(BattleContext aContext)
        {
            // 対象が 1 体も解決できなければ発動しない
            var targets = aContext.ResolveTargets(Source, TargetResolver);
            if (targets.Count == 0)
            {
                aContext.NotifyCastFailed(Source, Skill, CastFailReason.InvalidTarget);
                return;
            }

            // クールダウン・使用回数などの発動条件を検証する
            var validation = aContext.Rules.CastValidator.Validate(Source, Skill, aContext);
            if (!validation.CanCast)
            {
                aContext.NotifyCastFailed(Source, Skill, validation.Reason);
                return;
            }

            // 定義アセットから必要スキルゲージ量を取り、発動者自身のゲージから実際に支払う
            // 支払いに失敗した場合はこの時点で中止するため、ゲージは減らない
            float cost = (Skill.SourceDefinition as PPSkillDefinition)?.SkillGaugeCost ?? 0f;
            if (Source is not PPBattleUnit ppSource ||
                !PPGaugeUtility.TryPay(ppSource.ExtraParameters.SkillGauge, cost))
            {
                aContext.NotifyCastFailed(Source, Skill, CastFailReason.NotEnoughResource);
                return;
            }

            Skill.Execute(Source, targets, aContext);
            Skill.NotifyUsed();
            CustomConsoleLog.Log("Battle", $"{Source.DisplayName}が{Skill.DisplayName}を発動しました。");
        }
    }
}
