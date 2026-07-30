/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPTargeting.cs
 * @author hqrse
 * @date 2026/07/02
 * @brief 種別/スコープ判定のユーティリティ
 * =====================================*/

using CommandBattleCore;

namespace PPCore
{
    // ターゲット範囲に関する判定を集約したユーティリティ
    // 入力フローの分岐（対象選択に進むか即確定か、候補は味方か敵か）で使う
    public static class PPTargeting
    {
        // プレイヤーに対象を選ばせる必要があるかを判定する
        // 単体対象のみ true で、全体・自己完結の行動は選択を挟まず即確定できる
        // aScope : スキルのターゲット範囲
        public static bool NeedsManualTarget(TargetScope aScope)
            => aScope is TargetScope.SingleEnemy or TargetScope.SingleAlly;

        // 対象候補が味方側かを判定する。対象選択で敵味方どちらを選択可能にするかの判断に使う
        // aScope : スキルのターゲット範囲
        public static bool IsAllySide(TargetScope aScope)
            => aScope is TargetScope.SingleAlly or TargetScope.AllAllies or TargetScope.Self;
    }
}
