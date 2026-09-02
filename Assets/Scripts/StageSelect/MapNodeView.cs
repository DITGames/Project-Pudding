using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace StageSelect
{
    public enum NodeState
    {
        Locked,      // まだ行けない
        Selectable,  // 今クリックできる
        Visited,     // 通過済み
        Current      // 現在地
    }

    /// <summary>マップ上の1マスの見た目。プレハブのルートに貼る。</summary>
    [RequireComponent(typeof(RectTransform))]
    public class MapNodeView : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("参照")]
        [SerializeField] Image background;   // クリック判定用（Raycast Target を ON に）
        [SerializeField] Image icon;
        [SerializeField] Image ring;         // 選択可能時に光らせる枠（任意）

        [Header("状態カラー")]
        [SerializeField] Color lockedColor = new Color(1f, 1f, 1f, 0.28f);
        [SerializeField] Color selectableColor = Color.white;
        [SerializeField] Color visitedColor = new Color(1f, 0.85f, 0.5f, 0.9f);
        [SerializeField] Color currentColor = new Color(1f, 0.95f, 0.6f, 1f);

        [Header("演出")]
        [SerializeField] float pulseSpeed = 3.2f;
        [SerializeField] float pulseAmount = 0.10f;
        [SerializeField] float hoverScale = 1.15f;

        public MapNode Node { get; private set; }
        public NodeState State { get; private set; } = NodeState.Locked;

        System.Action<MapNodeView> onClick;
        RectTransform rect;
        Vector3 baseScale;
        bool hovered;

        void Awake()
        {
            rect = (RectTransform)transform;
            baseScale = rect.localScale;
        }

        public void Setup(MapNode node, Sprite sprite, System.Action<MapNodeView> onClick)
        {
            if (rect == null) { rect = (RectTransform)transform; baseScale = rect.localScale; }

            Node = node;
            this.onClick = onClick;

            name = $"Node_{node.Floor}_{node.Column}_{node.Type}";
            rect.anchoredPosition = node.LocalPosition;

            if (icon != null && sprite != null) icon.sprite = sprite;
            if (ring != null) ring.enabled = false;

            SetState(NodeState.Locked);
        }

        public void SetState(NodeState state)
        {
            State = state;

            var c = state switch
            {
                NodeState.Selectable => selectableColor,
                NodeState.Visited => visitedColor,
                NodeState.Current => currentColor,
                _ => lockedColor
            };

            if (icon != null) icon.color = c;
            if (background != null)
            {
                var bc = background.color;
                background.color = new Color(bc.r, bc.g, bc.b, state == NodeState.Locked ? 0.35f : 1f);
            }
            if (ring != null) ring.enabled = (state == NodeState.Selectable || state == NodeState.Current);

            if (state != NodeState.Selectable) rect.localScale = baseScale;
        }

        void Update()
        {
            if (State != NodeState.Selectable) return;

            // 行き先候補だけゆっくり脈打たせる
            float pulse = 1f + Mathf.Sin(Time.unscaledTime * pulseSpeed) * pulseAmount;
            float target = hovered ? hoverScale : 1f;
            rect.localScale = baseScale * pulse * target;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (State != NodeState.Selectable) return;
            onClick?.Invoke(this);
        }

        public void OnPointerEnter(PointerEventData eventData) => hovered = true;
        public void OnPointerExit(PointerEventData eventData) => hovered = false;
    }
}
