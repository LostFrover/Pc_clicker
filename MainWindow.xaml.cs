using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using Pc_clicker.Views;

namespace Pc_clicker
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly SinglePointPage _singlePage = new SinglePointPage();
        private readonly ScriptLoopPage _scriptPage = new ScriptLoopPage();

        public MainWindow()
        {
            InitializeComponent();
            pageHost.Content = _singlePage;
            locating.IsEnabled = false;
        }

        private void MenuPageSingle_Click(object sender, RoutedEventArgs e)
        {
            pageHost.Content = _singlePage;
            locating.IsEnabled = false;
        }

        private void MenuPageMulti_Click(object sender, RoutedEventArgs e)
        {
            pageHost.Content = _scriptPage;
            locating.IsEnabled = true;
        }

        private void MenuPageGuide_Click(object sender, RoutedEventArgs e)
        {
            // TODO: 打开指导文档
        }

        private void LoopMode_Checked(object sender, RoutedEventArgs e)
        {
            if (loop_num != null)
            {
                loop_num.Text = "1";
            }
        }

        private void LoopNum_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !Regex.IsMatch(e.Text, "^[0-9]+$");
        }

        private void Locating_Click(object sender, RoutedEventArgs e)
        {
            // TODO: 创建跟随鼠标的坐标气泡浮窗
        }

        private void Excuting_Click(object sender, RoutedEventArgs e)
        {
            // TODO: 开始/停止执行循环
        }
    }
}
