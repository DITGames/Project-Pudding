/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file ParameterModifierBehaviour.cs
 * @author hqrse
 * @date 2026/07/31
 * @brief パラメータを増減させる振る舞い
 * =====================================*/

using UnityEngine;

namespace CommandBattleCore
{
    public sealed class ParameterModifierBehaviour : StatusEffectBehaviour
    {
        private readonly string mParamId;
        private readonly ParameterModifierType mType;
        // 1スタックあたりの変動量。符号込みで受け取る
        private readonly float mValuePerStack;
        private readonly int mPriority;

        // 実際に適用した修飾子。スタック変動時に差し替えるため保持する
        private ParameterModifier mApplied;

        public ParameterModifierBehaviour(string aParamId, ParameterModifierType aType,
            float aValuePerStack, int aPriority = 0)
        {
            mParamId = aParamId;
            mType = aType;
            mValuePerStack = aValuePerStack;
            mPriority = aPriority;
        }

        public override void OnApply(StatusEffectContext aContext) => Reapply(aContext);
        public override void OnStackChanged(StatusEffectContext aContext) => Reapply(aContext);

        public override void OnRemove(StatusEffectContext aContext)
        {
            if (mApplied == null) return;
            aContext.Owner?.ResolveParameter(mParamId)?.RemoveModifier(mApplied);
            mApplied = null;
        }

        // 現在のスタック数に応じた値で修飾子を貼り直す
        private void Reapply(StatusEffectContext aContext)
        {
            var param = aContext.Owner?.ResolveParameter(mParamId);
            if (param == null) return;

            if (mApplied != null) param.RemoveModifier(mApplied);
            mApplied = new ParameterModifier(mType, aContext.Effect, ResolveValue(aContext.Stacks), mPriority);
            param.AddModifier(mApplied);
        }

        // 加算・上書きはスタック数倍、乗算はスタック数乗にする
        private float ResolveValue(int aStacks)
            => mType == ParameterModifierType.Multiply
                ? Mathf.Pow(mValuePerStack, aStacks)
                : mValuePerStack * aStacks;
    }
}
