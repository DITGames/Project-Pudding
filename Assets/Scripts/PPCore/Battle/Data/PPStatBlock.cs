/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPStatBlock.cs
 * @author hqrse
 * @date 2026/06/25
 * @brief スタータスブロック
 * =====================================*/

using System;
using CommandBattleCore;

namespace PPCore
{
    // ユニット定義がインスペクタ上で設定する、本作固有の追加ステータス
    // 基底の StatBlock（HP・攻撃・防御・速度）に対する差分にあたる
    [Serializable]
    public struct PPStatBlock
    {
        [Label("通常攻撃コスト")]
        public float AttackCost;
    }
}
