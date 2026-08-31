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

        // この行動で狙った対象へ、狙いを固定するティック数
        // 0 なら固定しない。1 以上なら、その対象が倒れるか指定ティック数が過ぎるまで固定が続く
        // 固定した狙いは、対象の選び方に「固定した敵」を指定した行動が拾う
        [Label("狙いを固定するティック数")]
        [SerializeField] private int mFocusTicks = 0;

        protected override string DefaultNodeName => mAction != null ? mAction.ActionName : "行動";

        // 実行する行動が設定されているか。エディタの診断から参照する
        public bool HasAction => mAction != null;

        // 実行する行動。エディタの診断が設定漏れを調べるために参照する
        public PPUnitAIActionBase Action => mAction;

        // 実行する行動を設定する。雛形の生成など、エディタからノードを組み立てる処理で使う
        // aAction : 設定する行動
        public void SetAction(PPUnitAIActionBase aAction) => mAction = aAction;

        // 行動の要約に、判断の維持と狙いの固定を添える
        public override string Summary
        {
            get
            {
                if (mAction == null) return "行動が未設定";

                string body = mAction.Summary;
                if (mCommitTicks > 0) body += $"\n維持 {mCommitTicks}T";
                if (mFocusTicks > 0) body += $" ／ 狙い固定 {mFocusTicks}T";
                return body;
            }
        }

        // アクションを組み立てて確定させる
        // aContext : 評価 1 回分の入力
        // return : 組み立てられた行動。組み立てられなければ Failed
        protected override PPUnitAINodeResult EvaluateCore(PPUnitAIEvalContext aContext)
        {
            if (mAction == null) return PPUnitAINodeResult.Failed;

            var result = mAction.Build(aContext);
            if (!result.IsDecided) return result;

            if (mCommitTicks > 0) result = result.WithCommit(mCommitTicks);
            if (mFocusTicks > 0) result = result.WithFocus(mFocusTicks);
            return result;
        }
    }
}
