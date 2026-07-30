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
    // コマンド入力の途中経過を保持するコンテキスト
    // 入力は複数のステートにまたがって進むため、各ステートが個別に状態を持つと
    // 戻る操作での整合が取りづらい。選択内容をここへ集約することで、
    // ステート側は自分の担当分を書き込むだけで済む
    // CommandBuilder は「対象さえ決まればコマンドを作れる」状態を表す
    // 対象選択を挟まないスキルでは、この時点で即座に確定できる
    public class PPBattleSelectionContext
    {
        // 行動するユニット。ユニット選択で決まる
        public BattleUnit Unit;
        // 使用するスキル。スキル選択で決まる。通常攻撃なら null のまま
        public BattleSkill Skill;
        // 対象ユニット。対象選択で決まる
        public BattleUnit Target;

        // 対象を受け取ってコマンドを生成する関数。コマンド／スキル選択の時点で設定される
        public Func<BattleUnit, BattleCommandBase> CommandBuilder;

        // 選択中の行動のターゲット範囲。未確定なら null
        public TargetScope? TargetScope;

        // すべての選択内容を破棄する。入力を最初から始めるときに使う
        public void Clear()
        {
            Unit = null;
            Skill = null;
            Target = null;
            CommandBuilder = null;
            TargetScope = null;
        }

        // 行動ユニットの選択だけを残して以降の選択を破棄する
        // コマンド選択まで戻ったときに、選び直しの対象がユニット以降だけになるようにする
        public void ClearSelectionKeepingUnit()
        {
            Skill = null;
            Target = null;
            CommandBuilder = null;
            TargetScope = null;
        }
    }
}
