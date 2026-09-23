using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace Pc_clicker.func
{
    /// <summary>
    /// 目标类型：显示器（屏幕）或窗口
    /// </summary>
    public enum TargetKind
    {
        Screen,
        Window
    }

    /// <summary>
    /// 可供选择的目标：一块显示器或一个窗口
    /// </summary>
    public class TargetItem
    {
        public TargetKind Kind { get; set; }

        /// <summary>
        /// 脚本首行使用的目标名称：窗口项为进程文件名（如 notepad.exe），屏幕项为 “屏幕x”。
        /// 进程文件名不会像窗口标题那样频繁变化，因此用它做校验依据。
        /// </summary>
        public string Title { get; set; }

        /// <summary>窗口句柄；屏幕项为 IntPtr.Zero</summary>
        public IntPtr Handle { get; set; }

        /// <summary>显示器左上角屏幕坐标（仅屏幕项有效）</summary>
        public int ScreenLeft { get; set; }
        public int ScreenTop { get; set; }
        public int ScreenWidth { get; set; }
        public int ScreenHeight { get; set; }

        /// <summary>下拉框中显示的文字</summary>
        public string Display { get; set; }

        /// <summary>下拉框提示文字（窗口标题或显示器信息）</summary>
        public string Detail { get; set; }

        public override string ToString()
        {
            return Display ?? Title ?? base.ToString();
        }
    }

    /// <summary>
    /// 枚举当前系统中可以点击的目标：先显示器（屏幕x），再所有可见的用户窗口。
    /// </summary>
    public static class TargetEnumerator
    {
        public static List<TargetItem> Enumerate()
        {
            var result = new List<TargetItem>();
            result.AddRange(EnumerateScreens());
            result.AddRange(EnumerateWindows());
            return result;
        }

        /// <summary>
        /// 枚举所有已连接的显示器，记为 屏幕1、屏幕2 ...
        /// </summary>
        public static List<TargetItem> EnumerateScreens()
        {
            var monitors = new List<NativeMethods.MONITORINFO>();

            NativeMethods.MonitorEnumProc callback = delegate(IntPtr hMonitor, IntPtr hdc, ref NativeMethods.RECT rect, IntPtr data)
            {
                var info = new NativeMethods.MONITORINFO();
                info.cbSize = Marshal.SizeOf(typeof(NativeMethods.MONITORINFO));
                if (NativeMethods.GetMonitorInfo(hMonitor, ref info))
                    monitors.Add(info);
                return true;
            };

            NativeMethods.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, callback, IntPtr.Zero);

            // 按位置排序，保证每次枚举顺序一致
            monitors.Sort(delegate(NativeMethods.MONITORINFO a, NativeMethods.MONITORINFO b)
            {
                if (a.rcMonitor.Left != b.rcMonitor.Left)
                    return a.rcMonitor.Left.CompareTo(b.rcMonitor.Left);
                return a.rcMonitor.Top.CompareTo(b.rcMonitor.Top);
            });

            var screens = new List<TargetItem>();
            for (int i = 0; i < monitors.Count; i++)
            {
                var m = monitors[i];
                int width = m.rcMonitor.Right - m.rcMonitor.Left;
                int height = m.rcMonitor.Bottom - m.rcMonitor.Top;
                int index = i + 1;

                screens.Add(new TargetItem
                {
                    Kind = TargetKind.Screen,
                    Title = "屏幕" + index,
                    Handle = IntPtr.Zero,
                    ScreenLeft = m.rcMonitor.Left,
                    ScreenTop = m.rcMonitor.Top,
                    ScreenWidth = width,
                    ScreenHeight = height,
                    Display = string.Format("屏幕{0} ({1}×{2})", index, width, height),
                    Detail = string.Format("显示器{0}：左上角({1},{2})，分辨率{3}×{4}",
                        index, m.rcMonitor.Left, m.rcMonitor.Top, width, height)
                });
            }

            if (screens.Count == 0)
            {
                // 极端情况下枚举不到显示器，至少保证有一项可用
                screens.Add(new TargetItem
                {
                    Kind = TargetKind.Screen,
                    Title = "屏幕1",
                    Display = "屏幕1",
                    Detail = "显示器1"
                });
            }

            return screens;
        }

        /// <summary>
        /// 枚举所有带标题的可见窗口（即“用户进程”窗口）
        /// </summary>
        public static List<TargetItem> EnumerateWindows()
        {
            var windows = new List<TargetItem>();
            IntPtr shellWindow = NativeMethods.GetShellWindow();
            uint selfPid = (uint)Process.GetCurrentProcess().Id;

            NativeMethods.EnumWindowsProc callback = delegate(IntPtr hWnd, IntPtr lParam)
            {
                if (hWnd == shellWindow) return true;
                if (!NativeMethods.IsWindowVisible(hWnd)) return true;

                int length = NativeMethods.GetWindowTextLength(hWnd);
                if (length <= 0) return true;

                var buffer = new StringBuilder(length + 2);
                NativeMethods.GetWindowText(hWnd, buffer, buffer.Capacity);
                string title = buffer.ToString().Trim();
                if (title.Length == 0) return true;

                uint pid;
                NativeMethods.GetWindowThreadProcessId(hWnd, out pid);
                if (pid == 0 || pid == selfPid) return true; // 排除自身窗口（主窗口、浮窗）

                string processName = GetProcessFileName(pid);
                if (string.IsNullOrEmpty(processName)) processName = "未知进程";

                windows.Add(new TargetItem
                {
                    Kind = TargetKind.Window,
                    Title = processName,
                    Handle = hWnd,
                    Display = string.Format("{0}  [0x{1:X}]", processName, hWnd.ToInt64()),
                    Detail = "窗口标题：" + title
                });

                return true;
            };

            NativeMethods.EnumWindows(callback, IntPtr.Zero);

            // 按进程名排序，同一进程的多个窗口按窗口标题排
            windows.Sort(delegate(TargetItem a, TargetItem b)
            {
                int byName = string.Compare(a.Title, b.Title, StringComparison.OrdinalIgnoreCase);
                if (byName != 0) return byName;
                return string.Compare(a.Detail, b.Detail, StringComparison.OrdinalIgnoreCase);
            });

            return windows;
        }

        /// <summary>
        /// 取进程文件名（如 notepad.exe）。64 位系统上读取其它进程的主模块可能失败，
        /// 此时退回进程名并补上 .exe。
        /// </summary>
        private static string GetProcessFileName(uint pid)
        {
            try
            {
                using (Process process = Process.GetProcessById((int)pid))
                {
                    try
                    {
                        string path = process.MainModule == null ? null : process.MainModule.FileName;
                        if (!string.IsNullOrEmpty(path))
                            return System.IO.Path.GetFileName(path);
                    }
                    catch
                    {
                        // 跨位数或权限不足时读取主模块会失败
                    }

                    return process.ProcessName + ".exe";
                }
            }
            catch
            {
                return null;
            }
        }
    }
}
