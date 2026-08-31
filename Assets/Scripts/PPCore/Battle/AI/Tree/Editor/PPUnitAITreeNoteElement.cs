/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPUnitAITreeNoteElement.cs
 * @author hqrse
 * @date 2026/08/31
 * @brief 判断ツリーのグラフ上へ置く注記1枚分の表示
 * =====================================*/

using System;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace PPCore
{
    // グラフ上へ置く注記 1 枚分の表示
    //
    // ノードの後ろに敷く色付きの面と、その左上に置く見出しだけで出来ている
    // 「この一帯は開幕用」のように、枝のまとまりへ見出しを付けて読み方を示すためのもの
    //
    // GraphView の StickyNote は使わない
    // あちらは見出しと本文を UXML 側のラベルと隠し入力欄の組で扱う作りで、
    // 本文の入力欄が取り出せない・見出しが潰れる、といった噛み合わせの問題が出た
    // 必要なのは面と見出しだけなので、そのぶんだけを自前で組んだほうが確実で読みやすい
    //
    // 面はノードより後ろの層へ置く。前に出るとノードが隠れて、敷いた意味が無くなるため
    internal sealed class PPUnitAITreeNoteElement : GraphElement
    {
        // ノードより後ろへ置くための層。GraphView は層の小さいものから描く
        private const int NoteLayer = -1;
        // 見出しの高さ。ここだけが文字の領域で、残りは面として空ける
        private const float TitleHeight = 22f;
        // 色を選ぶ欄の幅
        private const float ColorFieldWidth = 44f;
        // 面の上に文字を置く際、白と黒のどちらを使うかを分ける明るさの境目
        private const float TextContrastThreshold = 0.55f;
        // 大きさを変えるつまみの太さ。細いと掴み損ねるため、余裕を持たせている
        private const float HandleThickness = 16f;
        // これより小さくはしない大きさ。掴めなくなるのを防ぐ
        // つまみが上下・左右で二重に重ならないよう、その分を見込んでおく
        private const float MinWidth = 160f;
        private const float MinHeight = TitleHeight + (HandleThickness * 2f) + 8f;
        // 選択したときの縁取りの太さ
        private const float SelectionBorderWidth = 2f;

        // 選択中を示す縁取りの色
        private static readonly Color SelectionOutlineColor = new(0.27f, 0.75f, 1f);
        // つまみに重ねる色。触れているときと掴んでいるときで濃さを変える
        private static readonly Color HandleHoverColor = new(1f, 1f, 1f, 0.22f);
        private static readonly Color HandleActiveColor = new(1f, 1f, 1f, 0.45f);

        // 掴んだつまみが動かす辺
        [Flags]
        private enum ResizeSide
        {
            None = 0,
            Left = 1 << 0,
            Right = 1 << 1,
            Top = 1 << 2,
            Bottom = 1 << 3,
        }

        // 見出しの入力欄
        private readonly TextField mTitleField = new();
        // 面の色を選ぶ欄
        private readonly ColorField mColorField = new();
        // 現在の面の色。選択の縁取りを解いたときに枠の色を戻すために覚えておく
        private Color mCurrentColor;

        // 対応する注記データの ID
        public string NoteId { get; }

        // 見出しの文字列
        public string NoteTitle => mTitleField.value;
        // 面の色
        public Color NoteColor => mColorField.value;

        // 見出しか色が変えられた際に通知する(引数なし)
        public event Action OnChanged;

        // aData : 表示する注記データ
        public PPUnitAITreeNoteElement(PPUnitAINoteData aData)
        {
            NoteId = aData.NoteId;

            // 掴んで動かす・選ぶ・消す、を GraphView の仕組みへ任せる
            // 大きさを変えるのだけは自前で持つ（SetupResizeHandles のコメント参照）
            capabilities |= Capabilities.Movable | Capabilities.Deletable | Capabilities.Selectable;
            // クリックで選択状態にする。これが無いと選べず、削除も移動もできない
            this.AddManipulator(new ClickSelector());

            layer = NoteLayer;

            SetupStyle();
            SetupHeader(aData);
            SetupResizeHandles();

            SetPosition(aData.Rect);
            ApplyColor(aData.Color);
        }

        // 大きさを変えるためのつまみを四辺と四隅へ置く
        //
        // GraphView の ResizableElement は使わない
        // 下と右は正しく効くが、上と左を掴んでも位置が動かず反対側へ伸びてしまい、
        // 掴んだ辺と逆へ広がる挙動になる
        // 掴んだ辺に応じて位置と大きさの両方を動かすだけなので、自前で持ったほうが確実
        //
        // 隅は辺より後に足す。重なった場所では後から足したほうが手前になり、隅が優先して掴める
        private void SetupResizeHandles()
        {
            AddResizeHandle(ResizeSide.Left, 0f, float.NaN, 0f, 0f, HandleThickness, float.NaN);
            AddResizeHandle(ResizeSide.Right, float.NaN, 0f, 0f, 0f, HandleThickness, float.NaN);
            AddResizeHandle(ResizeSide.Top, 0f, 0f, 0f, float.NaN, float.NaN, HandleThickness);
            AddResizeHandle(ResizeSide.Bottom, 0f, 0f, float.NaN, 0f, float.NaN, HandleThickness);

            AddResizeHandle(ResizeSide.Left | ResizeSide.Top,
                0f, float.NaN, 0f, float.NaN, HandleThickness, HandleThickness);
            AddResizeHandle(ResizeSide.Right | ResizeSide.Top,
                float.NaN, 0f, 0f, float.NaN, HandleThickness, HandleThickness);
            AddResizeHandle(ResizeSide.Left | ResizeSide.Bottom,
                0f, float.NaN, float.NaN, 0f, HandleThickness, HandleThickness);
            AddResizeHandle(ResizeSide.Right | ResizeSide.Bottom,
                float.NaN, 0f, float.NaN, 0f, HandleThickness, HandleThickness);
        }

        // つまみを 1 つ足す
        // 位置の指定は NaN を「その辺には貼り付けない」の意味で使う
        // aSide : 掴んだときに動かす辺
        // aLeft / aRight / aTop / aBottom : 面のどの辺へ貼り付けるか
        // aWidth / aHeight : つまみの大きさ。NaN なら貼り付けた辺いっぱいに伸びる
        private void AddResizeHandle(ResizeSide aSide, float aLeft, float aRight, float aTop, float aBottom,
            float aWidth, float aHeight)
        {
            var handle = new VisualElement { style = { position = Position.Absolute } };
            if (!float.IsNaN(aLeft)) handle.style.left = aLeft;
            if (!float.IsNaN(aRight)) handle.style.right = aRight;
            if (!float.IsNaN(aTop)) handle.style.top = aTop;
            if (!float.IsNaN(aBottom)) handle.style.bottom = aBottom;
            if (!float.IsNaN(aWidth)) handle.style.width = aWidth;
            if (!float.IsNaN(aHeight)) handle.style.height = aHeight;

            RegisterResizeDrag(handle, aSide);
            hierarchy.Add(handle);
        }

        // つまみへドラッグの処理を結び付ける
        // aHandle : つまみの要素
        // aSide : 掴んだときに動かす辺
        private void RegisterResizeDrag(VisualElement aHandle, ResizeSide aSide)
        {
            Rect startRect = default;
            Vector2 startMouse = default;
            bool isResizing = false;
            bool isHovered = false;

            // つまみは普段は透明で、どこを掴めるのかが分からない
            // 触れたら薄く、掴んでいる間は濃く出して、掴める場所と掴んでいる場所を示す
            void RefreshHandleColor()
            {
                if (isResizing) aHandle.style.backgroundColor = HandleActiveColor;
                else if (isHovered) aHandle.style.backgroundColor = HandleHoverColor;
                else aHandle.style.backgroundColor = Color.clear;
            }

            aHandle.RegisterCallback<PointerEnterEvent>(_ =>
            {
                isHovered = true;
                RefreshHandleColor();
            });

            aHandle.RegisterCallback<PointerLeaveEvent>(_ =>
            {
                isHovered = false;
                RefreshHandleColor();
            });

            aHandle.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0 || parent == null) return;

                startRect = GetPosition();
                startMouse = parent.WorldToLocal(evt.position);
                isResizing = true;
                RefreshHandleColor();
                aHandle.CapturePointer(evt.pointerId);
                // 面そのものの移動や選択を起こさないよう、ここで止める
                evt.StopPropagation();
            });

            aHandle.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (!isResizing || !aHandle.HasPointerCapture(evt.pointerId)) return;

                Vector2 delta = parent.WorldToLocal(evt.position) - startMouse;
                SetPosition(ResolveResizedRect(startRect, aSide, delta));
                evt.StopPropagation();
            });

            aHandle.RegisterCallback<PointerUpEvent>(evt =>
            {
                if (!isResizing) return;

                aHandle.ReleasePointer(evt.pointerId);
                isResizing = false;
                RefreshHandleColor();
                OnChanged?.Invoke();
                evt.StopPropagation();
            });

            aHandle.RegisterCallback<PointerCaptureOutEvent>(_ =>
            {
                isResizing = false;
                RefreshHandleColor();
            });
        }

        // 掴んだ辺と動かした量から、変更後の位置と大きさを求める
        //
        // 上と左は、位置と大きさの両方を動かす必要がある
        // 大きさだけを変えると、掴んだ辺が動かず反対側の辺が動いてしまう
        // 最小の大きさに当たったところで位置も止め、掴んだ辺が行き過ぎないようにする
        //
        // aStart : 掴んだ時点の位置と大きさ
        // aSide : 掴んだ辺
        // aDelta : 掴んでからの移動量
        // return : 変更後の位置と大きさ
        private static Rect ResolveResizedRect(Rect aStart, ResizeSide aSide, Vector2 aDelta)
        {
            var rect = aStart;

            if ((aSide & ResizeSide.Left) != 0)
            {
                float x = Mathf.Min(aStart.x + aDelta.x, aStart.xMax - MinWidth);
                rect.x = x;
                rect.width = aStart.xMax - x;
            }
            else if ((aSide & ResizeSide.Right) != 0)
            {
                rect.width = Mathf.Max(aStart.width + aDelta.x, MinWidth);
            }

            if ((aSide & ResizeSide.Top) != 0)
            {
                float y = Mathf.Min(aStart.y + aDelta.y, aStart.yMax - MinHeight);
                rect.y = y;
                rect.height = aStart.yMax - y;
            }
            else if ((aSide & ResizeSide.Bottom) != 0)
            {
                rect.height = Mathf.Max(aStart.height + aDelta.y, MinHeight);
            }
            return rect;
        }

        // 面そのものの見た目を整える
        private void SetupStyle()
        {
            style.position = Position.Absolute;
            style.flexDirection = FlexDirection.Column;
            // 小さくし過ぎて掴めなくなるのを防ぐ。見出しの高さぶんは最低限残す
            style.minWidth = MinWidth;
            style.minHeight = MinHeight;
            // つまみの分だけ中身を内側へ寄せる
            // 寄せないと、太くしたつまみが見出しや色の欄へ重なってクリックを奪ってしまう
            style.paddingTop = HandleThickness;
            style.paddingBottom = HandleThickness;
            style.paddingLeft = HandleThickness;
            style.paddingRight = HandleThickness;
        }

        // 見出しと色の欄を並べる
        // aData : 表示する注記データ
        private void SetupHeader(PPUnitAINoteData aData)
        {
            var header = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            header.style.height = TitleHeight;
            // 面の残りを本文に取られないよう、見出しの行は縮まないことを明示する
            header.style.flexShrink = 0f;
            header.style.paddingLeft = 4f;
            header.style.paddingRight = 4f;

            mTitleField.SetValueWithoutNotify(aData.Title);
            mTitleField.style.flexGrow = 1f;
            mTitleField.style.marginLeft = 0f;
            mTitleField.RegisterValueChangedCallback(_ => OnChanged?.Invoke());
            header.Add(mTitleField);

            mColorField.SetValueWithoutNotify(aData.Color);
            mColorField.showAlpha = true;
            mColorField.style.width = ColorFieldWidth;
            mColorField.style.marginRight = 0f;
            mColorField.tooltip = "面の色を変える";
            mColorField.RegisterValueChangedCallback(evt =>
            {
                ApplyColor(evt.newValue);
                OnChanged?.Invoke();
            });
            header.Add(mColorField);

            Add(header);
        }

        // 面の色を当て、見出しの文字色を読める側へ寄せる
        // aColor : 設定する色
        private void ApplyColor(Color aColor)
        {
            mCurrentColor = aColor;
            style.backgroundColor = aColor;
            RefreshBorder();

            // 明るい面なら黒、暗い面なら白。色を自由に選べるため、その都度決める
            float luminance = (aColor.r * 0.299f) + (aColor.g * 0.587f) + (aColor.b * 0.114f);
            var textColor = luminance > TextContrastThreshold ? Color.black : Color.white;

            var input = mTitleField.Q("unity-text-input");
            if (input == null) return;

            input.style.backgroundColor = Color.clear;
            input.style.borderTopWidth = 0f;
            input.style.borderBottomWidth = 0f;
            input.style.borderLeftWidth = 0f;
            input.style.borderRightWidth = 0f;
            input.style.color = textColor;
            input.style.unityFontStyleAndWeight = FontStyle.Bold;
        }

        // 枠の見た目を、選択の有無と現在の色から決める
        //
        // 選択中は太い明るい縁取りにする
        // 面はノードの後ろに敷くため、選択されていることが背景色の変化では分かりにくい
        private void RefreshBorder()
        {
            bool isSelected = selected;
            float width = isSelected ? SelectionBorderWidth : 1f;
            var color = isSelected
                ? SelectionOutlineColor
                : new Color(mCurrentColor.r * 0.7f, mCurrentColor.g * 0.7f, mCurrentColor.b * 0.7f, 1f);

            style.borderTopWidth = width;
            style.borderBottomWidth = width;
            style.borderLeftWidth = width;
            style.borderRightWidth = width;
            style.borderTopColor = color;
            style.borderBottomColor = color;
            style.borderLeftColor = color;
            style.borderRightColor = color;
        }

        // 選択されたときに縁取りを出す
        public override void OnSelected()
        {
            base.OnSelected();
            RefreshBorder();
        }

        // 選択が外れたときに縁取りを戻す
        public override void OnUnselected()
        {
            base.OnUnselected();
            RefreshBorder();
        }

        // グラフ上の位置と大きさを返す
        // return : 位置と大きさ
        public override Rect GetPosition()
            => new(style.left.value.value, style.top.value.value,
                style.width.value.value, style.height.value.value);

        // グラフ上の位置と大きさを設定する
        // aRect : 設定する位置と大きさ
        public override void SetPosition(Rect aRect)
        {
            style.left = aRect.x;
            style.top = aRect.y;
            style.width = aRect.width;
            style.height = aRect.height;
        }
    }
}
