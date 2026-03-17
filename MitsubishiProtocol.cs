using System;
using System.Linq;
using System.Text;

namespace SPIGAIICode.Models
{
    public class MitsubishiProtocol
    {
        public const byte SOH = 0x01;
        public const byte STX = 0x02;
        public const byte ETX = 0x03;

        public static byte[] CreateFrame(int station, string cmd, string dn, string data = "")
        {
            // Convert station to ASCII char (1-9, A-F)
            string stChar = station <= 9 ? station.ToString() : ((char)('A' + station - 10)).ToString();

            // Format: Station + Cmd(2) + STX + DataNo(2) + [Data] + ETX
            string payload = $"{stChar}{cmd}{(char)STX}{dn}{data}{(char)ETX}";
            byte[] payloadBytes = Encoding.ASCII.GetBytes(payload);

            // Checksum: Lower 2 hex digits of byte-sum
            string chk = (payloadBytes.Sum(b => b) & 0xFF).ToString("X2");
            byte[] chkBytes = Encoding.ASCII.GetBytes(chk);

            byte[] frame = new byte[payloadBytes.Length + 3]; // SOH + Payload + Checksum
            frame[0] = SOH;
            Array.Copy(payloadBytes, 0, frame, 1, payloadBytes.Length);
            Array.Copy(chkBytes, 0, frame, frame.Length - 2, 2);

            return frame;
        }

        public static (bool ok, string data, string msg) ParseResponse(byte[] raw)
        {
            try
            {
                if (raw == null || raw.Length < 4 || raw[0] != STX) return (false, "", "Bad SOF");

                char ec = (char)raw[2]; // Error Code
                int etxIndex = Array.IndexOf(raw, ETX);
                string data = Encoding.ASCII.GetString(raw, 3, etxIndex - 3);

                bool isOk = (ec == 'A' || ec == 'a');
                return (isOk, data, isOk ? "OK" : $"Error {ec}");
            }
            catch { return (false, "", "Parse Error"); }
        }
    }
}