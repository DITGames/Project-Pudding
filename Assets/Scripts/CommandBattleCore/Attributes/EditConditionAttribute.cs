/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file EditConditionAttribute.cs
 * @author hqrse
 * @date 2026/07/12
 * @brief 指定した条件に応じてインスペクタ表示を制御する属性（UE5のEditCondition/EditConditionHides相当）
 * =====================================*/
using UnityEngine;

namespace CommandBattleCore
{
    public class EditConditionAttribute : PropertyAttribute
    {
        /// <summary>条件として参照するbool型フィールド/プロパティ/引数なしメソッドの名前</summary>
        public string ConditionMember { get; }

        /// <summary>trueの場合、条件を満たさないとき完全非表示（EditConditionHides相当）。falseの場合はグレーアウト（EditCondition相当）</summary>
        public bool Hides { get; }

        /// <summary>trueの場合、条件の真偽を反転する</summary>
        public bool Negate { get; }

        public EditConditionAttribute(string conditionMember, bool hides = false, bool negate = false)
        {
            ConditionMember = conditionMember;
            Hides = hides;
            Negate = negate;
        }
    }
}
