/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPWaveFlowDiagram.cs
 * @author hqrse
 * @date 2026/11/26
 * @brief ウェーブエディタの下の流れの図(見るだけ)。段階を一覧の順に並べ、行き先を矢印で結ぶ
 * =====================================*/

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace PPCore
{
    // 流れの図を描く。前へ戻る矢印は下へ曲げ、飛ばして先へ進む矢印は上へ曲げる。2 つ目からの行き先(別の流れ)は点線
    public static class PPWaveFlowDiagram
    {
        /* 定数 */

        private const float Margin = 16f;
        private const float Gap = 40f;
        private const float BoxHeight = 28f;
        private const float MinBoxWidth = 84f;
        private const float MaxBoxWidth = 140f;
        private const float BoxTop = 30f;
        private const float LineWidth = 2f;
        private const float ArrowSize = 7f;
        private const int CurveSegments = 24;

        /* 状態 */

        private static Vector2 sScroll;
        private static GUIStyle sBoxLabel;

        /* プロパティ */

        private static GUIStyle BoxLabel => sBoxLabel ??= new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleCenter, clipping = TextClipping.Clip, fontSize = 12 };

        /* 描画 */

        // 図を描き、押された段階の番号を返す。押されなければ -1
        public static int Draw(Rect aRect, PPWaveDefinition aWave, int aSelected)
        {
            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(aRect, PPWaveEditorStyles.FlowBackColor);
                EditorGUI.DrawRect(new Rect(aRect.x, aRect.y, aRect.width, 1f), PPWaveEditorStyles.BorderColor);
            }
            GUI.Label(new Rect(aRect.x + 8f, aRect.y + 4f, aRect.width - 16f, 18f), "流れ(見るだけ。押すとその段階を選ぶ)", PPWaveEditorStyles.Section);

            int count = aWave.Stages.Count;
            if (count == 0) return -1;
            float boxWidth = Mathf.Clamp((aRect.width - Margin * 2f - Gap * (count - 1)) / count, MinBoxWidth, MaxBoxWidth);
            float contentWidth = Margin * 2f + boxWidth * count + Gap * (count - 1);
            var view = new Rect(aRect.x, aRect.y + 20f, aRect.width, aRect.height - 20f);
            var content = new Rect(0f, 0f, Mathf.Max(contentWidth, view.width - 16f), view.height - 16f);

            int clicked = -1;
            sScroll = GUI.BeginScrollView(view, sScroll, content);
            var boxes = new Rect[count];
            for (int i = 0; i < count; i++) boxes[i] = new Rect(Margin + i * (boxWidth + Gap), BoxTop, boxWidth, BoxHeight);
            var isSideFlow = FindSideFlowStages(aWave);

            if (Event.current.type == EventType.Repaint)
            {
                for (int i = 0; i < count; i++) DrawEdges(aWave, i, boxes);
                for (int i = 0; i < count; i++) DrawBox(aWave, i, boxes[i], i == aSelected, isSideFlow[i]);
            }
            else
            {
                for (int i = 0; i < count; i++)
                {
                    var stage = aWave.Stages[i];
                    GUI.Label(boxes[i], new GUIContent(string.Empty, aWave.DescribeStart(stage)));
                }
            }
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
            {
                for (int i = 0; i < count; i++)
                {
                    if (!boxes[i].Contains(Event.current.mousePosition)) continue;
                    clicked = i;
                    Event.current.Use();
                    break;
                }
            }
            GUI.EndScrollView();
            return clicked;
        }

        private static void DrawBox(PPWaveDefinition aWave, int aIndex, Rect aBox, bool aIsSelected, bool aIsSideFlow)
        {
            var stage = aWave.Stages[aIndex];
            var fill = aIsSelected ? PPWaveEditorStyles.SelectedColor : aIsSideFlow ? PPWaveEditorStyles.SideFlowColor : PPWaveEditorStyles.BoxColor;
            var border = aIsSelected ? new Color(0.35f, 0.6f, 0.85f) : aIsSideFlow ? PPWaveEditorStyles.SideFlowLineColor : PPWaveEditorStyles.LineColor;
            EditorGUI.DrawRect(aBox, border);
            EditorGUI.DrawRect(new Rect(aBox.x + 1f, aBox.y + 1f, aBox.width - 2f, aBox.height - 2f), fill);
            GUI.Label(aBox, $"{aIndex + 1} {aWave.GetStageName(stage.Id)}", BoxLabel);

            // バトル開始から待つ段階は、左から入る短い矢印で示す
            if (!stage.WaitsFromBattleStart) return;
            var to = new Vector2(aBox.x - 1f, aBox.center.y);
            DrawLine(new[] { to + Vector2.left * 12f, to }, PPWaveEditorStyles.OkColor, false);
            DrawArrowHead(to, Vector2.right, PPWaveEditorStyles.OkColor);
        }

        // 段階 aIndex の行き先の矢印を描く。1 つ目は実線、2 つ目からは点線
        private static void DrawEdges(PPWaveDefinition aWave, int aIndex, Rect[] aBoxes)
        {
            var nexts = aWave.Stages[aIndex].Nexts;
            for (int k = 0; k < nexts.Count; k++)
            {
                int target = aWave.IndexOf(nexts[k]?.TargetId);
                if (target < 0) continue;
                bool isSide = k > 0;
                var color = isSide ? PPWaveEditorStyles.SideFlowLineColor : PPWaveEditorStyles.LineColor;
                var from = aBoxes[aIndex];
                var to = aBoxes[target];
                Vector2[] points;
                if (target == aIndex + 1)
                {
                    points = new[] { new Vector2(from.xMax, from.center.y), new Vector2(to.x - 1f, to.center.y) };
                }
                else if (target > aIndex)
                {
                    float lift = Mathf.Min(14f + 4f * (target - aIndex) + 3f * k, 24f);
                    var start = new Vector2(from.center.x + 10f, from.y);
                    var end = new Vector2(to.center.x - 10f, to.y - 1f);
                    points = Bezier(start, start + Vector2.down * lift, end + Vector2.down * lift, end);
                }
                else
                {
                    float drop = Mathf.Min(18f + 5f * (aIndex - target) + 3f * k, 30f);
                    var start = new Vector2(from.center.x - (target == aIndex ? -18f : 10f), from.yMax);
                    var end = new Vector2(to.center.x + (target == aIndex ? -18f : 10f), to.yMax + 1f);
                    points = Bezier(start, start + Vector2.up * drop, end + Vector2.up * drop, end);
                }
                DrawLine(points, color, isSide);
                DrawArrowHead(points[^1], (points[^1] - points[^2]).normalized, color);
            }
        }

        /* ヘルパー */

        // 別の流れだけで行く段階と、先頭以外のバトル開始から待つ段階(互いに関係しない流れ)
        private static bool[] FindSideFlowStages(PPWaveDefinition aWave)
        {
            int count = aWave.Stages.Count;
            var hasMain = new bool[count];
            var hasSide = new bool[count];
            for (int i = 0; i < count; i++)
            {
                var nexts = aWave.Stages[i].Nexts;
                for (int k = 0; k < nexts.Count; k++)
                {
                    int target = aWave.IndexOf(nexts[k]?.TargetId);
                    if (target < 0) continue;
                    if (k == 0) hasMain[target] = true;
                    else hasSide[target] = true;
                }
            }
            var result = new bool[count];
            bool isFirstEntrySeen = false;
            for (int i = 0; i < count; i++)
            {
                var stage = aWave.Stages[i];
                bool isLaterEntry = stage.WaitsFromBattleStart && isFirstEntrySeen;
                if (stage.WaitsFromBattleStart) isFirstEntrySeen = true;
                result[i] = !hasMain[i] && (hasSide[i] || isLaterEntry);
            }
            return result;
        }

        private static Vector2[] Bezier(Vector2 aStart, Vector2 aControl1, Vector2 aControl2, Vector2 aEnd)
        {
            var points = new Vector2[CurveSegments + 1];
            for (int i = 0; i <= CurveSegments; i++)
            {
                float t = i / (float)CurveSegments;
                float u = 1f - t;
                points[i] = u * u * u * aStart + 3f * u * u * t * aControl1 + 3f * u * t * t * aControl2 + t * t * t * aEnd;
            }
            return points;
        }

        // 折れ線を描く。点線なら短い区切りで交互に描く
        private static void DrawLine(IReadOnlyList<Vector2> aPoints, Color aColor, bool aIsDashed)
        {
            Handles.color = aColor;
            if (!aIsDashed)
            {
                var solid = new Vector3[aPoints.Count];
                for (int i = 0; i < aPoints.Count; i++) solid[i] = aPoints[i];
                Handles.DrawAAPolyLine(LineWidth, solid);
                return;
            }
            const float dash = 5f;
            bool isOn = true;
            float left = dash;
            for (int i = 1; i < aPoints.Count; i++)
            {
                Vector2 a = aPoints[i - 1];
                Vector2 b = aPoints[i];
                float length = Vector2.Distance(a, b);
                float done = 0f;
                while (done < length)
                {
                    float step = Mathf.Min(left, length - done);
                    Vector2 p = Vector2.Lerp(a, b, done / length);
                    Vector2 q = Vector2.Lerp(a, b, (done + step) / length);
                    if (isOn) Handles.DrawAAPolyLine(LineWidth, p, q);
                    done += step;
                    left -= step;
                    if (left > 0.001f) continue;
                    isOn = !isOn;
                    left = dash;
                }
            }
        }

        private static void DrawArrowHead(Vector2 aTip, Vector2 aDirection, Color aColor)
        {
            if (aDirection.sqrMagnitude < 0.0001f) return;
            Vector2 side = new Vector2(-aDirection.y, aDirection.x) * (ArrowSize * 0.5f);
            Vector2 back = aTip - aDirection * ArrowSize;
            Handles.color = aColor;
            Handles.DrawAAConvexPolygon(aTip, back + side, back - side);
        }
    }
}
