/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPUnitAIDefaultsDefinition.cs
 * @author hqrse
 * @date 2026/11/06
 * @brief ビヘイビアツリー未設定のユニットが使う既定ツリーの定義
 * =====================================*/

using System;
using System.Collections.Generic;
using AttributeUtility;
using CustomConsole;
using UnityEngine;

namespace PPCore
{
    // ロールと既定ツリーの組 1 つ分
    [Serializable]
    public sealed class PPUnitAIRoleTreeEntry
    {
        /* インスペクタ設定 */

        [Label("ロール")]
        [SerializeField] private PPUnitRole mRole = PPUnitRole.Balance;
        [Label("既定ツリー")]
        [SerializeField] private PPUnitBehaviourTree mTree;

        /* プロパティ */

        public PPUnitRole Role => mRole;
        public PPUnitBehaviourTree Tree => mTree;
    }

    // ビヘイビアツリー未設定のユニットが使う既定ツリーを、ロール別と全体で持つ定義
    // PPRuntimeSettings から参照し、解決順は ResolveTree に集約する
    [CreateAssetMenu(fileName = "PUAD_Default", menuName = "Project-Pudding/AI/PPUnitAIDefaultsDefinition")]
    public class PPUnitAIDefaultsDefinition : ScriptableObject
    {
        /* 定数 */

        private const string LogTag = "AI";

        /* インスペクタ設定 */

        // エントリが無いロールは全体既定ツリーで動く
        [Header("既定ツリー")]
        [Label("ロール別の既定ツリー", true)]
        [SerializeField] protected List<PPUnitAIRoleTreeEntry> mRoleTrees = new();

        [Label("全体既定ツリー")]
        [SerializeField] protected PPUnitBehaviourTree mGlobalTree;

        /* 状態 */

        // ロールから既定ツリーを引くための索引。FindRoleTree の初回アクセス時にだけ組み立てる
        private Dictionary<PPUnitRole, PPUnitBehaviourTree> mRoleTreeMap;
        // エディタで設定の問題を知らせる遅延処理を登録済みか。同じフレームに OnValidate が重なっても 1 回だけ出すために持つ
        [NonSerialized] private bool mIsReportPending;

        /* プロパティ */

        public PPUnitBehaviourTree GlobalTree => mGlobalTree;

        /* static 関数 */

        // ユニットが使うツリーを 個別 → ロール既定 → 全体既定 の順で解決する
        public static PPUnitBehaviourTree ResolveTree(PPUnitDefinition aUnit, PPUnitAIDefaultsDefinition aDefaults,
            out PPUnitAITreeSource aSource)
        {
            if (aUnit != null && aUnit.BehaviourTree != null)
            {
                aSource = PPUnitAITreeSource.Individual;
                return aUnit.BehaviourTree;
            }

            if (aDefaults != null)
            {
                var roleTree = aUnit != null ? aDefaults.FindRoleTree(aUnit.Role) : null;
                if (roleTree != null)
                {
                    aSource = PPUnitAITreeSource.RoleDefault;
                    return roleTree;
                }
                if (aDefaults.GlobalTree != null)
                {
                    aSource = PPUnitAITreeSource.GlobalDefault;
                    return aDefaults.GlobalTree;
                }
            }

            aSource = PPUnitAITreeSource.None;
            return null;
        }

        /* 生成 */

        // ロールから既定ツリーを引く索引を組み立て、設定の問題をログへ出す
        // ロールが重複している場合は先勝ちとし、ツリーが空のエントリは索引に載せない
        private Dictionary<PPUnitRole, PPUnitBehaviourTree> BuildRoleTreeMap()
        {
            var map = new Dictionary<PPUnitRole, PPUnitBehaviourTree>();
            var seenRoles = new HashSet<PPUnitRole>();
            foreach (var entry in mRoleTrees)
            {
                if (entry == null) continue;

                string roleName = PPUnitRoleDefinition.ToDisplayString(entry.Role);
                if (!seenRoles.Add(entry.Role))
                {
                    CustomConsoleLog.Error(LogTag,
                        $"{name}: ロール「{roleName}」のエントリが重複しています。先のエントリだけが使われるため、1 つにまとめてください。", this);
                    continue;
                }
                if (entry.Tree == null)
                {
                    CustomConsoleLog.Error(LogTag,
                        $"{name}: ロール「{roleName}」の既定ツリーが未設定です。ツリーを割り当てるか、エントリを削除してください。", this);
                    continue;
                }
                map[entry.Role] = entry.Tree;
            }

            if (mGlobalTree == null)
            {
                CustomConsoleLog.Warning(LogTag,
                    $"{name}: 全体既定ツリーが未設定です。既定ツリーを割り当てていないロールのユニットは待機します。", this);
            }
            return map;
        }

        /* Unity イベント */

        // アセットの読み込み・インスペクタ編集のたびに索引を捨てる
        // 設定の問題は遅らせて知らせる（読み込み途中は参照先が null に見えて誤検出するため）
        protected virtual void OnValidate()
        {
            mRoleTreeMap = null;
#if UNITY_EDITOR
            if (mIsReportPending) return;

            mIsReportPending = true;
            UnityEditor.EditorApplication.delayCall += ReportIssuesAfterValidate;
#endif
        }

        /* 操作 */

#if UNITY_EDITOR
        // 設定の問題をログへ出す。インポート・コンパイル中は終わるまで次の機会へ回す
        // ここで組み立てた索引は保存しない
        private void ReportIssuesAfterValidate()
        {
            if (this == null) return;

            if (UnityEditor.EditorApplication.isUpdating || UnityEditor.EditorApplication.isCompiling)
            {
                UnityEditor.EditorApplication.delayCall += ReportIssuesAfterValidate;
                return;
            }

            mIsReportPending = false;
            BuildRoleTreeMap();
        }
#endif

        /* 問い合わせ */

        // ロールの既定ツリーを引く
        public PPUnitBehaviourTree FindRoleTree(PPUnitRole aRole)
        {
            mRoleTreeMap ??= BuildRoleTreeMap();
            return mRoleTreeMap.GetValueOrDefault(aRole);
        }
    }
}
