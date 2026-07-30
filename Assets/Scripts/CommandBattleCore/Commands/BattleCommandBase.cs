/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file BattleCommandBase.cs
 * @author hqrse
 * @date 2026/06/13
 * @brief 全コマンドの基底クラス
 * =====================================*/

namespace CommandBattleCore
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
        // リアクション（反撃など）として生成されたコマンドなら true。連鎖抑止の判定に使う
        public bool IsReaction { get; protected internal set; }

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
}
