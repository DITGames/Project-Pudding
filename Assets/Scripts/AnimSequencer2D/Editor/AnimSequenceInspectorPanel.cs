/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file AnimSequenceInspectorPanel.cs
 * @author hqrse
 * @date 2026/08/21
 * @brief 選択対象(エントリ、または選択中のキーフレーム/イベントキー)を編集するインスペクタ相当パネル
 * =====================================*/

using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace AnimSequencer2D.Editor
{
    // SerializedPropertyを列挙して存在するフィールドだけを表示する
    // (Label/EditConditionAttributeのPropertyDrawerがPropertyField経由でもそのまま適用される)
    internal class AnimSequenceInspectorPanel : VisualElement
    {
        private readonly VisualElement mContentContainer;
        private readonly Action mOnPropertyChanged;
        private readonly Action<string, string> mOnKeyRenamed;
        private readonly Action<float> mOnKeyframeTimeChanged;

        // aOnPropertyChanged : 値が変化した際に呼ぶ(タイムラインの非破壊な位置更新のトリガーに使う)。
        // RequestPositionRefresh側はフラグを立てるだけの非破壊処理のため、フィールド単位で呼んでもフォーカスは失われない
        // aOnKeyRenamed : エントリのアニメーションキーがリネームされた際に呼ぶ(リネーム前, リネーム後)。グラフのノードタイトル同期に使う
        // aOnKeyframeTimeChanged : 選択中キーフレームの時刻が変更された際に呼ぶ(変更後の時刻)。
        // ここでの編集はタイムラインの再構築(選択変更通知)を経ないため、呼び出し元が保持している選択中時刻の更新に使う
        public AnimSequenceInspectorPanel(Action aOnPropertyChanged, Action<string, string> aOnKeyRenamed, Action<float> aOnKeyframeTimeChanged)
        {
            mOnPropertyChanged = aOnPropertyChanged;
            mOnKeyRenamed = aOnKeyRenamed;
            mOnKeyframeTimeChanged = aOnKeyframeTimeChanged;

            var scrollView = new ScrollView();
            Add(scrollView);
            mContentContainer = scrollView.contentContainer;

            SetTargetProperty(null, false);
        }

        // aProperty : 表示対象(エントリ、またはキーフレーム/イベントキー)のSerializedProperty。未選択時はnull
        // aIsKeyframeLevel : true ならキーフレーム/イベントキー単体を表示する。false ならエントリ自体を表示する
        public void SetTargetProperty(SerializedProperty aProperty, bool aIsKeyframeLevel)
        {
            mContentContainer.Clear();

            if (aProperty == null)
            {
                mContentContainer.Add(new Label("アニメーションキーを選択してください") { style = { paddingTop = 8, paddingLeft = 8 } });
                return;
            }

            SerializedProperty iterator = aProperty.Copy();
            SerializedProperty end = aProperty.GetEndProperty();
            bool enterChildren = true;

            while (iterator.NextVisible(enterChildren) && !SerializedProperty.EqualContents(iterator, end))
            {
                enterChildren = false;

                if (iterator.name == "mKeyframeId")
                {
                    continue; // 内部管理用フィールドのため表示しない
                }

                if (!aIsKeyframeLevel && (iterator.name == "mTracks" || iterator.name == "mEventKeys"))
                {
                    continue; // トラック/イベントキーはタイムラインUIで編集するためここには表示しない
                }

                if (!aIsKeyframeLevel && iterator.name == "mKey")
                {
                    AddKeyField(iterator.Copy());
                    continue;
                }

                if (aIsKeyframeLevel && iterator.name == "mTime")
                {
                    AddTimeField(iterator.Copy());
                    continue;
                }

                var field = new PropertyField(iterator.Copy());
                field.Bind(aProperty.serializedObject);
                // Durationやキーフレームの時刻等、タイムライン表示に影響しうる値の変更をその都度伝える。
                // RequestPositionRefresh側は非破壊(フラグを立てるだけ)なのでフォーカスは失われない
                field.RegisterCallback<SerializedPropertyChangeEvent>(_ => mOnPropertyChanged?.Invoke());
                mContentContainer.Add(field);
            }

            // 表示対象が切り替わったタイミングでも一度知らせておく(Rebuild直後の初期位置を合わせるため)
            mOnPropertyChanged?.Invoke();
        }

        // キーフレーム/イベントキーの時刻の編集フィールド。汎用のPropertyFieldは1文字入力するたびに値を適用するため、
        // AnimSequenceDefinition.OnValidateの時刻昇順ソートが入力途中の値で走り、編集中のSerializedPropertyが
        // 並べ替え後の別のキーフレームを指してしまう(例: 0.22を0.2へ打ち替える途中の「0」が適用されて先頭へ移動する)。
        // 確定(Enter/フォーカス移動)時のみ適用されるようisDelayedにして防ぐ
        // aTimeProperty : mTimeのSerializedProperty
        private void AddTimeField(SerializedProperty aTimeProperty)
        {
            var field = new FloatField("時刻(秒)") { value = aTimeProperty.floatValue, isDelayed = true };
            field.RegisterValueChangedCallback(evt =>
            {
                // 負の時刻は評価順序が壊れるため0秒までとする
                float time = Mathf.Max(0f, evt.newValue);
                aTimeProperty.floatValue = time;
                aTimeProperty.serializedObject.ApplyModifiedProperties();
                if (!Mathf.Approximately(time, evt.newValue))
                {
                    field.SetValueWithoutNotify(time);
                }
                mOnPropertyChanged?.Invoke();
                // タイムラインの再構築(=選択変更通知)を経ないため、呼び出し元が保持している選択中キーフレームの
                // 時刻をここで明示的に追従させる(ギズモ編集の書き込み先・プレビュー表示位置がずれないようにするため)
                mOnKeyframeTimeChanged?.Invoke(time);
            });
            mContentContainer.Add(field);
        }

        // アニメーションキー名の編集フィールド。他エントリと重複しない名前へ自動調整し、
        // 変更後はグラフのノードタイトルへ同期する(アニメーションキー名の変更はここでのみ行える)
        // aKeyProperty : mKeyのSerializedProperty
        private void AddKeyField(SerializedProperty aKeyProperty)
        {
            string currentKey = aKeyProperty.stringValue;
            var field = new TextField("アニメーションキー") { value = currentKey, isDelayed = true };
            field.RegisterValueChangedCallback(evt =>
            {
                string oldKey = currentKey;
                SerializedProperty entriesProperty = aKeyProperty.serializedObject.FindProperty("mEntries");
                string uniqueKey = AnimSequenceEntryGraphView.MakeUniqueKey(entriesProperty, aKeyProperty, evt.newValue);

                aKeyProperty.stringValue = uniqueKey;
                aKeyProperty.serializedObject.ApplyModifiedProperties();
                if (uniqueKey != evt.newValue)
                {
                    field.SetValueWithoutNotify(uniqueKey);
                }

                currentKey = uniqueKey;
                mOnKeyRenamed?.Invoke(oldKey, uniqueKey);
            });
            mContentContainer.Add(field);
        }
    }
}
