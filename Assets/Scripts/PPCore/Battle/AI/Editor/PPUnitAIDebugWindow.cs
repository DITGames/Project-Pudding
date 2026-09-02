/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPUnitAIDebugWindow.cs
 * @author hqrse
 * @date 2026/08/24
 * @brief ユニットAIの思考内容を確認するデバッグウィンドウ
 * =====================================*/

using System.Collections.Generic;
using CommandBattleCore;
using UnityEditor;
using UnityEngine;

namespace PPCore
{
    // ユニット AI の思考内容を 1 回分ずつ確認するウィンドウ
    // 「そのユニットが何を選んだか」「なぜ撃たなかったか」「ゲージがどこまで溜まっているか」を
    // 1 画面で追えるようにしてある
    // 待機はログに現れにくく、ゲージ残量と併せて見ないと意図通りか判断できないため
    public sealed class PPUnitAIDebugWindow : EditorWindow
    {
        // 絞り込み設定の保存キー
        // Play のたびにドメインリロードが走ってウィンドウの状態が初期化されるため、
        // 毎回設定し直さずに済むよう EditorPrefs へ逃がしている
        private const string PrefKeyFollowLatest = "PPCore.PPUnitAIDebugWindow.FollowLatest";
        private const string PrefKeyShowWaiting = "PPCore.PPUnitAIDebugWindow.ShowWaiting";
        private const string PrefKeySideFilter = "PPCore.PPUnitAIDebugWindow.SideFilter";

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
        // 行動しなかったユニットも表示するか
        private bool mIsShowWaiting = true;

        private Vector2 mListScroll;
        private Vector2 mDetailScroll;

        [MenuItem("Window/Unit AI Debug")]
        private static void Open() => GetWindow<PPUnitAIDebugWindow>("Unit AI");

        private void OnEnable()
        {
            PPUnitAIDebugStore.OnAdded += Repaint;
            LoadPreferences();
        }

        private void OnDisable()
        {
            PPUnitAIDebugStore.OnAdded -= Repaint;
            SavePreferences();
        }

        // 保存済みの絞り込み設定を読み込む。未保存なら既定値のまま
        private void LoadPreferences()
        {
            mIsFollowLatest = EditorPrefs.GetBool(PrefKeyFollowLatest, mIsFollowLatest);
            mIsShowWaiting = EditorPrefs.GetBool(PrefKeyShowWaiting, mIsShowWaiting);
            mSideFilterIndex = Mathf.Clamp(EditorPrefs.GetInt(PrefKeySideFilter, mSideFilterIndex),
                0, sSideFilterOptions.Length - 1);
        }

        // 絞り込み設定を保存する
        // ウィンドウを閉じたときだけでなく変更時にも呼ぶ。
        // ドメインリロードでは OnDisable が呼ばれないことがあり、閉じるまで保存しないと取りこぼす
        private void SavePreferences()
        {
            EditorPrefs.SetBool(PrefKeyFollowLatest, mIsFollowLatest);
            EditorPrefs.SetBool(PrefKeyShowWaiting, mIsShowWaiting);
            EditorPrefs.SetInt(PrefKeySideFilter, mSideFilterIndex);
        }

        private void OnGUI()
        {
            DrawToolbar();

            var reports = PPUnitAIDebugStore.Reports;
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
                mIsShowWaiting = GUILayout.Toggle(mIsShowWaiting, "待機も表示", EditorStyles.toolbarButton, GUILayout.Width(90f));
                mSideFilterIndex = EditorGUILayout.Popup(mSideFilterIndex, sSideFilterOptions,
                    EditorStyles.toolbarPopup, GUILayout.Width(70f));

                if (EditorGUI.EndChangeCheck())
                {
                    SavePreferences();
                }

                GUILayout.FlexibleSpace();
                if (GUILayout.Button("クリア", EditorStyles.toolbarButton, GUILayout.Width(60f)))
                {
                    PPUnitAIDebugStore.Clear();
                    mSelectedIndex = -1;
                }
            }
        }

        // 左側の記録一覧を描画する
        // aReports : ためられている記録
        private void DrawReportList(IReadOnlyList<PPUnitAIThinkReport> aReports)
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(240f)))
            {
                mListScroll = EditorGUILayout.BeginScrollView(mListScroll);
                for (int i = 0; i < aReports.Count; i++)
                {
                    var report = aReports[i];
                    if (!IsVisibleSide(report.Side)) continue;

                    bool isSelected = i == ResolveSelectedIndex(aReports);
                    string label = $"[{report.TurnCount}T {report.Timestamp:F1}s] {SideLabel(report.Side)} 採用{report.AdoptedCount}件";
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
        private void DrawDetail(PPUnitAIThinkReport aReport)
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
                EditorGUILayout.LabelField("採用行動数", aReport.AdoptedCount.ToString());

                EditorGUILayout.Space();
                EditorGUILayout.LabelField("ユニット（編成順）", EditorStyles.boldLabel);
                foreach (var entry in aReport.Units)
                {
                    if (!mIsShowWaiting && entry.Decision == PPUnitAIDecision.Wait) continue;

                    DrawUnitEntry(entry);
                }

                EditorGUILayout.EndScrollView();
            }
        }

        // ユニット 1 体分の判断結果を描画する
        // 行を押すとツリーウィンドウ側でその経路が強調表示される
        // aEntry : 描画する判断結果
        private void DrawUnitEntry(PPUnitAIThinkEntry aEntry)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    bool isSelected = ReferenceEquals(PPUnitAITreeHighlightHub.Selected, aEntry);
                    // 行動回数が複数のユニットは何手目かを添えて区別する
                    string unitLabel = aEntry.ActionIndex > 0
                        ? $"{aEntry.UnitName}（{aEntry.ActionIndex + 1}手目）"
                        : aEntry.UnitName;
                    if (GUILayout.Toggle(isSelected, unitLabel, EditorStyles.miniButton, GUILayout.Width(160f))
                        && !isSelected)
                    {
                        PPUnitAITreeHighlightHub.Select(aEntry);
                        OpenTreeWindow(aEntry.Profile);
                    }
                    EditorGUILayout.LabelField(DecisionLabel(aEntry.Decision), GUILayout.Width(80f));
                    EditorGUILayout.LabelField(aEntry.Decision == PPUnitAIDecision.Wait
                        ? RejectLabel(aEntry.RejectReason) : "-", GUILayout.Width(140f));
                }

                EditorGUILayout.LabelField("行動 → 対象", $"{aEntry.ActionName} → {aEntry.TargetName}");
                EditorGUILayout.LabelField("スキルゲージ", $"{aEntry.SkillGauge:F1} / {aEntry.SkillGaugeMax:F1}");
                EditorGUILayout.LabelField("コインゲージ", $"{aEntry.CoinGauge:F1} / {aEntry.CoinGaugeMax:F1}");

                // 維持中は「今の判断がいつまで固定されるか」がわからないと待ちの是非を判断できない
                if (aEntry.CommitRemainingTicks > 0)
                {
                    EditorGUILayout.LabelField("判断の維持", $"あと {aEntry.CommitRemainingTicks} ティック");
                }
            }
        }

        // 選んだ思考記録のツリーをツリーウィンドウで開く
        // 経路のハイライトだけでは今どのツリーの話か分かりにくいため、選んだ時点で該当のツリーへ切り替える
        // aProfile : 開く判断ツリー。null なら何もしない
        private static void OpenTreeWindow(PPUnitAIProfileDefinition aProfile)
        {
            if (aProfile == null) return;

            var window = GetWindow<PPUnitAITreeWindow>();
            window.titleContent = new GUIContent("Unit AI Tree");
            window.SetTarget(aProfile);
            window.Show();
            window.Focus();
        }

        // 表示対象の記録を解決する
        // aReports : ためられている記録
        // return : 表示する記録。1 件も無ければ null
        private PPUnitAIThinkReport ResolveSelected(IReadOnlyList<PPUnitAIThinkReport> aReports)
        {
            int index = ResolveSelectedIndex(aReports);
            return index >= 0 && index < aReports.Count ? aReports[index] : null;
        }

        // 表示対象の位置を解決する。最新追従中は絞り込みに合う最後の記録を指す
        // aReports : ためられている記録
        // return : 表示する記録の位置。該当が無ければ -1
        private int ResolveSelectedIndex(IReadOnlyList<PPUnitAIThinkReport> aReports)
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

        // 選んだ行動を日本語表記へ変換する
        // aDecision : 選んだ行動
        // return : 日本語の表記
        private static string DecisionLabel(PPUnitAIDecision aDecision)
            => aDecision switch
            {
                PPUnitAIDecision.Skill => "スキル",
                PPUnitAIDecision.NormalAttack => "通常攻撃",
                _ => "待機",
            };

        // 行動しなかった理由を日本語表記へ変換する
        // aReason : 行動しなかった理由
        // return : 日本語の表記
        private static string RejectLabel(PPUnitAIRejectReason aReason)
            => aReason switch
            {
                PPUnitAIRejectReason.NoProfile => "AIプロファイル未設定",
                PPUnitAIRejectReason.NoActionBudget => "行動回数なし",
                PPUnitAIRejectReason.NoMatchedNode => "成立した枝なし",
                PPUnitAIRejectReason.DecidedToWait => "意図的な待機",
                _ => "-",
            };
    }
}
