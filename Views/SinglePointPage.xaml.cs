using System.Windows.Controls;
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
    }
}
