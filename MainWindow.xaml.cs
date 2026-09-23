using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Pc_clicker.func;
using Pc_clicker.Views;

namespace Pc_clicker
{
    /// <summary>
    /// 主界面：菜单切换子界面、循环模式选择、鼠标坐标浮窗、循环执行控制
    /// </summary>
    public partial class MainWindow : Window
    {
        // 全局快捷键：F8 开始/停止，F9 关闭鼠标坐标浮窗
        private const int HotkeyToggleRun = 0xA001;
        private const int HotkeyCloseBubble = 0xA002;
        private const uint VkF8 = 0x77;
        private const uint VkF9 = 0x78;

        private readonly ScriptLoopPage _scriptPage = new ScriptLoopPage();
        private readonly LoopRunner _runner = new LoopRunner();

        private MouseBubbleWindow _bubble;
        private HwndSource _hwndSource;

        public MainWindow()
        {
            InitializeComponent();

            ShowSinglePointPage();
            UpdateLoopInputUi();

            _runner.Failed += Runner_Failed;
            _runner.Finished += Runner_Finished;

            SourceInitialized += MainWindow_SourceInitialized;
            Closing += MainWindow_Closing;
            PreviewKeyDown += MainWindow_PreviewKeyDown;
        }

        #region 子界面切换

        private void MenuPageSingle_Click(object sender, RoutedEventArgs e)
        {
            ShowSinglePointPage();
        }

        private void MenuPageMulti_Click(object sender, RoutedEventArgs e)
        {
            ShowScriptPage();
        }

        private void MenuPageGuide_Click(object sender, RoutedEventArgs e)
        {
            OpenGuide();
        }

        private void ShowSinglePointPage()
        {
            pageHost.Content = singlePage;
            menuPageSingle.FontWeight = FontWeights.Bold;
            menuPageMulti.FontWeight = FontWeights.Normal;

            // 单点循环不使用浮窗
            CloseBubble();
            locating.IsEnabled = false;
        }

        private void ShowScriptPage()
        {
            pageHost.Content = _scriptPage;
            menuPageMulti.FontWeight = FontWeights.Bold;
            menuPageSingle.FontWeight = FontWeights.Normal;

            locating.IsEnabled = !_runner.IsRunning;
        }

        private bool IsScriptPageActive
        {
            get { return ReferenceEquals(pageHost.Content, _scriptPage); }
        }

        private void OpenGuide()
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "指导文档.md");

            try
            {
                if (!File.Exists(path))
                    File.WriteAllText(path, FallbackGuide, new System.Text.UTF8Encoding(true));

                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "打开指导文档失败：" + ex.Message, "错误",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        #endregion

        #region 循环模式

        private void LoopMode_Checked(object sender, RoutedEventArgs e)
        {
            UpdateLoopInputUi();
        }

        private void UpdateLoopInputUi()
        {
            if (loop_num == null) return;

            // 说明 label 文字固定不变（次数与时间共用一个输入框），只有无限循环时不需要填写
            loop_num.IsEnabled = loop_continue.IsChecked != true;

            // 切换循环模式时把输入框重置为 1
            loop_num.Text = "1";
        }

        private void LoopNum_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !Regex.IsMatch(e.Text, "^[0-9]+$");
        }

        private int ReadLoopValue()
        {
            int value;
            return int.TryParse((loop_num.Text ?? string.Empty).Trim(), out value) ? value : 0;
        }

        #endregion

        #region 执行 / 停止

        private void Excuting_Click(object sender, RoutedEventArgs e)
        {
            ToggleExecution();
        }

        private void ToggleExecution()
        {
            if (_runner.IsRunning)
                StopExecution();
            else
                StartExecution();
        }

        private void StartExecution()
        {
            ScriptEngine engine;
            TargetItem target = null;
            int startDelayMs = 0;

            if (IsScriptPageActive)
            {
                string error = PrepareScriptExecution(out engine, out target);
                if (error != null)
                {
                    MessageBox.Show(this, error, "无法执行", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }
            else
            {
                // 单点循环：不读取脚本文件，直接在界面类里预设参数
                engine = ScriptEngine.CreateEmpty(null);
                engine.Actions.Add(new ScriptAction
                {
                    Type = "mouse",
                    Button = singlePage.SelectedButton,
                    X = -1,   // 当前位置
                    Y = -1
                });

                // 点击间隔：每点击一次后等待指定毫秒
                engine.Actions.Add(new ScriptAction
                {
                    Type = "wait",
                    DelayMs = singlePage.ClickIntervalMs
                });

                // 点击「执行」时鼠标就停在按钮上，延时一会儿让用户把鼠标移到目标位置
                startDelayMs = 1000;
            }

            LoopMode mode;
            int value = 1;

            if (loop_continue.IsChecked == true)
            {
                mode = LoopMode.Infinite;
            }
            else if (loop_time.IsChecked == true)
            {
                mode = LoopMode.Timed;
                value = ReadLoopValue();
            }
            else
            {
                mode = LoopMode.Count;
                value = ReadLoopValue();
            }

            if (mode != LoopMode.Infinite && value <= 0)
            {
                MessageBox.Show(this, "请输入大于 0 的整数。", "无法执行",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                loop_num.Focus();
                return;
            }

            SetRunningState(true);
            _runner.Start(engine, target, mode, value, startDelayMs);
        }

        /// <summary>
        /// 读取并校验脚本：首行目标校验 + 每行操作规范校验。
        /// 返回 null 表示可以执行，否则返回需要提示用户的信息。
        /// </summary>
        private string PrepareScriptExecution(out ScriptEngine engine, out TargetItem target)
        {
            engine = null;
            target = _scriptPage.SelectedTarget;

            if (target == null)
                return "请先在「目标程序」中选择要操作的目标（窗口或屏幕）。";

            string path = _scriptPage.SelectedScriptPath;
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return "请先选择要执行的脚本文件。";

            var script = new ScriptEngine();
            string error = script.Load(path);
            if (error != null)
                return "脚本内容不符合规范：\n" + error;

            string targetError = script.ValidateTarget(target.Title);
            if (targetError != null)
                return targetError;

            engine = script;
            return null;
        }

        private void StopExecution()
        {
            _runner.Stop();
        }

        private void SetRunningState(bool running)
        {
            excuting.Content = running ? "停止" : "执行";

            if (running)
                excuting.Background = Brushes.LightCoral;
            else
                excuting.ClearValue(Control.BackgroundProperty);

            locating.IsEnabled = !running && IsScriptPageActive;
        }

        private void Runner_Failed(string message)
        {
            SafeInvoke(delegate
            {
                MessageBox.Show(this, message, "执行中断", MessageBoxButton.OK, MessageBoxImage.Warning);
            });
        }

        private void Runner_Finished()
        {
            SafeInvoke(delegate { SetRunningState(false); });
        }

        private void SafeInvoke(Action action)
        {
            try
            {
                Dispatcher.BeginInvoke(action);
            }
            catch
            {
                // 窗口已关闭，忽略
            }
        }

        #endregion

        #region 鼠标位置浮窗

        private void Locating_Click(object sender, RoutedEventArgs e)
        {
            ToggleBubble();
        }

        private void ToggleBubble()
        {
            if (_bubble != null)
            {
                CloseBubble();
                return;
            }

            if (!IsScriptPageActive) return;

            var bubble = new MouseBubbleWindow(_scriptPage.SelectedTarget);
            bubble.Closed += delegate
            {
                _bubble = null;
                locating.Content = "鼠标位置";
            };

            _bubble = bubble;
            locating.Content = "关闭浮窗";
            bubble.Show();
        }

        private void CloseBubble()
        {
            MouseBubbleWindow bubble = _bubble;
            if (bubble == null) return;

            _bubble = null;
            locating.Content = "鼠标位置";

            try
            {
                bubble.Close();
            }
            catch
            {
            }
        }

        #endregion

        #region 快捷键

        private void MainWindow_SourceInitialized(object sender, EventArgs e)
        {
            var helper = new WindowInteropHelper(this);

            _hwndSource = HwndSource.FromHwnd(helper.Handle);
            if (_hwndSource != null)
                _hwndSource.AddHook(WndProc);

            // 注册失败（如已被其它程序占用）时仍可使用界面按钮和窗口内快捷键
            NativeMethods.RegisterHotKey(helper.Handle, HotkeyToggleRun, NativeMethods.MOD_NOREPEAT, VkF8);
            NativeMethods.RegisterHotKey(helper.Handle, HotkeyCloseBubble, NativeMethods.MOD_NOREPEAT, VkF9);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == NativeMethods.WM_HOTKEY)
            {
                int id = wParam.ToInt32();

                if (id == HotkeyToggleRun)
                {
                    ToggleExecution();
                    handled = true;
                }
                else if (id == HotkeyCloseBubble)
                {
                    CloseBubble();
                    handled = true;
                }
            }

            return IntPtr.Zero;
        }

        private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // 窗口激活时同样支持 F8 / F9
            if (e.Key == Key.F8)
            {
                ToggleExecution();
                e.Handled = true;
            }
            else if (e.Key == Key.F9)
            {
                CloseBubble();
                e.Handled = true;
            }
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _runner.Stop();
            CloseBubble();

            var helper = new WindowInteropHelper(this);
            NativeMethods.UnregisterHotKey(helper.Handle, HotkeyToggleRun);
            NativeMethods.UnregisterHotKey(helper.Handle, HotkeyCloseBubble);

            if (_hwndSource != null)
            {
                _hwndSource.RemoveHook(WndProc);
                _hwndSource = null;
            }
        }

        #endregion

        private const string FallbackGuide =
            "# Pc_clicker 指导文档\n\n" +
            "脚本首行填写目标进程文件名（如 notepad.exe）或「屏幕x」，之后每行一条操作：\n\n" +
            "- mouse 按键类型 x y    （按键类型 0左键/1右键/2中键/3前侧键/4后侧键；x y 为 0-1 相对比例；-1 -1 表示鼠标当前位置）\n" +
            "- key 按键              （如 ctrl+alt+del、shift+a、win+r、menu、volumeup、medianext）\n" +
            "- wait 毫秒             （如 wait 500）\n\n" +
            "快捷键：F8 开始/停止，F9 关闭鼠标坐标浮窗。\n";
    }
}
