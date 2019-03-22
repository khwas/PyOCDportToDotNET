using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WpfApp1
{
    public class DCToolChain
    {
        public DCTool Tool1 { get; set; }
        public DCTool Tool2 { get; set; }
        public DCTool Tool3 { get; set; }

        public DCToolChain()
        {
            Tool3 = new DCToolDebugUnit();
            Tool2 = new DCToolDebugUnitSession(Tool3 as DCToolDebugUnit);
            Tool1 = new DCToolWinUsb(Tool2 as DCToolDebugUnitSession);
        }
    }
}
