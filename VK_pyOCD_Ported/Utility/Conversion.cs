using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
//using @struct;

//using binascii;

namespace openocd.Utility
{


    public static class Conversion
    {

        // Convert a list of bytes to a list of 32-bit integers (little endian)
        public static List<UInt32> byteListToU32leList(List<byte> data)
        {
            Debug.Assert(data.Count % 4 == 0);
            List<UInt32> res = new List<UInt32>();
            for (int i = 0; i < data.Count; i += 4)
            {
                var entry =
                    (
                    (data[i + 0] << 0) |
                    (data[i + 1] << 8) |
                    (data[i + 2] << 16) |
                    (data[i + 3] << 24)
                    );
                unchecked
                {
                    res.Add((UInt32)entry);
                }
            }
            return res;
        }

        // Convert a word array into a byte array
        public static List<byte> u32leListToByteList(List<UInt32> data)
        {
            List<byte> res = new List<byte>();
            foreach (UInt32 x in data)
            {
                res.Add((byte)((x >> 0) & 0xFF));
                res.Add((byte)((x >> 8) & 0xFF));
                res.Add((byte)((x >> 16) & 0xFF));
                res.Add((byte)((x >> 24) & 0xFF));
            }
            return res;
        }

        // Convert a halfword array into a byte array
        public static List<byte> u16leListToByteList(List<UInt16> data)
        {
            List<byte> byteData = new List<byte>();
            foreach (var h in data)
            {
                byteData.Add((byte)(h & 0xFF));
                byteData.Add((byte)((h >> 8) & 0xFF));
            }
            return byteData;
        }

        // Convert a byte array into a halfword array
        public static List<UInt16> byteListToU16leList(List<byte> byteData)
        {
            Debug.Assert(byteData.Count % 2 == 0);
            List<UInt16> data = new List<UInt16>();
            for (int i = 0; i < byteData.Count; i += 2)
            {
                data.Add((UInt16)(byteData[i] | byteData[i + 1] << 8));
            }
            return data;
        }

        // Convert a 32-bit int to an IEEE754 float
        public static float u32BEToFloat32BE(object data)
        {
            throw new NotImplementedException();
            // var d = @struct.pack(">I", data);
            // return @struct.unpack(">f", d)[0];
        }

        // Convert an IEEE754 float to a 32-bit int
        public static UInt32 float32beToU32be(float data)
        {
            throw new NotImplementedException();
            // var d = @struct.pack(">f", data);
            // return @struct.unpack(">I", d)[0];
        }

        // Create 8-digit hexadecimal string from 32-bit register value
        public static string u32beToHex8le(UInt32 val)
        {
            return String.Join("", (new UInt32[] { val, val >> 8, val >> 16, val >> 24 }).Select(x => String.Format("{0:X02}", x & 0xFF)));
        }

        // Build 32-bit register value from little-endian 8-digit hexadecimal string
        public static UInt32 hex8leToU32be(string data)
        {
            throw new NotImplementedException();
            //return Convert.ToInt32(data[6::8] + data[4::6] + data[2::4] + data[0::2], 16);
        }

        // Build 32-bit register value from little-endian 8-digit hexadecimal string
        public static UInt32 hex8leToU32le(string data)
        {
            throw new NotImplementedException();
            // return Convert.ToInt32(data[0::2] + data[2::4] + data[4::6] + data[6::8], 16);
        }

        // Create 2-digit hexadecimal string from 8-bit value
        public static string byteToHex2(byte val)
        {
            return String.Format("{0:X02}", val);
        }

        // Convert string of hex bytes to list of integers
        public static object hexToByteList(object data)
        {
            throw new NotImplementedException();
            //return binascii.unhexlify(data).Select(i => ord(i));
        }

        public static object hexDecode(object cmd)
        {
            throw new NotImplementedException();
            //return binascii.unhexlify(cmd);
        }

        public static object hexEncode(object @string)
        {
            throw new NotImplementedException();
            //return binascii.hexlify(@string);
        }
    }
}
