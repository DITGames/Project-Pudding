/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPTacticParallelActionDefinition.cs
 * @author hqrse
 * @date 2026/08/11
 * @brief 戦術の手順とは独立して実行される並行アクションの定義
 * =====================================*/

using System.Collections.Generic;
using AttributeUtility;
using UnityEngine;

namespace PPCore
{
    // 戦術の手順とは独立して実行されるアクション（ScriptableObject）
    // ステップが「順番に進める手順」なのに対し、こちらは進行位置を持たず、
    // 条件さえ合えば毎思考そのまま評価される
    // 実行内容は PPTacticStepBase をそのまま流用する。実行者条件・対象選択方針・
    // スキルタグ絞り込みがステップと全く同じ形で必要になるため
    //
    // 戦術へ埋め込まずアセットにしているのは、同じアクションを複数の戦術で使い回すため
    // 「HPが低い味方を回復する」を全戦術へ書き写すと調整のたびに全箇所を直すことになる
    // 実行時の状態を持たないので、何個の戦術から参照されても互いに干渉しない
    [CreateAssetMenu(fileName = "PPTacticParallelActionDefinition",
        menuName = "Project-Pudding/AI/PPTacticParallelActionDefinition")]
    public class PPTacticParallelActionDefinition : ScriptableObject
    {
        [Header("表示")]
        [Label("アクション名")]
        [SerializeField] private string mActionName = "";
        [Label("説明")]
        [SerializeField][Multiline] private string mDescription = "";

        [Header("発動条件")]
        // AND 判定。空なら常に成立。パーティ全体の状況を見る
        [SerializeReference]
        [Label("発動条件", true)] private List<PPPartyConditionValidator> mConditions = new();

        [Header("内容")]
        // 実行内容。ステップの「達成済み判定条件」は進行位置を持たないため使わない
        [SerializeReference]
        [Label("アクション")] private PPTacticStepBase mAction;

        // 1 回の思考で何回まで実行するか。0 以下なら資源・行動回数・対象が尽きるまで繰り返す
        [Label("最大実行回数")]
        [SerializeField] private int mMaxExecutions = 1;

        // true ならメイン戦術のステップより前の実行順序を与える
        // 攻撃バフを並行アクションに置いた場合、ステップの攻撃より後だとバフが乗らないため
        // 実行順序だけを前へ動かす。資源の確保順は変えず、従来どおりステップが先に取る
        [Label("ステップより先に実行")]
        [SerializeField] private bool mIsBeforeSteps = false;

        // 表示に使うアクション名。未入力ならアセット名で代用する
        public string ActionName => string.IsNullOrEmpty(mActionName) ? name : mActionName;
        public string Description => mDescription;
        public PPTacticStepBase Action => mAction;
        public int MaxExecutions => mMaxExecutions;
        public bool IsBeforeSteps => mIsBeforeSteps;

        // 発動条件をすべて満たすかを判定する
        // 条件は AND 判定。1 つでも満たさなければ不成立
        // aSnap : 評価対象のパーティ状況スナップショット
        // return : 全ての条件を満たす場合 true
        public bool EvaluateConditions(PPPartyAIContext aSnap)
        {
            foreach (var condition in mConditions)
            {
                if (condition == null) continue;
                if (!condition.Evaluate(aSnap)) return false;
            }
            return true;
        }
    }
}
