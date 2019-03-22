using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WpfApp1
{
    public class DCMain
    {
        public DCToolChain ToolChain { get; set; }
        public DCMain()
        {
            ToolChain = new DCToolChain();
        }
    }
}
