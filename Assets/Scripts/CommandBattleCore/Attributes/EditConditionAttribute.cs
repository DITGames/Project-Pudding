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
    // 他のメンバーの値に応じてインスペクタ表示を制御する属性（UE5 の EditCondition / EditConditionHides 相当）
    // 条件に使えるのは同じオブジェクト上の bool 型フィールド・プロパティ・引数なしメソッド
    // 条件を満たさないときにグレーアウトさせるか完全に隠すかは Hides で切り替える
    // 描画は Editor/EditConditionDrawer.cs が担う
    public class EditConditionAttribute : PropertyAttribute
    {
        // 条件として参照するbool型フィールド/プロパティ/引数なしメソッドの名前
        public string ConditionMember { get; }

        // trueの場合、条件を満たさないとき完全非表示（EditConditionHides相当）。falseの場合はグレーアウト（EditCondition相当）
        public bool Hides { get; }

        // trueの場合、条件の真偽を反転する
        public bool Negate { get; }

        // aConditionMember : 条件として参照するメンバー名
        // aHides : true なら非表示、false ならグレーアウト
        // aNegate : true なら条件の真偽を反転する
        public EditConditionAttribute(string conditionMember, bool hides = false, bool negate = false)
        {
            ConditionMember = conditionMember;
            Hides = hides;
            Negate = negate;
        }
    }
}
