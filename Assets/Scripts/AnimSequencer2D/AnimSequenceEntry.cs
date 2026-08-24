/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file AnimSequenceEntry.cs
 * @author hqrse
 * @date 2026/08/21
 * @brief 1つのアニメーション(アニメーションキー1件分)の定義。トラック・イベントキー・終了時挙動を保持する
 * =====================================*/

using System;
using System.Collections.Generic;
using AttributeUtility;
using UnityEngine;

namespace AnimSequencer2D
{
    [Serializable]
    public class AnimSequenceEntry
    {
        [Label("アニメーションキー")]
        [SerializeField] private string mKey;

        // 開始/終了イベント通知時にアニメーションキーと一緒に渡す分類用の識別子。自由文字列で、体系化はプロジェクト側に委ねる
        [Label("タグ")]
        [SerializeField] private string mTag;

        [Label("長さ(秒)")]
        [SerializeField] private float mDuration = 1f;

        [Label("トラック", true)]
        [SerializeField] private List<AnimSequenceTrack> mTracks = new();

        [Label("イベントキー", true)]
        [SerializeField] private List<AnimSequenceEventKey> mEventKeys = new();

        [Label("終了時の挙動")]
        [SerializeField] private AnimSequenceEndBehavior mEndBehavior = AnimSequenceEndBehavior.Stop;

        // EditConditionはbool型メンバしか条件に取れないため、遷移設定の表示制御用に判定プロパティを用意する
        [Label("遷移先キー")]
        [EditCondition(nameof(IsTransition), true)]
        [SerializeField] private string mTransitionTargetKey;

        [Label("時間の種類")]
        [SerializeField] private AnimSequenceTimeMode mTimeMode = AnimSequenceTimeMode.Scaled;

        // TimeModeがUnscaledの場合は常にポーズの影響を受けないため、この設定はScaled時のみ意味を持つ
        [Label("ポーズ中も進行する")]
        [EditCondition(nameof(IsScaledTimeMode), true)]
        [SerializeField] private bool mPlayWhilePaused;

        // AnimSequenceEntryGraphView上での配置座標。通常のインスペクタには表示させない
        [SerializeField, HideInInspector] private Vector2 mGraphPosition;

        public string Key => mKey;
        public string Tag => mTag;
        public float Duration => mDuration;
        public IReadOnlyList<AnimSequenceTrack> Tracks => mTracks;
        public IReadOnlyList<AnimSequenceEventKey> EventKeys => mEventKeys;
        public AnimSequenceEndBehavior EndBehavior => mEndBehavior;
        public string TransitionTargetKey => mTransitionTargetKey;
        public AnimSequenceTimeMode TimeMode => mTimeMode;
        public bool PlayWhilePaused => mPlayWhilePaused;
        public Vector2 GraphPosition { get => mGraphPosition; set => mGraphPosition = value; }

        // EditConditionAttributeの条件対象(bool型のみ参照可能なため用意する判定プロパティ)
        private bool IsTransition => mEndBehavior == AnimSequenceEndBehavior.Transition;
        private bool IsScaledTimeMode => mTimeMode == AnimSequenceTimeMode.Scaled;

        // 評価は時刻昇順を前提とするため、トラックのキーフレームとイベントキーを時刻昇順に整列する
        public void SortKeyframes()
        {
            foreach (AnimSequenceTrack track in mTracks)
            {
                track.SortKeyframes();
            }
            mEventKeys.Sort((a, b) => a.Time.CompareTo(b.Time));
        }
    }
}
