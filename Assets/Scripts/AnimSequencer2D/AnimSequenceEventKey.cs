/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file AnimSequenceEventKey.cs
 * @author hqrse
 * @date 2026/08/21
 * @brief タイムライン上の任意時刻に通知イベントを発火させるためのイベントキー定義
 * =====================================*/

using System;
using AttributeUtility;
using UnityEngine;

namespace AnimSequencer2D
{
    [Serializable]
    public class AnimSequenceEventKey
    {
        [Label("時刻(秒)")]
        [SerializeField] private float mTime;

        [Label("イベントキー")]
        [SerializeField] private string mEventKey;

        // タイムライン上で選択・ドラッグ対象を追跡するための内部ID。並べ替え後も選択を保てるようにする
        [SerializeField, HideInInspector] private string mKeyframeId = Guid.NewGuid().ToString("N");

        public float Time => mTime;
        public string EventKey => mEventKey;
        public string KeyframeId => mKeyframeId;
    }
}
