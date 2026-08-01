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
    // リソース消費を伴うコマンドであることを示すインターフェース
    // UI がコマンド実行前に必要量を表示する際、コマンドの具象型を問わずコストを引けるようにする
    public interface IPPBattleCommand
    {
        // このコマンドの実行に必要なリソースコスト
        public PPResourceCost AttackCost { get; }
    }

    // リソースを消費する通常攻撃コマンド
    // 基底の AttackCommand との違いは 2 点
    // 1. 属性相性を含む本作のダメージ計算（PPDamageUtility）を使う
    // 2. 実行時にパーティのリソースを消費し、支払えなければ攻撃しない
    // ダメージは先に全対象分を計算しておき、コストを支払えた場合にのみまとめて適用する
    // 支払い失敗時に一部の対象だけダメージが入る状態を避けるための順序
    public class PPAttackCommand : AttackCommand, IPPBattleCommand
    {
        // この攻撃に必要なリソースコスト。生成時点の攻撃コストで固定される
        public PPResourceCost AttackCost {get; private set;}

        // aSource : 攻撃するユニット
        // aResolver : 対象を決めるリゾルバ
        public PPAttackCommand(PPBattleUnit aSource, ITargetResolver aResolver)
            : base(aSource, aResolver)
        {
            // バフ・デバフ込みでの攻撃コストを適用(時間経過でバフ切れたときに消費できず失敗する可能性がありそう)
            AttackCost = PPResourceCost.BaseCost(aSource.ExtraParameters.Get(PPParameterSet.ParameterIdAttackCost).CurrentValue);
        }

        // 対象ごとにダメージを算出したうえで、リソースを消費できた場合のみ適用する
        // 発動可否は PPBattleCastValidator でも確認されるが、
        // キューに積んでから実行するまでに残量が変わりうるため、ここでも支払いの成否を見る
        // aContext : 実行時のバトルコンテキスト
        public override void Execute(BattleContext aContext)
        {
            if (aContext.GetParty(Source.Side) is not PPBattleParty party)
            {
                CustomConsoleLog.Warning("Battle", $"{Source.DisplayName}の通常攻撃: 対象パーティがPPBattlePartyではありません。");
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

            // CastValidatorを通して実行可能かチェックされるが念のためコスト消費ができた場合のみ攻撃実行
            if (party.ResourcePool.TryPay(AttackCost))
            {
                foreach (var damageInfo in damages) damageInfo.Target?.ApplyDamage(damageInfo, aContext);
            }
            else
            {
                CustomConsoleLog.Warning("Battle", $"{Source.DisplayName}の通常攻撃はコスト不足のため中止されました。");
            }
        }
    }
}
