/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPSceneTransition.cs
 * @author hqrse
 * @date 2026/11/22
 * @brief ロード画面を出しながらシーンを切り替える共通の仕組み
 * =====================================*/

using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using CustomConsole;

namespace PPCore
{
    // シーンを非同期で読み込み、その間ロード画面を出す。シーンをまたいで残る 1 つを、最初の切り替えで作る
    public class PPSceneTransition : MonoBehaviour
    {
        /* 定数 */

        private const string LogTag = "Scene";
        // LoadSceneAsync の progress は読み込みが終わると 0.9 で止まるため、ここを 1 とみなす
        private const float LoadedProgress = 0.9f;

        /* static 変数 */

        // 切り替えを受け持つインスタンス。null なら、まだ一度も切り替えていない
        private static PPSceneTransition sInstance;

        /* 状態 */

        // 設定にロード画面が無いときは null
        private PPLoadingScreenView mLoadingView;

        /* プロパティ */

        // 切り替えの途中なら true
        public static bool IsLoading { get; private set; }

        /* static 関数 */

        // シーンを切り替える。切り替えの途中か、ビルドに登録されていないシーンなら受け付けずに false
        public static bool Load(string aSceneName)
        {
            if (IsLoading)
            {
                CustomConsoleLog.Warning(LogTag, $"シーンを切り替えている途中のため、{aSceneName} への切り替えを受け付けません");
                return false;
            }
            if (!Application.CanStreamedLevelBeLoaded(aSceneName))
            {
                CustomConsoleLog.Error(LogTag, $"シーン {aSceneName} を読み込めません。名前が合っているか、Build Profiles のシーン一覧に登録されているかを確かめてください");
                return false;
            }

            IsLoading = true;
            var instance = EnsureInstance();
            instance.StartCoroutine(instance.LoadRoutine(aSceneName));
            return true;
        }

        /* 操作 */

        // ロード画面を出してシーンを読み込み、切り替わったら消す
        private IEnumerator LoadRoutine(string aSceneName)
        {
            CustomConsoleLog.Log(LogTag, $"シーン切り替え: {SceneManager.GetActiveScene().name} → {aSceneName}");
            if (mLoadingView != null) mLoadingView.Show();

            var operation = SceneManager.LoadSceneAsync(aSceneName);
            while (!operation.isDone)
            {
                if (mLoadingView != null) mLoadingView.SetProgress(operation.progress / LoadedProgress);
                yield return null;
            }

            if (mLoadingView != null) mLoadingView.Hide();
            IsLoading = false;
        }

        /* ヘルパー */

        // シーンをまたいで残るインスタンスと、設定にあるロード画面を作る
        private static PPSceneTransition EnsureInstance()
        {
            if (sInstance != null) return sInstance;

            var host = new GameObject(nameof(PPSceneTransition));
            DontDestroyOnLoad(host);
            sInstance = host.AddComponent<PPSceneTransition>();

            var prefab = PPRuntimeSettings.Current.SceneLoadingScreen;
            if (prefab == null)
            {
                CustomConsoleLog.Warning(LogTag, "PPRuntimeSettings の「シーン切り替えのロード画面」が未設定のため、ロード画面を出さずに切り替えます");
                return sInstance;
            }

            var screen = Instantiate(prefab, host.transform);
            sInstance.mLoadingView = screen.GetComponentInChildren<PPLoadingScreenView>(true);
            if (sInstance.mLoadingView == null)
            {
                CustomConsoleLog.Error(LogTag, $"ロード画面のプレハブ {prefab.name} に PPLoadingScreenView がありません。ロード画面を出さずに切り替えます");
                return sInstance;
            }
            sInstance.mLoadingView.Hide();
            return sInstance;
        }

        // Play を始めるたびに戻す。ドメインの再読み込みを切っているため、前回の Play の状態が残るのを防ぐ
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            sInstance = null;
            IsLoading = false;
        }
    }
}
