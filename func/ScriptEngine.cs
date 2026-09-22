using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Pc_clicker.func
{
    /// <summary>
    /// 脚本中的一条操作
    /// </summary>
    public class ScriptAction
    {
        public string Type;            // mouse / key / wait
        public MouseButtonType Button; // mouse 有效
        public int X;                  // mouse 有效，相对坐标
        public int Y;
        public List<ushort> Keys;      // key 有效
        public int DelayMs;            // wait 有效
    }

    /// <summary>
    /// 脚本解析与校验
    /// </summary>
    public class ScriptEngine
    {
        /// <summary>脚本首行记录的目标窗口标题</summary>
        public string TargetTitle { get; private set; }

        public List<ScriptAction> Actions { get; private set; } = new List<ScriptAction>();

        /// <summary>
        /// 创建一个空脚本（用于单点循环等在界面类里预设参数的情况）。
        /// </summary>
        public static ScriptEngine CreateEmpty(string targetTitle)
        {
            return new ScriptEngine { TargetTitle = targetTitle };
        }

        /// <summary>
        /// 读取脚本文件的所有行。自动识别 UTF-8/UTF-16 BOM，
        /// 无 BOM 时优先按 UTF-8（严格）解析，失败则退回系统默认编码（如 GBK）。
        /// </summary>
        public static string[] ReadAllLines(string filePath)
        {
            byte[] bytes = File.ReadAllBytes(filePath);

            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
                return SplitLines(Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3));

            if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
                return SplitLines(Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2));

            if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
                return SplitLines(Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2));

            try
            {
                // 严格模式：遇到非法 UTF-8 字节会抛异常，从而回退到默认编码
                var strictUtf8 = new UTF8Encoding(false, true);
                return SplitLines(strictUtf8.GetString(bytes));
            }
            catch (DecoderFallbackException)
            {
                return SplitLines(Encoding.Default.GetString(bytes));
            }
        }

        private static string[] SplitLines(string text)
        {
            return text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        }

        /// <summary>
        /// 从文件加载脚本（不做目标校验），返回错误信息（null 表示成功）。
        /// </summary>
        public string Load(string filePath)
        {
            Actions.Clear();
            TargetTitle = null;

            if (!File.Exists(filePath))
                return "脚本文件不存在: " + filePath;

            string[] lines;
            try
            {
                lines = ReadAllLines(filePath);
            }
            catch (Exception ex)
            {
                return "读取脚本失败: " + ex.Message;
            }

            return ParseLines(lines);
        }

        /// <summary>
        /// 解析文本行（首行为窗口标题）。
        /// </summary>
        public string ParseLines(string[] lines)
        {
            Actions.Clear();
            TargetTitle = null;

            if (lines == null || lines.Length == 0)
                return "脚本为空";

            TargetTitle = (lines[0] ?? string.Empty).Trim();

            for (int i = 1; i < lines.Length; i++)
            {
                string line = (lines[i] ?? string.Empty).Trim();
                if (line.Length == 0) continue; // 跳过空行

                string err;
                var action = ParseLine(line, i + 1, out err);
                if (err != null) return err;
                Actions.Add(action);
            }

            if (Actions.Count == 0)
                return TargetTitle.Length == 0 ? "脚本为空" : "脚本未包含任何操作";

            return null;
        }

        /// <summary>
        /// 校验脚本首行窗口标题与当前选择的目标是否一致。
        /// targetTitle 为当前选择的目标窗口标题（屏幕项则跳过校验）。
        /// </summary>
        public string ValidateTarget(string targetTitle)
        {
            if (string.IsNullOrEmpty(targetTitle)) return null; // 未选择目标时不校验
            if (!string.Equals(TargetTitle, targetTitle, StringComparison.Ordinal))
                return string.Format("脚本首行 \"{0}\" 与当前选择的目标 \"{1}\" 不一致，\n请修改脚本首行后再执行。",
                    TargetTitle, targetTitle);
            return null;
        }

        private ScriptAction ParseLine(string line, int lineNo, out string error)
        {
            error = null;
            // 结构: 指令类型 + 空格 + 参数
            int sp = line.IndexOf(' ');
            string cmd = sp < 0 ? line : line.Substring(0, sp);
            string arg = sp < 0 ? string.Empty : line.Substring(sp + 1).Trim();

            switch (cmd.ToLowerInvariant())
            {
                case "mouse":
                    return ParseMouse(arg, lineNo, out error);
                case "key":
                    return ParseKey(arg, lineNo, out error);
                case "wait":
                    return ParseWait(arg, lineNo, out error);
                default:
                    error = string.Format("第 {0} 行：未知指令类型 \"{1}\"（应为 mouse/key/wait）", lineNo, cmd);
                    return null;
            }
        }

        private ScriptAction ParseMouse(string arg, int lineNo, out string error)
        {
            error = null;
            // 参数: 按键类型 x y
            var parts = arg.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 3)
            {
                error = string.Format("第 {0} 行：mouse 参数应为 \"按键类型 x y\"（3 个值）", lineNo);
                return null;
            }

            int btn, x, y;
            if (!int.TryParse(parts[0], out btn) || btn < 0 || btn > 4)
            {
                error = string.Format("第 {0} 行：mouse 按键类型应为 0-4（左0 右1 中2 侧键3/4）", lineNo);
                return null;
            }
            if (!int.TryParse(parts[1], out x) || !int.TryParse(parts[2], out y))
            {
                error = string.Format("第 {0} 行：mouse 坐标应为整数（-1 -1 表示当前位置）", lineNo);
                return null;
            }

            return new ScriptAction
            {
                Type = "mouse",
                Button = (MouseButtonType)btn,
                X = x,
                Y = y
            };
        }

        private ScriptAction ParseKey(string arg, int lineNo, out string error)
        {
            error = null;
            var keys = InputSimulator.ParseKeys(arg);
            if (keys == null)
            {
                error = string.Format("第 {0} 行：key 按键 \"{1}\" 无法识别（如 ctrl+alt+del、shift+a、win+r）", lineNo, arg);
                return null;
            }
            return new ScriptAction { Type = "key", Keys = keys };
        }

        private ScriptAction ParseWait(string arg, int lineNo, out string error)
        {
            error = null;
            int ms;
            if (!int.TryParse(arg, out ms) || ms < 0)
            {
                error = string.Format("第 {0} 行：wait 参数应为非负整数毫秒", lineNo);
                return null;
            }
            return new ScriptAction { Type = "wait", DelayMs = ms };
        }
    }
}
