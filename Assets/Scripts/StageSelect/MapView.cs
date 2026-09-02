using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace StageSelect
{
    [System.Serializable]
    public class NodeSpriteEntry
    {
        public NodeType type;
        public Sprite sprite;
    }

    /// <summary>
    /// マップの生成・描画・進行を担当するメインクラス。
    /// ScrollRect の Content に貼り付けて使う。
    /// </summary>
    public class MapView : MonoBehaviour
    {
        [Header("参照")]
        [SerializeField] RectTransform content;          // ノードと線を並べる親（未指定なら自分自身）
        [SerializeField] MapNodeView nodePrefab;
        [SerializeField] MapConnectionView connectionPrefab;
        [SerializeField] ScrollRect scrollRect;          // 任意

        [Header("アイコン")]
        [SerializeField] List<NodeSpriteEntry> nodeSprites = new List<NodeSpriteEntry>();

        [Header("生成設定")]
        [SerializeField] MapGenerationSettings settings = new MapGenerationSettings();
        [SerializeField] bool useRandomSeed = true;
        [SerializeField] int seed = 12345;
        [SerializeField] float contentMargin = 200f;

        /// <summary>マスに入った時に発火。ここから戦闘シーンなどへ繋ぐ。</summary>
        public event System.Action<MapNode> OnNodeEntered;

        public MapGraph Graph { get; private set; }
        public MapNode CurrentNode { get; private set; }

        readonly Dictionary<MapNode, MapNodeView> nodeViews = new Dictionary<MapNode, MapNodeView>();
        readonly List<MapConnectionView> connectionViews = new List<MapConnectionView>();
        readonly HashSet<MapNode> visited = new HashSet<MapNode>();

        void Start()
        {
            if (content == null) content = (RectTransform)transform;
            Build();
        }

        // ---------------------------------------------------------------
        // 構築
        // ---------------------------------------------------------------
        [ContextMenu("Rebuild")]
        public void Build()
        {
            if (content == null) content = (RectTransform)transform;

            Clear();

            int actualSeed = useRandomSeed ? Random.Range(int.MinValue, int.MaxValue) : seed;
            Graph = MapGenerator.Generate(settings, actualSeed);

            // Content のサイズを合わせる（縦スクロール用）
            content.sizeDelta = new Vector2(
                settings.width * settings.spacingX + contentMargin,
                (settings.height + 1) * settings.spacingY + contentMargin);

            // 線を先に生成（ノードの後ろに描画されるようにする）
            foreach (var node in Graph.AllNodesWithBoss())
                foreach (var next in node.Next)
                    CreateConnection(node, next);

            foreach (var node in Graph.AllNodesWithBoss())
                CreateNodeView(node);

            CurrentNode = null;
            visited.Clear();
            RefreshStates();

            if (scrollRect != null) scrollRect.verticalNormalizedPosition = 0f; // 最下段から開始
        }

        void Clear()
        {
            foreach (var v in nodeViews.Values)
                if (v != null) DestroyImmediateSafe(v.gameObject);
            foreach (var c in connectionViews)
                if (c != null) DestroyImmediateSafe(c.gameObject);

            nodeViews.Clear();
            connectionViews.Clear();
        }

        void DestroyImmediateSafe(GameObject go)
        {
            if (Application.isPlaying) Destroy(go);
            else DestroyImmediate(go);
        }

        void CreateNodeView(MapNode node)
        {
            var view = Instantiate(nodePrefab, content);
            var rt = (RectTransform)view.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0.5f);

            view.Setup(node, GetSprite(node.Type), OnNodeClicked);
            nodeViews[node] = view;
        }

        void CreateConnection(MapNode from, MapNode to)
        {
            var view = Instantiate(connectionPrefab, content);
            var rt = (RectTransform)view.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);

            view.Setup(from, to);
            connectionViews.Add(view);
        }

        Sprite GetSprite(NodeType type)
        {
            foreach (var e in nodeSprites)
                if (e.type == type) return e.sprite;
            return null;
        }

        // ---------------------------------------------------------------
        // 進行
        // ---------------------------------------------------------------
        void OnNodeClicked(MapNodeView view)
        {
            var node = view.Node;
            if (!IsSelectable(node)) return;

            CurrentNode = node;
            visited.Add(node);
            RefreshStates();

            OnNodeEntered?.Invoke(node);
        }

        /// <summary>今クリックできるマスか。未出発なら最下段すべて、出発後は現在地の Next のみ。</summary>
        public bool IsSelectable(MapNode node)
        {
            if (CurrentNode == null) return node.Floor == 0;
            return CurrentNode.Next.Contains(node);
        }

        void RefreshStates()
        {
            foreach (var pair in nodeViews)
            {
                var node = pair.Key;
                NodeState state;

                if (node == CurrentNode) state = NodeState.Current;
                else if (IsSelectable(node)) state = NodeState.Selectable;
                else if (visited.Contains(node)) state = NodeState.Visited;
                else state = NodeState.Locked;

                pair.Value.SetState(state);
            }

            foreach (var line in connectionViews)
            {
                bool active = CurrentNode != null && line.From == CurrentNode;
                bool taken = visited.Contains(line.From) && visited.Contains(line.To);
                line.SetState(active, taken);
            }
        }

        /// <summary>戦闘などから戻ってきた時に、外部から進行状態を復帰させる場合に使う。</summary>
        public void SetCurrent(MapNode node)
        {
            CurrentNode = node;
            if (node != null) visited.Add(node);
            RefreshStates();
        }
    }
}
