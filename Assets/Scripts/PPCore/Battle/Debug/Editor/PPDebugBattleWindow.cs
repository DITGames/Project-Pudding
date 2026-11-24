/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPDebugBattleWindow.cs
 * @author hqrse
 * @date 2026/11/22
 * @brief 味方パーティを組み、ステージ定義を読み込んで S_Battle を始めるデバッグバトルのウィンドウ
 * =====================================*/

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PPCore
{
    // デバッグバトルで変えた Play の開始シーンを、Play を終えたら元に戻す
    // ウィンドウを閉じていても戻すよう、Editor の読み込み時に購読しておく
    [InitializeOnLoad]
    public static class PPDebugBattleSession
    {
        /* 定数 */

        // 変える前の開始シーンのパス。デバッグバトルで Play していなければ未設定
        private const string RestoreKey = "PPCore.DebugBattle.RestoreStartScene";
        // 変える前に開始シーンが無かったことを表す値
        private const string NoStartScene = "-";

        /* 生成 */

        static PPDebugBattleSession()
        {
            EditorApplication.playModeStateChanged += HandlePlayModeChanged;
        }

        /* 操作 */

        // 設定を S_Battle へ渡し、開始シーンを差し替えて Play に入る
        public static void Start(PPDebugBattleSettings aSettings, SceneAsset aBattleScene)
        {
            var previous = EditorSceneManager.playModeStartScene;
            SessionState.SetString(RestoreKey, previous != null ? AssetDatabase.GetAssetPath(previous) : NoStartScene);
            SessionState.SetString(PPDebugBattleLauncher.RequestKey, JsonUtility.ToJson(aSettings));
            EditorSceneManager.playModeStartScene = aBattleScene;
            EditorApplication.EnterPlaymode();
        }

        /* イベントハンドラ */

        // Play を終えたら開始シーンを戻し、使われなかった受け渡しも消す
        private static void HandlePlayModeChanged(PlayModeStateChange aState)
        {
            if (aState != PlayModeStateChange.EnteredEditMode) return;

            SessionState.EraseString(PPDebugBattleLauncher.RequestKey);
            string restore = SessionState.GetString(RestoreKey, string.Empty);
            if (string.IsNullOrEmpty(restore)) return;

            SessionState.EraseString(RestoreKey);
            EditorSceneManager.playModeStartScene = restore == NoStartScene ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(restore);
        }
    }

    // 味方の 3 枠を組み、ステージ定義を選んで「バトル開始」でその内容の S_Battle を Play する
    // 設定は ScriptableObject ではなくこの PC だけ(EditorPrefs)に残し、Git の競合を起こさない
    public class PPDebugBattleWindow : EditorWindow
    {
        /* 定数 */

        private const string SettingsPrefKey = "PPCore.DebugBattle.Settings";
        private const string ScenePrefKey = "PPCore.DebugBattle.SceneGuid";
        private const string DefaultScenePath = "Assets/Scenes/Main/S_Battle.unity";

        /* 状態 */

        private PPDebugBattleSettings mSettings;
        private SceneAsset mBattleScene;
        private Vector2 mScroll;

        /* static 関数 */

        [MenuItem("Window/Project-Pudding/デバッグバトル")]
        public static void Open() => GetWindow<PPDebugBattleWindow>("デバッグバトル");

        /* Unity イベント */

        // 前回の設定とシーンを戻す。無ければ空の設定と S_Battle を使う
        private void OnEnable()
        {
            mSettings = LoadSettings();
            mBattleScene = LoadByGuid<SceneAsset>(EditorPrefs.GetString(GetPrefKey(ScenePrefKey), string.Empty))
                ?? AssetDatabase.LoadAssetAtPath<SceneAsset>(DefaultScenePath);
        }

        private void OnGUI()
        {
            mScroll = EditorGUILayout.BeginScrollView(mScroll);
            EditorGUI.BeginChangeCheck();
            DrawScene();
            EditorGUILayout.Space();
            DrawAllyParty();
            EditorGUILayout.Space();
            DrawStage();
            if (EditorGUI.EndChangeCheck()) SaveSettings();
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space();
            DrawStartButton();
        }

        /* 描画 */

        private void DrawScene()
        {
            mBattleScene = (SceneAsset)EditorGUILayout.ObjectField("バトルのシーン", mBattleScene, typeof(SceneAsset), false);
        }

        // 中央・右・左の枠ごとに、ユニットとレベルを選ぶ。ユニットが空の枠には置かない
        private void DrawAllyParty()
        {
            EditorGUILayout.LabelField("味方パーティ", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            for (int i = 0; i < PPDebugBattleSettings.Positions.Length; i++)
            {
                EditorGUILayout.LabelField(GetPositionName(PPDebugBattleSettings.Positions[i]));
                EditorGUI.indentLevel++;
                var unit = LoadByGuid<PPUnitDefinition>(mSettings.AllyUnitGuids[i]);
                unit = (PPUnitDefinition)EditorGUILayout.ObjectField("ユニット", unit, typeof(PPUnitDefinition), false);
                mSettings.AllyUnitGuids[i] = GetGuid(unit);
                mSettings.AllyLevels[i] = Mathf.Max(1, EditorGUILayout.IntField("レベル", mSettings.AllyLevels[i]));
                EditorGUI.indentLevel--;
            }
            EditorGUI.indentLevel--;
        }

        // 戦うステージ定義と、マスの深さの代わりに割り振るレベルを選ぶ
        private void DrawStage()
        {
            EditorGUILayout.LabelField("敵", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            var stage = LoadByGuid<PPEncounterDefinition>(mSettings.StageGuid);
            stage = (PPEncounterDefinition)EditorGUILayout.ObjectField("ステージ定義", stage, typeof(PPEncounterDefinition), false);
            mSettings.StageGuid = GetGuid(stage);
            mSettings.LevelPoints = Mathf.Max(0, EditorGUILayout.IntField(new GUIContent("割り振るレベル", "マップでは (基底レベル - 1) + (深さ - 1)。出てくる敵の種類へ 1 レベルずつ割り振る"), mSettings.LevelPoints));
            EditorGUI.indentLevel--;
        }

        // Play していなければ「バトル開始」、Play 中なら「停止」を出す
        private void DrawStartButton()
        {
            if (EditorApplication.isPlaying)
            {
                if (GUILayout.Button("停止", GUILayout.Height(32f))) EditorApplication.ExitPlaymode();
                return;
            }

            bool hasAlly = !string.IsNullOrEmpty(mSettings.AllyUnitGuids[0]);
            if (!hasAlly) EditorGUILayout.HelpBox("味方の中央にユニットを置いてください", MessageType.Info);
            if (string.IsNullOrEmpty(mSettings.StageGuid)) EditorGUILayout.HelpBox("ステージ定義を選んでください", MessageType.Info);
            using (new EditorGUI.DisabledScope(!hasAlly || string.IsNullOrEmpty(mSettings.StageGuid) || mBattleScene == null || EditorApplication.isCompiling))
            {
                if (GUILayout.Button("バトル開始", GUILayout.Height(32f)))
                {
                    SaveSettings();
                    PPDebugBattleSession.Start(mSettings, mBattleScene);
                }
            }
        }

        /* ヘルパー */

        // 別のプロジェクトの設定と混ざらないよう、キーにプロジェクトの GUID を付ける
        private static string GetPrefKey(string aKey) => aKey + "." + PlayerSettings.productGUID;

        private static PPDebugBattleSettings LoadSettings()
        {
            string json = EditorPrefs.GetString(GetPrefKey(SettingsPrefKey), string.Empty);
            var settings = string.IsNullOrEmpty(json) ? new PPDebugBattleSettings() : JsonUtility.FromJson<PPDebugBattleSettings>(json);
            settings.Normalize();
            return settings;
        }

        private void SaveSettings()
        {
            EditorPrefs.SetString(GetPrefKey(SettingsPrefKey), JsonUtility.ToJson(mSettings));
            EditorPrefs.SetString(GetPrefKey(ScenePrefKey), GetGuid(mBattleScene));
        }

        private static string GetPositionName(PPPartyPosition aPosition) => aPosition switch
        {
            PPPartyPosition.Right => "右",
            PPPartyPosition.Left => "左",
            _ => "中央",
        };

        private static T LoadByGuid<T>(string aGuid) where T : Object
        {
            if (string.IsNullOrEmpty(aGuid)) return null;
            var asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(aGuid));
            // UnityEngine.Object は見つからなくても ?? で null 扱いにならないため、== で判定してから返す
            return asset == null ? null : asset;
        }

        private static string GetGuid(Object aAsset) => aAsset != null ? AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(aAsset)) : string.Empty;
    }
}
