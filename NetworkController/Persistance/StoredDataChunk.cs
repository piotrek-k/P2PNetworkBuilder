using NetworkController.Interfaces;
using System;
using System.Net;

namespace NetworkController.Persistance
{
    public class StoredDataChunk
    {
        public IPAddress LastIP { get; set; }
        public int LastPort { get; set; }
        public byte[] Key { get; set; }
        public Guid Id { get; set; }

        public StoredDataChunk(string[] data)
        {
            Id = Guid.Parse(data[0]);
            LastIP = IPAddress.Parse(data[1]);
            LastPort = int.Parse(data[2]);
            Key = StringToByteArray(data[3]);
        }

        public StoredDataChunk(IExternalNode node)
        {
            Id = node.Id;
            LastIP = node.CurrentEndpoint.Address;
            LastPort = node.CurrentEndpoint.Port;
            Key = node.GetSecurityKeys();
        }

        public override string ToString()
        {
            return $"{Id}\t{LastIP}\t{LastPort}\t{ByteArrayToString(Key)}";
        }

        public static string ByteArrayToString(byte[] ba)
        {
            return BitConverter.ToString(ba).Replace("-", "");
        }

        public static byte[] StringToByteArray(String hex)
        {
            int NumberChars = hex.Length;
            byte[] bytes = new byte[NumberChars / 2];
            for (int i = 0; i < NumberChars; i += 2)
                bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
            return bytes;
        }
    }
}
