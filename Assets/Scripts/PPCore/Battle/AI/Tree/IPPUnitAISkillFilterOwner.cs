/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file IPPUnitAISkillFilterOwner.cs
 * @author hqrse
 * @date 2026/08/27
 * @brief スキル絞り込みを保持していることを示すインターフェース
 * =====================================*/

namespace PPCore
{
    // スキルの絞り込み条件を保持していることを示すインターフェース
    // 行動と条件のどちらも同じ絞り込みを持つため、
    // エディタの診断が「直接指定なのにスキル未設定」を型を問わず拾えるようにする
    public interface IPPUnitAISkillFilterOwner
    {
        // 保持しているスキルの絞り込み条件
        PPUnitAISkillFilter Filter { get; }
    }
}
