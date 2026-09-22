using System;
using System.Collections.Generic;
using System.Threading;

namespace Pc_clicker.func
{
    /// <summary>
    /// 鼠标按键类型
    /// </summary>
    public enum MouseButtonType
    {
        Left = 0,
        Right = 1,
        Middle = 2,
        SideFront = 3,  // XButton2 (Forward)
        SideBack = 4    // XButton1 (Back)
    }

    /// <summary>
    /// 鼠标/键盘/等待 输入模拟
    /// </summary>
    internal static class InputSimulator
    {
        #region 鼠标

        /// <summary>
        /// 执行一次鼠标点击。
        /// screenX/screenY 为屏幕绝对坐标；当两者都为 -1 时在当前位置点击（不移动鼠标）。
        /// </summary>
        public static void MouseClick(MouseButtonType button, int screenX, int screenY)
        {
            if (screenX != -1 || screenY != -1)
            {
                NativeMethods.SetCursorPos(screenX, screenY);
                Thread.Sleep(10);
            }

            uint downFlag, upFlag;
            uint mouseData = 0;

            switch (button)
            {
                case MouseButtonType.Left:
                    downFlag = NativeMethods.MOUSEEVENTF_LEFTDOWN;
                    upFlag = NativeMethods.MOUSEEVENTF_LEFTUP;
                    break;
                case MouseButtonType.Right:
                    downFlag = NativeMethods.MOUSEEVENTF_RIGHTDOWN;
                    upFlag = NativeMethods.MOUSEEVENTF_RIGHTUP;
                    break;
                case MouseButtonType.Middle:
                    downFlag = NativeMethods.MOUSEEVENTF_MIDDLEDOWN;
                    upFlag = NativeMethods.MOUSEEVENTF_MIDDLEUP;
                    break;
                case MouseButtonType.SideFront:
                    downFlag = NativeMethods.MOUSEEVENTF_XDOWN;
                    upFlag = NativeMethods.MOUSEEVENTF_XUP;
                    mouseData = NativeMethods.XBUTTON2;
                    break;
                case MouseButtonType.SideBack:
                    downFlag = NativeMethods.MOUSEEVENTF_XDOWN;
                    upFlag = NativeMethods.MOUSEEVENTF_XUP;
                    mouseData = NativeMethods.XBUTTON1;
                    break;
                default:
                    return;
            }

            SendMouse(downFlag, mouseData);
            Thread.Sleep(20);
            SendMouse(upFlag, mouseData);
        }

        private static void SendMouse(uint flags, uint mouseData)
        {
            var input = new NativeMethods.INPUT
            {
                type = NativeMethods.INPUT_MOUSE,
                u = new NativeMethods.INPUTUNION
                {
                    mi = new NativeMethods.MOUSEINPUT
                    {
                        dx = 0,
                        dy = 0,
                        mouseData = mouseData,
                        dwFlags = flags,
                        time = 0,
                        dwExtraInfo = IntPtr.Zero
                    }
                }
            };
            NativeMethods.SendInput(1, new[] { input }, System.Runtime.InteropServices.Marshal.SizeOf(typeof(NativeMethods.INPUT)));
        }

        #endregion

        #region 键盘

        // 按键名称 -> Virtual-Key Code
        private static readonly Dictionary<string, ushort> KeyMap = new Dictionary<string, ushort>(StringComparer.OrdinalIgnoreCase)
        {
            { "ctrl", 0x11 }, { "control", 0x11 },
            { "alt", 0x12 }, { "menu", 0x12 },
            { "shift", 0x10 },
            { "win", 0x5B }, { "lwin", 0x5B },
            { "tab", 0x09 }, { "enter", 0x0D }, { "return", 0x0D },
            { "esc", 0x1B }, { "escape", 0x1B },
            { "space", 0x20 },
            { "backspace", 0x08 }, { "back", 0x08 },
            { "del", 0x2E }, { "delete", 0x2E },
            { "ins", 0x2D }, { "insert", 0x2D },
            { "home", 0x24 }, { "end", 0x23 },
            { "pageup", 0x21 }, { "pagedown", 0x22 },
            { "up", 0x26 }, { "down", 0x28 }, { "left", 0x25 }, { "right", 0x27 },
            { "capslock", 0x14 }, { "numlock", 0x90 }, { "scrolllock", 0x91 },
            { "printscreen", 0x2C },
            { "f1", 0x70 }, { "f2", 0x71 }, { "f3", 0x72 }, { "f4", 0x73 },
            { "f5", 0x74 }, { "f6", 0x75 }, { "f7", 0x76 }, { "f8", 0x77 },
            { "f9", 0x78 }, { "f10", 0x79 }, { "f11", 0x7A }, { "f12", 0x7B },
            { "num0", 0x60 }, { "num1", 0x61 }, { "num2", 0x62 }, { "num3", 0x63 },
            { "num4", 0x64 }, { "num5", 0x65 }, { "num6", 0x66 }, { "num7", 0x67 },
            { "num8", 0x68 }, { "num9", 0x69 },
        };

        /// <summary>
        /// 解析按键表达式（如 ctrl+alt+del、shift+a、win+r、menu），返回虚拟键码列表。
        /// 解析失败返回 null。
        /// </summary>
        public static List<ushort> ParseKeys(string expr)
        {
            var result = new List<ushort>();
            if (string.IsNullOrWhiteSpace(expr)) return null;

            foreach (var raw in expr.Split('+'))
            {
                var token = raw.Trim();
                if (token.Length == 0) return null;

                ushort vk;
                if (KeyMap.TryGetValue(token, out vk))
                {
                    result.Add(vk);
                }
                else if (token.Length == 1)
                {
                    char c = char.ToUpperInvariant(token[0]);
                    if ((c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9'))
                        result.Add(c);
                    else
                        return null;
                }
                else
                {
                    return null;
                }
            }
            return result.Count > 0 ? result : null;
        }

        /// <summary>
        /// 按下并释放一组按键（组合键）。
        /// </summary>
        public static void KeyPress(List<ushort> keys)
        {
            if (keys == null || keys.Count == 0) return;

            // 依次按下
            foreach (var k in keys) SendKey(k, false);
            Thread.Sleep(20);
            // 逆序释放
            for (int i = keys.Count - 1; i >= 0; i--) SendKey(keys[i], true);
        }

        private static void SendKey(ushort vk, bool keyUp)
        {
            var input = new NativeMethods.INPUT
            {
                type = NativeMethods.INPUT_KEYBOARD,
                u = new NativeMethods.INPUTUNION
                {
                    ki = new NativeMethods.KEYBDINPUT
                    {
                        wVk = vk,
                        wScan = 0,
                        dwFlags = keyUp ? NativeMethods.KEYEVENTF_KEYUP : 0,
                        time = 0,
                        dwExtraInfo = IntPtr.Zero
                    }
                }
            };
            NativeMethods.SendInput(1, new[] { input }, System.Runtime.InteropServices.Marshal.SizeOf(typeof(NativeMethods.INPUT)));
        }

        #endregion
    }
}
