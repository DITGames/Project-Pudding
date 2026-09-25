/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPUnitCreatorWindow.cs
 * @author hqrse
 * @date 2026/09/09
 * @brief 4分割ビューでユニットの作成を支援する
 * =====================================*/

using System;
using System.Linq;
using AttributeUtility;
using UnityEditor;
using UnityEngine;

namespace PPCore
{
    public sealed class PPUnitCreatorWindow : EditorWindow
    {
        [Label("下書きID")][SerializeField] private string mDraftId;
        [Label("左右比率")][SerializeField] private float mSplitX = 0.32f;
        [Label("上下比率")][SerializeField] private float mSplitY = 0.58f;
        private PPUnitCreationDraft mDraft;
        private readonly Vector2[] mScroll = new Vector2[5];
        private readonly int[] mTemplates = new int[PPUnitCreationDraft.StatNames.Length];
        private readonly bool[] mSeries = { true, true, true };
        private int mDragging;
        private int mSkillIndex = -1;
        private UnityEditor.Editor mSkillEditor;
        [NonSerialized] private PPUnitCreationValidation mValidation;
        [NonSerialized] private int mValidatedRevision = -1;
        private string mError;

        [MenuItem("Window/Project-Pudding/ユニット作成")]
        public static void Open() => GetWindow<PPUnitCreatorWindow>("ユニット作成");

        public static void OpenDraft(string aId)
        {
            var window = GetWindow<PPUnitCreatorWindow>("ユニット作成");
            window.mDraftId = aId;
            window.mValidatedRevision = -1;
            window.Repaint();
        }

        private void OnEnable()
        {
            // 再コンパイルでは検証結果と更新番号を必ず一緒に破棄する
            mValidation = null;
            mValidatedRevision = -1;
            minSize = new Vector2(780, 600);
            EditorApplication.projectChanged += Invalidate;
            Undo.undoRedoPerformed += OnUndo;
        }

        private void OnDisable()
        {
            EditorApplication.projectChanged -= Invalidate;
            Undo.undoRedoPerformed -= OnUndo;
            if (mSkillEditor != null) DestroyImmediate(mSkillEditor);
        }

        private void Invalidate() { mValidatedRevision = -1; Repaint(); }
        private void OnInspectorUpdate() => Repaint();
        private void OnUndo() { if (mDraft != null && !mDraft.IsCreated) mDraft.Commit(); Invalidate(); }

        private void NewDraft()
        {
            mDraft = PPUnitCreationDraft.Create();
            PPUnitDraftStore.Save(mDraft);
            mDraftId = mDraft.DraftId;
            mValidatedRevision = -1;
            mSkillIndex = -1;
            mError = null;
        }

        private void OnGUI()
        {
            try
            {
                if (string.IsNullOrEmpty(mDraftId))
                {
                    mDraftId = PPUnitDraftStore.LastId;
                    if (string.IsNullOrEmpty(mDraftId)) NewDraft();
                }
                mDraft = PPUnitDraftStore.Load(mDraftId);
            }
            catch (Exception error)
            {
                EditorGUILayout.HelpBox(error.Message, MessageType.Error);
                if (GUILayout.Button("新しい下書きを作成")) NewDraft();
                return;
            }
            var revision = mDraft.Revision;
            GUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("下書き: " + mDraftId + " / 更新 " + revision);
            if (GUILayout.Button("新しい下書き", EditorStyles.toolbarButton, GUILayout.Width(110))) { NewDraft(); GUIUtility.ExitGUI(); }
            GUILayout.EndHorizontal();
            var footerRect = new Rect(4, position.height - 184, position.width - 8, 180);
            // ヘッダーは「トロールバー + (Header装飾込みの)ユニットID/表示名行 + 保存先フォルダー行」の
            // 固定3行構成。GUILayoutUtility.GetRectでの動的確保はBeginArea基準のビュー群と
            // 相性が悪く描画されなくなる事象を確認したため、実測に基づく固定オフセットで確保する
            const float headerBottom = 100f;
            var body = new Rect(4, headerBottom, position.width - 8, Mathf.Max(260, footerRect.y - headerBottom - 8));
            EditorGUI.BeginChangeCheck();
            using (new EditorGUI.DisabledScope(mDraft.IsCreated))
            {
                var so = new SerializedObject(mDraft.Unit);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PropertyField(so.FindProperty("mUnitId"));
                EditorGUILayout.PropertyField(so.FindProperty("mDisplayName"));
                EditorGUILayout.EndHorizontal();
                so.ApplyModifiedPropertiesWithoutUndo();
                mDraft.Folder = EditorGUILayout.TextField("保存先フォルダー", mDraft.Folder);

                var leftWidth = body.width * mSplitX;
                var topHeight = body.height * mSplitY;
                Pane(new Rect(body.x, body.y, leftWidth - 3, topHeight - 3), 0, "ビジュアル", DrawVisual);
                Pane(new Rect(body.x + leftWidth + 3, body.y, body.width - leftWidth - 3, topHeight - 3), 1, "パラメータ", DrawParameters);
                Pane(new Rect(body.x, body.y + topHeight + 3, leftWidth - 3, body.height - topHeight - 3), 2, "ディテイル", DrawDetails);
                Pane(new Rect(body.x + leftWidth + 3, body.y + topHeight + 3, body.width - leftWidth - 3, body.height - topHeight - 3), 3, "スキル", DrawSkills);
                DrawSplitters(body, leftWidth, topHeight);
            }
            if (EditorGUI.EndChangeCheck() && !mDraft.IsCreated)
            {
                try { mDraft.RequireRevision(revision); mDraft.Commit(); }
                catch (Exception error) { mError = error.Message; }
            }
            DrawFooter(footerRect);
        }

        private void Pane(Rect aRect, int aIndex, string aTitle, Action aDraw)
        {
            GUILayout.BeginArea(aRect, EditorStyles.helpBox);
            GUILayout.Label(aTitle, EditorStyles.boldLabel);
            mScroll[aIndex] = EditorGUILayout.BeginScrollView(mScroll[aIndex]);
            aDraw();
            EditorGUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawVisual()
        {
            mDraft.Icon = (Sprite)EditorGUILayout.ObjectField("アイコン", mDraft.Icon, typeof(Sprite), false);
            if (mDraft.Icon != null)
            {
                var preview = AssetPreview.GetAssetPreview(mDraft.Icon) ?? AssetPreview.GetMiniThumbnail(mDraft.Icon);
                var rect = GUILayoutUtility.GetRect(150, 150, GUILayout.ExpandWidth(true));
                if (preview != null) GUI.DrawTexture(rect, preview, ScaleMode.ScaleToFit);
            }
            else EditorGUILayout.HelpBox("未設定でも作成できます。", MessageType.Info);
        }

        private void DrawDetails()
        {
            var so = new SerializedObject(mDraft.Unit);
            EditorGUILayout.PropertyField(so.FindProperty("mTypeAttribute"));
            mDraft.NewAI = EditorGUILayout.Toggle("空のAIを新規作成", mDraft.NewAI);
            using (new EditorGUI.DisabledScope(mDraft.NewAI)) EditorGUILayout.PropertyField(so.FindProperty("mAIProfile"));
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorGUILayout.HelpBox("新規AIは生成後にUnit AI Treeで設定します。", MessageType.Info);
            GUILayout.Label("カタログ登録（任意）", EditorStyles.boldLabel);
            mDraft.UnitCatalog = (PPUnitCatalog)EditorGUILayout.ObjectField("ユニット", mDraft.UnitCatalog, typeof(PPUnitCatalog), false);
            mDraft.VisualCatalog = (PPUnitVisualCatalog)EditorGUILayout.ObjectField("ビジュアル", mDraft.VisualCatalog, typeof(PPUnitVisualCatalog), false);
            mDraft.SkillCatalog = (PPSkillCatalog)EditorGUILayout.ObjectField("スキル", mDraft.SkillCatalog, typeof(PPSkillCatalog), false);
        }

        private void DrawParameters()
        {
            var max = EditorGUILayout.DelayedIntField("最大レベル", mDraft.MaxLevel);
            if (max != mDraft.MaxLevel)
            {
                try { mDraft.SetMaxLevel(max); mError = null; }
                catch (Exception error) { mError = error.Message; }
            }
            mDraft.PreviewLevel = EditorGUILayout.IntSlider("プレビューレベル", mDraft.PreviewLevel, 1, mDraft.MaxLevel);
            var stats = mDraft.Unit.EvaluateStats(mDraft.PreviewLevel);
            var values = new[] { stats.MaxHP, stats.Attack, stats.Defense, stats.Speed, mDraft.Unit.EvaluateDexterity(mDraft.PreviewLevel) };
            for (var i = 0; i < PPUnitCreationDraft.StatNames.Length; i++)
            {
                GUILayout.Label(PPUnitCreationDraft.StatNames[i], EditorStyles.boldLabel);
                EditorGUILayout.BeginHorizontal();
                var oldLabelWidth = EditorGUIUtility.labelWidth;
                EditorGUIUtility.labelWidth = 45;
                var initial = EditorGUILayout.DelayedFloatField("初期", mDraft.GetInitial(i));
                float final;
                using (new EditorGUI.DisabledScope(mDraft.MaxLevel == 1))
                    final = EditorGUILayout.DelayedFloatField("終端", mDraft.MaxLevel == 1 ? initial : mDraft.GetFinal(i));
                EditorGUIUtility.labelWidth = oldLabelWidth;
                EditorGUILayout.EndHorizontal();
                if (initial != mDraft.GetInitial(i) || final != mDraft.GetFinal(i))
                {
                    if (!PPUnitCreationValidator.IsFinite(initial) || !PPUnitCreationValidator.IsFinite(final) || initial < 0 || final < initial || (initial == 0 && final != 0) || (mDraft.MaxLevel == 1 && initial != final))
                        mError = "初期値・終端値が不正です。初期値0は終端0、最大レベル1は初期値と終端を同じにしてください。";
                    else { mDraft.SetStat(i, initial, final); mError = null; }
                }
                EditorGUILayout.LabelField("指定レベル", values[i].ToString("G7"));
                EditorGUILayout.BeginHorizontal();
                mTemplates[i] = EditorGUILayout.Popup(mTemplates[i], PPUnitGrowthTemplate.Names);
                if (GUILayout.Button("適用", GUILayout.Width(48)))
                {
                    mDraft.SetCurve(i, PPUnitGrowthTemplate.Create(mTemplates[i], mDraft.MaxLevel, mDraft.GetInitial(i) > 0 ? mDraft.GetFinal(i) / mDraft.GetInitial(i) : 1));
                    GUI.changed = true;
                }
                EditorGUILayout.EndHorizontal();
                EditorGUI.BeginChangeCheck();
                var curve = EditorGUILayout.CurveField("成長曲線", mDraft.GetCurve(i));
                if (EditorGUI.EndChangeCheck())
                {
                    try { mDraft.SetCurve(i, curve); mError = null; }
                    catch (Exception error) { mError = error.Message; }
                }
                if (i < PPUnitCreationDraft.ChartAxisCount)
                    mDraft.Scales[i] = EditorGUILayout.FloatField("チャート基準値", mDraft.Scales[i]);
                else // チャートに載らないきようさは、基準値の代わりに補正なしの会心率を表示する
                    EditorGUILayout.LabelField("会心率（補正なし）", PPCriticalResolver.ToCriticalPercent(values[i]).ToString("0.##") + "%");
            }
            DrawRadar();
            var so = new SerializedObject(mDraft.Unit);
            // きようさは上で成長曲線と一緒に編集するため、成長しない追加パラメータだけを並べる
            var expand = so.FindProperty("mExpandStatBlock");
            GUILayout.Label("追加パラメータ（成長なし）", EditorStyles.boldLabel);
            var end = expand.GetEndProperty();
            var child = expand.Copy();
            for (var enter = true; child.NextVisible(enter) && !SerializedProperty.EqualContents(child, end); enter = false)
                if (child.name != nameof(PPStatBlock.Dexterity)) EditorGUILayout.PropertyField(child, true);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private void DrawRadar()
        {
            EditorGUILayout.BeginHorizontal();
            var labels = new[] { "初期", "指定", "最大Lv" };
            for (var i = 0; i < 3; i++) mSeries[i] = GUILayout.Toggle(mSeries[i], labels[i]);
            EditorGUILayout.EndHorizontal();
            var rect = GUILayoutUtility.GetRect(230, 210, GUILayout.ExpandWidth(true));
            if (Event.current.type != EventType.Repaint) return;
            var center = rect.center;
            var radius = Mathf.Min(rect.width / 2 - 55, rect.height / 2 - 25);
            var directions = new[] { Vector2.up * -1, Vector2.right, Vector2.up, Vector2.left };
            var colors = new[] { new Color(0.4f, 0.8f, 1), new Color(1, 0.7f, 0.2f), new Color(0.6f, 1, 0.5f) };
            Handles.BeginGUI();
            var oldColor = Handles.color;
            for (var ring = 1; ring <= 4; ring++)
            {
                Handles.color = new Color(0.5f, 0.5f, 0.5f, 0.5f);
                var points = Enumerable.Range(0, 5).Select(aIndex => (Vector3)(center + directions[aIndex % 4] * radius * ring / 4)).ToArray();
                Handles.DrawAAPolyLine(points);
            }
            var levels = new[] { 1, mDraft.PreviewLevel, mDraft.MaxLevel };
            var overflow = false;
            for (var series = 0; series < 3; series++)
            {
                if (!mSeries[series]) continue;
                var stats = mDraft.Unit.EvaluateStats(levels[series]);
                var values = new[] { stats.MaxHP, stats.Attack, stats.Defense, stats.Speed };
                var points = new Vector3[5];
                for (var i = 0; i < 4; i++)
                {
                    var ratio = values[i] / Mathf.Max(0.001f, mDraft.Scales[i]);
                    overflow |= ratio > 1;
                    if (!PPUnitCreationValidator.IsFinite(ratio)) ratio = 0;
                    points[i] = center + directions[i] * radius * Mathf.Clamp01(ratio);
                }
                points[4] = points[0];
                Handles.color = colors[series];
                Handles.DrawAAPolyLine(2.5f, points);
                GUI.Label(new Rect(rect.x + series * 95, rect.y, 95, 20), new GUIContent("● " + labels[series]), new GUIStyle(EditorStyles.label) { normal = { textColor = colors[series] } });
            }
            Handles.color = oldColor;
            Handles.EndGUI();
            for (var i = 0; i < 4; i++)
            {
                var p = center + directions[i] * (radius + 14);
                GUI.Label(new Rect(p.x - 30, p.y - 10, 65, 20), PPUnitCreationDraft.StatNames[i]);
            }
            if (overflow) GUI.Label(new Rect(rect.x, rect.yMax - 20, rect.width, 20), "基準超過あり（外周で表示を制限）", EditorStyles.boldLabel);
        }

        private void DrawSkills()
        {
            var added = (PPSkillDefinition)EditorGUILayout.ObjectField("既存スキルを追加", null, typeof(PPSkillDefinition), false);
            if (added != null) mDraft.Unit.Skills.Add(added);
            if (GUILayout.Button("新規スキルを下書きへ追加")) { mDraft.AddNewSkill(); mSkillIndex = mDraft.Unit.Skills.Count - 1; GUI.changed = true; }
            for (var i = 0; i < mDraft.Unit.Skills.Count; i++)
            {
                var skill = mDraft.Unit.Skills[i];
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button(skill != null ? skill.DisplayName + " [" + skill.SkillId + "]" : "未設定")) mSkillIndex = i;
                using (new EditorGUI.DisabledScope(i == 0))
                    if (GUILayout.Button("↑", GUILayout.Width(24))) { (mDraft.Unit.Skills[i - 1], mDraft.Unit.Skills[i]) = (mDraft.Unit.Skills[i], mDraft.Unit.Skills[i - 1]); mSkillIndex = i - 1; GUI.changed = true; }
                using (new EditorGUI.DisabledScope(i == mDraft.Unit.Skills.Count - 1))
                    if (GUILayout.Button("↓", GUILayout.Width(24))) { (mDraft.Unit.Skills[i + 1], mDraft.Unit.Skills[i]) = (mDraft.Unit.Skills[i], mDraft.Unit.Skills[i + 1]); mSkillIndex = i + 1; GUI.changed = true; }
                if (GUILayout.Button("解除", GUILayout.Width(42)))
                {
                    mDraft.Unit.Skills.RemoveAt(i);
                    if (skill is PPSkillDefinition ppSkill && !mDraft.Unit.Skills.Contains(skill)) mDraft.NewSkills.Remove(ppSkill);
                    mSkillIndex = -1;
                    GUI.changed = true;
                    EditorGUILayout.EndHorizontal();
                    break;
                }
                EditorGUILayout.EndHorizontal();
            }
            if (mSkillIndex < 0 || mSkillIndex >= mDraft.Unit.Skills.Count) return;
            var selected = mDraft.Unit.Skills[mSkillIndex];
            if (selected == null) return;
            if (selected is PPSkillDefinition newSkill && mDraft.NewSkills.Contains(newSkill))
            {
                UnityEditor.Editor.CreateCachedEditor(selected, null, ref mSkillEditor);
                mSkillEditor.OnInspectorGUI();
            }
            else
            {
                EditorGUILayout.HelpBox("既存アセット: " + AssetDatabase.GetAssetPath(selected), MessageType.Info);
                if (GUILayout.Button("既存スキルをInspectorで開く")) Selection.activeObject = selected;
            }
        }

        private void DrawSplitters(Rect aBody, float aLeft, float aTop)
        {
            var vertical = new Rect(aBody.x + aLeft - 3, aBody.y, 6, aBody.height);
            var horizontal = new Rect(aBody.x, aBody.y + aTop - 3, aBody.width, 6);
            EditorGUIUtility.AddCursorRect(vertical, MouseCursor.ResizeHorizontal);
            EditorGUIUtility.AddCursorRect(horizontal, MouseCursor.ResizeVertical);
            var evt = Event.current;
            if (evt.type == EventType.MouseDown && evt.button == 0)
            {
                if (vertical.Contains(evt.mousePosition)) mDragging = 1;
                else if (horizontal.Contains(evt.mousePosition)) mDragging = 2;
                if (mDragging != 0) evt.Use();
            }
            if (evt.type == EventType.MouseDrag && mDragging != 0)
            {
                if (mDragging == 1) mSplitX = Mathf.Clamp((evt.mousePosition.x - aBody.x) / aBody.width, 0.23f, 0.7f);
                else mSplitY = Mathf.Clamp((evt.mousePosition.y - aBody.y) / aBody.height, 0.25f, 0.8f);
                evt.Use(); Repaint();
            }
            if (evt.type == EventType.MouseUp) mDragging = 0;
        }

        private void DrawFooter(Rect aRect)
        {
            GUILayout.BeginArea(aRect, EditorStyles.helpBox);
            if (mDraft.IsCreated)
            {
                GUILayout.Label("作成完了。下書きは生成結果として保持しています。", EditorStyles.boldLabel);
                mScroll[4] = EditorGUILayout.BeginScrollView(mScroll[4]);
                foreach (var path in mDraft.CreatedPaths)
                    if (GUILayout.Button(path)) Selection.activeObject = AssetDatabase.LoadMainAssetAtPath(path);
                EditorGUILayout.EndScrollView();
                if (mDraft.NewAI && GUILayout.Button("生成したAIをUnit AI Treeで編集"))
                    GetWindow<PPUnitAITreeWindow>().SetTarget(AssetDatabase.LoadAssetAtPath<PPUnitAIProfileDefinition>(mDraft.CreatedPaths[2]));
            }
            else
            {
                if (mValidation == null || mValidatedRevision != mDraft.Revision)
                {
                    mValidation = PPUnitCreationValidator.Validate(mDraft);
                    mValidatedRevision = mDraft.Revision;
                }
                mScroll[4] = EditorGUILayout.BeginScrollView(mScroll[4]);
                if (!string.IsNullOrEmpty(mError))
                {
                    EditorGUILayout.HelpBox(mError, MessageType.Error);
                    if (GUILayout.Button("入力エラーを閉じる（現在の下書きの値を使用）")) mError = null;
                }
                foreach (var message in mValidation.Errors) EditorGUILayout.HelpBox(message, MessageType.Error);
                foreach (var message in mValidation.Warnings) EditorGUILayout.HelpBox(message, MessageType.Warning);
                GUILayout.Label("生成予定: " + string.Join("\n", mValidation.Paths), EditorStyles.wordWrappedLabel);
                EditorGUILayout.EndScrollView();
                using (new EditorGUI.DisabledScope(!mValidation.IsValid || !string.IsNullOrEmpty(mError) || EditorApplication.isPlayingOrWillChangePlaymode))
                {
                    if (GUILayout.Button("ユニットと関連アセットを作成", GUILayout.Height(28)))
                    {
                        GUI.FocusControl(null);
                        try { PPUnitCreationService.Create(mDraft, mDraft.Revision, Guid.NewGuid().ToString("N")); mError = null; }
                        catch (Exception error) { mError = error.Message; Invalidate(); }
                    }
                }
            }
            GUILayout.EndArea();
        }
    }
}
