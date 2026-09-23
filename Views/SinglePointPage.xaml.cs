using System.Text.RegularExpressions;
using System.Windows.Controls;
using System.Windows.Input;
using Pc_clicker.func;

namespace Pc_clicker.Views
{
    /// <summary>
    /// 鼠标单点循环界面
    /// </summary>
    public partial class SinglePointPage : UserControl
    {
        public SinglePointPage()
        {
            InitializeComponent();
        }

        /// <summary>当前选择的鼠标按键</summary>
        public MouseButtonType SelectedButton
        {
            get
            {
                if (rightbtn.IsChecked == true) return MouseButtonType.Right;
                if (middlebtn.IsChecked == true) return MouseButtonType.Middle;
                if (sidebtn1.IsChecked == true) return MouseButtonType.SideFront;
                if (sidebtn2.IsChecked == true) return MouseButtonType.SideBack;
                return MouseButtonType.Left;
            }
        }

        /// <summary>两次点击之间的等待毫秒数（默认 50）</summary>
        public int ClickIntervalMs
        {
            get
            {
                int value;
                if (int.TryParse((clickInterval.Text ?? string.Empty).Trim(), out value) && value >= 0)
                    return value;
                return DefaultClickIntervalMs;
            }
        }

        private const int DefaultClickIntervalMs = 50;

        private void ClickInterval_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !Regex.IsMatch(e.Text, "^[0-9]+$");
        }

        /// <summary>失焦时把非法内容恢复为默认值</summary>
        protected override void OnLostFocus(System.Windows.RoutedEventArgs e)
        {
            base.OnLostFocus(e);

            int value;
            if (!int.TryParse((clickInterval.Text ?? string.Empty).Trim(), out value))
                clickInterval.Text = DefaultClickIntervalMs.ToString();
        }
    }
}
