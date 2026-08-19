/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PreviewVFXSequenceHost.cs
 * @author hqrse
 * @date 2026/08/18
 * @brief エディタ埋め込みプレビュー用に、PreviewRenderUtility上でVFXの再生・停止・パラメータ適用を行うIVFXSequenceHost実装
 * VFXSequenceGraphExecutorを差し替えるだけでランタイムと同じDelay計算・並列分岐・イベント発火・Stop処理を共有できる
 * =====================================*/

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.VFX;

namespace VFXUtility.Editor
{
    internal class PreviewVFXSequenceHost : IVFXSequenceHost, IDisposable
    {
        // カメラのズーム可能範囲
        private const float MinCameraDistance = 1f;
        private const float MaxCameraDistance = 30f;
        // グリッドの半径(マス数)とマス間隔
        private const int GridHalfExtent = 5;

        private readonly PreviewRenderUtility mPreviewUtility;
        private readonly List<VisualEffect> mActiveEffects = new();

        // カメラの周回角度(x: yaw, y: pitch)と原点からの距離。マウスドラッグ・スクロールで変更する
        private Vector2 mCameraOrbit;
        private float mCameraDistance;

        // グリッド表示のON/OFF。ウィンドウ側のトグルボタンから設定される
        public bool ShowGrid { get; set; }

        public Vector2 CameraOrbit => mCameraOrbit;
        public float CameraDistance => mCameraDistance;

        // aInitialCameraOrbit : 初期カメラ周回角度(x: yaw, y: pitch)
        // aInitialCameraDistance : 初期カメラ距離
        // aShowGrid : グリッド表示の初期状態
        public PreviewVFXSequenceHost(Vector2 aInitialCameraOrbit, float aInitialCameraDistance, bool aShowGrid)
        {
            mCameraOrbit = aInitialCameraOrbit;
            mCameraDistance = Mathf.Clamp(aInitialCameraDistance, MinCameraDistance, MaxCameraDistance);
            ShowGrid = aShowGrid;

            mPreviewUtility = new PreviewRenderUtility();
            mPreviewUtility.cameraFieldOfView = 30f;
            mPreviewUtility.camera.nearClipPlane = 0.1f;
            mPreviewUtility.camera.farClipPlane = 100f;
        }

        object IVFXSequenceHost.PlayVFX(VisualEffectAsset aAsset)
        {
            var go = new GameObject($"PreviewVFX_{aAsset.name}");
            var visualEffect = go.AddComponent<VisualEffect>();
            visualEffect.visualEffectAsset = aAsset;
            mPreviewUtility.AddSingleGO(go);
            visualEffect.Play();

            mActiveEffects.Add(visualEffect);
            return visualEffect;
        }

        void IVFXSequenceHost.StopVFX(object aVfxHandle)
        {
            if (aVfxHandle is not VisualEffect visualEffect || visualEffect == null)
            {
                return;
            }

            mActiveEffects.Remove(visualEffect);
            visualEffect.Stop();
            UnityEngine.Object.DestroyImmediate(visualEffect.gameObject);
        }

        // 型に応じたVisualEffect.Set*を呼び出す。ColorのみVector4(r,g,b,a)に変換してSetVector4を呼ぶ
        void IVFXSequenceHost.ApplyParameter(object aVfxHandle, string aParamName, VFXParameterType aParamType, object aValue)
        {
            if (aVfxHandle is not VisualEffect visualEffect || visualEffect == null)
            {
                return;
            }

            switch (aParamType)
            {
                case VFXParameterType.Float:
                    visualEffect.SetFloat(aParamName, (float)aValue);
                    break;
                case VFXParameterType.Int:
                    visualEffect.SetInt(aParamName, (int)aValue);
                    break;
                case VFXParameterType.Bool:
                    visualEffect.SetBool(aParamName, (bool)aValue);
                    break;
                case VFXParameterType.Vector2:
                    visualEffect.SetVector2(aParamName, (Vector2)aValue);
                    break;
                case VFXParameterType.Vector3:
                    visualEffect.SetVector3(aParamName, (Vector3)aValue);
                    break;
                case VFXParameterType.Vector4:
                    visualEffect.SetVector4(aParamName, (Vector4)aValue);
                    break;
                case VFXParameterType.Color:
                    Color color = (Color)aValue;
                    visualEffect.SetVector4(aParamName, new Vector4(color.r, color.g, color.b, color.a));
                    break;
                case VFXParameterType.Event:
                    visualEffect.SendEvent(aParamName);
                    break;
            }
        }

        bool IVFXSequenceHost.IsAlive(object aVfxHandle)
        {
            return aVfxHandle is VisualEffect visualEffect && visualEffect != null && visualEffect.aliveParticleCount > 0;
        }

        // 全ての再生中VFXを明示的にシミュレーションで進める。エディタはPlayモード外でVFXを自動進行させないため必須
        // aDeltaTime : 進める秒数
        public void AdvanceSimulation(float aDeltaTime)
        {
            foreach (VisualEffect visualEffect in mActiveEffects)
            {
                if (visualEffect != null)
                {
                    visualEffect.Simulate(aDeltaTime, 1);
                }
            }
        }

        // aRect : 描画領域。マウス入力の処理も行うため、毎GUIイベントで呼ぶこと(描画自体はRepaintイベント時のみ行う)
        public void DrawPreview(Rect aRect)
        {
            HandleCameraInput(aRect);

            if (Event.current.type != EventType.Repaint)
            {
                return;
            }

            Quaternion rotation = Quaternion.Euler(mCameraOrbit.y, mCameraOrbit.x, 0f);
            mPreviewUtility.camera.transform.rotation = rotation;
            mPreviewUtility.camera.transform.position = rotation * new Vector3(0f, 0f, -mCameraDistance);

            mPreviewUtility.BeginPreview(aRect, GUIStyle.none);

            // 本プロジェクトはURP(Scriptable Render Pipeline)を使用しているため、allowScriptableRenderPipelines を true にしないと何も描画されない
            mPreviewUtility.Render(true);

            // グリッドはカメラのRender()より後に描画する(先に描画するとRender()の通常描画で上書きされて消える)
            if (ShowGrid)
            {
                DrawGrid();
            }

            mPreviewUtility.EndAndDrawPreview(aRect);
        }

        // XZ平面上に原点を中心とした基準グリッドを描画する
        private void DrawGrid()
        {
            Handles.SetCamera(mPreviewUtility.camera);
            Handles.color = new Color(1f, 1f, 1f, 0.25f);

            for (int i = -GridHalfExtent; i <= GridHalfExtent; i++)
            {
                Handles.DrawLine(new Vector3(i, 0f, -GridHalfExtent), new Vector3(i, 0f, GridHalfExtent));
                Handles.DrawLine(new Vector3(-GridHalfExtent, 0f, i), new Vector3(GridHalfExtent, 0f, i));
            }
        }

        // 左ドラッグでカメラを原点周りに周回、スクロールで距離(ズーム)を変更する。Unity標準のプレビューカメラ操作(Editor.OnPreviewGUIのDrag2D相当)を踏襲
        // aRect : マウス入力を受け付ける領域
        private void HandleCameraInput(Rect aRect)
        {
            int controlId = GUIUtility.GetControlID("VFXSequencerPreviewCamera".GetHashCode(), FocusType.Passive);
            Event evt = Event.current;

            switch (evt.GetTypeForControl(controlId))
            {
                case EventType.MouseDown:
                    if (aRect.Contains(evt.mousePosition))
                    {
                        GUIUtility.hotControl = controlId;
                        EditorGUIUtility.SetWantsMouseJumping(1);
                        evt.Use();
                    }
                    break;

                case EventType.MouseDrag:
                    if (GUIUtility.hotControl == controlId)
                    {
                        mCameraOrbit.x += evt.delta.x;
                        mCameraOrbit.y = Mathf.Clamp(mCameraOrbit.y + evt.delta.y, -89f, 89f);
                        evt.Use();
                        GUI.changed = true;
                    }
                    break;

                case EventType.MouseUp:
                    if (GUIUtility.hotControl == controlId)
                    {
                        GUIUtility.hotControl = 0;
                        EditorGUIUtility.SetWantsMouseJumping(0);
                        evt.Use();
                    }
                    break;

                case EventType.ScrollWheel:
                    if (aRect.Contains(evt.mousePosition))
                    {
                        mCameraDistance = Mathf.Clamp(mCameraDistance + evt.delta.y * 0.3f, MinCameraDistance, MaxCameraDistance);
                        evt.Use();
                        GUI.changed = true;
                    }
                    break;
            }
        }

        public void Dispose()
        {
            foreach (VisualEffect visualEffect in mActiveEffects)
            {
                if (visualEffect != null)
                {
                    UnityEngine.Object.DestroyImmediate(visualEffect.gameObject);
                }
            }
            mActiveEffects.Clear();
            mPreviewUtility.Cleanup();
        }
    }
}
