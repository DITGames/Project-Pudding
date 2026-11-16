/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPAIBattleCommandDecider.cs
 * @author hqrse
 * @date 2026/06/25
 * @brief ユニット単位のコマンド決定
 * =====================================*/

using System.Collections.Generic;

namespace PPCore
{
    // ユニット単体のコマンド決定を担うインターフェース
    // 「誰が決めるか」（プレイヤー入力か AI か）を BattleUnit から切り離すためのもの
    // バトル側は決め方を問わず、返ってきたコマンドをキューへ積むだけで済む
    public interface ICommandDecider
    {
        // このユニットが取る行動を決める
        // aSelf : 行動を決めるユニット
        // aContext : バトルコンテキスト
        // return : 実行するコマンド
        BattleCommandBase DecideCommand(BattleUnit aSelf, BattleContext aContext);
    }

    // AI によるコマンド決定の基底クラス
    // プレイヤー入力側の実装と型で区別を付けるための足場で、現状は追加の振る舞いを持たない
    public abstract class AICommandDeciderBase : ICommandDecider
    {
        // このユニットが取る行動を決める。派生側で実装する
        // aSelf : 行動を決めるユニット
        // aContext : バトルコンテキスト
        // return : 実行するコマンド
        public abstract BattleCommandBase DecideCommand(BattleUnit aSelf, BattleContext aContext);
    }

    // 通常攻撃と発動可能なスキルから完全にランダムで選ぶ、最も単純な AI
    // UnitDefinition.CreateRuntimeUnit で decider を指定しなかった場合の既定値になる
    // 乱数はシード管理・再現性のため、行動するユニット自身の供給元を経由する
    public class RandomAICommandDecider : AICommandDeciderBase
    {
        // 通常攻撃を必ず候補に入れたうえで、今撃てるスキルを候補へ追加し、
        // その中から等確率で 1 つ選ぶ。候補が必ず 1 件以上あるため選択に失敗しない
        // aSelf : 行動を決めるユニット
        // aContext : バトルコンテキスト
        // return : 選ばれたコマンド
        public override BattleCommandBase DecideCommand(BattleUnit aSelf, BattleContext aContext)
        {
            var options = new List<BattleCommandBase>
            {
                new AttackCommand(aSelf, new RandomEnemyResolver())
            };

            foreach (var skill in aSelf.Skills)
            {
                // 選択可能なスキルのみ選択肢に入れる
                if (aContext.Rules.CastValidator.Validate(aSelf, skill, aContext).CanCast)
                {
                    options.Add(new SkillCommand(aSelf, skill));
                }
            }
            return options[aSelf.ResolveRandom(aContext).NextInt(options.Count)];
        }
    }

    // 本作のコマンドを使うランダム AI
    // 挙動は基底の RandomAICommandDecider と同じだが、
    // 生成するコマンドがゲージ消費に対応した PPAttackCommand / PPSkillCommand になる点が異なる
    // ユニット単位で動く簡易 AI で、UnitDefinition.CreateRuntimeUnit の既定値として使われる
    // 敵パーティの本来の思考は PPUnitAIStrategist が担う
    public class PPRandomAICommandDecider : AICommandDeciderBase
    {
        // 通常攻撃を必ず候補に入れたうえで、今撃てるスキルを候補へ追加し、その中から等確率で選ぶ
        // aSelf : 行動を決めるユニット
        // aContext : バトルコンテキスト
        // return : 選ばれたコマンド
        public override BattleCommandBase DecideCommand(BattleUnit aSelf, BattleContext aContext)
        {
            // PP専用の攻撃コマンドをベースで追加
            var options = new List<BattleCommandBase>
            {
                new PPAttackCommand(aSelf as PPBattleUnit, new RandomEnemyResolver())
            };

            foreach (var skill in aSelf.Skills)
            {
                if (aContext.Rules.CastValidator.Validate(aSelf, skill, aContext).CanCast)
                {
                    // 継承クラスを追加する予定
                    options.Add(new PPSkillCommand(aSelf, skill));
                }
            }

            return options[aSelf.ResolveRandom(aContext).NextInt(options.Count)];
        }
    }
}
