/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPSkillCommand.cs
 * @author hqrse
 * @date 2026/07/16
 * @brief スキルゲージ消費を行うスキルコマンド
 * =====================================*/

using CommandBattleCore;
using CustomConsole;

namespace PPCore
{
    // 発動者自身のスキルゲージ消費を伴うスキルコマンド
    // 基底の SkillCommand との違いは、実行前に PPSkillDefinition.SkillGaugeCost を
    // 発動者のスキルゲージから支払う点
    // 対象不在・発動条件不成立・ゲージ不足のいずれかで発動を中止し、
    // それぞれ理由付きで BattleContext.NotifyCastFailed を通知する
    public class PPSkillCommand : SkillCommand
    {
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
