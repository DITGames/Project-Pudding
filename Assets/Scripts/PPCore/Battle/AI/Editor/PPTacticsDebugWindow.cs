/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPTacticsDebugWindow.cs
 * @author hqrse
 * @date 2026/08/11
 * @brief 戦術AIの思考内容を確認するデバッグウィンドウ
 * =====================================*/

using System.Collections.Generic;
using CommandBattleCore;
using UnityEditor;
using UnityEngine;

namespace PPCore
{
    // 戦術 AI の思考内容を 1 回分ずつ確認するウィンドウ
    // 「成立した戦術は何か」「なぜ他の戦術は動かなかったか」「メイン戦術がどこまで進んだか」を
    // 1 画面で追えるようにしてある
    // 戦術は進行状況を持つため、ログを 1 行ずつ追うだけでは挙動を掴みにくい
    public sealed class PPTacticsDebugWindow : EditorWindow
    {
        // 絞り込み設定の保存キー
        // Play のたびにドメインリロードが走ってウィンドウの状態が初期化されるため、
        // 毎回設定し直さずに済むよう EditorPrefs へ逃がしている
        private const string PrefKeyFollowLatest = "PPCore.PPTacticsDebugWindow.FollowLatest";
        private const string PrefKeyShowRejected = "PPCore.PPTacticsDebugWindow.ShowRejected";
        private const string PrefKeySideFilter = "PPCore.PPTacticsDebugWindow.SideFilter";

        // 陣営フィルタの選択肢。値は mSideFilterIndex と対応する
        private static readonly string[] sSideFilterOptions = { "両陣営", "味方", "敵" };

        // 表示中の記録の位置。-1 なら最新を追いかける
        // 記録は Play のたびに破棄されるため、この位置は保存しない
        private int mSelectedIndex = -1;
        // 記録が増えたら自動で最新へ送るか
        private bool mIsFollowLatest = true;
        // 表示する陣営の絞り込み。0=両陣営 / 1=味方 / 2=敵
        // BattleSide? は Nullable のためそのままでは保存できず、選択肢の添字で持つ
        private int mSideFilterIndex;
        // 不成立だった戦術も表示するか
        private bool mIsShowRejected = true;

        private Vector2 mListScroll;
        private Vector2 mDetailScroll;

        [MenuItem("Window/Tactics AI Debug")]
        private static void Open() => GetWindow<PPTacticsDebugWindow>("Tactics AI");

        private void OnEnable()
        {
            PPTacticsDebugStore.OnAdded += Repaint;
            LoadPreferences();
        }

        private void OnDisable()
        {
            PPTacticsDebugStore.OnAdded -= Repaint;
            SavePreferences();
        }

        // 保存済みの絞り込み設定を読み込む。未保存なら既定値のまま
        private void LoadPreferences()
        {
            mIsFollowLatest = EditorPrefs.GetBool(PrefKeyFollowLatest, mIsFollowLatest);
            mIsShowRejected = EditorPrefs.GetBool(PrefKeyShowRejected, mIsShowRejected);
            mSideFilterIndex = Mathf.Clamp(EditorPrefs.GetInt(PrefKeySideFilter, mSideFilterIndex),
                0, sSideFilterOptions.Length - 1);
        }

        // 絞り込み設定を保存する
        // ウィンドウを閉じたときだけでなく変更時にも呼ぶ。
        // ドメインリロードでは OnDisable が呼ばれないことがあり、閉じるまで保存しないと取りこぼす
        private void SavePreferences()
        {
            EditorPrefs.SetBool(PrefKeyFollowLatest, mIsFollowLatest);
            EditorPrefs.SetBool(PrefKeyShowRejected, mIsShowRejected);
            EditorPrefs.SetInt(PrefKeySideFilter, mSideFilterIndex);
        }

        private void OnGUI()
        {
            DrawToolbar();

            var reports = PPTacticsDebugStore.Reports;
            if (reports.Count == 0)
            {
                EditorGUILayout.HelpBox("思考記録がありません。Play して AI を動かすと記録されます。", MessageType.Info);
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawReportList(reports);
                DrawDetail(ResolveSelected(reports));
            }
        }

        // 上部のツールバーを描画する
        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                // 変更をその場で保存する。次の Play でドメインリロードが走っても設定が残るようにする
                EditorGUI.BeginChangeCheck();

                mIsFollowLatest = GUILayout.Toggle(mIsFollowLatest, "最新を追う", EditorStyles.toolbarButton, GUILayout.Width(80f));
                mIsShowRejected = GUILayout.Toggle(mIsShowRejected, "不成立も表示", EditorStyles.toolbarButton, GUILayout.Width(90f));
                mSideFilterIndex = EditorGUILayout.Popup(mSideFilterIndex, sSideFilterOptions,
                    EditorStyles.toolbarPopup, GUILayout.Width(70f));

                if (EditorGUI.EndChangeCheck())
                {
                    SavePreferences();
                }

                GUILayout.FlexibleSpace();
                if (GUILayout.Button("クリア", EditorStyles.toolbarButton, GUILayout.Width(60f)))
                {
                    PPTacticsDebugStore.Clear();
                    mSelectedIndex = -1;
                }
            }
        }

        // 左側の記録一覧を描画する
        // aReports : ためられている記録
        private void DrawReportList(IReadOnlyList<PPTacticsThinkReport> aReports)
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(240f)))
            {
                mListScroll = EditorGUILayout.BeginScrollView(mListScroll);
                for (int i = 0; i < aReports.Count; i++)
                {
                    var report = aReports[i];
                    if (!IsVisibleSide(report.Side)) continue;

                    bool isSelected = i == ResolveSelectedIndex(aReports);
                    string label = $"[{report.TurnCount}T {report.Timestamp:F1}s] {SideLabel(report.Side)} {report.MainTacticsName}";
                    if (GUILayout.Toggle(isSelected, label, EditorStyles.miniButton) && !isSelected)
                    {
                        mSelectedIndex = i;
                        mIsFollowLatest = false;
                    }
                }
                EditorGUILayout.EndScrollView();
            }
        }

        // 右側の詳細を描画する
        // aReport : 表示する記録
        private void DrawDetail(PPTacticsThinkReport aReport)
        {
            using (new EditorGUILayout.VerticalScope())
            {
                if (aReport == null)
                {
                    EditorGUILayout.LabelField("記録を選択してください");
                    return;
                }

                mDetailScroll = EditorGUILayout.BeginScrollView(mDetailScroll);

                EditorGUILayout.LabelField("思考", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("陣営", SideLabel(aReport.Side));
                EditorGUILayout.LabelField("経過ターン", aReport.TurnCount.ToString());
                EditorGUILayout.LabelField("メイン戦術", $"{aReport.MainTacticsName}（{aReport.MainSelectReason}）");
                EditorGUILayout.LabelField("採用行動数", aReport.AdoptedCount.ToString());
                EditorGUILayout.LabelField("リソース平均増加量", $"{aReport.AverageGainPerTick:F2} / ティック");

                EditorGUILayout.Space();
                EditorGUILayout.LabelField("戦術（優先度順）", EditorStyles.boldLabel);
                foreach (var entry in aReport.Tactics)
                {
                    if (!mIsShowRejected && !entry.IsExecutable) continue;

                    DrawTacticsEntry(entry);
                }

                EditorGUILayout.EndScrollView();
            }
        }

        // 戦術 1 件分の判定結果を描画する
        // aEntry : 描画する判定結果
        private static void DrawTacticsEntry(PPTacticsThinkEntry aEntry)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField($"[{aEntry.Priority}] {aEntry.TacticsName}", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField(aEntry.IsExecutable ? "実行可能" : RejectLabel(aEntry.RejectReason),
                        GUILayout.Width(140f));
                }

                EditorGUILayout.LabelField("進行", $"{aEntry.StepIndex} / {aEntry.StepCount}");
                if (aEntry.IsExecutable)
                {
                    EditorGUILayout.LabelField("実行者 → 行動 → 対象",
                        $"{aEntry.ActorName} → {aEntry.ActionName} → {aEntry.TargetName}");
                    EditorGUILayout.LabelField("今すぐ実行", aEntry.IsAffordableNow ? "可" : $"不可（{aEntry.EstimatedWaitTicks:F1} ティック待ち）");
                }
                if (aEntry.RemainingCooldown > 0)
                {
                    EditorGUILayout.LabelField("残クールタイム", $"{aEntry.RemainingCooldown} ティック");
                }
            }
        }

        // 表示対象の記録を解決する
        // aReports : ためられている記録
        // return : 表示する記録。1 件も無ければ null
        private PPTacticsThinkReport ResolveSelected(IReadOnlyList<PPTacticsThinkReport> aReports)
        {
            int index = ResolveSelectedIndex(aReports);
            return index >= 0 && index < aReports.Count ? aReports[index] : null;
        }

        // 表示対象の位置を解決する。最新追従中は絞り込みに合う最後の記録を指す
        // aReports : ためられている記録
        // return : 表示する記録の位置。該当が無ければ -1
        private int ResolveSelectedIndex(IReadOnlyList<PPTacticsThinkReport> aReports)
        {
            if (!mIsFollowLatest && mSelectedIndex >= 0 && mSelectedIndex < aReports.Count)
                return mSelectedIndex;

            for (int i = aReports.Count - 1; i >= 0; i--)
            {
                if (IsVisibleSide(aReports[i].Side)) return i;
            }
            return -1;
        }

        // 現在の絞り込みでその陣営を表示するか
        // aSide : 判定する陣営
        // return : 表示する場合 true
        private bool IsVisibleSide(BattleSide aSide)
            => mSideFilterIndex switch
            {
                1 => aSide == BattleSide.Ally,
                2 => aSide == BattleSide.Enemy,
                _ => true,
            };

        // 陣営を日本語表記へ変換する
        // aSide : 対象の陣営
        // return : 日本語の表記
        private static string SideLabel(BattleSide aSide) => aSide == BattleSide.Ally ? "味方" : "敵";

        // 不成立の理由を日本語表記へ変換する
        // aReason : 不成立の理由
        // return : 日本語の表記
        private static string RejectLabel(PPTacticRejectReason aReason)
            => aReason switch
            {
                PPTacticRejectReason.DoneOnce => "1回のみ消化済み",
                PPTacticRejectReason.Cooldown => "クールタイム中",
                PPTacticRejectReason.ConditionFailed => "条件不成立",
                PPTacticRejectReason.NoSteps => "ステップなし",
                PPTacticRejectReason.NoActor => "実行者なし",
                PPTacticRejectReason.NoSkill => "使えるスキルなし",
                PPTacticRejectReason.NoTarget => "対象なし",
                PPTacticRejectReason.NoIncome => "収入見込みなし",
                PPTacticRejectReason.TooFarToWait => "待ちが長すぎる",
                _ => "-",
            };
    }
}
