/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPPartyAIDebugWindow.cs
 * @author hqrse
 * @date 2026/08/10
 * @brief パーティAIの思考内訳を表示するデバッグウィンドウ
 * =====================================*/

using UnityEditor;
using UnityEngine;

namespace PPCore
{
    // パーティ AI の思考内訳を表示するウィンドウ
    // 「どの状況ルールが成立し、どんな作戦になり、予算がいくらで、
    // なぜその行動が採用／却下されたか」を 1 画面で追えるようにする
    // AI の調整回数を考えると、ログを読み解く運用では追いつかないため専用の表示を用意している
    public class PPPartyAIDebugWindow : EditorWindow
    {
        // 一覧のスクロール位置
        private Vector2 mScroll;
        // 詳細を開いている記録の番号
        private int mSelectedIndex = 0;
        // 却下された候補も表示するか
        private bool mIsShowRejected = true;

        [MenuItem("Window/Party AI Debug")]
        private static void Open()
        {
            var window = GetWindow<PPPartyAIDebugWindow>();
            window.titleContent = new GUIContent("Party AI Debug");
            window.Show();
        }

        private void OnEnable()
        {
            PPPartyAIDebugStore.OnReportsChanged += Repaint;
        }

        private void OnDisable()
        {
            PPPartyAIDebugStore.OnReportsChanged -= Repaint;
        }

        private void OnGUI()
        {
            DrawToolbar();

            var reports = PPPartyAIDebugStore.Reports;
            if (reports.Count == 0)
            {
                EditorGUILayout.HelpBox("思考記録がありません。バトルを再生すると記録されます。", MessageType.Info);
                return;
            }

            mSelectedIndex = Mathf.Clamp(mSelectedIndex, 0, reports.Count - 1);
            DrawHistorySelector(reports.Count);

            mScroll = EditorGUILayout.BeginScrollView(mScroll);
            DrawReport(reports[mSelectedIndex]);
            EditorGUILayout.EndScrollView();
        }

        // 上部のツールバーを描画する
        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Button("クリア", EditorStyles.toolbarButton, GUILayout.Width(60f)))
            {
                PPPartyAIDebugStore.Clear();
                mSelectedIndex = 0;
            }
            mIsShowRejected = GUILayout.Toggle(mIsShowRejected, "却下も表示", EditorStyles.toolbarButton, GUILayout.Width(90f));
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField($"記録 {PPPartyAIDebugStore.Reports.Count} 件", GUILayout.Width(80f));
            EditorGUILayout.EndHorizontal();
        }

        // 履歴を遡るための選択欄を描画する
        // aCount : 記録の件数
        private void DrawHistorySelector(int aCount)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("履歴", GUILayout.Width(30f));

            using (new EditorGUI.DisabledScope(mSelectedIndex >= aCount - 1))
            {
                if (GUILayout.Button("← 古い", GUILayout.Width(60f))) mSelectedIndex++;
            }
            using (new EditorGUI.DisabledScope(mSelectedIndex <= 0))
            {
                if (GUILayout.Button("新しい →", GUILayout.Width(70f))) mSelectedIndex--;
            }

            EditorGUILayout.LabelField($"{mSelectedIndex + 1} / {aCount}", GUILayout.Width(60f));
            EditorGUILayout.EndHorizontal();
        }

        // 思考記録 1 件分を描画する
        // aReport : 表示する記録
        private void DrawReport(PPPartyAIThinkReport aReport)
        {
            EditorGUILayout.LabelField("思考", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("陣営", aReport.Side.ToString());
            EditorGUILayout.LabelField("ターン", aReport.TurnCount.ToString());
            EditorGUILayout.LabelField("時刻", $"{aReport.Timestamp:0.00}");

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("状況と作戦", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("適用ルール", aReport.ResolvedRules);
            EditorGUILayout.LabelField("作戦", aReport.DoctrineSummary);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("予算", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(aReport.BudgetSummary);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"行動候補（採用 {aReport.AdoptedCount} 件 / 全 {aReport.Candidates.Count} 件）", EditorStyles.boldLabel);
            DrawCandidateHeader();

            foreach (var entry in aReport.Candidates)
            {
                if (!entry.IsAdopted && !mIsShowRejected)
                    continue;

                DrawCandidate(entry);
            }
        }

        // 候補一覧のヘッダ行を描画する
        private static void DrawCandidateHeader()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            EditorGUILayout.LabelField("ユニット", EditorStyles.miniBoldLabel, GUILayout.Width(90f));
            EditorGUILayout.LabelField("行動", EditorStyles.miniBoldLabel, GUILayout.Width(110f));
            EditorGUILayout.LabelField("対象", EditorStyles.miniBoldLabel, GUILayout.Width(90f));
            EditorGUILayout.LabelField("効用", EditorStyles.miniBoldLabel, GUILayout.Width(55f));
            EditorGUILayout.LabelField("コスト", EditorStyles.miniBoldLabel, GUILayout.Width(50f));
            EditorGUILayout.LabelField("λ×コスト", EditorStyles.miniBoldLabel, GUILayout.Width(65f));
            EditorGUILayout.LabelField("結果", EditorStyles.miniBoldLabel);
            EditorGUILayout.EndHorizontal();
        }

        // 候補 1 件を描画する
        // aEntry : 表示する候補
        private static void DrawCandidate(PPPartyAIThinkCandidateEntry aEntry)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(aEntry.UnitName, GUILayout.Width(90f));
            EditorGUILayout.LabelField(aEntry.ActionName, GUILayout.Width(110f));
            EditorGUILayout.LabelField(aEntry.TargetName, GUILayout.Width(90f));
            EditorGUILayout.LabelField($"{aEntry.Utility:0.###}", GUILayout.Width(55f));
            EditorGUILayout.LabelField($"{aEntry.CostTotal:0.#}", GUILayout.Width(50f));
            EditorGUILayout.LabelField($"{aEntry.LambdaCost:0.###}", GUILayout.Width(65f));

            var prevColor = GUI.color;
            GUI.color = aEntry.IsAdopted ? Color.green : prevColor;
            EditorGUILayout.LabelField(BuildResultText(aEntry));
            GUI.color = prevColor;

            EditorGUILayout.EndHorizontal();
        }

        // 候補の採否を日本語表記へ変換する
        // 本命が落ちた後に採用された候補はフォールバックである旨を添える
        // aEntry : 表示する候補
        // return : 表示用の文字列
        private static string BuildResultText(PPPartyAIThinkCandidateEntry aEntry)
        {
            if (!aEntry.IsAdopted)
                return BuildRejectText(aEntry.RejectReason);

            return aEntry.IsFallback ? "採用(フォールバック)" : "採用";
        }

        // 却下理由を日本語表記へ変換する
        // aReason : 却下理由
        // return : 表示用の文字列
        private static string BuildRejectText(PPActionRejectReason aReason)
            => aReason switch
            {
                PPActionRejectReason.BelowLambda => "却下：撃つ価値なし(λ未達)",
                PPActionRejectReason.NotEnoughBudget => "却下：予算不足",
                PPActionRejectReason.ActionLimit => "却下：行動数上限",
                PPActionRejectReason.UnitAlreadyActed => "却下：同ユニットが行動済み",
                PPActionRejectReason.NoEffect => "却下：効果なし",
                _ => "-",
            };
    }
}
