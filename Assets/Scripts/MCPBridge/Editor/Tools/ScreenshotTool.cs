/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file ScreenshotTool.cs
 * @author hqrse
 * @date 2026/08/19
 * @brief オンデマンドでシーンのスクリーンショットを取得するMCPツール
 * 継続的な動画ストリーミングは行わず、呼び出し時点の単発キャプチャのみを返す。
 *
 * ScreenCapture.CaptureScreenshotAsTextureはEditモードでは無効なテクスチャを返し、
 * Playモードではフレーム終端(WaitForEndOfFrame)に依存するため、コルーチンを持たない
 * 本ツールの同期実行(RunOnMainThread)から呼ぶと応答が返らずEditorごとフリーズしうることが
 * 実際の動作確認で判明した。そのためCamera.Render()による同期的なRenderTexture書き込みに
 * 置き換え、Edit/Playいずれのモードでも即座に完了する形にしている
 * =====================================*/

using System;
using MCPBridge.Editor.Server;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace MCPBridge.Editor.Tools
{
    public sealed class ScreenshotTool : IMCPTool
    {
        private const int CaptureWidth = 1280;
        private const int CaptureHeight = 720;

        public string Name => "screenshot";

        public string Description => "呼び出し時点のシーンをCameraからキャプチャし、PNG画像(base64)として返します。";

        public JObject InputSchema => new()
        {
            ["type"] = "object",
            ["properties"] = new JObject(),
        };

        public JToken Invoke(JObject aArguments)
        {
            return MCPMainThreadDispatcher.RunOnMainThread(() =>
            {
                var camera = Camera.main != null ? Camera.main : UnityEngine.Object.FindFirstObjectByType<Camera>();
                if (camera == null)
                {
                    throw new InvalidOperationException("キャプチャ可能なCameraがシーンに見つかりません。");
                }

                var renderTexture = new RenderTexture(CaptureWidth, CaptureHeight, 24);
                var previousTargetTexture = camera.targetTexture;
                var previousActive = RenderTexture.active;
                Texture2D texture = null;

                try
                {
                    camera.targetTexture = renderTexture;
                    camera.Render();

                    RenderTexture.active = renderTexture;
                    texture = new Texture2D(CaptureWidth, CaptureHeight, TextureFormat.RGB24, false);
                    texture.ReadPixels(new Rect(0, 0, CaptureWidth, CaptureHeight), 0, 0);
                    texture.Apply();

                    var pngBytes = texture.EncodeToPNG();
                    return new JObject
                    {
                        ["mimeType"] = "image/png",
                        ["base64"] = Convert.ToBase64String(pngBytes),
                    };
                }
                finally
                {
                    camera.targetTexture = previousTargetTexture;
                    RenderTexture.active = previousActive;
                    if (texture != null)
                    {
                        UnityEngine.Object.DestroyImmediate(texture);
                    }
                    renderTexture.Release();
                    UnityEngine.Object.DestroyImmediate(renderTexture);
                }
            });
        }
    }
}
