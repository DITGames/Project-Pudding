/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPUnitAIDebugStore.cs
 * @author hqrse
 * @date 2026/08/24
 * @brief ユニットAIの思考記録をためておくエディタ側のストア
 * =====================================*/

using System;
using System.Collections.Generic;
using UnityEditor;

namespace PPCore
{
    // ユニット AI の思考記録をためておくエディタ側のストア
    // バトルはプッシャーと並行してリアルタイムに進むため、見たい瞬間をその場で捉えられない
    // 一定件数を保持しておき、あとから遡って確認できるようにするのがこのクラスの役割
    // ウィンドウを開いていない間もためたいので、購読はウィンドウではなくここが持つ
    [InitializeOnLoad]
    public static class PPUnitAIDebugStore
    {
        // 保持する記録の最大件数。超えた分は古いものから捨てる
        public const int MaxCount = 200;

        // ためている記録。新しいものほど後ろ
        private static readonly List<PPUnitAIThinkReport> mReports = new();

        // 記録が追加されたときに呼ばれる。ウィンドウの再描画に使う
        public static event Action OnAdded;

        // ためている記録の読み取り専用ビュー
        public static IReadOnlyList<PPUnitAIThinkReport> Reports => mReports;

        static PPUnitAIDebugStore()
        {
            PPUnitAIDebugHub.OnReported += Add;
            // ドメインリロードやプレイモード切り替えをまたいだ記録は混ざると紛らわしいので捨てる
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        // 記録を 1 件追加する。上限を超えた分は古い方から捨てる
        // aReport : 追加する記録
        private static void Add(PPUnitAIThinkReport aReport)
        {
            if (aReport == null) return;

            mReports.Add(aReport);
            while (mReports.Count > MaxCount)
            {
                mReports.RemoveAt(0);
            }
            OnAdded?.Invoke();
        }

        // ためている記録をすべて捨てる
        public static void Clear()
        {
            mReports.Clear();
            OnAdded?.Invoke();
        }

        // プレイモードに入るタイミングで記録を初期化する
        // aState : 遷移後のプレイモード状態
        private static void OnPlayModeChanged(PlayModeStateChange aState)
        {
            if (aState == PlayModeStateChange.EnteredPlayMode)
            {
                Clear();
            }
        }
    }
}
