/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPDebugBattleWindow.cs
 * @author hqrse
 * @date 2026/11/22
 * @brief デバッグバトルの設定を編集し、その設定で S_Battle を始めるウィンドウ
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
        public static void Start(PPDebugBattleDefinition aDefinition, SceneAsset aBattleScene)
        {
            var previous = EditorSceneManager.playModeStartScene;
            SessionState.SetString(RestoreKey, previous != null ? AssetDatabase.GetAssetPath(previous) : NoStartScene);
            SessionState.SetString(PPDebugBattleLauncher.RequestKey, AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(aDefinition)));
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

    // デバッグバトルの設定アセットをその場で編集し、「バトル開始」でその設定の S_Battle を Play する
    // 設定アセットに項目を足せば、このウィンドウにもそのまま並ぶ
    public class PPDebugBattleWindow : EditorWindow
    {
        /* 定数 */

        private const string DefinitionPrefKey = "PPCore.DebugBattle.DefinitionGuid";
        private const string ScenePrefKey = "PPCore.DebugBattle.SceneGuid";
        private const string DefaultScenePath = "Assets/Scenes/Main/S_Battle.unity";
        private const string NewAssetPath = "Assets/GameData/Debug/PPDebugBattle.asset";

        /* 状態 */

        private PPDebugBattleDefinition mDefinition;
        private SceneAsset mBattleScene;
        // 設定アセットのインスペクタ。設定アセットを変えたら作り直す
        private Editor mDefinitionEditor;
        private Vector2 mScroll;

        /* static 関数 */

        [MenuItem("Window/Project-Pudding/デバッグバトル")]
        public static void Open() => GetWindow<PPDebugBattleWindow>("デバッグバトル");

        /* Unity イベント */

        // 前回選んでいた設定アセットとシーンを戻す。無ければ最初に見つかった設定アセットと S_Battle を使う
        private void OnEnable()
        {
            mDefinition = LoadByGuid<PPDebugBattleDefinition>(EditorPrefs.GetString(DefinitionPrefKey, string.Empty)) ?? FindFirstDefinition();
            mBattleScene = LoadByGuid<SceneAsset>(EditorPrefs.GetString(ScenePrefKey, string.Empty))
                ?? AssetDatabase.LoadAssetAtPath<SceneAsset>(DefaultScenePath);
        }

        private void OnDisable()
        {
            if (mDefinitionEditor != null) DestroyImmediate(mDefinitionEditor);
        }

        private void OnGUI()
        {
            DrawSelection();
            EditorGUILayout.Space();
            DrawStartButton();
            EditorGUILayout.Space();
            DrawDefinition();
        }

        /* 描画 */

        // 設定アセットとシーンを選ぶ欄。設定アセットが無ければ作るボタンを出す
        private void DrawSelection()
        {
            EditorGUI.BeginChangeCheck();
            var definition = (PPDebugBattleDefinition)EditorGUILayout.ObjectField("設定", mDefinition, typeof(PPDebugBattleDefinition), false);
            var scene = (SceneAsset)EditorGUILayout.ObjectField("バトルのシーン", mBattleScene, typeof(SceneAsset), false);
            if (EditorGUI.EndChangeCheck())
            {
                mDefinition = definition;
                mBattleScene = scene;
                EditorPrefs.SetString(DefinitionPrefKey, GetGuid(mDefinition));
                EditorPrefs.SetString(ScenePrefKey, GetGuid(mBattleScene));
            }

            if (mDefinition == null && GUILayout.Button("設定アセットを新しく作る")) CreateDefinition();
        }

        // Play していなければ「バトル開始」、Play 中なら「停止」を出す
        private void DrawStartButton()
        {
            if (EditorApplication.isPlaying)
            {
                if (GUILayout.Button("停止", GUILayout.Height(32f))) EditorApplication.ExitPlaymode();
                return;
            }

            using (new EditorGUI.DisabledScope(mDefinition == null || mBattleScene == null || EditorApplication.isCompiling))
            {
                if (GUILayout.Button("バトル開始", GUILayout.Height(32f)))
                {
                    AssetDatabase.SaveAssetIfDirty(mDefinition);
                    PPDebugBattleSession.Start(mDefinition, mBattleScene);
                }
            }
        }

        // 設定アセットのインスペクタをそのまま並べる
        private void DrawDefinition()
        {
            if (mDefinition == null) return;

            Editor.CreateCachedEditor(mDefinition, null, ref mDefinitionEditor);
            mScroll = EditorGUILayout.BeginScrollView(mScroll);
            mDefinitionEditor.OnInspectorGUI();
            EditorGUILayout.EndScrollView();
        }

        /* 操作 */

        private void CreateDefinition()
        {
            string folder = System.IO.Path.GetDirectoryName(NewAssetPath).Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/GameData", "Debug");

            var asset = CreateInstance<PPDebugBattleDefinition>();
            AssetDatabase.CreateAsset(asset, AssetDatabase.GenerateUniqueAssetPath(NewAssetPath));
            AssetDatabase.SaveAssets();
            mDefinition = asset;
            EditorPrefs.SetString(DefinitionPrefKey, GetGuid(mDefinition));
            EditorGUIUtility.PingObject(asset);
        }

        /* ヘルパー */

        private static PPDebugBattleDefinition FindFirstDefinition()
        {
            var guids = AssetDatabase.FindAssets($"t:{nameof(PPDebugBattleDefinition)}");
            return guids.Length > 0 ? LoadByGuid<PPDebugBattleDefinition>(guids[0]) : null;
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
