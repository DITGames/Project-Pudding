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

    // 味方を並べ(最大 PPPartyFactory.MaxMembers 体)、ステージ定義を選んで「バトル開始」でその内容の S_Battle を Play する
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

        // 味方を 1 行ずつ、ユニットとレベルで並べる。行を足す・外すボタンも出す。ユニットが空の行は使わない
        private void DrawAllyParty()
        {
            var allies = mSettings.Allies;
            EditorGUILayout.LabelField($"味方パーティ({allies.Count}/{PPPartyFactory.MaxMembers})", EditorStyles.boldLabel);
            int removeIndex = -1;
            for (int i = 0; i < allies.Count; i++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField($"{i + 1}", GUILayout.Width(24f));
                    var unit = LoadByGuid<PPUnitDefinition>(allies[i].UnitGuid);
                    unit = (PPUnitDefinition)EditorGUILayout.ObjectField(unit, typeof(PPUnitDefinition), false);
                    allies[i].UnitGuid = GetGuid(unit);
                    EditorGUILayout.LabelField("Lv", GUILayout.Width(20f));
                    allies[i].Level = Mathf.Max(1, EditorGUILayout.IntField(allies[i].Level, GUILayout.Width(40f)));
                    if (GUILayout.Button("外す", GUILayout.Width(44f))) removeIndex = i;
                }
            }
            if (removeIndex >= 0) allies.RemoveAt(removeIndex);

            using (new EditorGUI.DisabledScope(allies.Count >= PPPartyFactory.MaxMembers))
            {
                if (GUILayout.Button("味方を足す")) allies.Add(new PPDebugBattleMember());
            }
        }

        // 戦うステージ定義と、マスの深さの代わりに割り振るレベルを選ぶ
        private void DrawStage()
        {
            EditorGUILayout.LabelField("敵", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            var stage = LoadByGuid<PPEncounterDefinition>(mSettings.StageGuid);
            stage = (PPEncounterDefinition)EditorGUILayout.ObjectField("ステージ定義", stage, typeof(PPEncounterDefinition), false);
            mSettings.StageGuid = GetGuid(stage);
            mSettings.LevelPoints = Mathf.Max(0, EditorGUILayout.IntField(new GUIContent("割り振るレベル", "マップでは (基底レベル - 1) + 深さごとに足すレベル を四捨五入した値。出てくる敵の種類へ 1 レベルずつ割り振る"), mSettings.LevelPoints));
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

            bool hasAlly = mSettings.Allies.Exists(m => !string.IsNullOrEmpty(m.UnitGuid));
            if (!hasAlly) EditorGUILayout.HelpBox("味方を 1 体以上置いてください", MessageType.Info);
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
