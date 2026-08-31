/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPUnitAIWaitAction.cs
 * @author hqrse
 * @date 2026/08/24
 * @brief 何もせず待機する行動
 * =====================================*/

using System;

namespace PPCore
{
    // 何もせずそのティックを見送る行動
    // 「上位スキルが撃てるまで溜める」のように、意図的に手を出さないことを明示したい場合に置く
    // 常に確定するため、これより下の候補は評価されない
    [Serializable]
    [PPTypeMenuName("待機")]
    public sealed class PPUnitAIWaitAction : PPUnitAIActionBase
    {
        protected override string DefaultActionName => "待機";

        // 常に待機で確定する
        // aContext : 評価 1 回分の入力
        // return : 待機を表す結果
        public override PPUnitAINodeResult Build(PPUnitAIEvalContext aContext)
            => PPUnitAINodeResult.Wait(ActionName);
    }
}
