/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPUnitAITreeEdge.cs
 * @author hqrse
 * @date 2026/09/02
 * @brief 経路強調で色を上書きできる接続線
 * =====================================*/

using UnityEditor.Experimental.GraphView;
using UnityEngine;

namespace PPCore
{
    // 既定の Edge は繋がっているポートの色から毎回線の色を計算し直すため、
    // edgeControl.inputColor / outputColor へ直接書き込んでも次の再計算で上書きされてしまう
    // 強調用の色を保持しておき、計算の入り口である InputColor / OutputColor を差し替えて対処する
    internal sealed class PPUnitAITreeEdge : Edge
    {
        // 強調時に使う色。null なら既定の計算に任せる
        private Color? mHighlightColor;

        // 強調時の線の太さ
        private const int HighlightedEdgeWidth = 4;

        // 強調色を設定する。null を渡すと解除して既定の色・太さへ戻る
        // aColor : 強調色。解除する場合は null
        public void SetHighlightColor(Color? aColor)
        {
            mHighlightColor = aColor;
            UpdateEdgeControl();
            MarkDirtyRepaint();
        }

        // 既定の更新のあとに強調色・太さで上書きする
        // 既定の実装がポートの色から毎回計算し直すため、そのまま書き込むだけでは次の更新で消えてしまう
        // return : 既定の実装の戻り値をそのまま返す
        public override bool UpdateEdgeControl()
        {
            bool result = base.UpdateEdgeControl();
            if (mHighlightColor.HasValue && edgeControl != null)
            {
                edgeControl.inputColor = mHighlightColor.Value;
                edgeControl.outputColor = mHighlightColor.Value;
                edgeControl.edgeWidth = HighlightedEdgeWidth;
            }
            return result;
        }
    }
}
