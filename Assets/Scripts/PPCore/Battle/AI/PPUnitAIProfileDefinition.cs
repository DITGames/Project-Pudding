/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPUnitAIProfileDefinition.cs
 * @author hqrse
 * @date 2026/08/24
 * @brief ユニット単位の判断ツリー
 * =====================================*/

using System.Collections.Generic;
using AttributeUtility;
using UnityEngine;

namespace PPCore
{
    // 判断ツリーの位置づけ
    // 評価そのものには影響せず、ツリーを探すときの絞り込みに使う
    //
    // 値はアセットへ数値のまま保存されるため、必ず明示的に振ること
    // 途中へ挿入して後続の値がずれると、既存アセットの種別が黙って別のものへ書き換わる
    // ツリーウィンドウの絞り込みボタンはこの列挙子から作られるため、足せば絞り込みも増える
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

    // ユニット 1 体分の判断ツリーを持つ ScriptableObject
    // PPUnitDefinition へアタッチし、そのユニットが毎ティック「何をするか」を決める
    //
    // 中身は条件と行動をつないだ木そのもので、スコアや重みのような間接的なパラメータは持たない
    // 「条件を満たしたら即実行、実行できなければ次の候補へ」だけで動くため、
    // アセットを読めばそのユニットの振る舞いがそのまま読める
    //
    // ノードは入れ子ではなくフラットなリストで保持し、親子関係はノード ID の参照で表す
    // ノードエディタ（Window > Unit AI Tree）で「作ってから繋ぐ」「一旦切り離す」を成立させるための構成で、
    // 評価時は根から ID を辿って木として読む
    [CreateAssetMenu(fileName = "PPUnitAIProfileDefinition", menuName = "Project-Pudding/AI/PPUnitAIProfileDefinition")]
    public class PPUnitAIProfileDefinition : ScriptableObject
    {
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

        // ID からノードを引くための索引。初回アクセス時に組み立てる
        private Dictionary<string, PPUnitAINode> mNodeMap;

        public PPUnitAITreeKind TreeKind => mTreeKind;
        public string Description => mDescription;
        public string RootNodeId => mRootNodeId;
        public IReadOnlyList<PPUnitAINode> Nodes => mNodes;
        public IReadOnlyList<PPUnitAINoteData> Notes => mNotes;

        // 評価を開始するノード。未設定・見つからない場合は null
        public PPUnitAINode Root => FindNode(mRootNodeId);

        // ID からノードを引く
        // aNodeId : 引くノードの ID
        // return : 該当ノード。未接続（ID が空）や見つからない場合は null
        public PPUnitAINode FindNode(string aNodeId)
        {
            if (string.IsNullOrEmpty(aNodeId)) return null;

            mNodeMap ??= BuildNodeMap();
            return mNodeMap.TryGetValue(aNodeId, out var node) ? node : null;
        }

        // ルートから木を評価して、このティックの行動を決める
        // aContext : 評価 1 回分の入力
        // return : 確定した行動。ルート未設定・どの枝も成立しない場合は Failed
        public virtual PPUnitAINodeResult Evaluate(PPUnitAIEvalContext aContext)
        {
            var root = Root;
            return root == null ? PPUnitAINodeResult.Failed : root.Evaluate(aContext);
        }

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

        // ID から注記を引く
        // aNoteId : 引く注記の ID
        // return : 該当する注記。見つからなければ null
        public PPUnitAINoteData FindNote(string aNoteId)
        {
            if (string.IsNullOrEmpty(aNoteId)) return null;

            foreach (var note in mNotes)
            {
                if (note != null && note.NoteId == aNoteId) return note;
            }
            return null;
        }

        // ID からノードを引く索引を組み立てる
        // ID が重複している場合は先勝ちとし、後続は引けなくなる（エディタ側で採番するため通常は起きない）
        // return : 組み立てた索引
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

        // 全ノードが持つ条件の説明文を組み立て直す
        // 説明文はグラフ上のサマリ表示に使うため、設定を変えたら追従させる必要がある
        public void RefreshConditionDescriptions()
        {
            foreach (var node in mNodes)
            {
                node?.RefreshConditionDescriptions();
            }
        }

        // アセットの読み込み・インスペクタ編集のたびに索引を捨てる
        // ノードを差し替えたのに古い索引を引き続けるのを防ぐ
        // 併せて条件の説明文も組み直し、設定とサマリ表示がずれないようにする
        protected virtual void OnValidate()
        {
            InvalidateNodeMap();
            RefreshConditionDescriptions();
        }
    }
}
