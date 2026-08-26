/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPUnitAIActionNode.cs
 * @author hqrse
 * @date 2026/08/24
 * @brief 行動を実行する葉ノード
 * =====================================*/

using System;
using AttributeUtility;
using UnityEngine;

namespace PPCore
{
    // 行動を 1 つ実行する葉ノード
    // アクションが実際に組み立てられた場合のみ確定とし、
    // 組み立てられなければ不成立として親の次の候補へ処理を渡す
    // 「条件は満たしたがゲージが足りない・対象が居ない」場合に自然と次の手へ流れる
    //
    // 維持ティック数を設定すると、この行動を選んだあとしばらく判断が固定される
    // 「強いスキルが撃てるまで通常攻撃で溜める」のように、
    // 数ティックまたいで初めて意味を持つ判断がティックごとに揺れるのを防ぐためのもの
    [Serializable]
    [PPTypeMenuName("行動/実行")]
    public sealed class PPUnitAIActionNode : PPUnitAINode
    {
        [Header("行動")]
        [Label("実行する行動")]
        [SerializeReference]
        [SerializeField] private PPUnitAIActionBase mAction;

        // この行動を選んだあと、判断を維持するティック数
        // 0 なら維持せず、次のティックはツリーの先頭から自由に選び直す
        [Label("判断を維持するティック数")]
        [SerializeField] private int mCommitTicks = 0;

        protected override string DefaultNodeName => mAction != null ? mAction.ActionName : "行動";

        // アクションを組み立てて確定させる
        // aContext : 評価 1 回分の入力
        // return : 組み立てられた行動。組み立てられなければ Failed
        public override PPUnitAINodeResult Evaluate(PPUnitAIEvalContext aContext)
        {
            if (mAction == null) return PPUnitAINodeResult.Failed;

            var result = mAction.Build(aContext);
            return result.IsDecided && mCommitTicks > 0 ? result.WithCommit(mCommitTicks) : result;
        }
    }
}
