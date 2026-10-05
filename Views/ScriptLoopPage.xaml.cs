using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Pc_clicker.func;

namespace Pc_clicker.Views
{
    /// <summary>
    /// 预编辑脚本循环界面：选择目标程序、管理脚本文件、查看脚本内容
    /// </summary>
    public partial class ScriptLoopPage : UserControl
    {
        private bool _reloading;

        public ScriptLoopPage()
        {
            InitializeComponent();

            EnsureScriptFolder();
            ReloadScriptList(true);
            UpdateTargetHint();
        }

        private static string _scriptFolder;

        /// <summary>
        /// 脚本文件所在文件夹（程序目录下的 scripts；
        /// 若程序目录不可写则退回到用户目录，避免在只读目录下启动失败）
        /// </summary>
        public static string ScriptFolder
        {
            get
            {
                if (_scriptFolder != null) return _scriptFolder;

                string primary = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "scripts");
                try
                {
                    Directory.CreateDirectory(primary);
                    _scriptFolder = primary;
                }
                catch
                {
                    string fallback = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "Pc_clicker", "scripts");
                    try { Directory.CreateDirectory(fallback); }
                    catch { }
                    _scriptFolder = fallback;
                }

                return _scriptFolder;
            }
        }

        /// <summary>当前选择的目标（窗口或屏幕），未选择时为 null</summary>
        public TargetItem SelectedTarget
        {
            get { return comboTarget.SelectedItem as TargetItem; }
        }

        /// <summary>当前选择的脚本文件名</summary>
        public string SelectedScriptName
        {
            get { return comboScript.SelectedItem as string; }
        }

        /// <summary>当前选择的脚本完整路径，未选择时为 null</summary>
        public string SelectedScriptPath
        {
            get
            {
                string name = SelectedScriptName;
                return string.IsNullOrEmpty(name) ? null : Path.Combine(ScriptFolder, name);
            }
        }

        /// <summary>ListView 中的一行（用于把目标行加粗显示）</summary>
        private class ScriptLineItem
        {
            public string Text { get; set; }
            public bool IsTargetLine { get; set; }

            // 让列表项的自动化名称（读屏、UI 自动化）显示真正的文字
            public override string ToString()
            {
                return Text;
            }
        }

        /// <summary>脚本文件所在文件夹是否存在，不存在则创建</summary>
        public static void EnsureScriptFolder()
        {
            try
            {
                string folder = ScriptFolder;
                if (!Directory.Exists(folder))
                    Directory.CreateDirectory(folder);
            }
            catch
            {
                // 目录不可用时由后续具体操作给出提示
            }
        }

        #region 目标程序

        /// <summary>
        /// 下拉时再重新查找系统中正在运行的窗口，以及所有显示器
        /// </summary>
        private void ComboTarget_DropDownOpened(object sender, EventArgs e)
        {
            ReloadTargets();
        }

        private void ComboTarget_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateTargetHint();
        }

        /// <summary>重新枚举目标（屏幕 + 窗口），尽量保留原选择</summary>
        public void ReloadTargets()
        {
            string previousIdentity = GetTargetIdentity(SelectedTarget);

            comboTarget.Items.Clear();
            foreach (TargetItem item in TargetEnumerator.Enumerate())
                comboTarget.Items.Add(item);

            if (previousIdentity != null)
            {
                foreach (TargetItem item in comboTarget.Items)
                {
                    if (GetTargetIdentity(item) == previousIdentity)
                    {
                        comboTarget.SelectedItem = item;
                        break;
                    }
                }
            }

            UpdateTargetHint();
        }

        /// <summary>
        /// 目标的唯一标识：同一进程可能有多个窗口，必须带上窗口句柄才能区分。
        /// </summary>
        private static string GetTargetIdentity(TargetItem item)
        {
            return item == null ? null : item.Kind + "|" + item.Title + "|" + item.Handle.ToInt64();
        }

        private void UpdateTargetHint()
        {
            string header = ReadScriptHeader();
            TargetItem target = SelectedTarget;

            if (target == null)
            {
                targetHint.Foreground = Brushes.DimGray;
                targetHint.Text = "请选择目标程序或屏幕；脚本首行需与所选目标名称一致。";
                return;
            }

            if (header == null)
            {
                targetHint.Foreground = Brushes.DimGray;
                targetHint.Text = "脚本首行需与所选目标一致：" + target.Title;
                return;
            }

            if (string.Equals(header, target.Title, StringComparison.OrdinalIgnoreCase))
            {
                targetHint.Foreground = Brushes.Green;
                targetHint.Text = "脚本首行：" + header + "（与所选目标一致）";
            }
            else
            {
                targetHint.Foreground = Brushes.Firebrick;
                targetHint.Text = "脚本首行：" + header + "（与所选目标「" + target.Title + "」不一致，执行前请修改）";
            }
        }

        #endregion

        #region 脚本文件

        /// <summary>
        /// 下拉时再读取脚本文件夹下的所有脚本文件
        /// </summary>
        private void ComboScript_DropDownOpened(object sender, EventArgs e)
        {
            ReloadScriptList(false);
        }

        private void ComboScript_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_reloading) return;

            // 读取配置文件后，自动匹配运行中的目标程序
            AutoSelectTargetForScript();

            ShowScriptContent();
            UpdateTargetHint();
        }

        /// <summary>
        /// 读取脚本首行的目标名称，在正在运行的目标中匹配并选中；
        /// 匹配不到（程序没在运行、名称写错等）则选中鼠标当前所在的屏幕。
        /// 未选择脚本文件时不改变当前选择。
        /// </summary>
        public void AutoSelectTargetForScript()
        {
            string header = ReadScriptHeader();

            // 没有读取到脚本（没选文件）时保持现状
            if (string.IsNullOrEmpty(SelectedScriptPath)) return;

            // 记录当前选择，若它本来就和脚本首行同名则继续沿用（同一进程多个窗口时保留用户的选择）
            TargetItem previous = SelectedTarget;
            string previousIdentity = GetTargetIdentity(previous);

            // 先枚举一次目标（屏幕在前，窗口在后）
            comboTarget.Items.Clear();
            foreach (TargetItem item in TargetEnumerator.Enumerate())
                comboTarget.Items.Add(item);

            TargetItem matched = null;

            if (!string.IsNullOrEmpty(header))
                matched = FindTargetByTitle(header, previousIdentity);

            // 没有匹配到任何目标时，退回鼠标当前所在的屏幕
            if (matched == null)
                matched = FindScreenUnderMouse();

            if (matched != null && !ReferenceEquals(comboTarget.SelectedItem, matched))
                comboTarget.SelectedItem = matched;
        }

        /// <summary>
        /// 按名称（进程文件名或 screenX）查找目标；同名项中优先返回原来选中的那一个。
        /// </summary>
        private TargetItem FindTargetByTitle(string title, string previousIdentity)
        {
            TargetItem fallback = null;

            foreach (TargetItem item in comboTarget.Items)
            {
                if (string.IsNullOrEmpty(item.Title)) continue;
                if (!string.Equals(item.Title, title, StringComparison.OrdinalIgnoreCase)) continue;

                if (!string.IsNullOrEmpty(previousIdentity) &&
                    GetTargetIdentity(item) == previousIdentity)
                    return item;

                if (fallback == null) fallback = item;
            }

            return fallback;
        }

        /// <summary>找到鼠标当前所在的显示器项；找不到时退回第一块屏幕</summary>
        private TargetItem FindScreenUnderMouse()
        {
            NativeMethods.POINT pt;
            TargetItem firstScreen = null;

            if (NativeMethods.GetCursorPos(out pt))
            {
                foreach (TargetItem item in comboTarget.Items)
                {
                    if (item.Kind != TargetKind.Screen) continue;
                    if (firstScreen == null) firstScreen = item;

                    if (pt.X >= item.ScreenLeft && pt.X < item.ScreenLeft + item.ScreenWidth &&
                        pt.Y >= item.ScreenTop && pt.Y < item.ScreenTop + item.ScreenHeight)
                        return item;
                }
            }

            // 保底：返回第一块屏幕，没有屏幕则保持现状
            if (firstScreen != null) return firstScreen;

            foreach (TargetItem item in comboTarget.Items)
                if (item.Kind == TargetKind.Screen) return item;

            return null;
        }

        /// <summary>
        /// 重新读取脚本文件夹下的文件列表。
        /// </summary>
        /// <param name="selectFirst">没有保留原选择时，是否默认选中第一个</param>
        public void ReloadScriptList(bool selectFirst)
        {
            EnsureScriptFolder();

            string previous = SelectedScriptName;

            _reloading = true;
            try
            {
                comboScript.Items.Clear();
                foreach (string name in ListScriptFiles())
                    comboScript.Items.Add(name);

                if (previous != null && comboScript.Items.Contains(previous))
                    comboScript.SelectedItem = previous;
                else if (selectFirst && comboScript.Items.Count > 0)
                    comboScript.SelectedItem = FirstScriptItem();
            }
            finally
            {
                _reloading = false;
            }

            // 脚本（重新）选定后，按脚本首行自动匹配目标
            AutoSelectTargetForScript();

            ShowScriptContent();
            UpdateTargetHint();
        }

        private object FirstScriptItem()
        {
            return comboScript.Items.Count > 0 ? comboScript.Items[0] : null;
        }

        private static List<string> ListScriptFiles()
        {
            var names = new List<string>();
            if (!Directory.Exists(ScriptFolder)) return names;

            foreach (string path in Directory.GetFiles(ScriptFolder, "*.txt"))
                names.Add(Path.GetFileName(path));

            names.Sort(StringComparer.OrdinalIgnoreCase);
            return names;
        }

        /// <summary>把当前脚本内容显示到 listview 中</summary>
        public void ShowScriptContent()
        {
            listScript.Items.Clear();

            string path = SelectedScriptPath;
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;

            try
            {
                string[] lines = ScriptEngine.ReadAllLines(path);
                if (lines.Length == 0) return;

                // 首行为目标行（加粗），其余行转换成人类可读的描述（空行不显示）
                listScript.Items.Add(new ScriptLineItem
                {
                    Text = ScriptFormatter.FormatTargetLine((lines[0] ?? string.Empty).Trim()),
                    IsTargetLine = true
                });

                for (int i = 1; i < lines.Length; i++)
                {
                    string text = ScriptFormatter.FormatLine(lines[i]);
                    if (text != null)
                        listScript.Items.Add(new ScriptLineItem { Text = text, IsTargetLine = false });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("读取脚本失败：" + ex.Message, "错误",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private string ReadScriptHeader()
        {
            string path = SelectedScriptPath;
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;

            try
            {
                string[] lines = ScriptEngine.ReadAllLines(path);
                return lines.Length == 0 ? string.Empty : (lines[0] ?? string.Empty).Trim();
            }
            catch
            {
                return null;
            }
        }

        #endregion

        #region 脚本文件操作

        private void btnAddScript_Click(object sender, RoutedEventArgs e)
        {
            EnsureScriptFolder();

            string name;
            if (!InputDialog.Show(Window.GetWindow(this), "新建脚本", "请输入脚本文件名：", "脚本1", out name))
                return;

            if (name.Length == 0)
            {
                MessageBox.Show("文件名不能为空。", "新建脚本");
                return;
            }

            if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                MessageBox.Show("文件名包含非法字符。", "新建脚本");
                return;
            }

            if (!name.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
                name += ".txt";

            string path = Path.Combine(ScriptFolder, name);
            if (File.Exists(path))
            {
                MessageBox.Show("已存在同名脚本文件。", "新建脚本");
                return;
            }

            // 首行预填当前选择的目标名称，后续可在记事本中修改
            string header = SelectedTarget != null ? SelectedTarget.Title : "屏幕1";

            try
            {
                File.WriteAllText(path, header + Environment.NewLine + Environment.NewLine, new UTF8Encoding(true));
            }
            catch (Exception ex)
            {
                MessageBox.Show("新建脚本失败：" + ex.Message, "错误",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            ReloadScriptList(false);
            comboScript.SelectedItem = name;
        }

        private void btnDeleteScript_Click(object sender, RoutedEventArgs e)
        {
            string path = SelectedScriptPath;
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                MessageBox.Show("请先选择要删除的脚本。", "删除脚本");
                return;
            }

            MessageBoxResult result = MessageBox.Show(
                "确定删除脚本「" + SelectedScriptName + "」吗？此操作不可恢复。",
                "删除脚本", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) return;

            try
            {
                File.Delete(path);
            }
            catch (Exception ex)
            {
                MessageBox.Show("删除失败：" + ex.Message, "错误",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            ReloadScriptList(true);
        }

        private void btnEditScript_Click(object sender, RoutedEventArgs e)
        {
            string path = SelectedScriptPath;
            if (string.IsNullOrEmpty(path))
            {
                MessageBox.Show("请先选择要编辑的脚本。", "编辑脚本");
                return;
            }

            if (!File.Exists(path))
            {
                MessageBox.Show("脚本文件不存在：" + path, "编辑脚本");
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo("notepad.exe", "\"" + path + "\"") { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show("打开记事本失败：" + ex.Message, "错误",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            MessageBox.Show("已在记事本中打开脚本。\n\n请编辑并保存脚本，然后关闭本提示窗口。",
                "编辑脚本", MessageBoxButton.OK, MessageBoxImage.Information);

            // 编辑后首行可能已改，重新匹配目标
            AutoSelectTargetForScript();
            ShowScriptContent();
            UpdateTargetHint();
        }

        private void btnOpenFolder_Click(object sender, RoutedEventArgs e)
        {
            EnsureScriptFolder();
            try
            {
                Process.Start(new ProcessStartInfo("explorer.exe", "\"" + ScriptFolder + "\"") { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show("打开文件夹失败：" + ex.Message, "错误",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        #endregion
    }
}
