/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPUnitBehaviourTree.cs
 * @author hqrse
 * @date 2026/08/24
 * @brief ユニットAIのビヘイビアツリーアセットとノートデータ
 * =====================================*/

using System;
using System.Collections.Generic;
using UnityEngine;
using AttributeUtility;

namespace PPCore
{
    // 判断ツリーの位置づけ。評価には影響せず、ツリーを探すときの絞り込みに使う
    // アセットへ数値で保存されるため、値は必ず明示的に振り、途中へ挿入しない
    public enum PPUnitAITreeKind
    {
        // ユニットへ直接割り当てる、根から評価が始まるツリー
        [InspectorName("メインツリー")]
        Main = 0,
        // サブツリー参照ノードから呼ばれる、部品として使い回すツリー
        // 下の役割別に当てはまらないものはこれにする
        [InspectorName("サブツリー")]
        SubTree = 1,
        // 攻撃の組み立てを担うサブツリー
        [InspectorName("サブツリー(攻撃)")]
        SubTreeAttack = 2,
        // 回復の判断を担うサブツリー
        [InspectorName("サブツリー(回復)")]
        SubTreeHeal = 3,
        // バフ・デバフなど支援の判断を担うサブツリー
        [InspectorName("サブツリー(支援)")]
        SubTreeSupport = 4,
    }

    // ユニット 1 体分の判断ツリーを持つ ScriptableObject。条件と行動をつないだ木で、次の行動を決める
    // ノードはフラットなリストで保持し、親子関係はノード ID の参照で表す
    [CreateAssetMenu(fileName = "PPUnitBehaviourTree", menuName = "Project-Pudding/AI/PPUnitBehaviourTree")]
    public class PPUnitBehaviourTree : ScriptableObject
    {
        /* インスペクタ設定 */

        [Header("表示")]
        // ツリーの位置づけ。評価には影響せず、ツリーウィンドウの一覧で絞り込むために使う
        [Label("ツリー種別")]
        [SerializeField] protected PPUnitAITreeKind mTreeKind = PPUnitAITreeKind.Main;

        [Label("説明")]
        [SerializeField][Multiline] protected string mDescription = "";

        [Header("判断ツリー")]
        // 評価を開始するノードの ID。ノードエディタから設定する
        [Label("ルートノードID")]
        [SerializeField] protected string mRootNodeId = "";

        // ツリーを構成する全ノード。並び順に意味は無く、接続は ID の参照で表す
        [Label("ノード", true)]
        [SerializeReference]
        [SerializeField] protected List<PPUnitAINode> mNodes = new();

        // グラフ上へ置く注記。評価には関わらないため、ノードとは別のリストで持つ
        [Label("注記", true)]
        [SerializeField] protected List<PPUnitAINoteData> mNotes = new();

        /* 状態 */

        // ID からノードを引くための索引。初回アクセス時に組み立てる
        private Dictionary<string, PPUnitAINode> mNodeMap;

        /* プロパティ */

        public PPUnitAITreeKind TreeKind => mTreeKind;
        public string Description => mDescription;
        public string RootNodeId => mRootNodeId;
        public IReadOnlyList<PPUnitAINode> Nodes => mNodes;
        public IReadOnlyList<PPUnitAINoteData> Notes => mNotes;
        // 評価を開始するノード。未設定・見つからない場合は null
        public PPUnitAINode Root => FindNode(mRootNodeId);

        /* 生成 */

        // ID からノードを引く索引を組み立てる
        // ID が重複している場合は先勝ちとし、後続は引けなくなる（エディタ側で採番するため通常は起きない）
        private Dictionary<string, PPUnitAINode> BuildNodeMap()
        {
            var map = new Dictionary<string, PPUnitAINode>();
            foreach (var node in mNodes)
            {
                if (node == null || string.IsNullOrEmpty(node.NodeId)) continue;

                map.TryAdd(node.NodeId, node);
            }
            return map;
        }

        /* Unity イベント */

        // アセットの読み込み・インスペクタ編集のたびに索引を捨てる
        // 併せて条件の説明文も組み直し、設定とサマリ表示を一致させる
        protected virtual void OnValidate()
        {
            InvalidateNodeMap();
            RefreshConditionDescriptions();
        }

        /* 操作 */

        // ID 索引を捨てて、次のアクセスで組み立て直させる
        // ノードエディタがノードを追加・削除した際に呼ぶ
        public void InvalidateNodeMap() => mNodeMap = null;

        // ID 未採番のノードへ ID を振る
        // 手でリストへ追加したノードや、古いアセットを開いた場合の取りこぼしを埋める
        public void EnsureNodeIds()
        {
            foreach (var node in mNodes)
            {
                node?.EnsureNodeId();
            }
            foreach (var note in mNotes)
            {
                note?.EnsureNoteId();
            }
            InvalidateNodeMap();
        }

        /* 評価・判定 */

        // ルートから木を評価して、次の行動を決める
        public virtual PPUnitAINodeResult Evaluate(PPUnitAIEvalContext aContext)
        {
            var root = Root;
            return root == null ? PPUnitAINodeResult.Failed : root.Evaluate(aContext);
        }

        /* 問い合わせ */

        // ID からノードを引く
        public PPUnitAINode FindNode(string aNodeId)
        {
            if (string.IsNullOrEmpty(aNodeId)) return null;

            mNodeMap ??= BuildNodeMap();
            return mNodeMap.TryGetValue(aNodeId, out var node) ? node : null;
        }

        // ID から注記を引く
        public PPUnitAINoteData FindNote(string aNoteId)
        {
            if (string.IsNullOrEmpty(aNoteId)) return null;

            foreach (var note in mNotes)
            {
                if (note != null && note.NoteId == aNoteId) return note;
            }
            return null;
        }

        /* 説明文 */

        // 全ノードが持つ条件の説明文を組み立て直す
        // 説明文はグラフ上のサマリ表示に使うため、設定を変えたら追従させる必要がある
        public void RefreshConditionDescriptions()
        {
            foreach (var node in mNodes)
            {
                node?.RefreshConditionDescriptions();
            }
        }
    }

    // 判断ツリーのグラフ上へ置く注記 1 枚分。色付きの面と見出しだけを持ち、評価には関わらない
    // ノードとは独立したリストで持ち、ID も別系統で振る
    [Serializable]
    public sealed class PPUnitAINoteData
    {
        /* 定数 */

        // 注記の既定の色。付箋らしい黄色
        public static readonly Color DefaultColor = new(0.98f, 0.85f, 0.45f, 0.55f);

        /* インスペクタ設定 */

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

        /* プロパティ */

        public string NoteId => mNoteId;
        public string Title => mTitle;
        public Color Color => mColor;
        public Rect Rect => mRect;

        /* 生成 */

        // aNoteId : 採番済みの ID
        // aRect : グラフ上の位置と大きさ
        public PPUnitAINoteData(string aNoteId, Rect aRect)
        {
            mNoteId = aNoteId;
            mRect = aRect;
            mTitle = "メモ";
            mColor = DefaultColor;
        }

        /* 操作 */

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
