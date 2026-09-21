using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Pc_clicker
{
    public class command_cls
    {
        UInt16 type;    //命令类型 0:鼠标、1:组合键、2:等待
        string[] keys;  //命令按键
         
        command_cls(string cmd)
        {
            string[] cmd_split = cmd.Split(' ');
            type = Convert.ToUInt16(cmd_split[0]);
        }
    }
}
