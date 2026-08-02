using UnityEngine;
using UnityEngine.UI;

namespace StageSelect
{
    /// <summary>
    /// 2ノードを結ぶ線。Image を1枚、始点と終点の中点に置いて回転・伸縮させるだけの実装。
    /// 頂点を自前で組む UILineRenderer より軽く、点線スプライトを Tiled にすればスレスパ風になる。
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class MapConnectionView : MonoBehaviour
    {
        [SerializeField] Image line;
        [SerializeField] float thickness = 8f;
        [SerializeField] float padding = 34f; // ノードの半径ぶん線を短くする

        [SerializeField] Color dimColor = new Color(1f, 1f, 1f, 0.20f);
        [SerializeField] Color activeColor = new Color(1f, 0.9f, 0.45f, 1f);
        [SerializeField] Color takenColor = new Color(1f, 0.75f, 0.3f, 0.75f);

        public MapNode From { get; private set; }
        public MapNode To { get; private set; }

        RectTransform rect;

        void Awake() => Cache();

        void Cache()
        {
            if (rect != null) return;
            rect = (RectTransform)transform;
            if (line == null) line = GetComponent<Image>();
        }

        public void Setup(MapNode from, MapNode to)
        {
            Cache();
            From = from;
            To = to;
            name = $"Line_{from.Floor}_{from.Column}→{to.Floor}_{to.Column}";

            Vector2 a = from.LocalPosition;
            Vector2 b = to.LocalPosition;
            Vector2 dir = b - a;
            float length = dir.magnitude;

            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = (a + b) * 0.5f;
            rect.sizeDelta = new Vector2(Mathf.Max(length - padding * 2f, 1f), thickness);
            rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);

            if (line != null) line.raycastTarget = false;
            SetState(false, false);
        }

        /// <param name="active">現在地から選べる行き先へ伸びている線か</param>
        /// <param name="taken">実際に通った線か</param>
        public void SetState(bool active, bool taken)
        {
            if (line == null) return;
            line.color = active ? activeColor : (taken ? takenColor : dimColor);
        }
    }
}
