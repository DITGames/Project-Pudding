/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPUnitAITreeHighlightHub.cs
 * @author hqrse
 * @date 2026/08/27
 * @brief 思考経路の選択をデバッグウィンドウからツリーウィンドウへ伝える中継点
 * =====================================*/

using System;

namespace PPCore
{
    // デバッグウィンドウで選ばれた思考記録を、ツリーウィンドウへ伝える中継点
    // 2 つのウィンドウが互いを直接参照すると、片方だけ開いている場合の扱いが煩雑になるため、
    // 選択の通知だけをここへ集約する
    public static class PPUnitAITreeHighlightHub
    {
        // 現在選ばれている思考記録。選択されていなければ null
        public static PPUnitAIThinkEntry Selected { get; private set; }

        // 選択が変わったときに発火する(選ばれた思考記録。解除時は null)
        public static event Action<PPUnitAIThinkEntry> OnSelectionChanged;

        // 選択を差し替えて通知する
        // aEntry : 選ばれた思考記録。解除する場合は null
        public static void Select(PPUnitAIThinkEntry aEntry)
        {
            Selected = aEntry;
            OnSelectionChanged?.Invoke(aEntry);
        }
    }
}
