/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file IPPBattleCommand.cs
 * @author hqrse
 * @date 2026/06/21
 * @brief コマンドインターフェース
 * =====================================*/

using System.Collections.Generic;
using CommandBattleCore;
using CustomConsole;

namespace PPCore
{
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
                var damageInfo = PPDamageUtility.CreateDamageInfo(Source, target, raw, PPSkillCategory.Physical, sourceAttribute, this, aContext);
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
}
