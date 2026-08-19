/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPSkillTagDefinition.cs
 * @author hqrse
 * @date 2026/08/11
 * @brief スキルの分類タグ定義
 * =====================================*/

using AttributeUtility;
using UnityEngine;

namespace PPCore
{
    // スキルを分類するタグ（ScriptableObject）
    // 戦術ステップが「どのスキルを使うか」を指すための目印になる
    // 文字列で持つとタイポでマッチしなくなるため、アセットの参照で持たせている
    // ピッカーのツリー構造はアセットの置き場ではなくカテゴリパスから組み立てるため、
    // フォルダを整理しても見た目の階層は変わらない
    [CreateAssetMenu(fileName = "PPSkillTagDefinition", menuName = "Project-Pudding/AI/PPSkillTagDefinition")]
    public class PPSkillTagDefinition : ScriptableObject
    {
        [Label("タグ名")]
        [SerializeField] private string mTagName = "";
        // "攻撃/範囲" のようなスラッシュ区切りのパス。ピッカーのツリー構造はこの値から組む
        [Label("カテゴリパス")]
        [SerializeField] private string mCategoryPath = "";
        [Label("説明")]
        [SerializeField][Multiline] private string mDescription = "";

        // 表示に使うタグ名。未入力ならアセット名で代用する
        public string TagName => string.IsNullOrEmpty(mTagName) ? name : mTagName;
        public string CategoryPath => mCategoryPath;
        public string Description => mDescription;

        // ツリー上でのフルパス。カテゴリパス未設定のタグは「未分類」へ集める
        public string MenuPath
            => string.IsNullOrEmpty(mCategoryPath) ? $"未分類/{TagName}" : $"{mCategoryPath}/{TagName}";
    }
}
