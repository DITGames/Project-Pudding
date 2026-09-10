/* =====================================
 * Copyright DITGames. All rights reserved.
 * @file PPBattleTransitionDirection.cs
 * @author DITGames
 * @date 2026/09/10
 * @brief ボタンメニューの入退場演出の向き
 * =====================================*/

namespace PPCore
{
    // ボタン列の入退場演出がどちら向きに流れるかを表す
    // Down : 上から下へ流れる（先へ進むとき。既存のボタンは下へ抜け、新しいボタンは上から現れて同じ場所へ収まる）
    // Up   : 下から上へ流れる（戻るとき。既存のボタンは上へ抜け、新しいボタンは下から現れて同じ場所へ収まる）
    public enum PPBattleTransitionDirection
    {
        Down,
        Up,
    }
}
