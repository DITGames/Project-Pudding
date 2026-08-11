/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPBattleTacticsDefinition.cs
 * @author hqrse
 * @date 2026/08/11
 * @brief 戦術定義
 * =====================================*/

using System.Collections.Generic;
using CommandBattleCore;
using UnityEngine;

namespace PPCore
{
    // パーティ AI が取る戦術 1 件分の定義（ScriptableObject）
    // 「どういう状況で（条件リスト）」「何をするか（ステップリスト）」を 1 アセットにまとめたもの
    // 複数のプロファイルから同じ戦術を使い回せるよう、優先度はプロファイル側の並び順で持ち、
    // このアセット自体は優先度を持たない
    // ステップを 1 つも持たない戦術は「何もしない」＝溜めを表す
    // 待機のための専用フラグを設けず、低い優先度に置いた空の戦術で表現する
    [CreateAssetMenu(fileName = "PPBattleTacticsDefinition", menuName = "Project-Pudding/AI/PPBattleTacticsDefinition")]
    public class PPBattleTacticsDefinition : ScriptableObject
    {
        [Header("表示")]
        [Label("戦術名")]
        [SerializeField] private string mTacticsName = "";
        [Label("説明")]
        [SerializeField][Multiline] private string mDescription = "";

        [Header("成立条件")]
        // AND 判定。空なら常に成立する
        [SerializeReference]
        [Label("条件リスト", true)] private List<PPPartyConditionValidator> mConditions = new();

        [Header("戦術内容")]
        // 順序付きの手順。空リストなら「何もしない（溜め）」戦術になる
        [SerializeReference]
        [Label("ステップリスト", true)] private List<PPTacticStepBase> mSteps = new();

        [Header("制御")]
        [Label("1バトル1回のみ")]
        [SerializeField] private bool mIsDoOnce = false;
        // 完走・中断した時点から数え始める
        [Label("クールタイム(ティック)")]
        [SerializeField] private int mCooldownTicks = 0;
        // 「あと何ティック待てば撃てるか」の許容値。0 なら今払える場合のみ成立する
        [Label("許容待機ティック数")]
        [SerializeField] private float mAllowedWaitTicks = 0f;
        // true なら達成済みステップのスキップを行わず、必ず先頭から実行する
        [Label("常に先頭から実行")]
        [SerializeField] private bool mIsAlwaysRestart = false;

        // 表示に使う戦術名。未入力ならアセット名で代用する
        public string TacticsName => string.IsNullOrEmpty(mTacticsName) ? name : mTacticsName;
        public string Description => mDescription;
        public IReadOnlyList<PPPartyConditionValidator> Conditions => mConditions;
        public IReadOnlyList<PPTacticStepBase> Steps => mSteps;
        public bool IsDoOnce => mIsDoOnce;
        public int CooldownTicks => Mathf.Max(0, mCooldownTicks);
        public float AllowedWaitTicks => Mathf.Max(0f, mAllowedWaitTicks);
        public bool IsAlwaysRestart => mIsAlwaysRestart;

        // 実際に実行できるステップ数。null 要素は数えない
        public int ValidStepCount
        {
            get
            {
                int count = 0;
                foreach (var step in mSteps)
                {
                    if (step != null) count++;
                }
                return count;
            }
        }

        // 成立条件をすべて満たすかを判定する
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
