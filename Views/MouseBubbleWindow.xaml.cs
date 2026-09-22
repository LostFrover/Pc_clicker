using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Pc_clicker.func;

namespace Pc_clicker.Views
{
    /// <summary>
    /// 跟随鼠标移动的坐标气泡浮窗，显示鼠标相对目标（窗口/屏幕）的坐标。
    /// </summary>
    public partial class MouseBubbleWindow : Window
    {
        private const int Offset = 16;

        private readonly TargetItem _target;
        private readonly DispatcherTimer _timer;

        public MouseBubbleWindow(TargetItem target)
        {
            InitializeComponent();

            _target = target;

            if (_target != null && _target.Kind == TargetKind.Window)
                txtTip.Text = "相对窗口: " + _target.Title;
            else if (_target != null)
                txtTip.Text = "相对屏幕: " + _target.Title;

            _timer = new DispatcherTimer(DispatcherPriority.Render)
            {
                Interval = TimeSpan.FromMilliseconds(40)
            };
            _timer.Tick += Timer_Tick;

            Loaded += MouseBubbleWindow_Loaded;
            Closed += delegate { _timer.Stop(); };
            PreviewKeyDown += MouseBubbleWindow_PreviewKeyDown;
        }

        private void MouseBubbleWindow_Loaded(object sender, RoutedEventArgs e)
        {
            UpdatePosition();
            _timer.Start();
        }

        private void MouseBubbleWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Close();
                e.Handled = true;
            }
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            UpdatePosition();
        }

        private void UpdatePosition()
        {
            NativeMethods.POINT cursor;
            if (!NativeMethods.GetCursorPos(out cursor)) return;

            int relX, relY;
            GetRelative(cursor, out relX, out relY);

            txtPos.Text = string.Format("鼠标位置坐标:{0},{1}", relX, relY);
            txtScreenPos.Text = string.Format("屏幕坐标:{0},{1}", cursor.X, cursor.Y);

            NativeMethods.POINT probe = cursor;
            IntPtr monitor = NativeMethods.MonitorFromPoint(probe, NativeMethods.MONITOR_DEFAULTTONEAREST);
            var info = new NativeMethods.MONITORINFO();
            info.cbSize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(NativeMethods.MONITORINFO));
            NativeMethods.GetMonitorInfo(monitor, ref info);

            double width = ActualWidth > 0 ? ActualWidth : 160;
            double height = ActualHeight > 0 ? ActualHeight : 60;

            double left = cursor.X + Offset;
            double top = cursor.Y + Offset;

            // 靠近右/下边缘时自动翻到鼠标另一侧，避免被遮挡
            if (left + width > info.rcMonitor.Right)
                left = cursor.X - width - Offset;
            if (top + height > info.rcMonitor.Bottom)
                top = cursor.Y - height - Offset;

            if (left < info.rcMonitor.Left) left = info.rcMonitor.Left;
            if (top < info.rcMonitor.Top) top = info.rcMonitor.Top;

            Left = left;
            Top = top;
        }

        private void GetRelative(NativeMethods.POINT cursor, out int relX, out int relY)
        {
            relX = cursor.X;
            relY = cursor.Y;

            if (_target == null) return;

            if (_target.Kind == TargetKind.Screen)
            {
                relX = cursor.X - _target.ScreenLeft;
                relY = cursor.Y - _target.ScreenTop;
                return;
            }

            if (NativeMethods.IsWindow(_target.Handle))
            {
                var origin = new NativeMethods.POINT { X = 0, Y = 0 };
                if (NativeMethods.ClientToScreen(_target.Handle, ref origin))
                {
                    relX = cursor.X - origin.X;
                    relY = cursor.Y - origin.Y;
                }
            }
        }
    }
}
