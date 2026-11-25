/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPWaveNodeView.cs
 * @author hqrse
 * @date 2026/11/25
 * @brief ウェーブのノード図の 1 ノードの見た目(種類ごとの色・入口と出口・中身の要約・誤りの印)と、誤りを赤く出せる線
 * =====================================*/

using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace PPCore
{
    // ノード図の線。誤りのある線を赤く出す
    public class PPWaveEdge : Edge
    {
        /* 定数 */

        private static readonly Color ProblemColor = new(0.95f, 0.25f, 0.25f);

        /* 状態 */

        private bool mHasProblem;

        /* プロパティ */

        public bool HasProblem
        {
            get => mHasProblem;
            set
            {
                if (mHasProblem == value) return;
                mHasProblem = value;
                UpdateEdgeControl();
            }
        }

        /* 描画 */

        // 既定の色を塗ったあと、選ばれていなければ誤りの色で塗り直す
        public override bool UpdateEdgeControl()
        {
            bool result = base.UpdateEdgeControl();
            if (mHasProblem && !selected && edgeControl != null)
            {
                edgeControl.inputColor = ProblemColor;
                edgeControl.outputColor = ProblemColor;
            }
            return result;
        }
    }

    // ノード図の 1 ノードの見た目。見出し・入口と出口のポート・中身の要約を出し、中身の編集はウィンドウの右の欄で行う
    public class PPWaveNodeView : Node
    {
        /* 定数 */

        private const float MinWidth = 220f;
        private const float ProblemBorderWidth = 2f;
        private static readonly Color TriggerColor = new(0.55f, 0.36f, 0.12f);
        private static readonly Color SpawnColor = new(0.16f, 0.36f, 0.56f);
        private static readonly Color ConditionColor = new(0.38f, 0.24f, 0.54f);
        private static readonly Color ProblemColor = new(0.95f, 0.25f, 0.25f);

        /* 状態 */

        private readonly Dictionary<PPWaveNodePort, Port> mOutputPorts = new();
        private readonly Label mSummaryLabel;

        /* プロパティ */

        public string NodeId { get; }
        // 入口のポート。入口を受け付けないノードなら null
        public Port InputPort { get; }
        // 実行中の情報(色・数字など)を重ねるための場所。編集モードでは空
        public VisualElement OverlayContainer { get; }
        // ノード図の上の位置。GetPosition() は配置の計算が済むまで古い値を返すため、置いた位置そのものを読む
        public Vector2 Position => new(style.left.value.value, style.top.value.value);

        /* 生成 */

        public PPWaveNodeView(PPWaveNode aNode)
        {
            NodeId = aNode.Id;
            style.minWidth = MinWidth;
            titleContainer.style.backgroundColor = aNode switch
            {
                PPWaveTriggerNode => TriggerColor,
                PPWaveSpawnNode => SpawnColor,
                _ => ConditionColor,
            };

            if (aNode is not PPWaveTriggerNode { IgnoresInput: true })
            {
                InputPort = CreatePort(Direction.Input, "入口");
                inputContainer.Add(InputPort);
            }
            foreach (var output in aNode.Outputs)
            {
                var port = CreatePort(Direction.Output, PPWaveDefinition.GetPortName(output));
                port.userData = output;
                mOutputPorts[output] = port;
                outputContainer.Add(port);
            }

            OverlayContainer = new VisualElement();
            extensionContainer.Add(OverlayContainer);
            mSummaryLabel = new Label { style = { whiteSpace = WhiteSpace.Normal, paddingLeft = 6, paddingRight = 6, paddingTop = 4, paddingBottom = 4, fontSize = 11 } };
            extensionContainer.Add(mSummaryLabel);

            SetPosition(new Rect(aNode.Position, Vector2.zero));
            expanded = true;
            RefreshExpandedState();
            RefreshPorts();
        }

        /* 問い合わせ */

        // その出口のポート。このノードに無い出口なら null
        public Port GetOutputPort(PPWaveNodePort aPort) => mOutputPorts.TryGetValue(aPort, out var port) ? port : null;

        /* 表示 */

        // 見出しと中身の要約を、今のノードの値に合わせる
        public void Refresh(PPWaveNode aNode, SerializedProperty aNodeProperty, PPWaveDefinition aWave)
        {
            title = PPWaveNodeEditorUtility.GetNodeLabel(aNode);
            string summary = BuildSummary(aNode, aNodeProperty, aWave);
            mSummaryLabel.text = summary;
            mSummaryLabel.style.display = string.IsNullOrEmpty(summary) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        // 誤りの文章を受け取り、あれば赤い枠と、ポインタを乗せたときの説明にする。空なら印を外す
        public void SetProblems(IReadOnlyList<string> aMessages)
        {
            bool hasProblem = aMessages != null && aMessages.Count > 0;
            float width = hasProblem ? ProblemBorderWidth : 0f;
            mainContainer.style.borderLeftWidth = width;
            mainContainer.style.borderRightWidth = width;
            mainContainer.style.borderTopWidth = width;
            mainContainer.style.borderBottomWidth = width;
            var color = hasProblem ? ProblemColor : Color.clear;
            mainContainer.style.borderLeftColor = color;
            mainContainer.style.borderRightColor = color;
            mainContainer.style.borderTopColor = color;
            mainContainer.style.borderBottomColor = color;
            tooltip = hasProblem ? string.Join("\n", aMessages) : string.Empty;
        }

        /* 説明文 */

        // ノードの上に出す要約。種類ごとに主な値を 1〜数行で書く。知らない種類なら空
        private static string BuildSummary(PPWaveNode aNode, SerializedProperty aNodeProperty, PPWaveDefinition aWave)
        {
            var builder = new StringBuilder();
            switch (aNode)
            {
                case PPWaveSpawnNode spawnNode:
                    if (spawnNode.Spawns.Count == 0) builder.Append("出す敵がありません");
                    foreach (var spawn in spawnNode.Spawns)
                    {
                        if (spawn == null) continue;
                        if (builder.Length > 0) builder.Append('\n');
                        string unitName = spawn.Unit != null ? spawn.Unit.DisplayName : "(ユニット未設定)";
                        string count = spawn.IsInfinite ? "無限" : spawn.Count.ToString();
                        builder.Append($"{unitName} Lv{spawn.Level} ×{count} / {spawn.Interval:0.##}秒ごと");
                        if (spawn.FirstDelay > 0f) builder.Append($" / {spawn.FirstDelay:0.##}秒後から");
                    }
                    break;
                case PPWaveBattleStartTriggerNode:
                    builder.Append("バトルが始まったら起動");
                    break;
                case PPWaveElapsedTriggerNode elapsed:
                    builder.Append($"待ち始めて {elapsed.Seconds:0.##} 秒後");
                    break;
                case PPWaveBaseDamagedTriggerNode:
                    builder.Append($"{GetEnumName(aNodeProperty, "mSide")}の拠点がダメージを受けたら");
                    break;
                case PPWaveBaseHpTriggerNode:
                    builder.Append($"{GetEnumName(aNodeProperty, "mSide")}の拠点の HP が {GetFloat(aNodeProperty, "mPercent"):0.##}% 以下になったら");
                    break;
                case PPWaveWipedTriggerNode wiped:
                    var target = aWave.FindNode(wiped.TargetId);
                    builder.Append(target != null ? $"対象: {PPWaveNodeEditorUtility.GetNodeLabel(target)}" : "対象: (未設定か見つからない)");
                    break;
                case PPWaveBaseHpConditionNode:
                    builder.Append($"{GetEnumName(aNodeProperty, "mSide")}の拠点の HP が {GetFloat(aNodeProperty, "mPercent"):0.##}% {GetEnumName(aNodeProperty, "mCompare")}");
                    break;
            }

            if (aNode is PPWaveWaitTriggerNode waitTrigger)
            {
                if (waitTrigger.IsOnce) builder.Append("\n1 度だけ");
                if (!string.IsNullOrEmpty(waitTrigger.RaceGroup)) builder.Append($"\n早い者勝ち: {waitTrigger.RaceGroup}");
            }
            return builder.ToString();
        }

        /* ヘルパー */

        private static Port CreatePort(Direction aDirection, string aName)
        {
            var port = Port.Create<PPWaveEdge>(Orientation.Horizontal, aDirection, Port.Capacity.Multi, typeof(bool));
            port.portName = aName;
            return port;
        }

        // 列挙のフィールドの表示名([InspectorName] の名前)。見つからなければ空
        private static string GetEnumName(SerializedProperty aNodeProperty, string aName)
        {
            var property = aNodeProperty?.FindPropertyRelative(aName);
            if (property == null || property.propertyType != SerializedPropertyType.Enum) return string.Empty;
            int index = property.enumValueIndex;
            return index >= 0 && index < property.enumDisplayNames.Length ? property.enumDisplayNames[index] : string.Empty;
        }

        private static float GetFloat(SerializedProperty aNodeProperty, string aName)
        {
            var property = aNodeProperty?.FindPropertyRelative(aName);
            return property != null && property.propertyType == SerializedPropertyType.Float ? property.floatValue : 0f;
        }
    }
}
