/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPUnitAIDebugHub.cs
 * @author hqrse
 * @date 2026/08/24
 * @brief ユニットAIの思考記録をエディタへ流す中継点
 * =====================================*/

using System;

namespace PPCore
{
    // ユニット AI の思考記録をエディタ側へ流すための中継点
    // ランタイム側がエディタのクラスを直接参照しないよう、購読の口だけをここに置く
    // 購読者が居ないときは記録の組み立て自体を省けるよう、HasListener を先に確認できるようにしている
    public static class PPUnitAIDebugHub
    {
        // 思考記録が報告されたときに発火する(思考1回分の記録)
        public static event Action<PPUnitAIThinkReport> OnReported;

        // 購読者が居るか。記録の組み立てを省く判定に使う
        public static bool HasListener => OnReported != null;

        // 思考記録を購読者へ流す
        // aReport : 思考 1 回分の記録
        public static void Report(PPUnitAIThinkReport aReport) => OnReported?.Invoke(aReport);
    }
}
