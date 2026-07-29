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
    /// <summary>
    /// 他のメンバーの値に応じてインスペクタ表示を制御する属性（UE5 の EditCondition / EditConditionHides 相当）。
    /// <para>
    /// 条件に使えるのは同じオブジェクト上の bool 型フィールド・プロパティ・引数なしメソッド。
    /// 条件を満たさないときにグレーアウトさせるか完全に隠すかは <see cref="Hides"/> で切り替える。
    /// 描画は <c>Editor/EditConditionDrawer.cs</c> が担う。
    /// </para>
    /// </summary>
    public class EditConditionAttribute : PropertyAttribute
    {
        /// <summary>条件として参照するbool型フィールド/プロパティ/引数なしメソッドの名前</summary>
        public string ConditionMember { get; }

        /// <summary>trueの場合、条件を満たさないとき完全非表示（EditConditionHides相当）。falseの場合はグレーアウト（EditCondition相当）</summary>
        public bool Hides { get; }

        /// <summary>trueの場合、条件の真偽を反転する</summary>
        public bool Negate { get; }

        /// <param name="conditionMember">条件として参照するメンバー名。</param>
        /// <param name="hides">true なら非表示、false ならグレーアウト。</param>
        /// <param name="negate">true なら条件の真偽を反転する。</param>
        public EditConditionAttribute(string conditionMember, bool hides = false, bool negate = false)
        {
            ConditionMember = conditionMember;
            Hides = hides;
            Negate = negate;
        }
    }
}
