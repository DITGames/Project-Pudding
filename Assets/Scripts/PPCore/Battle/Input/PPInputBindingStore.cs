/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPInputBindingStore.cs
 * @author hqrse
 * @date 2026/11/18
 * @brief キー割り当ての変更・保存・読み込み（変更できる操作の定義・割り当ての窓口）
 * =====================================*/

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using CustomConsole;

namespace PPCore
{
    // キー設定で変更できる操作 1 つ分。入力アクションの名前と画面に出す名前を持つ
    public readonly struct PPRebindableAction
    {
        /* 状態 */

        public readonly string ActionName;
        public readonly string DisplayName;

        /* 生成 */

        public PPRebindableAction(string aActionName, string aDisplayName)
        {
            ActionName = aActionName;
            DisplayName = aDisplayName;
        }
    }

    // キー割り当ての窓口。各操作はキーボード 2 枠・ゲームパッド 2 枠を持ち、変更は PlayerPrefs に保存する
    // 左右・上下の操作とキー設定を開く操作は変更できない
    public static class PPInputBindingStore
    {
        /* 定数 */

        public const string LogTag = "Input";
        public const string MapName = "Battle";
        // 1 つの操作がデバイスごとに持つ割り当ての枠の数
        public const int SlotCount = 2;
        public const string KeyboardGroup = "Keyboard&Mouse";
        public const string GamepadGroup = "Gamepad";
        // 未割り当ての枠に出す文字
        public const string EmptyDisplayName = "なし";

        private const string PrefsKey = "PP.InputBindingOverrides";

        /* static 変数 */

        // キー設定に並べる操作。並び順が画面の行の順になる
        public static readonly PPRebindableAction[] RebindableActions =
        {
            new("Submit", "決定"),
            new("Cancel", "キャンセル"),
            new("OpenCommand", "コマンドを開く"),
            new("AutoBattle", "オートバトル"),
            new("DropLeft", "コインを左へ投入"),
            new("DropRight", "コインを右へ投入"),
        };

        // 操作の場所の名前を短く言い換える表。ゲームパッドは Xbox / PlayStation の両方の呼び名を並べる
        private static readonly Dictionary<string, string> sDisplayNames = new(StringComparer.OrdinalIgnoreCase)
        {
            { "<Gamepad>/buttonSouth", "A/×" },
            { "<Gamepad>/buttonEast", "B/○" },
            { "<Gamepad>/buttonNorth", "Y/△" },
            { "<Gamepad>/buttonWest", "X/□" },
            { "<Gamepad>/leftShoulder", "LB/L1" },
            { "<Gamepad>/rightShoulder", "RB/R1" },
            { "<Gamepad>/leftTrigger", "LT/L2" },
            { "<Gamepad>/rightTrigger", "RT/R2" },
            { "<Gamepad>/leftStickPress", "L3" },
            { "<Gamepad>/rightStickPress", "R3" },
            { "<Gamepad>/select", "Select" },
            { "<Gamepad>/dpad/up", "十字↑" },
            { "<Gamepad>/dpad/down", "十字↓" },
            { "<Gamepad>/dpad/left", "十字←" },
            { "<Gamepad>/dpad/right", "十字→" },
            { "<Keyboard>/upArrow", "↑" },
            { "<Keyboard>/downArrow", "↓" },
            { "<Keyboard>/leftArrow", "←" },
            { "<Keyboard>/rightArrow", "→" },
            { "<Mouse>/rightButton", "右クリック" },
            { "<Mouse>/middleButton", "中クリック" },
        };

        /* 保存・読み込み */

        // 保存されている割り当ての変更を読み込む。保存が無ければ既定のまま
        public static void Load(InputActionAsset aAsset)
        {
            string json = PlayerPrefs.GetString(PrefsKey, string.Empty);
            if (string.IsNullOrEmpty(json)) return;
            try
            {
                aAsset.LoadBindingOverridesFromJson(json);
            }
            catch (Exception e)
            {
                // 壊れた保存データで起動できなくならないよう、既定の割り当てに戻して続ける
                CustomConsoleLog.Warning(LogTag, $"キー設定の読み込みに失敗したため、既定の割り当てに戻します。{e.Message}");
                aAsset.RemoveAllBindingOverrides();
            }
        }

        // 今の割り当ての変更を保存する
        public static void Save(InputActionAsset aAsset)
        {
            PlayerPrefs.SetString(PrefsKey, aAsset.SaveBindingOverridesAsJson());
            PlayerPrefs.Save();
        }

        // 変更できる操作を既定の割り当てに戻して保存する
        public static void ResetToDefault(InputActionAsset aAsset)
        {
            foreach (var rebindable in RebindableActions) FindAction(aAsset, rebindable.ActionName)?.RemoveAllBindingOverrides();
            Save(aAsset);
        }

        /* 問い合わせ */

        // 変更できる操作の入力アクションを引く。見つからなければ null
        public static InputAction FindAction(InputActionAsset aAsset, string aActionName)
            => aAsset.FindActionMap(MapName)?.FindAction(aActionName);

        // 操作の、指定したデバイスの何枠目にあたる割り当ての番号を返す。無ければ -1
        public static int GetBindingIndex(InputAction aAction, bool aIsGamepad, int aSlot)
        {
            string group = aIsGamepad ? GamepadGroup : KeyboardGroup;
            int count = 0;
            var bindings = aAction.bindings;
            for (int i = 0; i < bindings.Count; i++)
            {
                if (bindings[i].isComposite || bindings[i].isPartOfComposite) continue;
                if (!HasGroup(bindings[i], group)) continue;
                if (count == aSlot) return i;
                count++;
            }
            return -1;
        }

        // 割り当ての場所を、画面に出す短い名前にする。未割り当てなら「なし」
        public static string GetDisplayName(string aPath)
        {
            if (string.IsNullOrEmpty(aPath)) return EmptyDisplayName;
            if (sDisplayNames.TryGetValue(aPath, out var name)) return name;
            return InputControlPath.ToHumanReadableString(aPath, InputControlPath.HumanReadableStringOptions.OmitDevice);
        }

        /* 変更 */

        // 押したキー・ボタンで割り当てを変更する。完了・取り消しのどちらでも、終わったら aOnFinished を呼ぶ
        // Esc で取り消し、一定時間何も押さなければ取り消す
        public static InputActionRebindingExtensions.RebindingOperation StartRebind(InputActionAsset aAsset, InputAction aAction,
            int aBindingIndex, bool aIsGamepad, float aTimeout, Action aOnFinished)
        {
            string oldPath = aAction.bindings[aBindingIndex].effectivePath;
            bool wasEnabled = aAction.enabled;
            aAction.Disable();

            var operation = aAction.PerformInteractiveRebinding(aBindingIndex)
                .WithExpectedControlType("Button")
                .WithControlsExcluding("<Keyboard>/escape")
                .WithControlsExcluding("<Keyboard>/anyKey")
                .WithControlsExcluding("<Gamepad>/start")
                .WithControlsExcluding("<Mouse>/leftButton")
                .WithCancelingThrough("<Keyboard>/escape")
                .WithTimeout(aTimeout)
                .OnMatchWaitForAnother(0.1f);
            if (aIsGamepad)
            {
                operation.WithControlsHavingToMatchPath("<Gamepad>");
            }
            else
            {
                operation.WithControlsHavingToMatchPath("<Keyboard>");
                operation.WithControlsHavingToMatchPath("<Mouse>");
            }

            void Finish(InputActionRebindingExtensions.RebindingOperation aOperation, bool aIsCompleted)
            {
                if (aIsCompleted)
                {
                    SwapDuplicate(aAsset, aAction, aBindingIndex, aIsGamepad, oldPath);
                    Save(aAsset);
                }
                aOperation.Dispose();
                if (wasEnabled) aAction.Enable();
                aOnFinished?.Invoke();
            }

            operation.OnComplete(op => Finish(op, true));
            operation.OnCancel(op => Finish(op, false));
            return operation.Start();
        }

        // 新しく割り当てたキーを、同じデバイスのほかの枠が使っていたら、その枠へ元のキーを渡して入れ替える
        private static void SwapDuplicate(InputActionAsset aAsset, InputAction aAction, int aBindingIndex, bool aIsGamepad, string aOldPath)
        {
            string newPath = aAction.bindings[aBindingIndex].effectivePath;
            if (string.IsNullOrEmpty(newPath)) return;
            foreach (var rebindable in RebindableActions)
            {
                var other = FindAction(aAsset, rebindable.ActionName);
                if (other == null) continue;
                for (int slot = 0; slot < SlotCount; slot++)
                {
                    int index = GetBindingIndex(other, aIsGamepad, slot);
                    if (index < 0 || (other == aAction && index == aBindingIndex)) continue;
                    if (!string.Equals(other.bindings[index].effectivePath, newPath, StringComparison.OrdinalIgnoreCase)) continue;
                    other.ApplyBindingOverride(index, aOldPath ?? string.Empty);
                }
            }
        }

        /* ヘルパー */

        private static bool HasGroup(InputBinding aBinding, string aGroup)
        {
            if (string.IsNullOrEmpty(aBinding.groups)) return false;
            foreach (var group in aBinding.groups.Split(InputBinding.Separator))
            {
                if (group == aGroup) return true;
            }
            return false;
        }
    }
}
