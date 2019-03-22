using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace openocd.CmsisDap
{

    // 
    //     A wrapper object representing a command send to the layer below (ex. USB).
    // 
    //     This class wraps the physical commands DAP_Transfer and DAP_TransferBlock
    //     to provide a uniform way to build the command to most efficiently transfer
    //     the data supplied.  Register reads and writes individually or in blocks
    //     are added to a command object until it is full.  Once full, this class
    //     decides if it is more efficient to use DAP_Transfer or DAP_TransferBlock.
    //     The payload to send over the layer below is constructed with
    //     encode_data.  The response to the command is decoded with decode_data.
    //     
    public class Command
    {
        internal UInt16 _size;
        internal UInt16 _read_count;
        internal UInt16 _write_count;
        internal bool _block_allowed;
        internal EDapTransferRequestByte? _block_request;
        internal bool _data_encoded;
        internal byte? _dap_index;
        internal List<Tuple<UInt16, EDapTransferRequestByte, List<UInt32>>> _data;

        public Command(UInt16 size)
        {
            this._size = size;
            this._read_count = 0;
            this._write_count = 0;
            this._block_allowed = true;
            this._block_request = null;
            this._data = new List<Tuple<UInt16, EDapTransferRequestByte, List<UInt32>>>();
            this._dap_index = null;
            this._data_encoded = false;
            if (DapAccessConsts.LOG_PACKET_BUILDS)
            {
                //this._logger = logging.getLogger(@__name__);
                Trace.TraceInformation("New _Command");
            }
        }

        // 
        //         Return the number of words free in the transmit packet
        //         
        public virtual UInt16 _get_free_words(bool blockAllowed, bool isRead)
        {
            UInt16 recv;
            UInt16 send;
            if (blockAllowed)
            {
                // DAP_TransferBlock request packet:
                //   BYTE | BYTE *****| SHORT**********| BYTE *************| WORD *********|
                // > 0x06 | DAP Index | Transfer Count | Transfer Request  | Transfer Data |
                //  ******|***********|****************|*******************|+++++++++++++++|
                send = (UInt16)(this._size - 5 - 4 * this._write_count);
                // DAP_TransferBlock response packet:
                //   BYTE | SHORT *********| BYTE *************| WORD *********|
                // < 0x06 | Transfer Count | Transfer Response | Transfer Data |
                //  ******|****************|*******************|+++++++++++++++|
                recv = (UInt16)(this._size - 4 - 4 * this._read_count);
                if (isRead)
                {
                    return (UInt16)(recv / 4);
                }
                else
                {
                    return (UInt16)(send / 4);
                }
            }
            else
            {
                // DAP_Transfer request packet:
                //   BYTE | BYTE *****| BYTE **********| BYTE *************| WORD *********|
                // > 0x05 | DAP Index | Transfer Count | Transfer Request  | Transfer Data |
                //  ******|***********|****************|+++++++++++++++++++++++++++++++++++|
                send = (UInt16)(this._size - 3 - 1 * this._read_count - 5 * this._write_count);
                // DAP_Transfer response packet:
                //   BYTE | BYTE **********| BYTE *************| WORD *********|
                // < 0x05 | Transfer Count | Transfer Response | Transfer Data |
                //  ******|****************|*******************|+++++++++++++++|
                recv = (UInt16)(this._size - 3 - 4 * this._read_count);
                if (isRead)
                {
                    // 1 request byte in request packet, 4 data bytes in response packet
                    return Math.Min(send, (UInt16)(recv / 4));
                }
                else
                {
                    // 1 request byte + 4 data bytes
                    return (UInt16)(send / 5);
                }
            }
        }

        public virtual UInt16 get_request_space(UInt16 count, EDapTransferRequestByte request, byte dap_index)
        {
            Debug.Assert(this._data_encoded == false);
            // Must create another command if the dap index is different.
            if (this._dap_index != null && dap_index != this._dap_index)
            {
                return 0;
            }
            // Block transfers must use the same request.
            var blockAllowed = this._block_allowed;
            if (this._block_request != null && request != this._block_request)
            {
                blockAllowed = false;
            }
            // Compute the portion of the request that will fit in this packet.
            bool is_read = (request & EDapTransferRequestByte.READ) != 0;
            UInt16 free = this._get_free_words(blockAllowed, is_read);
            int size = Math.Min(count, free);
            // Non-block transfers only have 1 byte for request count.
            if (!blockAllowed)
            {
                UInt16 max_count = (UInt16)(this._write_count + this._read_count + size);
                int delta = max_count - 255;
                size = Math.Min(size - delta, size);
                if (DapAccessConsts.LOG_PACKET_BUILDS)
                {
                    Trace.TraceInformation(String.Format("get_request_space(%d, %02x:%s)[wc=%d, rc=%d, ba=%d->%d] -> (sz=%d, free=%d, delta=%d)", count, request, is_read ? "r" : "w", this._write_count, this._read_count, this._block_allowed, blockAllowed, size, free, delta));
                }
            }
            else if (DapAccessConsts.LOG_PACKET_BUILDS)
            {
                Trace.TraceInformation(String.Format("get_request_space(%d, %02x:%s)[wc=%d, rc=%d, ba=%d->%d] -> (sz=%d, free=%d)", count, request, is_read ? "r" : "w", this._write_count, this._read_count, this._block_allowed, blockAllowed, size, free));
            }
            // We can get a negative free count if the packet already contains more data than can be
            // sent by a DAP_Transfer command, but the new request forces DAP_Transfer. In this case,
            // just return 0 to force the DAP_Transfer_Block to be sent.
            return (UInt16)Math.Max(size, 0);
        }

        public virtual bool get_full()
        {
            return this._get_free_words(this._block_allowed, true) == 0 || this._get_free_words(this._block_allowed, false) == 0;
        }

        // 
        //         Return True if no transfers have been added to this packet
        //         
        public virtual bool get_empty()
        {
            return this._data.Count == 0;
        }

        // 
        //         Add a single or block register transfer operation to this command
        //         
        public virtual void add(UInt16 count, EDapTransferRequestByte request, List<UInt32> data, byte dap_index)
        {
            Debug.Assert(this._data_encoded == false);
            if (this._dap_index == null)
            {
                this._dap_index = dap_index;
            }
            Debug.Assert(this._dap_index == dap_index);
            if (this._block_request == null)
            {
                this._block_request = request;
            }
            else if (request != this._block_request)
            {
                this._block_allowed = false;
            }
            Debug.Assert(!this._block_allowed || this._block_request == request);
            if ((request & EDapTransferRequestByte.READ) != 0)
            {
                this._read_count += count;
            }
            else
            {
                this._write_count += count;
            }
            this._data.Add(Tuple.Create(count, request, data));
            if (DapAccessConsts.LOG_PACKET_BUILDS)
            {
                //Trace.TraceInformation(String.Format("add(%d, %02x:%s) -> [wc=%d, rc=%d, ba=%d]", count, request, request & READ ? "r" : "w", this._write_count, this._read_count, this._block_allowed));
            }
        }

        // 
        //         Encode this command into a byte array that can be sent
        // 
        //         The data returned by this function is a bytearray in
        //         the format that of a DAP_Transfer CMSIS-DAP command.
        //         
        public virtual byte[] _encode_transfer_data()
        {
            Debug.Assert(this.get_empty() == false);
            byte[] buf = new byte[this._size];
            byte transfer_count = (byte)(this._read_count + this._write_count);
            var pos = 0;
            buf[pos] = (byte)EDapCommandByte.DAP_TRANSFER;
            pos += 1;
            buf[pos] = (byte)this._dap_index;
            pos += 1;
            buf[pos] = (byte)transfer_count;
            pos += 1;
            foreach (var _tup_1 in this._data)
            {
                UInt32 count = _tup_1.Item1;
                var request = _tup_1.Item2;
                List<UInt32> write_list = _tup_1.Item3;
                Debug.Assert(write_list == null || write_list.Count <= count);
                var write_pos = 0;
                foreach (var _ in Enumerable.Range(0, (int)count))
                {
                    buf[pos] = (byte)request;
                    pos += 1;
                    if ((request & EDapTransferRequestByte.READ) == 0)
                    {
                        buf[pos] = (byte)((write_list[write_pos] >> 8 * 0) & 0xFF);
                        pos += 1;
                        buf[pos] = (byte)((write_list[write_pos] >> 8 * 1) & 0xFF);
                        pos += 1;
                        buf[pos] = (byte)((write_list[write_pos] >> 8 * 2) & 0xFF);
                        pos += 1;
                        buf[pos] = (byte)((write_list[write_pos] >> 8 * 3) & 0xFF);
                        pos += 1;
                        write_pos += 1;
                    }
                }
            }
            return buf;
        }

        // 
        //         Take a byte array and extract the data from it
        // 
        //         Decode the response returned by a DAP_Transfer CMSIS-DAP command
        //         and return it as an array of bytes.
        //         
        public virtual List<byte> _decode_transfer_data(List<byte> data)
        {
            Debug.Assert(this.get_empty() == false);
            if (data[0] != (byte)EDapCommandByte.DAP_TRANSFER)
            {
                throw new ArgumentOutOfRangeException("DAP_TRANSFER response error");
            }
            if (data[2] != (byte)EDapTransferResponseByte.DAP_TRANSFER_SWD_OK)
            {
                if (data[2] == (byte)EDapTransferResponseByte.DAP_TRANSFER_FAULT)
                {
                    throw new TransferFaultError();
                }
                else if (data[2] == (byte)EDapTransferResponseByte.DAP_TRANSFER_WAIT)
                {
                    throw new TransferTimeoutError();
                }
                else if (data[2] == (byte)EDapTransferResponseByte.DAP_TRANSFER_NO_ACK)
                {
                    throw new Exception("No response from target");
                }
                throw new TransferError();
            }
            // Check for count mismatch after checking for DAP_TRANSFER_FAULT
            // This allows TransferFaultError or TransferTimeoutError to get
            // thrown instead of TransferFaultError
            if (data[1] != this._read_count + this._write_count)
            {
                throw new TransferError();
            }
            return data.GetRange(3, 4 * this._read_count);
        }

        // 
        //         Encode this command into a byte array that can be sent
        // 
        //         The data returned by this function is a bytearray in
        //         the format that of a DAP_TransferBlock CMSIS-DAP command.
        //         
        public virtual byte[] _encode_transfer_block_data()
        {
            Debug.Assert(this.get_empty() == false);
            byte[] buf = new byte[this._size];
            UInt16 transfer_count = (UInt16)(this._read_count + this._write_count);
            Debug.Assert(!(this._read_count != 0 && this._write_count != 0));
            Debug.Assert(this._block_request != null);
            var pos = 0;
            buf[pos] = (byte)EDapCommandByte.DAP_TRANSFER_BLOCK;
            pos += 1;
            buf[pos] = (byte)this._dap_index;
            pos += 1;
            buf[pos] = (byte)(transfer_count & 0xFF);
            pos += 1;
            buf[pos] = (byte)((transfer_count >> 8) & 0xFF);
            pos += 1;
            buf[pos] = (byte)this._block_request;
            pos += 1;
            foreach (var _tup_1 in this._data)
            {
                UInt32 count = _tup_1.Item1;
                EDapTransferRequestByte request = _tup_1.Item2;
                List<UInt32> write_list = _tup_1.Item3;
                Debug.Assert(write_list == null || write_list.Count <= count);
                Debug.Assert(request == this._block_request);
                var write_pos = 0;
                if ((request & EDapTransferRequestByte.READ) == 0)
                {
                    foreach (var _ in Enumerable.Range(0, (int)count))
                    {
                        buf[pos] = (byte)((write_list[write_pos] >> (8 * 0)) & 0xFF);
                        pos += 1;
                        buf[pos] = (byte)((write_list[write_pos] >> (8 * 1)) & 0xFF);
                        pos += 1;
                        buf[pos] = (byte)((write_list[write_pos] >> (8 * 2)) & 0xFF);
                        pos += 1;
                        buf[pos] = (byte)((write_list[write_pos] >> (8 * 3)) & 0xFF);
                        pos += 1;
                        write_pos += 1;
                    }
                }
            }
            return buf;
        }

        // 
        //         Take a byte array and extract the data from it
        // 
        //         Decode the response returned by a DAP_TransferBlock CMSIS-DAP command
        //         and return it as an array of bytes.
        //         
        public virtual List<byte> _decode_transfer_block_data(List<byte> data)
        {
            Debug.Assert(this.get_empty() == false);
            if (data[0] != (byte)EDapCommandByte.DAP_TRANSFER_BLOCK)
            {
                throw new ArgumentOutOfRangeException("DAP_TRANSFER_BLOCK response error");
            }
            if (data[3] != (byte)EDapTransferResponseByte.DAP_TRANSFER_SWD_OK)
            {
                if (data[3] == (byte)EDapTransferResponseByte.DAP_TRANSFER_FAULT)
                {
                    throw new TransferFaultError();
                }
                else if (data[3] == (byte)EDapTransferResponseByte.DAP_TRANSFER_WAIT)
                {
                    throw new TransferTimeoutError();
                }
                else if (data[3] == (byte)EDapTransferResponseByte.DAP_TRANSFER_NO_ACK)
                {
                    throw new Exception("No response from the target");
                }
                throw new TransferError();
            }
            // Check for count mismatch after checking for DAP_TRANSFER_FAULT
            // This allows TransferFaultError or TransferTimeoutError to get
            // thrown instead of TransferFaultError
            var transfer_count = data[1] | data[2] << 8;
            if (transfer_count != this._read_count + this._write_count)
            {
                throw new TransferError();
            }
            return data.GetRange(4, 4 * this._read_count);
        }

        // 
        //         Encode this command into a byte array that can be sent
        // 
        //         The actual command this is encoded into depends on the data
        //         that was added.
        //         
        public virtual byte[] encode_data()
        {
            byte[] data;
            Debug.Assert(this.get_empty() == false);
            this._data_encoded = true;
            if (this._block_allowed)
            {
                data = this._encode_transfer_block_data();
            }
            else
            {
                data = this._encode_transfer_data();
            }
            return data;
        }

        // 
        //         Decode the response data
        //         
        public virtual List<byte> decode_data(List<byte> data)
        {
            Debug.Assert(this.get_empty() == false);
            Debug.Assert(this._data_encoded == true);
            if (this._block_allowed)
            {
                data = this._decode_transfer_block_data(data);
            }
            else
            {
                data = this._decode_transfer_data(data);
            }
            return data;
        }
    }

}
