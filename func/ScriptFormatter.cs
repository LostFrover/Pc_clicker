using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Pc_clicker.func
{
    /// <summary>
    /// 脚本行与界面文字之间的转换：把脚本里的指令转换成人类可读的描述。
    /// 所有由代码生成的界面文字都集中在这里，后续做多语言时替换本类即可。
    /// </summary>
    public static class ScriptFormatter
    {
        #region 界面文字

        private const string TargetPrefix = "目标：";
        private const string EmptyTarget = "（未填写）";
        private const string WaitText = "等待 {0} 毫秒";
        private const string MouseClickText = "点击{0} ({1})";
        private const string MouseCurrentPosition = "鼠标当前位置";
        private const string KeyText = "按键 {0}";
        private const string ScreenDisplayName = "屏幕";
        private const string ScreenScriptName = "screen";

        /// <summary>按键编号对应的名称（与 mouse 指令的按键编号一致）</summary>
        private static readonly string[] MouseButtonNames =
        {
            "左键", "右键", "中键", "前侧键", "后侧键"
        };

        /// <summary>按键名称的美化显示表</summary>
        private static readonly Dictionary<string, string> KeyDisplayNames =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "ctrl", "Ctrl" }, { "control", "Ctrl" }, { "lctrl", "左Ctrl" },
            { "alt", "Alt" }, { "lalt", "左Alt" },
            { "shift", "Shift" }, { "lshift", "左Shift" },
            { "win", "Win" }, { "lwin", "左Win" },
            { "rctrl", "右Ctrl" }, { "ralt", "右Alt" }, { "rshift", "右Shift" }, { "rwin", "右Win" },
            { "menu", "菜单键" }, { "apps", "菜单键" }, { "contextmenu", "菜单键" },
            { "enter", "回车" }, { "return", "回车" }, { "numenter", "小键盘回车" },
            { "space", "空格" }, { "tab", "Tab" }, { "esc", "Esc" }, { "escape", "Esc" },
            { "backspace", "退格" }, { "back", "退格" },
            { "del", "Delete" }, { "delete", "Delete" }, { "ins", "Insert" }, { "insert", "Insert" },
            { "home", "Home" }, { "end", "End" }, { "pageup", "PageUp" }, { "pagedown", "PageDown" },
            { "up", "↑" }, { "down", "↓" }, { "left", "←" }, { "right", "→" },
            { "capslock", "大写锁定" }, { "numlock", "数字锁定" }, { "scrolllock", "滚动锁定" },
            { "printscreen", "PrintScreen" }, { "pause", "Pause" }, { "break", "Pause" },
            { "numadd", "小键盘+" }, { "numplus", "小键盘+" },
            { "numsubtract", "小键盘-" }, { "numminus", "小键盘-" },
            { "nummultiply", "小键盘*" }, { "numstar", "小键盘*" },
            { "numdivide", "小键盘/" }, { "numslash", "小键盘/" },
            { "numdecimal", "小键盘." }, { "numdot", "小键盘." }, { "numperiod", "小键盘." },
            { "numseparator", "小键盘分隔符" }, { "numclear", "小键盘清除" },
            { "volumeup", "音量+" }, { "volup", "音量+" },
            { "volumedown", "音量-" }, { "voldown", "音量-" },
            { "volumemute", "静音" }, { "volmute", "静音" }, { "mute", "静音" },
            { "medianext", "快进" }, { "nexttrack", "快进" }, { "next", "快进" },
            { "mediaprev", "快退" }, { "prevtrack", "快退" }, { "prev", "快退" },
            { "mediastop", "停止播放" }, { "mediaplaypause", "播放/暂停" },
            { "playpause", "播放/暂停" }, { "play", "播放/暂停" },
            { "launchmedia", "打开媒体" }, { "mediaselect", "打开媒体" },
            { "browserback", "浏览器后退" }, { "browserforward", "浏览器前进" },
            { "browserrefresh", "浏览器刷新" }, { "browserstop", "浏览器停止" },
            { "browsersearch", "浏览器搜索" }, { "browserfavorites", "浏览器收藏夹" },
            { "browserhome", "浏览器主页" },
            { "launchmail", "打开邮件" }, { "launchapp1", "应用键1" }, { "launchapp2", "应用键2" },
            { "sleep", "睡眠" },
        };

        #endregion

        /// <summary>
        /// 目标名称的界面写法：脚本里的 screen1 显示为「屏幕1」，其它（进程文件名）原样显示。
        /// </summary>
        public static string DisplayTargetName(string targetName)
        {
            if (string.IsNullOrEmpty(targetName)) return string.Empty;

            string name = targetName.Trim();
            if (name.Length > ScreenScriptName.Length &&
                name.StartsWith(ScreenScriptName, StringComparison.OrdinalIgnoreCase))
            {
                string index = name.Substring(ScreenScriptName.Length);
                int number;
                if (int.TryParse(index, out number))
                    return ScreenDisplayName + number.ToString(CultureInfo.InvariantCulture);
            }

            return name;
        }

        /// <summary>
        /// 脚本首行的界面写法：目标：xxx
        /// </summary>
        public static string FormatTargetLine(string targetName)
        {
            string name = DisplayTargetName(targetName);
            return TargetPrefix + (string.IsNullOrEmpty(name) ? EmptyTarget : name);
        }

        /// <summary>
        /// 把一行脚本转换成人类可读的描述。
        /// 空行返回 null（调用方跳过）；无法识别的行原样返回，方便对照修改。
        /// </summary>
        public static string FormatLine(string line)
        {
            if (line == null) return null;

            string text = line.Trim();
            if (text.Length == 0) return null;

            string error;
            ScriptAction action = ScriptEngine.ParseLine(text, 0, out error);
            if (error != null || action == null) return text;

            switch (action.Type)
            {
                case "mouse":
                    return FormatMouse(action);
                case "key":
                    return string.Format(KeyText, FormatKeyExpression(GetArgument(text)));
                case "wait":
                    return string.Format(WaitText, action.DelayMs);
                default:
                    return text;
            }
        }

        private static string FormatMouse(ScriptAction action)
        {
            int index = (int)action.Button;
            string button = index >= 0 && index < MouseButtonNames.Length
                ? MouseButtonNames[index]
                : index.ToString(CultureInfo.InvariantCulture);

            string position;
            if (action.X == -1 && action.Y == -1)
            {
                position = MouseCurrentPosition;
            }
            else
            {
                position = string.Format(CultureInfo.InvariantCulture, "{0}%, {1}%",
                    (action.X * 100).ToString("0.#", CultureInfo.InvariantCulture),
                    (action.Y * 100).ToString("0.#", CultureInfo.InvariantCulture));
            }

            return string.Format(MouseClickText, button, position);
        }

        /// <summary>取指令后面的参数部分（脚本行的第一个空格之后）</summary>
        private static string GetArgument(string line)
        {
            int space = line.IndexOf(' ');
            return space < 0 ? string.Empty : line.Substring(space + 1).Trim();
        }

        /// <summary>
        /// 美化按键表达式：ctrl+a -> Ctrl+A
        /// </summary>
        private static string FormatKeyExpression(string expression)
        {
            if (string.IsNullOrEmpty(expression)) return expression;

            var builder = new StringBuilder();
            foreach (string raw in expression.Split('+'))
            {
                string token = raw.Trim();
                if (token.Length == 0) continue;

                if (builder.Length > 0) builder.Append('+');
                builder.Append(FormatKeyToken(token));
            }

            return builder.Length > 0 ? builder.ToString() : expression;
        }

        private static string FormatKeyToken(string token)
        {
            string name;
            if (KeyDisplayNames.TryGetValue(token, out name)) return name;

            // 字母键、数字键
            if (token.Length == 1 && char.IsLetterOrDigit(token[0]))
                return token.ToUpperInvariant();

            // 小键盘数字 num0 ~ num9
            if (token.Length == 4 && token.StartsWith("num", StringComparison.OrdinalIgnoreCase))
            {
                int number;
                if (int.TryParse(token.Substring(3), out number)) return "小键盘" + number;
            }

            // 功能键 f1 ~ f12
            if (token.Length >= 2 && (token[0] == 'f' || token[0] == 'F'))
            {
                int number;
                if (int.TryParse(token.Substring(1), out number) && number >= 1 && number <= 24)
                    return "F" + number;
            }

            return token;
        }
    }
}
