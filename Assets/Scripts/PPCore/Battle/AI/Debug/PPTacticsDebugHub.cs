/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPTacticsDebugHub.cs
 * @author hqrse
 * @date 2026/08/11
 * @brief 戦術AIの思考記録をエディタ側へ受け渡すハブ
 * =====================================*/

using System;

namespace PPCore
{
    // 戦術 AI が作った思考記録を、購読側（デバッグウィンドウ）へ流すだけの中継点
    // ランタイム側がエディタのクラスを直接参照しないようにするために挟んでいる
    // 購読者が居なければ記録の生成自体を省けるよう、HasListener を見てから作ること
    public static class PPTacticsDebugHub
    {
        // 思考記録デリゲート(思考1回分の記録)
        public static event Action<PPTacticsThinkReport> OnReported;

        // 購読者が居るか。記録の組み立てコストを避けるための判定に使う
        public static bool HasListener => OnReported != null;

        // 思考記録を購読者へ流す
        // aReport : 流す記録
        public static void Report(PPTacticsThinkReport aReport)
        {
            if (aReport == null) return;

            OnReported?.Invoke(aReport);
        }
    }
}
