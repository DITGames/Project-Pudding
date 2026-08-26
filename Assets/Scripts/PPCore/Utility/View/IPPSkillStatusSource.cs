/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file IPPSkillStatusSource.cs
 * @author hqrse
 * @date 2026/06/30
 * @brief スキル情報読み取りインターフェース
 * =====================================*/

using System;

namespace PPCore
{
    // UI がスキルの表示情報を読み取るためのインターフェース
    // スキル名とコストに加えて、ボタンを押せるかどうか（IsCastable）と
    // 残クールダウンを露出させ、メニュー側が発動可否を自前で判定せずに済むようにする
    public interface IPPSkillStatusSource
    {
        // UI 表示名
        string DisplayName { get; }
        // 発動に必要なスキルゲージ量
        float SkillGaugeCost { get; }
        // 今このスキルを発動できるか。ボタンの有効・無効に使う
        bool IsCastable { get; }
        // 残りクールダウンターン数
        int CooldownRemaining { get; }
        // 表示内容が変化したときに発火する
        event Action Changed;
    }
}
