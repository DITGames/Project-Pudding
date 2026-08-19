/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file VFXSequenceGoalNode.cs
 * @author hqrse
 * @date 2026/08/18
 * @brief 到達するとそのセッションを完了させる終端ノード(VFXは持たない)
 * =====================================*/

using System;

namespace VFXUtility
{
    // シーケンスの完了を明示するノード。到達すると自セッションの再生中VFX・未発火予約を破棄して完了通知を発火する
    // 終端として扱うため、このノードに後続を接続しても実行されない
    [Serializable]
    public class VFXSequenceGoalNode : VFXSequenceNodeBase
    {
    }
}
