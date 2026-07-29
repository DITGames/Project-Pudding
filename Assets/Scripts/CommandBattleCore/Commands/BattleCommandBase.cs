/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file BattleCommandBase.cs
 * @author hqrse
 * @date 2026/06/13
 * @brief 全コマンドの基底クラス
 * =====================================*/

using System;
using UnityEngine;

namespace CommandBattleCore
{
    /// <summary>
    /// バトル中の 1 行動を表すコマンドの基底クラス。
    /// <para>
    /// 「誰が」（<see cref="Source"/>）「誰を狙って」（<see cref="TargetResolver"/>）行動するかだけを持ち、
    /// 実際の効果は派生クラスの <see cref="Execute"/> が実装する。
    /// 生成されたコマンドは <see cref="ActionQueue"/> に積まれ、<see cref="BattleManager"/> が順に実行する。
    /// </para>
    /// </summary>
    public abstract class BattleCommandBase
    {
        /// <summary>このコマンドを実行するユニット。</summary>
        public BattleUnit Source { get; }
        /// <summary>対象を決定するリゾルバ。実行時に <see cref="BattleContext.ResolveTargets"/> へ渡す。</summary>
        public ITargetResolver TargetResolver { get; }
        /// <summary>実行優先度。優先度付きキューへ拡張する際の差し込み口で、既定では未使用。</summary>
        public virtual int Priority => 0;
        /// <summary>リアクション（反撃など）として生成されたコマンドなら true。連鎖抑止の判定に使う。</summary>
        public bool IsReaction { get; protected internal set; }

        /// <param name="source">コマンドを実行するユニット。</param>
        /// <param name="targetResolver">対象を決定するリゾルバ。</param>
        protected BattleCommandBase(BattleUnit source, ITargetResolver targetResolver)
        {
            Source = source;
            TargetResolver = targetResolver;
        }

        /// <summary>
        /// コマンドの効果を実行する。派生クラスで具体的な処理（スキル発動・アイテム使用など）を実装する。
        /// </summary>
        /// <param name="aContext">実行時のバトルコンテキスト。</param>
        public abstract void Execute(BattleContext aContext);
    }
}
