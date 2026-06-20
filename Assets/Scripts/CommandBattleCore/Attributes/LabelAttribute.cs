/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file LabelAttribute.cs
 * @author hqrse
 * @date 2026/06/13
 * @brief インスペクタに任意表示名で表示する属性
 * =====================================*/
using UnityEngine;

namespace CommandBattleCore
{
    public class LabelAttribute : PropertyAttribute
    {
        public string Text { get; }

        public LabelAttribute(string text)
        {
            Text = text;
        }

        public LabelAttribute(string text, bool applyToCollection)
            : base(applyToCollection)
        {
            Text = text;
        }
    }
}