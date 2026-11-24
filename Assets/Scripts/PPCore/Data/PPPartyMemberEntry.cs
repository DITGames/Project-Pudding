/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPPartyMemberEntry.cs
 * @author hqrse
 * @date 2026/11/25
 * @brief パーティメンバー 1 体分の設定
 * =====================================*/

using System;
using AttributeUtility;

namespace PPCore
{
    // パーティメンバー 1 体分の設定。マップ定義の初期パーティや、バトルのランナーの味方パーティに並べる
    // ユニット定義の既定値をそのまま使い、編成上の違いはレベルで表現する
    [Serializable]
    public sealed class PPPartyMemberEntry
    {
        [Label("ユニット定義")] public PPUnitDefinition Unit;
        // 生成するレベル。成長曲線の評価に使う
        [Label("レベル")] public int Level = 1;
        // 乱数のシードを固定するか。固定すると毎回同じ乱数列で動く
        // 外した場合も、ユニットごとに別の乱数列は割り当てられる
        [Label("乱数シードを固定する")] public bool IsFixedSeed = false;

        // 固定時に使うシード値
        // 同じユニット定義を複数体並べる場合はここを変えないと全員が同じ行動をする
        [Label("乱数シード")]
        [EditCondition(nameof(IsFixedSeed), true, false)]
        public int Seed = 0;
    }
}
