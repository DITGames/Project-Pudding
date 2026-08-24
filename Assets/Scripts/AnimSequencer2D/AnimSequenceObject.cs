/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file AnimSequenceObject.cs
 * @author hqrse
 * @date 2026/08/23
 * @brief 複数のアニメーションキーで共有される、1つの表示オブジェクトの基準(デフォルト)状態
 * =====================================*/

using System;
using AttributeUtility;
using UnityEngine;

namespace AnimSequencer2D
{
    [Serializable]
    public class AnimSequenceObject
    {
        [Label("オブジェクトID")]
        [SerializeField] private string mObjectId;

        [Label("基準Sprite")]
        [SerializeField] private Sprite mSprite;

        [Label("基準Position")]
        [SerializeField] private Vector2 mPosition;

        [Label("基準Scale")]
        [SerializeField] private Vector2 mScale = Vector2.one;

        [Label("基準Rotation")]
        [SerializeField] private Vector3 mRotation;

        [Label("基準Color")]
        [SerializeField] private Color mColor = Color.white;

        [Label("基準Material")]
        [SerializeField] private Material mBaseMaterial;

        [Label("Materialをインスタンス化する")]
        [SerializeField] private bool mInstantiateMaterial;

        [Label("デフォルトで表示する")]
        [SerializeField] private bool mDefaultVisible = true;

        public string ObjectId => mObjectId;
        public Sprite Sprite => mSprite;
        public Vector2 Position => mPosition;
        public Vector2 Scale => mScale;
        public Vector3 Rotation => mRotation;
        public Color Color => mColor;
        public Material BaseMaterial => mBaseMaterial;
        public bool InstantiateMaterial => mInstantiateMaterial;
        public bool DefaultVisible => mDefaultVisible;

        // このオブジェクトの基準値をAnimSequenceTrackStateへ変換する。アニメーションキー再生時の基準状態取得
        // (AnimSequencePlayback.BeginEntry)と、初期配置画面でのプレビュー・ギズモ表示の両方から使う共通変換
        public AnimSequenceTrackState ToBaseState() => new()
        {
            AnchoredPosition = mPosition,
            Scale = mScale,
            Rotation = mRotation,
            Color = mColor,
            Sprite = mSprite,
            Material = mBaseMaterial,
            InstantiateMaterial = mInstantiateMaterial,
            // トラック側の上書きが無い場合の既定値。上書きがある場合は呼び出し元(BeginEntry等)が解決結果で差し替える
            IsVisible = mDefaultVisible,
        };
    }
}
