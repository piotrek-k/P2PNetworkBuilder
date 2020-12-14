using Newtonsoft.Json;
using System;
using System.Text;

namespace TransmissionComponent.Structures.Packers
{
    [Serializable]
    public abstract class ConvertableToJSONBytes<T>
    {
        public byte[] PackToBytes()
        {
            string asString = JsonConvert.SerializeObject(this);
            byte[] asBytes = Encoding.UTF8.GetBytes(asString);
            return asBytes;
        }

        public static T Unpack(byte[] encodedData)
        {
            string asString = Encoding.UTF8.GetString(encodedData);
            return JsonConvert.DeserializeObject<T>(asString);
        }
    }
}
