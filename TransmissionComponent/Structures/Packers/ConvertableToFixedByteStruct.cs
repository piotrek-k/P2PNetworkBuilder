using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;

namespace TransmissionComponent.Structures.Packers
{
    [Serializable]
    public abstract class ConvertableToFixedByteStruct<T> where T : new()
    {
        public static int EstimateSize()
        {
            PropertyInfo[] properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);

            int overallSize = 0;
            int unspBlocks = 0;

            foreach (var p in properties)
            {
                FixedSizeAttribute attr = p.GetCustomAttribute<FixedSizeAttribute>();

                if (attr != null)
                {
                    overallSize += attr.SizeInBytes;
                }
                else
                {
                    ValueToPackAttribute vtpattr = p.GetCustomAttribute<ValueToPackAttribute>();

                    if (vtpattr != null)
                    {
                        unspBlocks++;
                    }
                }
            }

            if (unspBlocks > 1)
            {
                throw new Exception("Too many properties with unspecified size");
            }

            return overallSize;
        }

        public byte[] PackToBytes()
        {
            PropertyInfo[] properties = GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
            SortedList<int, byte[]> valuesToPack = new SortedList<int, byte[]>();

            int overallSize = 0;

            foreach (var p in properties)
            {
                ValueToPackAttribute valToPack = p.GetCustomAttribute<ValueToPackAttribute>();
                FixedSizeAttribute fixedSize = p.GetCustomAttribute<FixedSizeAttribute>();

                if (valToPack != null)
                {
                    object value = p.GetValue(this);
                    byte[] valueAsBytes;
                    if (p.PropertyType == typeof(byte[]))
                    {
                        valueAsBytes = (byte[])value;
                    }
                    else
                    {
                        dynamic castedValue = Convert.ChangeType(value, p.PropertyType);
                        valueAsBytes = BitConverter.GetBytes(castedValue);
                    }

                    if (valueAsBytes == null)
                    {
                        valueAsBytes = new byte[fixedSize != null ? fixedSize.SizeInBytes : 0];
                    }

                    if (fixedSize != null && valueAsBytes.Length != fixedSize.SizeInBytes)
                    {
                        throw new Exception("Fixed size doesn't fit actual data");
                    }

                    valuesToPack.Add(valToPack.PlaceInSequence, valueAsBytes);
                    overallSize += valueAsBytes.Length;
                }
            }

            byte[] result = new byte[overallSize];
            int nextBlockStart = 0;
            foreach (var v in valuesToPack)
            {
                Buffer.BlockCopy(v.Value, 0, result, nextBlockStart, v.Value.Length);
                nextBlockStart += v.Value.Length;
            }

            return result;
        }

        public static T Unpack(byte[] encodedData)
        {
            T result = new T();

            List<PropertyInfo> properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance).ToList();
            var ordered = properties.Select((p) => new
            {
                prop = p,
                valToPack = p.GetCustomAttribute<ValueToPackAttribute>(),
                fixedSize = p.GetCustomAttribute<FixedSizeAttribute>(),
                zerosLikeNullAttr = p.GetCustomAttribute<TreatZerosLikeNullAttribute>()
            }).Where(x => x.valToPack != null).OrderBy(x => x.valToPack.PlaceInSequence);

            int startIndex = 0;

            foreach (var o in ordered)
            {
                if (o.prop.PropertyType == typeof(int))
                {
                    o.prop.SetValue(
                        result,
                        BitConverter.ToInt32(encodedData, startIndex));
                }
                else if (o.prop.PropertyType == typeof(uint))
                {
                    o.prop.SetValue(
                        result,
                        BitConverter.ToUInt32(encodedData, startIndex));
                }
                else if (o.prop.PropertyType == typeof(Guid))
                {
                    o.prop.SetValue(
                        result,
                        new Guid(encodedData.Skip(startIndex).Take(o.fixedSize.SizeInBytes).ToArray()));
                }
                else if (o.prop.PropertyType == typeof(bool))
                {
                    o.prop.SetValue(
                        result,
                        BitConverter.ToBoolean(encodedData, startIndex));
                }
                else if (o.prop.PropertyType == typeof(byte[]))
                {
                    if (o.fixedSize == null)
                    {
                        byte[] nonfixedArray = encodedData.Skip(startIndex).ToArray();
                        if (o.zerosLikeNullAttr != null && nonfixedArray.Length == 0 || nonfixedArray.All(x => x == 0))
                        {
                            o.prop.SetValue(result, null);
                        }
                        else
                        {
                            o.prop.SetValue(
                               result,
                               nonfixedArray);
                        }
                    }
                    else
                    {
                        if (o.zerosLikeNullAttr != null && encodedData.Skip(startIndex).Take(o.fixedSize.SizeInBytes).SequenceEqual(new byte[16]))
                        {
                            o.prop.SetValue(result, null);
                        }
                        else
                        {
                            o.prop.SetValue(
                                result,
                                encodedData.Skip(startIndex).Take(o.fixedSize.SizeInBytes).ToArray());
                        }
                    }
                }
                else
                {
                    throw new NotImplementedException();
                }

                if (o.fixedSize != null)
                {
                    startIndex += o.fixedSize.SizeInBytes;
                }
                else if (ordered.Last().valToPack.PlaceInSequence != o.valToPack.PlaceInSequence)
                {
                    throw new Exception("One non-fixed size allowed at the end of sequence");
                }

            }

            return result;
        }
    }
}
