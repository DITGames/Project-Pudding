/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
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
    // 既定値の定数もここに集約する（構造体はフィールド初期化子を持てないため、定義側がこれを使って初期化する）
    [Serializable]
    public struct PPStatBlock
    {
        // 新規作成したユニット定義のきようさ既定値（会心率 5%）
        public const float DefaultDexterity = 20f;
        // 新規作成したユニット定義のスキルゲージ上限の既定値
        public const float DefaultSkillGaugeMax = 100f;

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
        // 会心率の元になる基礎能力（レベル 1 時点の値）。PPUnitDefinition の成長曲線でレベル成長する
        // 会心率(%) = 補正後きようさ × 0.25（0～100 に丸める）。既定値 20 で会心率 5%
        // 構造体のためフィールド初期化子を持てず、既定値は PPUnitDefinition 側で与える
        [Label(PPUnitAbilityDefinition.NameDexterity)]
        public float Dexterity;
    }
}
