/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPUnitCreatorWindow.cs
 * @author hqrse
 * @date 2026/09/09
 * @brief タブ切替式のビューでユニットの作成を支援する
 * =====================================*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AttributeUtility;
using UnityEditor;
using UnityEngine;

namespace PPCore
{
    public sealed class PPUnitCreatorWindow : EditorWindow
    {
        private static readonly string[] TabNames = { "ビジュアル", "パラメータ", "スキル", "ディテイル" };
        private static readonly string[] ParameterViews = { "編集", "比較", "ランキング" };
        private static readonly Color[] SeriesColors = { new(0.4f, 0.8f, 1), new(1, 0.7f, 0.2f), new(0.6f, 1, 0.5f) };
        private static readonly Color[] CompareColors = { new(0.4f, 0.8f, 1), new(1, 0.45f, 0.45f) };
        // 編集ビューの表の列幅と棒グラフの高さ。既定サイズ付近で1画面に収まるよう詰めている
        private const float NameWidth = 60;
        private const float CellWidth = 64;
        private const float ApplyWidth = 40;
        private const float ChartHeight = 170;
        [Label("下書きID")][SerializeField] private string mDraftId;
        [Label("選択中タブ")][SerializeField] private int mTab;
        [Label("パラメータ表示")][SerializeField] private int mParameterView;
        // 比較・ランキングの評価レベルは編集のプレビューレベルと独立に持つ。初期値は最大Lvへ丸められる
        [Label("比較の評価レベル")][SerializeField] private int mCompareEvalLevel = int.MaxValue;
        [Label("ランキング能力")][SerializeField] private int mRankingStat;
        [Label("ランキングの評価レベル")][SerializeField] private int mRankingEvalLevel = int.MaxValue;
        // 既定は展開。旧フィールド（mExtraExpanded=false）の保存値を引き継がないよう名前を変えている
        [Label("追加パラメータを閉じる")][SerializeField] private bool mExtraCollapsed;
        private PPUnitCreationDraft mDraft;
        private readonly Vector2[] mScroll = new Vector2[5];
        private readonly int[] mTemplates = new int[PPUnitCreationDraft.StatNames.Length];
        private readonly bool[] mSeries = { true, true, true };
        private int mSkillIndex = -1;
        private UnityEditor.Editor mSkillEditor;
        [NonSerialized] private PPUnitCreationValidation mValidation;
        [NonSerialized] private int mValidatedRevision = -1;
        // ランキングの母集団。再コンパイルやプロジェクト変更で破棄し、次の描画で読み直す
        [NonSerialized] private List<PPUnitDefinition> mUnits;
        // ドラッグ中のプレビューレベル変更。ドラッグを離した時点で確定（Commit）する
        [NonSerialized] private bool mPreviewPending;
        private string mError;
        // 操作ツール（MCP の pp_unit_creator_window）へ渡す主要な操作対象の矩形（ウィンドウ座標）。Repaint のたびに記録し直す
        [NonSerialized] private readonly Dictionary<string, Rect> mControlRects = new();
        // Repaint 時のウィンドウ内容の左上（スクリーン座標）。矩形の換算と、SendEvent の座標とのずれの算出に使う
        [NonSerialized] private Vector2? mScreenOrigin;
        // 直近のマウスイベントが OnGUI に届いた時点の座標。操作ツールが送信座標と突き合わせてずれを実測する
        [NonSerialized] private Vector2? mLastMousePosition;

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

        private void Invalidate() { mValidatedRevision = -1; mUnits = null; Repaint(); }
        private void OnInspectorUpdate() => Repaint();
        private void OnUndo() { if (mDraft != null && !mDraft.IsCreated) mDraft.Commit(); Invalidate(); }

        // 既存ユニットを編集用の下書きへ読み込んで開く。元アセットは「保存」まで変わらない
        private void OpenExisting(PPUnitDefinition aSource)
        {
            try
            {
                mDraft = PPUnitCreationDraft.CreateFrom(aSource);
                PPUnitDraftStore.Save(mDraft);
                mDraftId = mDraft.DraftId;
                mValidatedRevision = -1;
                mSkillIndex = -1;
                mPreviewPending = false;
                mError = null;
            }
            catch (Exception error) { mError = error.Message; }
        }

        private void NewDraft()
        {
            mDraft = PPUnitCreationDraft.Create();
            PPUnitDraftStore.Save(mDraft);
            mDraftId = mDraft.DraftId;
            mValidatedRevision = -1;
            mSkillIndex = -1;
            mPreviewPending = false;
            mError = null;
        }

        private void OnGUI()
        {
            if (Event.current.type == EventType.Repaint) { mControlRects.Clear(); mScreenOrigin = GUIUtility.GUIToScreenPoint(Vector2.zero); }
            if (Event.current.isMouse || Event.current.type == EventType.MouseMove) mLastMousePosition = Event.current.mousePosition;
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
            GUILayout.Label("下書き: " + mDraftId + " / 更新 " + revision, GUILayout.ExpandWidth(false));
            GUILayout.Label(mDraft.IsEditing ? "編集: " + AssetDatabase.GUIDToAssetPath(mDraft.SourceGuid) : "新規作成", EditorStyles.boldLabel);
            GUILayout.Label("既存ユニットを開く", GUILayout.ExpandWidth(false));
            var picked = (PPUnitDefinition)EditorGUILayout.ObjectField(null, typeof(PPUnitDefinition), false, GUILayout.Width(160));
            if (picked != null) { OpenExisting(picked); GUIUtility.ExitGUI(); }
            if (GUILayout.Button("新しい下書き", EditorStyles.toolbarButton, GUILayout.Width(110))) { NewDraft(); GUIUtility.ExitGUI(); }
            GUILayout.EndHorizontal();
            var footerRect = new Rect(4, position.height - 184, position.width - 8, 180);
            // ヘッダーは「トロールバー + (Header装飾込みの)ユニットID/表示名行 + 保存先フォルダー行」の
            // 固定3行構成。GUILayoutUtility.GetRectでの動的確保はBeginArea基準のビュー群と
            // 相性が悪く描画されなくなる事象を確認したため、実測に基づく固定オフセットで確保する
            const float headerBottom = 100f;
            var tabRect = new Rect(4, headerBottom, position.width - 8, 22);
            var body = new Rect(4, tabRect.yMax + 4, position.width - 8, Mathf.Max(200, footerRect.y - tabRect.yMax - 12));
            mTab = Mathf.Clamp(mTab, 0, TabNames.Length - 1);
            EditorGUI.BeginChangeCheck();
            using (new EditorGUI.DisabledScope(mDraft.IsCreated))
            {
                var so = new SerializedObject(mDraft.Unit);
                EditorGUILayout.BeginHorizontal();
                // 編集モードではユニットIDと保存先は元アセットのまま固定する
                using (new EditorGUI.DisabledScope(mDraft.IsEditing)) EditorGUILayout.PropertyField(so.FindProperty("mUnitId"));
                EditorGUILayout.PropertyField(so.FindProperty("mDisplayName"));
                EditorGUILayout.EndHorizontal();
                so.ApplyModifiedPropertiesWithoutUndo();
                using (new EditorGUI.DisabledScope(mDraft.IsEditing)) mDraft.Folder = EditorGUILayout.TextField("保存先フォルダー", mDraft.Folder);
            }
            // 切替は描画後に反映し、同じイベント内でレイアウトの要素数が変わらないようにする
            var tab = KeepChanged(() => GUI.Toolbar(tabRect, mTab, TabNames));
            RecordRect("tabToolbar", tabRect);
            GUILayout.BeginArea(body, EditorStyles.helpBox);
            // スクロールは表示操作なので、下書きの変更として扱わない
            mScroll[mTab] = KeepChanged(() => EditorGUILayout.BeginScrollView(mScroll[mTab]));
            // 生成済みの下書きは編集できないが、パラメータの比較・ランキングは閲覧できる
            if (mTab == 1) DrawParameterTab();
            else
            {
                using (new EditorGUI.DisabledScope(mDraft.IsCreated))
                {
                    if (mTab == 0) DrawVisual();
                    else if (mTab == 2) DrawSkills();
                    else DrawDetails();
                }
            }
            KeepChanged(() => EditorGUILayout.EndScrollView());
            RecordRect("bodyScroll");
            GUILayout.EndArea();
            var changed = EditorGUI.EndChangeCheck();
            if (mPreviewPending && GUIUtility.hotControl == 0) { mPreviewPending = false; changed = true; }
            if (changed && !mDraft.IsCreated)
            {
                try { mDraft.RequireRevision(revision); mDraft.Commit(); }
                catch (Exception error) { mError = error.Message; }
            }
            DrawFooter(footerRect);
            if (tab != mTab) { mTab = tab; Repaint(); }
        }

        // 表示の切替はウィンドウの状態なので、下書きの変更（更新番号の加算）として扱わない
        private static T KeepChanged<T>(Func<T> aDraw)
        {
            var changed = GUI.changed;
            var result = aDraw();
            GUI.changed = changed;
            return result;
        }

        private static void KeepChanged(Action aDraw)
        {
            var changed = GUI.changed;
            aDraw();
            GUI.changed = changed;
        }

        // 幅があればアイコンと立ち絵を横に並べ、狭ければ縦に積む。立ち絵は縦長なので高さを大きく取る
        private void DrawVisual()
        {
            var wide = position.width >= 760;
            if (wide) EditorGUILayout.BeginHorizontal();
            using (new EditorGUILayout.VerticalScope(wide ? GUILayout.Width(position.width * 0.4f) : GUILayout.ExpandWidth(true)))
                mDraft.Icon = DrawSprite("アイコン", mDraft.Icon, 150);
            using (new EditorGUILayout.VerticalScope())
                mDraft.Portrait = DrawSprite("立ち絵", mDraft.Portrait, 360);
            if (wide) EditorGUILayout.EndHorizontal();
        }

        // 選び直したSpriteは次のイベントからプレビューし、同じイベント内でレイアウトの要素数が変わらないようにする
        private static Sprite DrawSprite(string aLabel, Sprite aSprite, float aHeight)
        {
            var result = (Sprite)EditorGUILayout.ObjectField(aLabel, aSprite, typeof(Sprite), false);
            if (aSprite == null)
            {
                EditorGUILayout.HelpBox("未設定でも作成できます。", MessageType.Info);
                return result;
            }
            var rect = GUILayoutUtility.GetRect(aHeight, aHeight, GUILayout.ExpandWidth(true));
            if (Event.current.type != EventType.Repaint) return result;
            var texture = aSprite.texture;
            if (texture == null)
            {
                var preview = AssetPreview.GetAssetPreview(aSprite) ?? AssetPreview.GetMiniThumbnail(aSprite);
                if (preview != null) GUI.DrawTexture(rect, preview, ScaleMode.ScaleToFit);
                return result;
            }
            // 低解像度のプレビュー画像ではなく元テクスチャの該当範囲を縦横比を保って描く（ScaleToFit相当）
            var source = aSprite.rect;
            var fit = rect;
            var aspect = source.width / Mathf.Max(1, source.height);
            if (rect.width / rect.height > aspect) { fit.width = rect.height * aspect; fit.x = rect.center.x - fit.width / 2; }
            else { fit.height = rect.width / aspect; fit.y = rect.center.y - fit.height / 2; }
            GUI.DrawTextureWithTexCoords(fit, texture, new Rect(source.x / texture.width, source.y / texture.height, source.width / texture.width, source.height / texture.height));
            return result;
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

        private void DrawParameterTab()
        {
            mParameterView = Mathf.Clamp(mParameterView, 0, ParameterViews.Length - 1);
            var view = KeepChanged(() => GUILayout.Toolbar(mParameterView, ParameterViews));
            RecordRect("parameterViewToolbar");
            EditorGUILayout.Space(4);
            if (mParameterView == 1) DrawCompare();
            else if (mParameterView == 2) DrawRanking();
            else DrawParameters();
            if (view != mParameterView) { mParameterView = view; Repaint(); }
        }

        // 編集ビューは既定サイズ付近で1画面に収まるよう、レベル1行・棒グラフ・能力ごと1行の表・折りたたみの順に詰める
        private void DrawParameters()
        {
            using (new EditorGUI.DisabledScope(mDraft.IsCreated)) DrawLevelRow();
            var labels = PPUnitStatComparer.LevelLabels(mDraft);
            DrawBarChart(labels, SeriesColors, PPUnitStatComparer.SeriesLevels(mDraft).Select(aLevel => PPUnitStatComparer.Evaluate(mDraft.Unit, aLevel)).ToArray(), mSeries);
            using (new EditorGUI.DisabledScope(mDraft.IsCreated)) DrawStatTable();
            DrawExtraParameters();
        }

        private void DrawLevelRow()
        {
            EditorGUILayout.BeginHorizontal();
            var oldLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 70;
            var max = EditorGUILayout.DelayedIntField("最大レベル", mDraft.MaxLevel, GUILayout.Width(150));
            EditorGUIUtility.labelWidth = 100;
            // プレビューレベルはドラッグ中に毎フレーム確定せず、離した時点で確定する（OnGUI末尾）
            var preview = KeepChanged(() => EditorGUILayout.IntSlider("プレビューレベル", mDraft.PreviewLevel, 1, mDraft.MaxLevel));
            RecordRect("previewSlider", aSlider: true);
            EditorGUIUtility.labelWidth = oldLabelWidth;
            EditorGUILayout.EndHorizontal();
            if (preview != mDraft.PreviewLevel) { mDraft.PreviewLevel = preview; mPreviewPending = true; }
            if (max != mDraft.MaxLevel)
            {
                try { mDraft.SetMaxLevel(max); mError = null; }
                catch (Exception error) { mError = error.Message; }
            }
        }

        // 列: 能力 / 初期 / 終端 / 指定Lvの値 / テンプレート＋適用 / 成長曲線 / 基準値 / 会心率
        private void DrawStatTable()
        {
            var values = PPUnitStatComparer.Evaluate(mDraft.Unit, mDraft.PreviewLevel);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("能力", EditorStyles.boldLabel, GUILayout.Width(NameWidth));
            GUILayout.Label("初期", EditorStyles.boldLabel, GUILayout.Width(CellWidth));
            GUILayout.Label("終端", EditorStyles.boldLabel, GUILayout.Width(CellWidth));
            GUILayout.Label("Lv" + mDraft.PreviewLevel, EditorStyles.boldLabel, GUILayout.Width(CellWidth));
            GUILayout.Label("テンプレート", EditorStyles.boldLabel, GUILayout.Width(CellWidth + ApplyWidth + 4));
            GUILayout.Label("成長曲線", EditorStyles.boldLabel, GUILayout.MinWidth(80), GUILayout.ExpandWidth(true));
            GUILayout.Label("基準値", EditorStyles.boldLabel, GUILayout.Width(CellWidth));
            GUILayout.Label("会心率", EditorStyles.boldLabel, GUILayout.Width(CellWidth));
            EditorGUILayout.EndHorizontal();
            for (var i = 0; i < PPUnitCreationDraft.StatNames.Length; i++)
            {
                var index = i;
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(PPUnitCreationDraft.StatNames[i], GUILayout.Width(NameWidth));
                var initial = EditorGUILayout.DelayedFloatField(mDraft.GetInitial(i), GUILayout.Width(CellWidth));
                float final;
                using (new EditorGUI.DisabledScope(mDraft.MaxLevel == 1))
                    final = EditorGUILayout.DelayedFloatField(mDraft.MaxLevel == 1 ? initial : mDraft.GetFinal(i), GUILayout.Width(CellWidth));
                GUILayout.Label(values[i].ToString("G7"), GUILayout.Width(CellWidth));
                // テンプレートの選択は適用ボタンを押すまで下書きを変えない
                mTemplates[i] = KeepChanged(() => EditorGUILayout.Popup(mTemplates[index], PPUnitGrowthTemplate.Names, GUILayout.Width(CellWidth)));
                var apply = GUILayout.Button("適用", GUILayout.Width(ApplyWidth));
                EditorGUI.BeginChangeCheck();
                var curve = EditorGUILayout.CurveField(mDraft.GetCurve(i), GUILayout.MinWidth(80), GUILayout.ExpandWidth(true));
                var curveChanged = EditorGUI.EndChangeCheck();
                mDraft.Scales[i] = EditorGUILayout.FloatField(mDraft.Scales[i], GUILayout.Width(CellWidth));
                // 会心率（補正なし）はきようさの行だけに出す
                GUILayout.Label(i == PPUnitCreationDraft.StatNames.Length - 1 ? PPCriticalResolver.ToCriticalPercent(values[i]).ToString("0.##") + "%" : "", GUILayout.Width(CellWidth));
                EditorGUILayout.EndHorizontal();
                if (initial != mDraft.GetInitial(i) || final != mDraft.GetFinal(i))
                {
                    if (!PPUnitCreationValidator.IsFinite(initial) || !PPUnitCreationValidator.IsFinite(final) || initial < 0 || final < initial || (initial == 0 && final != 0) || (mDraft.MaxLevel == 1 && initial != final))
                        mError = "初期値・終端値が不正です。初期値0は終端0、最大レベル1は初期値と終端を同じにしてください。";
                    else { mDraft.SetStat(i, initial, final); mError = null; }
                }
                if (apply)
                {
                    mDraft.SetCurve(i, PPUnitGrowthTemplate.Create(mTemplates[i], mDraft.MaxLevel, mDraft.GetInitial(i) > 0 ? mDraft.GetFinal(i) / mDraft.GetInitial(i) : 1));
                    GUI.changed = true;
                }
                else if (curveChanged)
                {
                    try { mDraft.SetCurve(i, curve); mError = null; }
                    catch (Exception error) { mError = error.Message; }
                }
            }
        }

        // きようさは上の表で成長曲線と一緒に編集するため、成長しない追加パラメータだけを折りたたみに並べる
        private void DrawExtraParameters()
        {
            var expanded = !mExtraCollapsed;
            var next = KeepChanged(() => EditorGUILayout.Foldout(expanded, "追加パラメータ（成長なし）", true));
            if (expanded)
            {
                using (new EditorGUI.DisabledScope(mDraft.IsCreated))
                {
                    var so = new SerializedObject(mDraft.Unit);
                    var expand = so.FindProperty("mExpandStatBlock");
                    var end = expand.GetEndProperty();
                    var child = expand.Copy();
                    EditorGUI.indentLevel++;
                    for (var enter = true; child.NextVisible(enter) && !SerializedProperty.EqualContents(child, end); enter = false)
                        if (child.name != nameof(PPStatBlock.Dexterity)) EditorGUILayout.PropertyField(child, true);
                    EditorGUI.indentLevel--;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }
            if (next != expanded) { mExtraCollapsed = !next; Repaint(); }
        }

        // 評価レベルのスライダー。最大Lvが下がった場合もここで 1〜最大Lv に丸める
        private int LevelSlider(int aLevel)
        {
            var level = Mathf.Clamp(aLevel, 1, mDraft.MaxLevel);
            return KeepChanged(() => EditorGUILayout.IntSlider("評価レベル", level, 1, mDraft.MaxLevel));
        }

        private void DrawCompare()
        {
            // 選び直した比較対象は次のイベントから描画し、同じイベント内でレイアウトの要素数が変わらないようにする
            var target = mDraft.CompareTarget;
            using (new EditorGUI.DisabledScope(mDraft.IsCreated))
                mDraft.CompareTarget = (PPUnitDefinition)EditorGUILayout.ObjectField("比較対象", mDraft.CompareTarget, typeof(PPUnitDefinition), false);
            mCompareEvalLevel = LevelSlider(mCompareEvalLevel);
            RecordRect("compareSlider", aSlider: true);
            if (target == null)
            {
                EditorGUILayout.HelpBox("比較する既存ユニット（PPUnitDefinition）を選択してください。比較対象も下書きと同じレベルで評価します。", MessageType.Info);
                return;
            }
            var comparison = PPUnitStatComparer.Compare(mDraft, target, mCompareEvalLevel);
            var suffix = " (Lv" + comparison.Level + ")";
            DrawBarChart(new[] { "下書き" + suffix, target.DisplayName + " [" + target.UnitId + "]" + suffix }, CompareColors, new[] { comparison.Draft, comparison.Target });
            DrawCompareRow(EditorStyles.boldLabel, "能力", "下書き", "比較対象", "差分（下書き − 比較対象）" + suffix);
            for (var i = 0; i < PPUnitStatComparer.StatCount; i++)
            {
                var diff = comparison.Diff[i];
                DrawCompareRow(EditorStyles.label, PPUnitCreationDraft.StatNames[i], comparison.Draft[i].ToString("G7"), comparison.Target[i].ToString("G7"), (diff > 0 ? "+" : "") + diff.ToString("G7"));
            }
        }

        private static void DrawCompareRow(GUIStyle aStyle, params string[] aCells)
        {
            EditorGUILayout.BeginHorizontal();
            for (var i = 0; i < aCells.Length; i++) GUILayout.Label(aCells[i], aStyle, i < aCells.Length - 1 ? GUILayout.Width(100) : GUILayout.ExpandWidth(true));
            EditorGUILayout.EndHorizontal();
        }

        private void DrawRanking()
        {
            mRankingStat = KeepChanged(() => EditorGUILayout.Popup("能力", Mathf.Clamp(mRankingStat, 0, PPUnitStatComparer.StatCount - 1), PPUnitCreationDraft.StatNames));
            RecordRect("rankingStatPopup");
            mRankingEvalLevel = LevelSlider(mRankingEvalLevel);
            RecordRect("rankingSlider", aSlider: true);
            mUnits ??= PPUnitStatComparer.FindUnits();
            var ranking = PPUnitStatComparer.Rank(mDraft, mUnits, mRankingStat, mRankingEvalLevel);
            var own = ranking.First(aRow => aRow.IsDraft);
            EditorGUILayout.LabelField("下書きの順位 (Lv" + mRankingEvalLevel + ")", own.Rank + " 位 / " + ranking.Count + " 体（同値は同順位）", EditorStyles.boldLabel);
            DrawRankingRow(EditorStyles.boldLabel, Color.clear, "順位", "ユニット名", "ID", PPUnitCreationDraft.StatNames[mRankingStat] + " (Lv" + mRankingEvalLevel + ")");
            foreach (var row in ranking)
            {
                if (row.IsDraft) DrawRankingRow(EditorStyles.boldLabel, new Color(1, 0.7f, 0.2f, 0.3f), row.Rank + " 位", row.Unit.DisplayName + "（下書き）", row.Unit.UnitId, row.Value.ToString("G7"));
                else DrawRankingRow(EditorStyles.label, Color.clear, row.Rank + " 位", row.Unit.DisplayName, row.Unit.UnitId, row.Value.ToString("G7"));
            }
        }

        private static void DrawRankingRow(GUIStyle aStyle, Color aBackground, string aRank, string aName, string aId, string aValue)
        {
            var rect = GUILayoutUtility.GetRect(0, 20, GUILayout.ExpandWidth(true));
            if (aBackground.a > 0) EditorGUI.DrawRect(rect, aBackground);
            GUI.Label(new Rect(rect.x + 4, rect.y, 54, rect.height), aRank, aStyle);
            GUI.Label(new Rect(rect.x + 58, rect.y, Mathf.Max(60, rect.width - 348), rect.height), aName, aStyle);
            GUI.Label(new Rect(rect.xMax - 286, rect.y, 150, rect.height), aId, aStyle);
            GUI.Label(new Rect(rect.xMax - 132, rect.y, 130, rect.height), aValue, aStyle);
        }

        // 能力ごとに系列の縦棒を並べる。高さは基準値で正規化し、基準超過は上端で止めて赤線で示す
        // aValues : 系列ごとの5能力の値（StatNames と同じ並び）
        // aVisible : 系列の表示フラグ。指定すると凡例がそのまま表示トグルになる
        private void DrawBarChart(string[] aNames, Color[] aColors, float[][] aValues, bool[] aVisible = null)
        {
            var rect = GUILayoutUtility.GetRect(300, ChartHeight, GUILayout.ExpandWidth(true));
            var legendStyle = new GUIStyle(aVisible != null ? EditorStyles.toggle : EditorStyles.label) { richText = true };
            for (var series = 0; series < aNames.Length; series++)
            {
                var legendRect = new Rect(rect.x + series * 180, rect.y, 175, 18);
                var content = "<color=#" + ColorUtility.ToHtmlStringRGB(aColors[series]) + ">■</color> " + aNames[series];
                var index = series;
                if (aVisible != null) aVisible[series] = KeepChanged(() => GUI.Toggle(legendRect, aVisible[index], content, legendStyle));
                else if (Event.current.type == EventType.Repaint) GUI.Label(legendRect, content, legendStyle);
            }
            if (Event.current.type != EventType.Repaint) return;
            // 上から 凡例1行・値ラベル・棒・能力名（基準値）の順
            var plot = new Rect(rect.x + 8, rect.y + 34, rect.width - 16, rect.height - 54);
            for (var line = 0; line <= 4; line++)
                EditorGUI.DrawRect(new Rect(plot.x, plot.yMax - plot.height * line / 4, plot.width, 1), new Color(0.5f, 0.5f, 0.5f, line == 0 ? 0.8f : 0.35f));
            var shown = Enumerable.Range(0, aNames.Length).Where(aIndex => aVisible == null || aVisible[aIndex]).ToArray();
            var group = plot.width / PPUnitStatComparer.StatCount;
            var width = shown.Length == 0 ? 0 : Mathf.Min(28, group * 0.8f / shown.Length);
            var valueStyle = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.LowerCenter };
            var nameStyle = new GUIStyle(EditorStyles.label) { alignment = TextAnchor.UpperCenter };
            var overflow = false;
            for (var stat = 0; stat < PPUnitStatComparer.StatCount; stat++)
            {
                var left = plot.x + group * stat + (group - width * shown.Length) / 2;
                for (var slot = 0; slot < shown.Length; slot++)
                {
                    var series = shown[slot];
                    var value = aValues[series][stat];
                    var ratio = value / Mathf.Max(0.001f, mDraft.Scales[stat]);
                    if (!PPUnitCreationValidator.IsFinite(ratio) || ratio < 0) ratio = 0;
                    overflow |= ratio > 1;
                    var height = plot.height * Mathf.Min(1, ratio);
                    var bar = new Rect(left + width * slot + 1, plot.yMax - height, width - 2, height);
                    EditorGUI.DrawRect(bar, aColors[series]);
                    if (ratio > 1) EditorGUI.DrawRect(new Rect(bar.x, plot.y - 3, bar.width, 3), Color.red);
                    GUI.Label(new Rect(bar.center.x - 30, bar.y - 15, 60, 14), value.ToString("G7"), valueStyle);
                }
                GUI.Label(new Rect(plot.x + group * stat, plot.yMax + 2, group, 18), PPUnitCreationDraft.StatNames[stat] + "（基準 " + mDraft.Scales[stat].ToString("G7") + "）", nameStyle);
            }
            if (overflow) GUI.Label(new Rect(rect.xMax - 300, rect.y, 300, 18), "基準超過あり（赤線の棒は上端で制限）", new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.UpperRight });
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
                if (KeepChanged(() => GUILayout.Button(skill != null ? skill.DisplayName + " [" + skill.SkillId + "]" : "未設定"))) mSkillIndex = i;
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
                if (KeepChanged(() => GUILayout.Button("既存スキルをInspectorで開く"))) Selection.activeObject = selected;
            }
        }

        private void DrawFooter(Rect aRect)
        {
            GUILayout.BeginArea(aRect, EditorStyles.helpBox);
            if (mDraft.IsCreated)
            {
                GUILayout.Label("作成完了。下書きは生成結果として保持しています。", EditorStyles.boldLabel);
                mScroll[4] = KeepChanged(() => EditorGUILayout.BeginScrollView(mScroll[4]));
                foreach (var path in mDraft.CreatedPaths)
                    if (GUILayout.Button(path)) Selection.activeObject = AssetDatabase.LoadMainAssetAtPath(path);
                KeepChanged(() => EditorGUILayout.EndScrollView());
                RecordRect("footerScroll");
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
                mScroll[4] = KeepChanged(() => EditorGUILayout.BeginScrollView(mScroll[4]));
                if (!string.IsNullOrEmpty(mError))
                {
                    EditorGUILayout.HelpBox(mError, MessageType.Error);
                    if (GUILayout.Button("入力エラーを閉じる（現在の下書きの値を使用）")) mError = null;
                }
                foreach (var message in mValidation.Errors) EditorGUILayout.HelpBox(message, MessageType.Error);
                foreach (var message in mValidation.Warnings) EditorGUILayout.HelpBox(message, MessageType.Warning);
                if (mDraft.IsEditing)
                {
                    // 保存は元アセットを上書きするため、書き換える・作成するアセットを事前に示す（Undo は非対応）
                    var lines = mValidation.Paths.Select(aPath => (File.Exists(aPath) ? "更新: " : "新規: ") + aPath)
                        .Concat(mValidation.CatalogPaths.Select(aPath => "カタログ登録: " + aPath));
                    GUILayout.Label("保存で書き換える・作成するアセット（Undo 不可）:\n" + string.Join("\n", lines), EditorStyles.wordWrappedLabel);
                    if (mDraft.SavedRevision >= 0) GUILayout.Label("最終保存: 更新 " + mDraft.SavedRevision + " の内容", EditorStyles.miniLabel);
                }
                else GUILayout.Label("生成予定: " + string.Join("\n", mValidation.Paths), EditorStyles.wordWrappedLabel);
                KeepChanged(() => EditorGUILayout.EndScrollView());
                RecordRect("footerScroll");
                using (new EditorGUI.DisabledScope(!mValidation.IsValid || !string.IsNullOrEmpty(mError) || EditorApplication.isPlayingOrWillChangePlaymode))
                {
                    if (GUILayout.Button(mDraft.IsEditing ? "保存" : "ユニットと関連アセットを作成", GUILayout.Height(28)))
                    {
                        GUI.FocusControl(null);
                        try { PPUnitCreationService.Create(mDraft, mDraft.Revision, Guid.NewGuid().ToString("N")); mError = null; }
                        catch (Exception error) { mError = error.Message; Invalidate(); }
                    }
                }
            }
            GUILayout.EndArea();
        }

        // 直前に描いた要素（または指定の矩形）をウィンドウ座標で記録する。スライダーは値を動かす溝の範囲も「〜Track」で記録する
        private void RecordRect(string aKey, Rect? aRect = null, bool aSlider = false)
        {
            if (Event.current.type != EventType.Repaint || mScreenOrigin == null) return;
            var rect = aRect ?? GUILayoutUtility.GetLastRect();
            rect.position = GUIUtility.GUIToScreenPoint(rect.position) - mScreenOrigin.Value;
            mControlRects[aKey] = rect;
            // EditorGUI のスライダーは「ラベル幅 | 溝 | 5px | 数値欄（fieldWidth）」の並び
            if (aSlider) mControlRects[aKey + "Track"] = new Rect(rect.x + EditorGUIUtility.labelWidth, rect.y, rect.width - EditorGUIUtility.labelWidth - EditorGUIUtility.fieldWidth - 5, rect.height);
        }

        // 操作ツール（MCP の pp_unit_creator_window）が表示状態と矩形を読み書きする口。描画には関与せず、下書きの内容も変えない
        internal static class PPWindowAccess
        {
            internal static string[] TabNames => PPUnitCreatorWindow.TabNames;
            internal static string[] ParameterViews => PPUnitCreatorWindow.ParameterViews;
            internal static string DraftId(PPUnitCreatorWindow aWindow) => aWindow.mDraftId;
            internal static int Tab(PPUnitCreatorWindow aWindow) => Mathf.Clamp(aWindow.mTab, 0, PPUnitCreatorWindow.TabNames.Length - 1);
            internal static int ParameterView(PPUnitCreatorWindow aWindow) => Mathf.Clamp(aWindow.mParameterView, 0, PPUnitCreatorWindow.ParameterViews.Length - 1);
            internal static int CompareLevel(PPUnitCreatorWindow aWindow) => aWindow.mCompareEvalLevel;
            internal static int RankingLevel(PPUnitCreatorWindow aWindow) => aWindow.mRankingEvalLevel;
            internal static int RankingStat(PPUnitCreatorWindow aWindow) => aWindow.mRankingStat;
            internal static bool ExtraCollapsed(PPUnitCreatorWindow aWindow) => aWindow.mExtraCollapsed;
            internal static bool PreviewPending(PPUnitCreatorWindow aWindow) => aWindow.mPreviewPending;
            internal static bool[] Series(PPUnitCreatorWindow aWindow) => aWindow.mSeries;
            internal static Vector2 BodyScroll(PPUnitCreatorWindow aWindow) => aWindow.mScroll[Tab(aWindow)];
            internal static IReadOnlyDictionary<string, Rect> Rects(PPUnitCreatorWindow aWindow) => aWindow.mControlRects;
            internal static Vector2? ScreenOrigin(PPUnitCreatorWindow aWindow) => aWindow.mScreenOrigin;
            internal static Vector2? LastMousePosition(PPUnitCreatorWindow aWindow) => aWindow.mLastMousePosition;
            internal static void ClearLastMousePosition(PPUnitCreatorWindow aWindow) => aWindow.mLastMousePosition = null;

            // 指定された項目だけを変える。値の範囲は描画時と同じく丸める
            internal static void Set(PPUnitCreatorWindow aWindow, int? aTab, int? aParameterView, int? aCompareLevel, int? aRankingLevel, int? aRankingStat, bool? aExtraCollapsed, bool[] aSeries)
            {
                if (aTab.HasValue) aWindow.mTab = Mathf.Clamp(aTab.Value, 0, PPUnitCreatorWindow.TabNames.Length - 1);
                if (aParameterView.HasValue) aWindow.mParameterView = Mathf.Clamp(aParameterView.Value, 0, PPUnitCreatorWindow.ParameterViews.Length - 1);
                if (aCompareLevel.HasValue) aWindow.mCompareEvalLevel = Mathf.Max(1, aCompareLevel.Value);
                if (aRankingLevel.HasValue) aWindow.mRankingEvalLevel = Mathf.Max(1, aRankingLevel.Value);
                if (aRankingStat.HasValue) aWindow.mRankingStat = Mathf.Clamp(aRankingStat.Value, 0, PPUnitStatComparer.StatCount - 1);
                if (aExtraCollapsed.HasValue) aWindow.mExtraCollapsed = aExtraCollapsed.Value;
                if (aSeries != null) for (var i = 0; i < Mathf.Min(aSeries.Length, aWindow.mSeries.Length); i++) aWindow.mSeries[i] = aSeries[i];
            }
        }
    }
}
