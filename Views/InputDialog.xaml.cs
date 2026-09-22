using System.Windows;

namespace Pc_clicker.Views
{
    /// <summary>
    /// 简单的文本输入弹窗（用于新建脚本时输入文件名）
    /// </summary>
    public partial class InputDialog : Window
    {
        private InputDialog(string title, string prompt, string defaultValue)
        {
            InitializeComponent();
            Title = title;
            txtPrompt.Text = prompt;
            txtInput.Text = defaultValue ?? string.Empty;
            Loaded += delegate
            {
                txtInput.Focus();
                txtInput.SelectAll();
            };
        }

        /// <summary>
        /// 弹出输入框，用户点击确定时返回 true，结果写入 result。
        /// </summary>
        public static bool Show(Window owner, string title, string prompt, string defaultValue, out string result)
        {
            var dialog = new InputDialog(title, prompt, defaultValue);
            if (owner != null && owner.IsVisible)
                dialog.Owner = owner;

            bool? ok = dialog.ShowDialog();
            result = dialog.txtInput.Text == null ? string.Empty : dialog.txtInput.Text.Trim();
            return ok == true;
        }

        private void btnOk_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }
    }
}
