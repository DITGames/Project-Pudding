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
    public abstract class BattleCommandBase
    {
        // 実行ユニット
        public BattleUnit Source { get; }
        // ターゲット解決
        public ITargetResolver TargetResolver { get; }
        // 優先度キュー拡張用の差し込み口
        public virtual int Priority => 0;
        // リアクションコマンドのフラグ
        public bool IsReaction { get; internal set; }
        
        protected BattleCommandBase(BattleUnit source, ITargetResolver targetResolver)
        {
            Source = source;
            TargetResolver = targetResolver;
        }

        public abstract void Execute(BattleContext aContext);
    }
}