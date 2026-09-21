using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace Pc_clicker
{
    internal class click_func
    {
        [System.Runtime.InteropServices.DllImport("user32")]
        private static extern int mouse_event(int dwFlags, int dx, int dy, int cButtons, int dwExtraInfo);
        enum mouse_flag
        {
            MOVE = 0x0001,      //鼠标移动 
            LEFTDOWN = 0x0002,  //鼠标左键按下 
            LEFTUP = 0x0004,    //鼠标左键抬起 
            RIGHTDOWN = 0x0008, //鼠标右键按下 
            RIGHTUP = 0x0010,   //鼠标右键抬起 
            MIDDLEDOWN = 0x0020,//鼠标中键按下 
            MIDDLEUP = 0x0040,  //鼠标中键抬起 
            WHEEL = 0x0800      //鼠标滚轮滚动操作，必须配合dwData参数
        }

        public void excecute(command_cls cmd)
        {
            switch (cmd.type)
            {
                case 0:
                    mouse_click();
                    break;
                case 1:

                case 2:

                default:
                    break;
            }
        }

        private void mouse_click()
        {

        }
    }
}
