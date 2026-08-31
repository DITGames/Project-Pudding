/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPTurnCycleCondition.cs
 * @author hqrse
 * @date 2026/08/27
 * @brief パーティ状況条件 : 経過ターン数の周期
 * =====================================*/

using System;
using AttributeUtility;
using UnityEngine;

namespace PPCore
{
    // パーティ状況条件: 経過ターン数が指定周期にあたるか
    // 「3 ターンごとに全体攻撃」のような、一定間隔で繰り返す行動を書くためのもの
    //
    // ノード側のクールダウンでも似た形は作れるが、あちらは「前回そのノードで確定してから」を数えるため
    // 実際に撃てた時刻に引きずられて周期がずれていく
    // こちらはバトル開始からの絶対ターン数で判定するので、撃てなかったターンがあっても周期は保たれる
    [Serializable]
    [PPTypeMenuName("進行/経過ターン数の周期")]
    public sealed class PPTurnCycleCondition : PPPartyConditionValidator
    {
        // 何ターンごとに成立させるか。1 以下なら毎ターン成立する
        [Label("周期(ターン)")]
        [SerializeField] private int mCycle = 3;
        // 周期の中のどの位置で成立させるか。0 なら周期の頭で成立する
        [Label("周期内の位置")]
        [SerializeField] private int mOffset = 0;
        // 反転すると「周期にあたらないターン」の判定になる
        [Label("条件を反転する")]
        [SerializeField] private bool mIsInvert = false;

        // 経過ターン数が周期にあたるかを判定する
        // aSnapShot : 評価対象のパーティ状況スナップショット
        // return : 条件を満たす場合 true
        public override bool Evaluate(PPPartyAIContext aSnapShot)
        {
            if (mCycle <= 1) return !mIsInvert;

            // 位置が周期をはみ出していても意味のある値へ丸めておく
            int offset = ((mOffset % mCycle) + mCycle) % mCycle;
            bool isOnCycle = aSnapShot.Context.TurnCount % mCycle == offset;
            return isOnCycle != mIsInvert;
        }

        // 設定内容から説明文を組み立てる
        [ContextMenu("説明文を生成")]
        protected override void BuildDescription()
        {
            string body = mCycle <= 1 ? "毎ターン" : $"{mCycle}ターンごと(位置{mOffset})";
            mDescription = mIsInvert ? $"{body}ではない" : body;
        }
    }
}
