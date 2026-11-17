/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPActionOrderResolver.cs
 * @author hqrse
 * @date 2026/08/26
 * @brief 行動順の解決（同時に行動できるユニットの実行順）
 * =====================================*/

using System.Collections.Generic;
using System.Linq;

namespace PPCore
{
    // 実行待ちの行動 1 件分。誰の行動かと、並び替えに使う優先度をコマンドと一緒に持つ
    // 優先度は積んだ時点の値を焼き込んでおく
    public readonly struct PPPendingAction
    {
        /* 状態 */

        // 行動するユニット
        public PPBattleUnit Unit { get; }
        // 実行するコマンド
        public BattleCommandBase Command { get; }
        // 並び替えに使う優先度
        public PPSkillActionPriority Priority { get; }

        /* static 関数 */

        // コマンドから優先度を判定して 1 件分を組み立てる
        // スキルは定義側の設定に従い、通常攻撃など定義を持たない行動は「通常」として扱う
        public static PPPendingAction FromCommand(PPBattleUnit aUnit, BattleCommandBase aCommand)
        {
            var priority = PPSkillActionPriority.Normal;
            if (aCommand is SkillCommand skillCommand &&
                skillCommand.Skill?.SourceDefinition is PPSkillDefinition definition)
            {
                priority = definition.ActionPriority;
            }
            return new PPPendingAction(aUnit, aCommand, priority);
        }

        /* 生成 */

        public PPPendingAction(PPBattleUnit aUnit, BattleCommandBase aCommand, PPSkillActionPriority aPriority)
        {
            Unit = aUnit;
            Command = aCommand;
            Priority = aPriority;
        }
    }

    // 同時に行動できるようになったユニットの行動を、実行順に並べるリゾルバ
    // 並べ方を差し替えられるよう、PPBattleRules へ差し込む形にしてある
    public interface IPPActionOrderResolver
    {
        // 行動を実行順に並べ替える
        List<PPPendingAction> ResolveOrder(IReadOnlyList<PPPendingAction> aActions, BattleContext aContext);
    }

    // 標準の行動順リゾルバ。第 1 キーが優先度（先攻 → 通常 → 後攻）、第 2 キーが速度の降順
    // 速度のジッターは行動 1 件ごとに、行動するユニット自身の乱数供給元から引く
    public class PPDefaultActionOrderResolver : IPPActionOrderResolver
    {
        // 速度に加算する揺らぎの最大値。0 なら揺らぎなしで純粋な速度順になる
        public float SpeedJitter { get; set; } = 0f;

        // 優先度と速度で行動を並べ替える
        public virtual List<PPPendingAction> ResolveOrder(IReadOnlyList<PPPendingAction> aActions,
            BattleContext aContext)
        {
            // 抽選をここで済ませてから並べる
            // OrderBy の比較の中で乱数を引くと、比較のたびに値が変わって並びが壊れる
            var scored = new List<(PPPendingAction Action, float Speed)>(aActions.Count);
            foreach (var action in aActions)
            {
                scored.Add((action, ResolveSpeed(action, aContext)));
            }

            return scored
                .OrderBy(x => (int)x.Action.Priority)
                .ThenByDescending(x => x.Speed)
                .Select(x => x.Action)
                .ToList();
        }

        // 行動 1 件分の速度を求める。ジッターが設定されていれば加算する
        protected virtual float ResolveSpeed(PPPendingAction aAction, BattleContext aContext)
        {
            float speed = aAction.Unit.Parameters.Speed.CurrentValue;
            if (SpeedJitter <= 0f) return speed;

            return speed + aAction.Unit.ResolveRandom(aContext).NextFloat() * SpeedJitter;
        }
    }
}
