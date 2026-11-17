/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPPusherStageSwitcher.cs
 * @author hqrse
 * @date 2026/11/17
 * @brief プッシャー台のステージの差分を切り替える
 * =====================================*/

using UnityEngine;
using AttributeUtility;

namespace PPCore
{
    // 登録したステージの差分のうち、選んだ 1 つだけを有効にする。編集中もインスペクタの変更をすぐ反映する
    public class PPPusherStageSwitcher : MonoBehaviour
    {
        /* インスペクタ設定 */

        // 切り替え候補のステージ。並び順が「使うステージ」の番号になる
        [Label("ステージの差分", true)]
        [SerializeField] private GameObject[] mStages = new GameObject[0];
        // 使うステージの番号。範囲外ならどのステージも表示しない
        [Label("使うステージ")]
        [SerializeField] private int mStageIndex;

        /* プロパティ */

        public int StageIndex => mStageIndex;

        /* Unity イベント */

        // 実行時もプレハブの保存状態に頼らず、選んだステージだけを有効にする
        private void Awake() => Apply();

#if UNITY_EDITOR
        // OnValidate の中では有効・無効を切り替えられないため、次の更新で反映する
        private void OnValidate()
        {
            UnityEditor.EditorApplication.delayCall += () =>
            {
                // 遅延の間に破棄されることがあるため、Unity の null 判定で確かめる
                if (this == null) return;
                Apply();
            };
        }
#endif

        /* 操作 */

        // 使うステージを切り替える。範囲外の番号を渡すとすべて非表示にする
        public void SetStage(int aIndex)
        {
            mStageIndex = aIndex;
            Apply();
        }

        // 選んだステージだけを有効にし、ほかを無効にする
        private void Apply()
        {
            for (int i = 0; i < mStages.Length; i++)
            {
                if (mStages[i] == null) continue;
                bool isActive = i == mStageIndex;
                if (mStages[i].activeSelf != isActive) mStages[i].SetActive(isActive);
            }
        }
    }
}
