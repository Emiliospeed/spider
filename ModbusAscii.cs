using System;
using System.Text;

namespace SPIGAIICode
{
    public static class ModbusAscii
    {
        public static byte CalculateLRC(byte[] data)
        {
            byte sum = 0;
            foreach (byte b in data) sum += b;
            return (byte)((~sum + 1) & 0xFF);
        }

        // Builds a Read request (Function 03)
        public static string BuildReadRequest(byte slave, ushort startAddr, ushort count)
        {
            byte[] payload = {
                slave,
                0x03,
                (byte)(startAddr >> 8), (byte)(startAddr & 0xFF),
                (byte)(count >> 8), (byte)(count & 0xFF)
            };
            return FormatFrame(payload);
        }

        public static string BuildSingleWrite(byte slave, ushort addr, ushort value)
        {
            byte[] payload = { slave, 0x06, (byte)(addr >> 8), (byte)(addr & 0xFF), (byte)(value >> 8), (byte)(value & 0xFF) };
            return FormatFrame(payload);
        }

        private static string FormatFrame(byte[] payload)
        {
            byte lrc = CalculateLRC(payload);
            StringBuilder sb = new StringBuilder(":");
            foreach (byte b in payload) sb.Append(b.ToString("X2"));
            sb.Append(lrc.ToString("X2"));
            sb.Append("\r\n");
            return sb.ToString();
        }

        // Parses the ASCII hex response back into integers
        public static int[] ParseReadResponse(string response)
        {
            try
            {
                if (!response.StartsWith(":") || response.Length < 11) return null;
                // Remove ":" and LRC/CRLF
                string dataHex = response.Substring(7, response.Length - 11);
                int[] values = new int[dataHex.Length / 4];
                for (int i = 0; i < values.Length; i++)
                {
                    values[i] = Convert.ToInt32(dataHex.Substring(i * 4, 4), 16);
                }
                return values;
            }
            catch { return null; }
        }
    }
}