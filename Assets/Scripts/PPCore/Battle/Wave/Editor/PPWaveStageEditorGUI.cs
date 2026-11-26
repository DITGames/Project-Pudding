/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPWaveStageEditorGUI.cs
 * @author hqrse
 * @date 2026/11/26
 * @brief ウェーブエディタの中央の欄(段階の名前・目安・出す敵の表・始まる条件・この段階のあと)と、画面の色と文字の形
 * =====================================*/

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace PPCore
{
    // ウェーブエディタの色と文字の形。暗い画面と明るい画面で色を変える
    public static class PPWaveEditorStyles
    {
        /* static 変数 */

        private static GUIStyle sPill;
        private static GUIStyle sProblemRow;
        private static GUIStyle sListPane;
        private static GUIStyle sStageTitle;
        private static GUIStyle sStageSummary;
        private static GUIStyle sErrorMark;
        private static GUIStyle sHeading;
        private static GUIStyle sSubLabel;
        private static GUIStyle sSection;
        private static GUIStyle sTag;
        private static GUIStyle sRowBox;
        private static GUIStyle sHeadingField;

        /* プロパティ */

        public static Color TextColor => EditorGUIUtility.isProSkin ? new Color(0.82f, 0.82f, 0.82f) : new Color(0.1f, 0.1f, 0.1f);
        public static Color SubTextColor => EditorGUIUtility.isProSkin ? new Color(0.6f, 0.6f, 0.6f) : new Color(0.35f, 0.35f, 0.35f);
        public static Color OkColor => EditorGUIUtility.isProSkin ? new Color(0.56f, 0.82f, 0.56f) : new Color(0.1f, 0.5f, 0.1f);
        public static Color ErrorColor => EditorGUIUtility.isProSkin ? new Color(1f, 0.45f, 0.4f) : new Color(0.75f, 0.1f, 0.1f);
        public static Color SelectedColor => EditorGUIUtility.isProSkin ? new Color(0.17f, 0.36f, 0.53f) : new Color(0.55f, 0.72f, 0.9f);
        public static Color ListRowColor => EditorGUIUtility.isProSkin ? new Color(0.18f, 0.18f, 0.18f) : new Color(0.84f, 0.84f, 0.84f);
        public static Color BorderColor => EditorGUIUtility.isProSkin ? new Color(0.12f, 0.12f, 0.12f) : new Color(0.6f, 0.6f, 0.6f);
        public static Color SideFlowColor => EditorGUIUtility.isProSkin ? new Color(0.42f, 0.29f, 0.12f) : new Color(0.98f, 0.8f, 0.55f);
        public static Color SideFlowLineColor => new(0.82f, 0.54f, 0.25f);
        public static Color BoxColor => EditorGUIUtility.isProSkin ? new Color(0.25f, 0.25f, 0.25f) : new Color(0.9f, 0.9f, 0.9f);
        public static Color LineColor => EditorGUIUtility.isProSkin ? new Color(0.6f, 0.6f, 0.6f) : new Color(0.35f, 0.35f, 0.35f);
        public static Color FlowBackColor => EditorGUIUtility.isProSkin ? new Color(0.17f, 0.17f, 0.17f) : new Color(0.8f, 0.8f, 0.8f);

        public static GUIStyle Pill => sPill ??= new GUIStyle(EditorStyles.toolbarButton) { fontSize = 11, alignment = TextAnchor.MiddleCenter };
        public static GUIStyle ProblemRow => sProblemRow ??= new GUIStyle(EditorStyles.helpBox) { fontSize = 11, wordWrap = true, normal = { textColor = ErrorColor } };
        public static GUIStyle ListPane => sListPane ??= new GUIStyle { padding = new RectOffset(0, 0, 0, 4) };
        public static GUIStyle StageTitle => sStageTitle ??= new GUIStyle(EditorStyles.boldLabel) { fontSize = 12, normal = { textColor = TextColor } };
        public static GUIStyle StageSummary => sStageSummary ??= new GUIStyle(EditorStyles.miniLabel) { clipping = TextClipping.Clip, normal = { textColor = SubTextColor } };
        public static GUIStyle ErrorMark => sErrorMark ??= new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter, normal = { textColor = ErrorColor } };
        public static GUIStyle Heading => sHeading ??= new GUIStyle(EditorStyles.boldLabel) { fontSize = 15 };
        public static GUIStyle SubLabel => sSubLabel ??= new GUIStyle(EditorStyles.label) { normal = { textColor = SubTextColor } };
        public static GUIStyle Section => sSection ??= new GUIStyle(EditorStyles.miniBoldLabel) { normal = { textColor = SubTextColor } };
        public static GUIStyle Tag => sTag ??= new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(0.95f, 0.83f, 0.65f) } };
        public static GUIStyle HeadingField => sHeadingField ??= new GUIStyle(EditorStyles.textField) { fontSize = 14, fontStyle = FontStyle.Bold, fixedHeight = 22f };
        public static GUIStyle RowBox => sRowBox ??= new GUIStyle(EditorStyles.helpBox) { padding = new RectOffset(6, 6, 4, 4), margin = new RectOffset(0, 0, 2, 2) };
    }

    // ウェーブエディタの中央の欄を描く。値はすべて SerializedProperty で読み書きし、変更は呼び出し元がまとめて確定させる
    public static class PPWaveStageEditorGUI
    {
        /* 定数 */

        private const float RowHeight = 20f;
        private const float CountWidth = 90f;
        private const float IntervalWidth = 80f;
        private const float DelayWidth = 96f;
        private const float RemoveWidth = 22f;
        private const float ColumnGap = 6f;
        private static readonly string[] KindNames = { "時間", "全滅", "被弾", "HP" };
        private static readonly string[] KindMenuNames = { "時間が経ったら", "段階の敵が全滅したら", "拠点がダメージを受けたら", "拠点の HP が x% 以下になったら" };
        private static readonly string[] SideNames = { "味方", "敵" };
        private static readonly string[] TimingNames = { "出し始めたら", "出し終えたら", "全滅したら" };
        private static readonly string[] NextConditionNames = { "いつでも進む", "HP が x% 以下なら", "HP が x% より上なら" };

        /* 描画 */

        // 番号 aIndex の段階 aStage を描く
        public static void Draw(PPBattleWaveEditorWindow aWindow, PPWaveDefinition aWave, int aIndex, SerializedProperty aStage)
        {
            var stage = aWave.Stages[aIndex];
            DrawHeader(aWave, aIndex, stage, aStage);
            EditorGUILayout.Space(6f);
            DrawSpawnTable(aWindow, aStage.FindPropertyRelative("mSpawns"));
            EditorGUILayout.Space(8f);
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope(GUILayout.MinWidth(200f)))
                {
                    DrawConditions(aWindow, aWave, stage, aStage);
                }
                GUILayout.Space(12f);
                using (new EditorGUILayout.VerticalScope(GUILayout.MinWidth(200f)))
                {
                    DrawNexts(aWindow, aWave, aStage.FindPropertyRelative("mNexts"));
                }
            }
        }

        private static void DrawHeader(PPWaveDefinition aWave, int aIndex, PPWaveStage aStage, SerializedProperty aProperty)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label($"{aIndex + 1}", PPWaveEditorStyles.Heading, GUILayout.Width(24f));
                var name = aProperty.FindPropertyRelative("mName");
                EditorGUI.BeginChangeCheck();
                string value = EditorGUILayout.TextField(name.stringValue, PPWaveEditorStyles.HeadingField);
                if (EditorGUI.EndChangeCheck()) name.stringValue = value;
            }
            string amount = aStage.IsInfinite ? $"敵 {aStage.FiniteEnemyCount} 体 + 無限に出し続ける" : $"敵 {aStage.FiniteEnemyCount} 体・約 {FormatSeconds(aStage.SpawnDuration)} 秒で出し終える";
            GUILayout.Label(amount, PPWaveEditorStyles.SubLabel);
        }

        // 出す敵の表。行 = ユニット、列 = 数・間隔・最初の待ち。マスを直接編集する
        private static void DrawSpawnTable(PPBattleWaveEditorWindow aWindow, SerializedProperty aSpawns)
        {
            GUILayout.Label("出す敵", PPWaveEditorStyles.Section);
            DrawTableRow(GUILayoutUtility.GetRect(0f, RowHeight, GUILayout.ExpandWidth(true)), null, -1, aWindow);
            for (int i = 0; i < aSpawns.arraySize; i++)
            {
                var rect = GUILayoutUtility.GetRect(0f, RowHeight + 2f, GUILayout.ExpandWidth(true));
                if (DrawTableRow(rect, aSpawns.GetArrayElementAtIndex(i), i, aWindow)) GUIUtility.ExitGUI();
            }
            if (aSpawns.arraySize == 0) GUILayout.Label("出す敵がありません。この段階は敵を出さずに出し終えます", PPWaveEditorStyles.SubLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("+ 敵を足す", GUILayout.Width(90f)) && aWindow.AddSpawnRow()) GUIUtility.ExitGUI();
                GUILayout.FlexibleSpace();
            }
        }

        // 表の 1 行を描く。aSpawn が null なら見出し。行を消したら true
        private static bool DrawTableRow(Rect aRect, SerializedProperty aSpawn, int aRow, PPBattleWaveEditorWindow aWindow)
        {
            float unitWidth = Mathf.Max(120f, aRect.width - CountWidth - IntervalWidth - DelayWidth - RemoveWidth - ColumnGap * 4f);
            var unitRect = new Rect(aRect.x, aRect.y + 1f, unitWidth, RowHeight - 2f);
            var countRect = new Rect(unitRect.xMax + ColumnGap, unitRect.y, CountWidth, unitRect.height);
            var intervalRect = new Rect(countRect.xMax + ColumnGap, unitRect.y, IntervalWidth, unitRect.height);
            var delayRect = new Rect(intervalRect.xMax + ColumnGap, unitRect.y, DelayWidth, unitRect.height);
            var removeRect = new Rect(delayRect.xMax + ColumnGap, unitRect.y, RemoveWidth, unitRect.height);
            if (aSpawn == null)
            {
                if (Event.current.type == EventType.Repaint) EditorGUI.DrawRect(aRect, PPWaveEditorStyles.ListRowColor);
                GUI.Label(unitRect, "ユニット", PPWaveEditorStyles.Section);
                GUI.Label(countRect, "数(0 で無限)", PPWaveEditorStyles.Section);
                GUI.Label(intervalRect, "間隔(秒)", PPWaveEditorStyles.Section);
                GUI.Label(delayRect, "最初の待ち(秒)", PPWaveEditorStyles.Section);
                return false;
            }

            var unit = aSpawn.FindPropertyRelative("mUnit");
            var definition = unit.objectReferenceValue as PPUnitDefinition;
            var markRect = new Rect(unitRect.x + 2f, unitRect.y + 4f, 10f, 10f);
            if (Event.current.type == EventType.Repaint) EditorGUI.DrawRect(markRect, GetAttributeColor(definition));
            var fieldRect = new Rect(unitRect.x + 16f, unitRect.y, unitRect.width - 16f, unitRect.height);
            EditorGUI.BeginChangeCheck();
            var picked = EditorGUI.ObjectField(fieldRect, definition, typeof(PPUnitDefinition), false);
            if (EditorGUI.EndChangeCheck()) unit.objectReferenceValue = picked;

            var count = aSpawn.FindPropertyRelative("mCount");
            EditorGUI.BeginChangeCheck();
            int countValue = EditorGUI.DelayedIntField(countRect, count.intValue);
            if (EditorGUI.EndChangeCheck()) count.intValue = Mathf.Max(0, countValue);
            DrawFloatCell(intervalRect, aSpawn.FindPropertyRelative("mInterval"), 0.1f);
            DrawFloatCell(delayRect, aSpawn.FindPropertyRelative("mFirstDelay"), 0f);
            return GUI.Button(removeRect, new GUIContent("×", "この行を消す"), EditorStyles.miniButton) && aWindow.RemoveSpawnRow(aRow);
        }

        // 始まる条件(どれか 1 つ)。「バトル開始から待つ」と条件の行
        private static void DrawConditions(PPBattleWaveEditorWindow aWindow, PPWaveDefinition aWave, PPWaveStage aStage, SerializedProperty aProperty)
        {
            GUILayout.Label("始まる条件(どれか 1 つ)", PPWaveEditorStyles.Section);
            var waits = aProperty.FindPropertyRelative("mWaitsFromBattleStart");
            EditorGUI.BeginChangeCheck();
            bool waitsValue = EditorGUILayout.ToggleLeft(new GUIContent("バトル開始から待つ", "入れると、バトル開始から待ち始めます。入れなければ、他の段階の行き先にされたときから待ちます"), waits.boolValue);
            if (EditorGUI.EndChangeCheck()) waits.boolValue = waitsValue;

            var conditions = aProperty.FindPropertyRelative("mStartConditions");
            if (conditions.arraySize == 0) GUILayout.Label(aStage.WaitsFromBattleStart && !aWave.HasIncoming(aStage) ? "条件なし: バトル開始ですぐ始まる" : "条件なし: 待ち始めたらすぐ始まる", PPWaveEditorStyles.SubLabel);
            for (int i = 0; i < conditions.arraySize; i++)
            {
                if (DrawCondition(aWindow, aWave, aStage, conditions.GetArrayElementAtIndex(i), i)) GUIUtility.ExitGUI();
            }
            if (EditorGUILayout.DropdownButton(new GUIContent("+ 条件"), FocusType.Passive, GUILayout.Width(70f)))
            {
                var menu = new GenericMenu();
                for (int k = 0; k < KindMenuNames.Length; k++)
                {
                    var kind = (PPWaveStartKind)k;
                    menu.AddItem(new GUIContent(KindMenuNames[k]), false, () => aWindow.AddCondition(kind));
                }
                menu.ShowAsContext();
            }
        }

        // 条件 1 行。種類・種類ごとの欄・1 度だけ・消すボタン。消したら true
        private static bool DrawCondition(PPBattleWaveEditorWindow aWindow, PPWaveDefinition aWave, PPWaveStage aStage, SerializedProperty aCondition, int aIndex)
        {
            using (new EditorGUILayout.HorizontalScope(PPWaveEditorStyles.RowBox))
            {
                var kind = aCondition.FindPropertyRelative("mKind");
                EditorGUI.BeginChangeCheck();
                int kindValue = EditorGUILayout.Popup(kind.enumValueIndex, KindNames, GUILayout.Width(52f));
                if (EditorGUI.EndChangeCheck()) kind.enumValueIndex = kindValue;

                switch ((PPWaveStartKind)kind.enumValueIndex)
                {
                    case PPWaveStartKind.Elapsed:
                        GUILayout.Label($"{aWave.GetWaitOriginText(aStage)}から", GUILayout.ExpandWidth(false));
                        DrawFloatField(aCondition.FindPropertyRelative("mSeconds"), 0f, 48f);
                        GUILayout.Label("秒", GUILayout.ExpandWidth(false));
                        break;
                    case PPWaveStartKind.StageWiped:
                        DrawStagePopup(aWave, aCondition.FindPropertyRelative("mStageId"), 120f);
                        GUILayout.Label("が全滅", GUILayout.ExpandWidth(false));
                        break;
                    case PPWaveStartKind.BaseDamaged:
                        DrawSidePopup(aCondition.FindPropertyRelative("mSide"));
                        GUILayout.Label("拠点が被弾", GUILayout.ExpandWidth(false));
                        break;
                    case PPWaveStartKind.BaseHp:
                        DrawSidePopup(aCondition.FindPropertyRelative("mSide"));
                        GUILayout.Label("拠点の HP", GUILayout.ExpandWidth(false));
                        DrawFloatField(aCondition.FindPropertyRelative("mPercent"), 0f, 40f);
                        GUILayout.Label("% 以下", GUILayout.ExpandWidth(false));
                        break;
                }
                GUILayout.FlexibleSpace();
                var once = aCondition.FindPropertyRelative("mIsOnce");
                EditorGUI.BeginChangeCheck();
                bool onceValue = GUILayout.Toggle(once.boolValue, new GUIContent("1 度だけ", "この条件で 1 度始まったら、以降は待ち直してもこの条件では始まりません"), GUILayout.ExpandWidth(false));
                if (EditorGUI.EndChangeCheck()) once.boolValue = onceValue;
                return GUILayout.Button(new GUIContent("×", "この条件を消す"), EditorStyles.miniButton, GUILayout.Width(RemoveWidth)) && aWindow.RemoveCondition(aIndex);
            }
        }

        // この段階のあと(行き先)。2 つ目からは別の流れとして札を付ける
        private static void DrawNexts(PPBattleWaveEditorWindow aWindow, PPWaveDefinition aWave, SerializedProperty aNexts)
        {
            GUILayout.Label("この段階のあと", PPWaveEditorStyles.Section);
            if (aNexts.arraySize == 0) GUILayout.Label("行き先なし: この段階のあとは何も待ち始めない", PPWaveEditorStyles.SubLabel);
            for (int i = 0; i < aNexts.arraySize; i++)
            {
                if (DrawNext(aWindow, aWave, aNexts.GetArrayElementAtIndex(i), i)) GUIUtility.ExitGUI();
            }
            using (new EditorGUI.DisabledScope(aWave.Stages.Count == 0))
            {
                if (GUILayout.Button("+ 行き先", GUILayout.Width(70f)) && aWindow.AddNext()) GUIUtility.ExitGUI();
            }
        }

        // 行き先 1 つ。1 行目にいつ・行き先・札・消すボタン、2 行目に進むときの条件。消したら true
        private static bool DrawNext(PPBattleWaveEditorWindow aWindow, PPWaveDefinition aWave, SerializedProperty aNext, int aIndex)
        {
            bool isRemoved;
            using (new EditorGUILayout.VerticalScope(PPWaveEditorStyles.RowBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawPopup(aNext.FindPropertyRelative("mTiming"), TimingNames, 92f);
                    GUILayout.Label("→", GUILayout.ExpandWidth(false));
                    DrawStagePopup(aWave, aNext.FindPropertyRelative("mTargetId"), 130f);
                    if (aIndex > 0)
                    {
                        var tagRect = GUILayoutUtility.GetRect(new GUIContent("別の流れ"), PPWaveEditorStyles.Tag, GUILayout.Width(56f));
                        if (Event.current.type == EventType.Repaint) EditorGUI.DrawRect(tagRect, PPWaveEditorStyles.SideFlowColor);
                        GUI.Label(tagRect, new GUIContent("別の流れ", "2 つ目からの行き先は、1 つ目と並んで別に進みます"), PPWaveEditorStyles.Tag);
                    }
                    GUILayout.FlexibleSpace();
                    isRemoved = GUILayout.Button(new GUIContent("×", "この行き先を消す"), EditorStyles.miniButton, GUILayout.Width(RemoveWidth));
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    var condition = aNext.FindPropertyRelative("mCondition");
                    DrawPopup(condition, NextConditionNames, 140f);
                    if (condition.enumValueIndex != (int)PPWaveNextCondition.None)
                    {
                        DrawSidePopup(aNext.FindPropertyRelative("mSide"));
                        GUILayout.Label("拠点", GUILayout.ExpandWidth(false));
                        DrawFloatField(aNext.FindPropertyRelative("mPercent"), 0f, 40f);
                        GUILayout.Label("%", GUILayout.ExpandWidth(false));
                    }
                    GUILayout.FlexibleSpace();
                }
            }
            return isRemoved && aWindow.RemoveNext(aIndex);
        }

        /* ヘルパー */

        // 段階を選ぶ欄。値は段階の識別子。見つからない識別子なら「(見つからない段階)」を出す
        private static void DrawStagePopup(PPWaveDefinition aWave, SerializedProperty aId, float aWidth)
        {
            var labels = new List<string>();
            // "/" はドロップダウンの階層の区切りになるため、見た目の近い文字に置き換える
            for (int i = 0; i < aWave.Stages.Count; i++) labels.Add(aWave.GetStageLabel(aWave.Stages[i].Id).Replace('/', '／'));
            int current = aWave.IndexOf(aId.stringValue);
            if (current < 0)
            {
                labels.Add("(見つからない段階)");
                current = labels.Count - 1;
            }
            EditorGUI.BeginChangeCheck();
            int picked = EditorGUILayout.Popup(current, labels.ToArray(), GUILayout.Width(aWidth));
            if (EditorGUI.EndChangeCheck() && picked < aWave.Stages.Count) aId.stringValue = aWave.Stages[picked].Id;
        }

        private static void DrawSidePopup(SerializedProperty aSide) => DrawPopup(aSide, SideNames, 48f);

        private static void DrawPopup(SerializedProperty aEnum, string[] aNames, float aWidth)
        {
            EditorGUI.BeginChangeCheck();
            int value = EditorGUILayout.Popup(aEnum.enumValueIndex, aNames, GUILayout.Width(aWidth));
            if (EditorGUI.EndChangeCheck()) aEnum.enumValueIndex = value;
        }

        private static void DrawFloatField(SerializedProperty aValue, float aMin, float aWidth)
        {
            EditorGUI.BeginChangeCheck();
            float value = EditorGUILayout.DelayedFloatField(aValue.floatValue, GUILayout.Width(aWidth));
            if (EditorGUI.EndChangeCheck()) aValue.floatValue = Mathf.Max(aMin, value);
        }

        private static void DrawFloatCell(Rect aRect, SerializedProperty aValue, float aMin)
        {
            EditorGUI.BeginChangeCheck();
            float value = EditorGUI.DelayedFloatField(aRect, aValue.floatValue);
            if (EditorGUI.EndChangeCheck()) aValue.floatValue = Mathf.Max(aMin, value);
        }

        // ユニットの属性の色。ユニットが無ければ灰色
        private static Color GetAttributeColor(PPUnitDefinition aUnit)
        {
            if (aUnit == null) return PPWaveEditorStyles.SubTextColor;
            return ColorUtility.TryParseHtmlString(PPUnitDisplay.AttributeColorCode(aUnit.TypeAttribute), out var color) ? color : PPWaveEditorStyles.SubTextColor;
        }

        private static string FormatSeconds(float aSeconds) => Mathf.RoundToInt(aSeconds).ToString();
    }
}
