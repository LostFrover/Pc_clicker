using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
// 录制功能类，实时获取窗口范围，实时获取鼠标相对位置
namespace Pc_clicker
{
    
    internal class recorder
    {
        public string window;

        recorder(string window=null)
        {
            this.window = window;
        }
        public string[] get_all_windows()
        {
            // 获取所有窗口的标题和句柄
            try
            {

                return new string[] { "窗口1", "窗口2", "窗口3" };

            }
            catch (Exception ex)
            {
                MessageBox.Show("获取窗口列表失败: " + ex.Message);
                return new string[] {  };
            }

        }
    }
}
