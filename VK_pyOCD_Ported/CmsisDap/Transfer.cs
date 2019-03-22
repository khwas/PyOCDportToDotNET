using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
 
namespace openocd.CmsisDap
{
    // 
    //     A wrapper object representing a command invoked by the layer above.
    // 
    //     The transfer class contains a logical register read or a block
    //     of reads to the same register.
    //     
    public class Transfer
    {

        internal UInt32 _size_bytes;
        internal Exception _error;
        internal List<UInt32> _result;
        internal DapAccessLink daplink;
        internal byte dap_index;
        internal UInt16 transfer_count;
        internal EDapTransferRequestByte transfer_request;
        internal List<UInt32> transfer_data;

        public Transfer(
            DapAccessLink daplink,
            byte dap_index,
            UInt16 transfer_count,
            EDapTransferRequestByte transfer_request,
            List<UInt32> transfer_data)
        {
            // Writes should not need a transfer object
            // since they don't have any response data
            Debug.Assert((transfer_request & EDapTransferRequestByte.READ) != 0);
            this.daplink = daplink;
            this.dap_index = dap_index;
            this.transfer_count = transfer_count;
            this.transfer_request = transfer_request;
            this.transfer_data = transfer_data;
            this._size_bytes = 0;
            if ((transfer_request & EDapTransferRequestByte.READ) != 0)
            {
                this._size_bytes = (UInt32)(transfer_count * 4);
            }
            this._result = null;
            this._error = null;
        }

        // 
        //         Get the size in bytes of the return value of this transfer
        //         
        public virtual UInt32 get_data_size()
        {
            return this._size_bytes;
        }

        // 
        //         Add data read from the remote device to this object.
        // 
        //         The size of data added must match exactly the size
        //         that get_data_size returns.
        //         
        public virtual void add_response(List<byte> data)
        {
            Debug.Assert(data.Count == this._size_bytes);
            Debug.Assert(this._size_bytes % 4 == 0);
            List<UInt32> result = new List<UInt32>();
            foreach (var i in Enumerable.Range(0, (int)this._size_bytes / 4))
            {
                UInt32 word = 
                    (
                    ((UInt32)data[0 + i * 4] << 0) |
                    ((UInt32)data[1 + i * 4] << 8) |
                    ((UInt32)data[2 + i * 4] << 16) |
                    ((UInt32)data[3 + i * 4] << 24)
                    );
                result.Add(word);
            }
            this._result = result;
        }

        // 
        //         Attach an exception to this transfer rather than data.
        //         
        public virtual void add_error(Exception error)
        {
            Debug.Assert(error is Exception);
            this._error = error;
        }

        // 
        //         Get the result of this transfer.
        //         
        public virtual List<UInt32> get_result()
        {
            while (this._result == null)
            {
                if (this.daplink._commands_to_read.Count > 0)
                {
                    this.daplink._read_packet();
                }
                else
                {
                    Debug.Assert(!this.daplink._crnt_cmd.get_empty());
                    this.daplink.flush();
                }
            }
            if (this._error != null)
            {
                // Pylint is confused and thinks self._error is None
                // since that is what it is initialized to.
                // Supress warnings for this.
                // pylint: disable=raising-bad-type
                throw this._error;
            }
            Debug.Assert(this._result != null);
            return this._result;
        }
    }
}

