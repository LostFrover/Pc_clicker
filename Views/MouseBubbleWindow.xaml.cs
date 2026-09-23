using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Pc_clicker.func;

namespace Pc_clicker.Views
{
    /// <summary>
    /// 跟随鼠标的坐标浮窗（纯展示，不接受鼠标交互，只能用 F9 或主界面按钮关闭）。
    /// 第一行显示鼠标在目标内的相对比例（0-1），超出目标范围时显示“超出范围”；
    /// 第二行显示当前目标（进程文件名或屏幕）。
    /// 坐标统一在 Win32 屏幕坐标系里计算，与点击使用的坐标系一致。
    /// </summary>
    public partial class MouseBubbleWindow : Window
    {
        /// <summary>浮窗与鼠标之间的距离（像素），保证不压住鼠标</summary>
        private const int Gap = 14;

        private static readonly Brush NormalBrush = Brushes.White;
        private static readonly Brush OutOfRangeBrush = Brushes.OrangeRed;

        private readonly TargetItem _target;
        private readonly DispatcherTimer _timer;
        private IntPtr _hwnd;

        public MouseBubbleWindow(TargetItem target)
        {
            InitializeComponent();

            _target = target;
            txtTarget.Text = DescribeTarget(target);

            _timer = new DispatcherTimer(DispatcherPriority.Render)
            {
                Interval = TimeSpan.FromMilliseconds(30)
            };
            _timer.Tick += Timer_Tick;

            Loaded += MouseBubbleWindow_Loaded;
            Closed += delegate { _timer.Stop(); };
        }

        private static string DescribeTarget(TargetItem target)
        {
            if (target == null) return "未选择目标";

            string name = string.IsNullOrEmpty(target.UiName) ? target.Title : target.UiName;
            return target.Kind == TargetKind.Window
                ? "进程：" + name
                : "屏幕：" + name;
        }

        /// <summary>
        /// 浮窗只是显示用：鼠标点击穿透、不抢焦点、不进入 Alt+Tab。
        /// </summary>
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            _hwnd = new WindowInteropHelper(this).Handle;
            if (_hwnd == IntPtr.Zero) return;

            int exStyle = NativeMethods.GetWindowLong(_hwnd, NativeMethods.GWL_EXSTYLE);
            exStyle |= NativeMethods.WS_EX_TRANSPARENT | NativeMethods.WS_EX_NOACTIVATE | NativeMethods.WS_EX_TOOLWINDOW;
            NativeMethods.SetWindowLong(_hwnd, NativeMethods.GWL_EXSTYLE, exStyle);
        }

        private void MouseBubbleWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (_hwnd == IntPtr.Zero)
                _hwnd = new WindowInteropHelper(this).Handle;

            UpdatePosition();
            _timer.Start();
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            UpdatePosition();
        }

        private void UpdatePosition()
        {
            NativeMethods.POINT cursor;
            if (!NativeMethods.GetCursorPos(out cursor)) return;

            double fx, fy;
            bool inside = TryGetRelativeRatio(cursor, out fx, out fy);

            if (inside)
            {
                txtPos.Foreground = NormalBrush;
                txtPos.Text = string.Format("{0:0.000},{1:0.000}", fx, fy);
            }
            else
            {
                txtPos.Foreground = OutOfRangeBrush;
                txtPos.Text = "超出范围";
            }

            MoveToCursor(cursor);
        }

        private void MoveToCursor(NativeMethods.POINT cursor)
        {
            if (_hwnd == IntPtr.Zero) return;

            NativeMethods.RECT self;
            if (!NativeMethods.GetWindowRect(_hwnd, out self)) return;

            int width = self.Right - self.Left;
            int height = self.Bottom - self.Top;

            NativeMethods.MONITORINFO monitor = GetMonitor(cursor);

            int left = cursor.X + Gap;
            int top = cursor.Y + Gap;

            // 贴近屏幕右/下边缘时翻到鼠标另一侧，避免超出屏幕、也避免压住鼠标
            if (left + width > monitor.rcMonitor.Right)
                left = cursor.X - width - Gap;
            if (top + height > monitor.rcMonitor.Bottom)
                top = cursor.Y - height - Gap;

            if (left < monitor.rcMonitor.Left) left = monitor.rcMonitor.Left;
            if (top < monitor.rcMonitor.Top) top = monitor.rcMonitor.Top;

            if (left == self.Left && top == self.Top) return;

            NativeMethods.SetWindowPos(_hwnd, NativeMethods.HWND_TOPMOST, left, top, 0, 0,
                NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
        }

        private static NativeMethods.MONITORINFO GetMonitor(NativeMethods.POINT point)
        {
            IntPtr monitor = NativeMethods.MonitorFromPoint(point, NativeMethods.MONITOR_DEFAULTTONEAREST);
            var info = new NativeMethods.MONITORINFO();
            info.cbSize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(NativeMethods.MONITORINFO));
            NativeMethods.GetMonitorInfo(monitor, ref info);
            return info;
        }

        /// <summary>
        /// 计算鼠标在目标内的相对比例（0-1）。
        /// 返回 false 表示鼠标在目标范围之外（或目标已失效），此时界面显示“超出范围”。
        /// </summary>
        private bool TryGetRelativeRatio(NativeMethods.POINT cursor, out double fx, out double fy)
        {
            fx = 0;
            fy = 0;

            if (_target == null) return false;

            if (_target.Kind == TargetKind.Screen)
            {
                if (_target.ScreenWidth <= 0 || _target.ScreenHeight <= 0) return false;

                fx = (cursor.X - _target.ScreenLeft) / (double)_target.ScreenWidth;
                fy = (cursor.Y - _target.ScreenTop) / (double)_target.ScreenHeight;
            }
            else
            {
                if (!NativeMethods.IsWindow(_target.Handle)) return false;

                var origin = new NativeMethods.POINT { X = 0, Y = 0 };
                NativeMethods.RECT client;
                if (!NativeMethods.ClientToScreen(_target.Handle, ref origin)) return false;
                if (!NativeMethods.GetClientRect(_target.Handle, out client)) return false;

                int width = client.Right - client.Left;
                int height = client.Bottom - client.Top;
                if (width <= 0 || height <= 0) return false;

                fx = (cursor.X - origin.X) / (double)width;
                fy = (cursor.Y - origin.Y) / (double)height;
            }

            // 鼠标不在目标范围内时不算相对坐标
            return fx >= 0 && fx <= 1 && fy >= 0 && fy <= 1;
        }
    }
}
