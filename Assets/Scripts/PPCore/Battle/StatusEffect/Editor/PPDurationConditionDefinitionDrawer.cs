/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPDurationConditionDefinitionDrawer.cs
 * @author hqrse
 * @date 2026/09/04
 * @brief PPDurationConditionDefinition の [SerializeReference] フィールド用インスペクタ拡張
 * =====================================*/

using System;
using System.Collections.Generic;
using System.Reflection;
using AttributeUtility;
using UnityEditor;
using UnityEngine;

namespace PPCore
{
    // PPDurationConditionDefinition 型のフィールドを、ラベル・種類のプルダウン・子フィールドの 1 行で描く
    //   持続条件   [ターン経過 ▼]  期間 [ 5 ]
    //   持続条件   [永続       ▼]
    // 種類は「ターン経過」「永続」のように少数のため、ツリーのポップアップではなくプルダウンで選ばせる
    // 候補は PPDurationConditionDefinition の具象派生型を自動で集めるため、種類を足してもこのクラスは変えなくてよい
    // 子フィールドが 1 つならプルダウンと同じ行に、2 つ以上なら次の行以降にインデントして並べる
    [CustomPropertyDrawer(typeof(PPDurationConditionDefinition), true)]
    public class PPDurationConditionDefinitionDrawer : PropertyDrawer
    {
        // 表示名の既定値。フィールド側に [PPFieldLabel] があればそちらを優先する
        // フィールドへ [Label] を付けると属性の PropertyDrawer が優先されて本ドロワーが呼ばれなくなるため、
        // 表示名は描画側で解決する
        private const string DefaultDisplayLabel = "持続条件";
        // 値が未設定（null）のときにプルダウンへ出す項目。StatusEffect 側の既定により永続として扱われる
        private const string UnassignedOptionText = "（未設定：永続扱い）";
        // プルダウンと同じ行に子フィールドを置くとき、プルダウンへ回す横幅の割合
        private const float InlinePopupRatio = 0.5f;
        // プルダウンと同じ行の子フィールドとのあいだに空ける余白
        private const float InlineSpacing = 4f;
        // 同じ行の子フィールドのラベルと入力欄のあいだに空ける余白
        private const float InlineLabelMargin = 6f;

        // 候補となる具象派生型。ドメインの再読み込みまで変わらないため 1 度だけ集める
        private static List<Type> sCandidateTypes;
        // プルダウンに並べる表示名。sCandidateTypes と同じ並び
        private static GUIContent[] sCandidateOptions;

        public override void OnGUI(Rect aPosition, SerializedProperty aProperty, GUIContent aLabel)
        {
            var label = new GUIContent(ResolveDisplayLabel(), aLabel.image, aLabel.tooltip);
            EnsureCandidates();

            EditorGUI.BeginProperty(aPosition, label, aProperty);

            var lineRect = new Rect(aPosition.x, aPosition.y, aPosition.width, EditorGUIUtility.singleLineHeight);
            var contentRect = EditorGUI.PrefixLabel(lineRect, label);

            // PrefixLabel で字下げ済みの領域へ描くため、ここから先は字下げを重ねない
            int previousIndent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;

            var children = CollectVisibleChildren(aProperty);
            bool isInline = children.Count == 1;
            var popupRect = isInline
                ? new Rect(contentRect.x, contentRect.y, contentRect.width * InlinePopupRatio, contentRect.height)
                : contentRect;

            bool isTypeChanged = DrawTypePopup(popupRect, aProperty);

            // 種類を変えた直後は子プロパティの並びが古いままのため、子の描画は次の再描画に任せる
            if (!isTypeChanged && isInline)
            {
                var childRect = new Rect(popupRect.xMax + InlineSpacing, contentRect.y,
                    contentRect.width - popupRect.width - InlineSpacing, contentRect.height);
                DrawInlineChild(childRect, children[0], aProperty);
            }

            EditorGUI.indentLevel = previousIndent;

            if (!isTypeChanged && children.Count >= 2)
            {
                EditorGUI.indentLevel++;
                float y = lineRect.yMax + EditorGUIUtility.standardVerticalSpacing;
                foreach (var child in children)
                {
                    float height = EditorGUI.GetPropertyHeight(child, true);
                    EditorGUI.PropertyField(new Rect(aPosition.x, y, aPosition.width, height), child, true);
                    y += height + EditorGUIUtility.standardVerticalSpacing;
                }
                EditorGUI.indentLevel--;
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty aProperty, GUIContent aLabel)
        {
            float lineHeight = EditorGUIUtility.singleLineHeight;
            var children = CollectVisibleChildren(aProperty);
            if (children.Count == 1)
            {
                return Mathf.Max(lineHeight, EditorGUI.GetPropertyHeight(children[0], true));
            }

            float height = lineHeight;
            if (children.Count >= 2)
            {
                foreach (var child in children)
                {
                    height += EditorGUIUtility.standardVerticalSpacing + EditorGUI.GetPropertyHeight(child, true);
                }
            }
            return height;
        }

        // 種類のプルダウンを描き、種類が変わった場合だけ新しいインスタンスを設定する
        // 同じ種類を選び直した場合は何もしないため、設定済みの値（期間など）は保たれる
        // 設定は SerializedProperty 経由で行うため、インスペクタの適用時に Undo へ記録される
        // aRect : プルダウンの描画領域
        // aProperty : 設定先の SerializeReference プロパティ
        // return : 種類を変えた場合 true
        private static bool DrawTypePopup(Rect aRect, SerializedProperty aProperty)
        {
            var currentType = aProperty.managedReferenceValue?.GetType();
            bool isUnassigned = currentType == null;

            // 未設定のときだけ先頭に「未設定」の項目を足す。設定済みから未設定へ戻す操作は用意しない
            int offset = isUnassigned ? 1 : 0;
            var options = new GUIContent[sCandidateOptions.Length + offset];
            if (isUnassigned) options[0] = new GUIContent(UnassignedOptionText);
            Array.Copy(sCandidateOptions, 0, options, offset, sCandidateOptions.Length);

            int currentIndex = isUnassigned ? 0 : sCandidateTypes.IndexOf(currentType) + offset;
            // 候補に無い型（属性の付いていない派生型など）が入っている場合は、型名をそのまま出して選択状態を保つ
            if (currentIndex < 0)
            {
                Array.Resize(ref options, options.Length + 1);
                options[options.Length - 1] = new GUIContent(currentType.Name);
                currentIndex = options.Length - 1;
            }

            int selectedIndex = EditorGUI.Popup(aRect, currentIndex, options);
            if (selectedIndex == currentIndex) return false;

            int typeIndex = selectedIndex - offset;
            if (typeIndex < 0 || typeIndex >= sCandidateTypes.Count) return false;

            var selectedType = sCandidateTypes[typeIndex];
            if (selectedType == currentType) return false;

            aProperty.managedReferenceValue = Activator.CreateInstance(selectedType);
            return true;
        }

        // プルダウンと同じ行に子フィールドを 1 つ描く
        // ラベルの幅は表示名が収まる分だけにし、残りを入力欄へ回す
        // aRect : 子フィールドの描画領域
        // aChild : 描画する子プロパティ
        // aParent : 子の型を調べるための親（SerializeReference）プロパティ
        private static void DrawInlineChild(Rect aRect, SerializedProperty aChild, SerializedProperty aParent)
        {
            var childLabel = new GUIContent(ResolveChildLabel(aParent, aChild));
            float previousWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = EditorStyles.label.CalcSize(childLabel).x + InlineLabelMargin;
            EditorGUI.PropertyField(aRect, aChild, childLabel, true);
            EditorGUIUtility.labelWidth = previousWidth;
        }

        // SerializeReference プロパティの直下にある、インスペクタ表示対象の子プロパティを集める
        // aProperty : 対象の SerializeReference プロパティ
        // return : 子プロパティのリスト。未設定または子が無ければ空
        private static List<SerializedProperty> CollectVisibleChildren(SerializedProperty aProperty)
        {
            var children = new List<SerializedProperty>();
            if (aProperty.managedReferenceValue == null) return children;

            var iterator = aProperty.Copy();
            var end = aProperty.GetEndProperty();
            if (!iterator.NextVisible(true)) return children;
            while (!SerializedProperty.EqualContents(iterator, end))
            {
                children.Add(iterator.Copy());
                if (!iterator.NextVisible(false)) break;
            }
            return children;
        }

        // 子プロパティの表示名を解決する
        // フィールドに [Label] があればその文字列（LabelAttributeDrawer が実際に描く文字列と揃える）、無ければ Unity の表示名を使う
        // aParent : 子の型を調べるための親（SerializeReference）プロパティ
        // aChild : 対象の子プロパティ
        // return : 表示名
        private static string ResolveChildLabel(SerializedProperty aParent, SerializedProperty aChild)
        {
            var field = FindField(aParent.managedReferenceValue?.GetType(), aChild.name);
            return field?.GetCustomAttribute<LabelAttribute>()?.Text ?? aChild.displayName;
        }

        // 型と基底型をたどってフィールドを探す。protected / private のフィールドも対象にする
        // aType : 探索を始める型
        // aName : フィールド名
        // return : 見つかったフィールド。無ければ null
        private static FieldInfo FindField(Type aType, string aName)
        {
            const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            for (var type = aType; type != null; type = type.BaseType)
            {
                var field = type.GetField(aName, Flags);
                if (field != null) return field;
            }
            return null;
        }

        // 描画に使う表示名を解決する
        // フィールドに [PPFieldLabel] があればその文字列、無ければ既定値を使う
        // return : 表示名
        private string ResolveDisplayLabel()
            => fieldInfo?.GetCustomAttribute<PPFieldLabelAttribute>()?.Text ?? DefaultDisplayLabel;

        // 候補の型と表示名を集める。集め方はツリー選択（PPTypeTreePickerTreeView）と同じ
        // 表示名は [PPTypeMenuName] のパスを使う
        private static void EnsureCandidates()
        {
            if (sCandidateTypes != null) return;

            sCandidateTypes = PPTypeTreePickerTreeView.CollectDerived<PPDurationConditionDefinition>(true);
            sCandidateOptions = new GUIContent[sCandidateTypes.Count];
            for (int i = 0; i < sCandidateTypes.Count; i++)
            {
                var type = sCandidateTypes[i];
                string name = type.GetCustomAttribute<PPTypeMenuNameAttribute>()?.Path
                              ?? ObjectNames.NicifyVariableName(type.Name);
                sCandidateOptions[i] = new GUIContent(name);
            }
        }
    }
}
