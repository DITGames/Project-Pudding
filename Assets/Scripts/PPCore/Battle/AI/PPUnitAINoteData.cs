/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPUnitAINoteData.cs
 * @author hqrse
 * @date 2026/08/27
 * @brief 判断ツリーへ書き添える注記1枚分
 * =====================================*/

using System;
using AttributeUtility;
using UnityEngine;

namespace PPCore
{
    // 判断ツリーのグラフ上へ置く注記 1 枚分
    //
    // 「この一帯は開幕用」「ここは調整中」といった、ツリーの読み方を助ける覚え書きを残すためのもの
    // ノードの後ろに敷く色付きの面と、そこへ付ける見出しだけを持つ
    // 本文は持たない。枝の意図はノード側の説明文に書けるため、注記は「どの範囲の話か」を示す役に絞っている
    //
    // 評価には一切関わらないため、ノードとは独立したリストとしてプロファイルが持つ
    // ノードのリストへ混ぜると、評価側が注記を読み飛ばす処理を持つことになり筋が悪い
    // ID もノード ID とは別系統で振る
    [Serializable]
    public sealed class PPUnitAINoteData
    {
        // 注記の既定の色。付箋らしい黄色
        public static readonly Color DefaultColor = new(0.98f, 0.85f, 0.45f, 0.55f);

        // 注記を一意に指す ID。エディタが自動採番するため手で編集しない
        [HideInInspector]
        [SerializeField] private string mNoteId = "";
        [Label("見出し")]
        [SerializeField] private string mTitle = "";
        // 面の色。ノードの後ろに敷くため、既定では半透明にしてある
        [Label("色")]
        [SerializeField] private Color mColor = DefaultColor;
        // グラフ上の位置と大きさ
        [HideInInspector]
        [SerializeField] private Rect mRect = new(0f, 0f, 240f, 160f);

        public string NoteId => mNoteId;
        public string Title => mTitle;
        public Color Color => mColor;
        public Rect Rect => mRect;

        // aNoteId : 採番済みの ID
        // aRect : グラフ上の位置と大きさ
        public PPUnitAINoteData(string aNoteId, Rect aRect)
        {
            mNoteId = aNoteId;
            mRect = aRect;
            mTitle = "メモ";
            mColor = DefaultColor;
        }

        // 見出しを書き込む
        // aTitle : 見出し
        public void SetTitle(string aTitle) => mTitle = aTitle;

        // 面の色を書き込む
        // aColor : 設定する色
        public void SetColor(Color aColor) => mColor = aColor;

        // グラフ上の位置と大きさを書き込む
        // aRect : 設定する位置と大きさ
        public void SetRect(Rect aRect) => mRect = aRect;

        // ID が未採番なら採番する
        // 手でリストへ追加した場合や、古いアセットを開いた場合の取りこぼしを埋める
        public void EnsureNoteId()
        {
            if (string.IsNullOrEmpty(mNoteId)) mNoteId = Guid.NewGuid().ToString("N");
        }
    }
}
