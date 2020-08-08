using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace NetworkController.DataTransferStructures
{
    [Serializable]
    public abstract class ConvertableToBytes<T>
    {
        /// <summary>
        /// Convert object to byte array
        /// </summary>
        /// <returns></returns>
        public byte[] PackToBytes()
        {
            byte[] result;

            BinaryFormatter formatter = new BinaryFormatter();
            using (MemoryStream stream = new MemoryStream())
            {
                formatter.Serialize(stream, this);
                result = stream.ToArray();
            }

            return result;
        }

        public static T Unpack(byte[] encodedData)
        {
            T result;

            using (MemoryStream stream = new MemoryStream())
            {
                stream.Write(encodedData, 0, encodedData.Length);
                stream.Seek(0, SeekOrigin.Begin);
                BinaryFormatter bf = new BinaryFormatter();
                result = (T)bf.Deserialize(stream);
            }

            return result;
        }
    }
}
