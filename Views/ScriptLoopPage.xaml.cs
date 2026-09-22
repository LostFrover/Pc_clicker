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
            string previousKey = GetTargetKey(SelectedTarget);

            comboTarget.Items.Clear();
            foreach (TargetItem item in TargetEnumerator.Enumerate())
                comboTarget.Items.Add(item);

            if (previousKey != null)
            {
                foreach (TargetItem item in comboTarget.Items)
                {
                    if (GetTargetKey(item) == previousKey)
                    {
                        comboTarget.SelectedItem = item;
                        break;
                    }
                }
            }

            UpdateTargetHint();
        }

        private static string GetTargetKey(TargetItem item)
        {
            return item == null ? null : item.Kind + "|" + item.Title;
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

            if (string.Equals(header, target.Title, StringComparison.Ordinal))
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
            ShowScriptContent();
            UpdateTargetHint();
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
                foreach (string line in ScriptEngine.ReadAllLines(path))
                    listScript.Items.Add(line);
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
