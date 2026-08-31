/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPUnitAISubTreeNode.cs
 * @author hqrse
 * @date 2026/08/27
 * @brief 別の判断ツリーへ評価を委ねるノード
 * =====================================*/

using System;
using AttributeUtility;
using CustomConsole;
using UnityEngine;

namespace PPCore
{
    // 別の判断ツリー（PPUnitAIProfileDefinition）へ評価をそのまま委ねるノード
    //
    // 「瀕死の味方を回復する」「弱点を突く」といった定番の枝を 1 つのアセットへ切り出し、
    // 複数のユニットのツリーから同じものを指して使い回すためのもの
    //
    // 評価は参照先のルートへ渡り、その結果をそのまま自分の結果として返す
    // 参照先で行動が確定しなければ自分も不成立となり、親の優先度リストが次の候補へ進む
    //
    // 参照先を評価している間は、子ノードの ID 解決先も参照先のツリーへ切り替わる
    // ノード ID はツリーごとに閉じているため、切り替えないと参照先の子を引けないまま不成立になる
    //
    // 循環参照（自分自身や、辿ると自分へ戻ってくるツリー）を踏むと評価が無限に潜るため、
    // 評価中のツリーを追跡して検出したらその場で打ち切る
    // エディタ側の診断でも同じ形を警告するが、アセットは実行時に差し替えられるためランタイム側にも歯止めを置く
    [Serializable]
    [PPTypeMenuName("制御/サブツリー参照")]
    public sealed class PPUnitAISubTreeNode : PPUnitAINode
    {
        [Header("参照先")]
        [Label("判断ツリー")]
        [SerializeField] private PPUnitAIProfileDefinition mSubTree;

        // 循環参照の警告を既に出したか
        // 評価は思考のたびに走るため、抑止しないと同じ警告でコンソールが埋まり他のトレースが読めなくなる
        // 診断のためだけのフラグでバトルの状態は変えないため、「ノードは状態を変えない」約束とは衝突しない
        [NonSerialized] private bool mIsWarnedCycle;

        protected override string DefaultNodeName
            => mSubTree != null ? $"参照 : {mSubTree.name}" : "サブツリー参照";

        // 参照先が設定されているか。エディタの診断から参照する
        public bool HasSubTree => mSubTree != null;

        // 参照先の判断ツリー。エディタの診断が循環参照を辿るために参照する
        public PPUnitAIProfileDefinition SubTree => mSubTree;

        // 参照先のアセット名を示す
        public override string Summary => mSubTree != null ? mSubTree.name : "参照先が未設定";

        // 参照先のルートへ評価を渡し、その結果をそのまま返す
        // aContext : 評価 1 回分の入力
        // return : 参照先が確定させた行動。未設定・循環・参照先が不成立なら Failed
        protected override PPUnitAINodeResult EvaluateCore(PPUnitAIEvalContext aContext)
        {
            if (mSubTree == null) return PPUnitAINodeResult.Failed;

            if (!aContext.TryPushProfile(mSubTree))
            {
                if (!mIsWarnedCycle)
                {
                    mIsWarnedCycle = true;
                    CustomConsoleLog.Warning("AI",
                        $"「{NodeName}」で判断ツリー「{mSubTree.name}」が循環参照しているため、この枝を打ち切りました。");
                }
                return PPUnitAINodeResult.Failed;
            }

            try
            {
                return mSubTree.Evaluate(aContext);
            }
            finally
            {
                // 参照先が不成立で終わっても必ず元のツリーへ戻す
                // 戻し忘れると、以降の兄弟ノードが参照先のツリーから子を引こうとして全て不成立になる
                aContext.PopProfile();
            }
        }
    }
}
