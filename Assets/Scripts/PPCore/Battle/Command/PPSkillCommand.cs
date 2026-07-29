/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPSkillCommand.cs
 * @author hqrse
 * @date 2026/07/16
 * @brief リソース消費を行うスキルコマンド
 * =====================================*/
using CommandBattleCore;

namespace PPCore
{
    /// <summary>
    /// パーティの行動リソース消費を伴うスキルコマンド。
    /// <para>
    /// 基底の <see cref="SkillCommand"/> との違いは、実行前に
    /// <see cref="PPSkillDefinition.Cost"/> を <see cref="PPBattleResourcePool"/> から支払う点。
    /// 対象不在・発動条件不成立・リソース不足のいずれかで発動を中止し、
    /// それぞれ理由付きで <see cref="BattleContext.NotifyCastFailed"/> を通知する。
    /// </para>
    /// </summary>
    public class PPSkillCommand : SkillCommand
    {
        /// <param name="aSource">スキルを使用するユニット。</param>
        /// <param name="aSkill">使用するスキル。</param>
        /// <param name="aResolverOverride">対象を明示指定する場合のリゾルバ。null ならスキル既定を使う。</param>
        public PPSkillCommand(BattleUnit aSource, BattleSkill aSkill, ITargetResolver aResolverOverride = null)
            : base(aSource, aSkill, aResolverOverride)
        {
        }

        /// <summary>
        /// スキルを発動する。
        /// 対象解決 → 発動可否検証 → リソース支払い → スキル効果実行 → 使用記録、の順に進み、
        /// いずれかの段階で失敗した場合はリソースを消費せずに中止する。
        /// </summary>
        /// <param name="aContext">実行時のバトルコンテキスト。</param>
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

            // 定義アセットからコストを取り、パーティのリソースプールから実際に支払う
            // 支払いに失敗した場合はこの時点で中止するため、リソースは減らない
            var cost = (Skill.SourceDefinition as PPSkillDefinition)?.Cost ?? PPResourceCost.Free;
            if (!cost.IsFree)
            {
                if (aContext.GetParty(Source.Side) is not PPBattleParty party ||
                    !party.ResourcePool.TryPay(cost))
                {
                    aContext.NotifyCastFailed(Source, Skill, CastFailReason.NotEnoughResource);
                    return;
                }
            }

            Skill.Execute(Source, targets, aContext);
            Skill.NotifyUsed();
        }
    }
}
