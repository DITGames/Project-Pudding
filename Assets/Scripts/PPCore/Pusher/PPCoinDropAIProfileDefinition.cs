/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPCoinDropAIProfileDefinition.cs
 * @author hqrse
 * @date 2026/11/22
 * @brief 型と段階の既定値に無い、個別のコイン投入 AI のプロファイル
 * =====================================*/

using UnityEngine;
using AttributeUtility;

namespace PPCore
{
    // パーティ定義の投入 AI に「カスタムのプロファイル」として指定する、個別の考え方の数値
    [CreateAssetMenu(fileName = "PCDAP_New", menuName = "Project-Pudding/AI/PPCoinDropAIProfileDefinition")]
    public class PPCoinDropAIProfileDefinition : ScriptableObject
    {
        [Label("プロファイル")]
        [SerializeField] private PPCoinDropAIProfile mProfile = new();

        public PPCoinDropAIProfile Profile => mProfile;
    }
}
