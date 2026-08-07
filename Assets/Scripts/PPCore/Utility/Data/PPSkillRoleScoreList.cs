/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPSkillRoleScoreList.cs
 * @author hqrse
 * @date 2026/08/07
 * @brief スキルのロール別AIスコアを保持するコンテナ
 * =====================================*/

using System.Collections.Generic;
using UnityEngine;

namespace PPCore
{
    // スキルのロール別AIスコアを保持するコンテナ
    // PPSkillDefinition.BattleSkillRole でチェックされているロールの数だけ、
    // 対応するエントリをインスペクタ上で編集する（フラグとの同期は PPSkillRoleScoreListDrawer が行う）
    // ロールが増えてもこのクラス自体は変更不要（Drawer 側が Enum を動的に走査するため）
    [System.Serializable]
    public sealed class PPSkillRoleScoreList
    {
        [SerializeField] private List<PPRoleValue> mEntries = new();

        // 指定ロールのAIスコアを取得する
        // aRole : 取得するロール（単一フラグを想定）
        // return : 設定されているスコア。未設定なら 0（この候補は選ばれにくくなる）
        public float Get(PPBattleSkillRole aRole) => mEntries.Resolve(aRole, 0f);

        // Drawer がフラグとの同期・描画に使うエントリ一覧
        public List<PPRoleValue> Entries => mEntries;
    }
}
