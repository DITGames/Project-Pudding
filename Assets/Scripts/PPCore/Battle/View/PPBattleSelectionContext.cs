/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPBattleSelectionContext.cs
 * @author hqrse
 * @date 2026/06/30
 * @brief バトル中のコマンド選択途中の蓄積データ
 * =====================================*/
using System;
using CommandBattleCore;

namespace PPCore
{
    /// <summary>
    /// コマンド入力の途中経過を保持するコンテキスト。
    /// <para>
    /// 入力は複数のステートにまたがって進むため、各ステートが個別に状態を持つと
    /// 戻る操作での整合が取りづらい。選択内容をここへ集約することで、
    /// ステート側は自分の担当分を書き込むだけで済む。
    /// </para>
    /// <para>
    /// <see cref="CommandBuilder"/> は「対象さえ決まればコマンドを作れる」状態を表す。
    /// 対象選択を挟まないスキルでは、この時点で即座に確定できる。
    /// </para>
    /// </summary>
    public class PPBattleSelectionContext
    {
        /// <summary>行動するユニット。ユニット選択で決まる。</summary>
        public BattleUnit Unit;
        /// <summary>使用するスキル。スキル選択で決まる。通常攻撃なら null のまま。</summary>
        public BattleSkill Skill;
        /// <summary>対象ユニット。対象選択で決まる。</summary>
        public BattleUnit Target;

        /// <summary>対象を受け取ってコマンドを生成する関数。コマンド／スキル選択の時点で設定される。</summary>
        public Func<BattleUnit, BattleCommandBase> CommandBuilder;

        /// <summary>選択中の行動のターゲット範囲。未確定なら null。</summary>
        public TargetScope? TargetScope;

        /// <summary>すべての選択内容を破棄する。入力を最初から始めるときに使う。</summary>
        public void Clear()
        {
            Unit = null;
            Skill = null;
            Target = null;
            CommandBuilder = null;
            TargetScope = null;
        }

        /// <summary>
        /// 行動ユニットの選択だけを残して以降の選択を破棄する。
        /// コマンド選択まで戻ったときに、選び直しの対象がユニット以降だけになるようにする。
        /// </summary>
        public void ClearSelectionKeepingUnit()
        {
            Skill = null;
            Target = null;
            CommandBuilder = null;
            TargetScope = null;
        }
    }
}
