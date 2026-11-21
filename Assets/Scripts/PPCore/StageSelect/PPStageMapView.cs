/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPStageMapView.cs
 * @author hqrse
 * @date 2026/11/21
 * @brief ステージセレクトのマップの表示
 * =====================================*/

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using AttributeUtility;

namespace PPCore
{
    // マップのマスと線を並べ、進行状態に合わせて色と選択中のマスを表示する
    // マスの種類ごとの色と短い名前はインスペクタで決める
    public class PPStageMapView : MonoBehaviour
    {
        /* 内部型 */

        // マスの種類 1 つ分の見た目
        [Serializable]
        public sealed class TypeStyle
        {
            [Label("種類")] public PPStageNodeType Type;
            [Label("名前")] public string Label;
            [Label("色")] public Color Color = Color.white;
        }

        /* インスペクタ設定 */

        [Header("参照")]
        // マスと線を並べる親。下端の中央を原点にして並べる
        [Label("並べる場所")]
        [SerializeField] private RectTransform mContent;
        // 未設定ならスクロールしない
        [Label("スクロール")]
        [SerializeField] private ScrollRect mScrollRect;
        [Label("マスのプレハブ")]
        [SerializeField] private PPStageMapNodeView mNodePrefab;
        [Label("線のプレハブ")]
        [SerializeField] private PPStageMapLineView mLinePrefab;

        [Header("配置")]
        [Label("列の間隔")]
        [SerializeField] private float mSpacingX = 130f;
        [Label("段の間隔")]
        [SerializeField] private float mSpacingY = 150f;
        // 機械的に見えないよう、位置をこの幅だけずらす
        [Label("位置のゆらぎ")]
        [SerializeField] private float mJitter = 26f;
        [Label("上下左右の余白")]
        [SerializeField] private float mMargin = 200f;

        [Header("種類")]
        [Label("種類ごとの見た目", true)]
        [SerializeField] private List<TypeStyle> mTypeStyles = new();

        /* 状態 */

        private readonly Dictionary<PPStageMapNode, PPStageMapNodeView> mNodeViews = new();
        private readonly List<PPStageMapLineView> mLineViews = new();
        private PPStageMap mMap;

        /* イベント */

        // 選べるマスがクリックされたとき(マス)
        public event Action<PPStageMapNode> OnNodeClicked;

        /* 表示 */

        // マップのマスと線を作り直す。線はマスの後ろに出すよう先に作る
        public void Build(PPStageMap aMap)
        {
            Clear();
            mMap = aMap;
            mContent.sizeDelta = new Vector2(aMap.Width * mSpacingX + mMargin, (aMap.Height + 1) * mSpacingY + mMargin);

            foreach (var node in aMap.AllNodesWithBoss())
            {
                foreach (var next in node.Next)
                {
                    var line = Instantiate(mLinePrefab, mContent);
                    SetBottomCenterAnchor((RectTransform)line.transform);
                    line.Setup(node, next, GetPosition(node), GetPosition(next));
                    mLineViews.Add(line);
                }
            }

            foreach (var node in aMap.AllNodesWithBoss())
            {
                var view = Instantiate(mNodePrefab, mContent);
                SetBottomCenterAnchor((RectTransform)view.transform);
                var style = FindStyle(node.Type);
                view.Setup(node, GetPosition(node), style?.Label ?? node.Type.ToString(), style?.Color ?? Color.white,
                    aView => OnNodeClicked?.Invoke(aView.Node));
                mNodeViews[node] = view;
            }
        }

        // 進行状態に合わせて、マスの状態・選択中のマス・線の色を書き直す
        public void Refresh(PPStageProgress aProgress, Func<PPStageMapNode, bool> aIsSelectable, PPStageMapNode aFocused)
        {
            var current = aProgress.CurrentNode.HasValue ? mMap.Get(aProgress.CurrentNode.Value) : null;
            foreach (var pair in mNodeViews)
            {
                var node = pair.Key;
                var state = node == current ? PPStageNodeState.Current
                    : aIsSelectable(node) ? PPStageNodeState.Selectable
                    : aProgress.IsVisited(node.Key) ? PPStageNodeState.Visited
                    : PPStageNodeState.Locked;
                pair.Value.SetState(state, node == aFocused);
            }

            foreach (var line in mLineViews)
            {
                bool isActive = current != null && line.From == current;
                bool isTaken = aProgress.IsVisited(line.From.Key) && aProgress.IsVisited(line.To.Key);
                line.SetState(isActive, isTaken);
            }
        }

        // マスが画面の縦の中央に来るようにスクロールする
        public void ScrollTo(PPStageMapNode aNode)
        {
            if (mScrollRect == null || aNode == null) return;

            float viewport = mScrollRect.viewport != null ? mScrollRect.viewport.rect.height : 0f;
            float scrollable = mContent.rect.height - viewport;
            if (scrollable <= 0f) return;

            float y = GetPosition(aNode).y + mMargin * 0.5f - viewport * 0.5f;
            mScrollRect.verticalNormalizedPosition = Mathf.Clamp01(y / scrollable);
        }

        private void Clear()
        {
            foreach (var view in mNodeViews.Values)
            {
                if (view != null) Destroy(view.gameObject);
            }
            foreach (var line in mLineViews)
            {
                if (line != null) Destroy(line.gameObject);
            }
            mNodeViews.Clear();
            mLineViews.Clear();
        }

        /* 問い合わせ */

        public PPStageMapNodeView GetNodeView(PPStageMapNode aNode) => mNodeViews.TryGetValue(aNode, out var view) ? view : null;

        // 下端の中央を原点にしたマスの位置。ボスはゆらがせず中央に置く
        private Vector2 GetPosition(PPStageMapNode aNode)
        {
            if (aNode == mMap.Boss) return new Vector2(0f, aNode.Floor * mSpacingY + mMargin * 0.5f);

            float centerColumn = (mMap.Width - 1) * 0.5f;
            return new Vector2((aNode.Column - centerColumn) * mSpacingX + aNode.Jitter.x * mJitter,
                aNode.Floor * mSpacingY + mMargin * 0.5f + aNode.Jitter.y * mJitter);
        }

        private TypeStyle FindStyle(PPStageNodeType aType)
        {
            foreach (var style in mTypeStyles)
            {
                if (style != null && style.Type == aType) return style;
            }
            return null;
        }

        /* ヘルパー */

        private static void SetBottomCenterAnchor(RectTransform aRect)
        {
            aRect.anchorMin = aRect.anchorMax = new Vector2(0.5f, 0f);
            aRect.pivot = new Vector2(0.5f, 0.5f);
        }
    }
}
