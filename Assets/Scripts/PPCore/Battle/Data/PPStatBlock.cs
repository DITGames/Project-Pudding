/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPStatBlock.cs
 * @author hqrse
 * @date 2026/06/25
 * @brief スタータスブロック
 * =====================================*/

using System;
using AttributeUtility;

namespace PPCore
{
    // ユニット定義がインスペクタ上で設定する、本作固有の追加ステータス
    // 基底の StatBlock（HP・攻撃・防御・速度）に対する差分にあたる
    [Serializable]
    public struct PPStatBlock
    {
        [Label("通常攻撃コスト")]
        public float AttackCost;
        // 1 ティックあたりに行動できる回数。バフで増減しうるため、ここでは初期値だけを持つ
        [Label("行動回数上限")]
        public int ActionCount;
        // スキルゲージの上限。スキルの必要スキルゲージ量はこの範囲内で設定する
        [Label("スキルゲージ上限")]
        public float SkillGaugeMax;
        // コインゲージの上限。通常攻撃コストはこの範囲内で設定する
        [Label("コインゲージ上限")]
        public float CoinGaugeMax;
    }
}
