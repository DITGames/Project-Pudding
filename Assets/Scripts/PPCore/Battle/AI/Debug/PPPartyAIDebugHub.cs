/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPPartyAIDebugHub.cs
 * @author hqrse
 * @date 2026/08/10
 * @brief AI思考記録をエディタ拡張へ受け渡す中継点
 * =====================================*/

using System;
using System.Diagnostics;

namespace PPCore
{
    // AI の思考記録をエディタ拡張へ流す中継点
    // ランタイム側が UnityEditor を直接参照しないよう、静的イベントだけを公開して
    // エディタ側（PPPartyAIDebugStore）が購読する形にしている
    // CustomConsoleLog と CustomConsoleLogStore の関係と同じ構成
    public static class PPPartyAIDebugHub
    {
        // 思考が 1 回完了したときに発火する(思考記録)
        public static event Action<PPPartyAIThinkReport> OnReported;

        // 思考記録を通知する
        // Conditional によりエディタ以外ではこの呼び出し自体が消えるため、
        // プレイヤービルドでは記録のコストが一切かからない
        // aReport : 通知する思考記録
        [Conditional("UNITY_EDITOR")]
        public static void Report(PPPartyAIThinkReport aReport)
        {
            if (aReport == null)
                return;

            OnReported?.Invoke(aReport);
        }
    }
}
