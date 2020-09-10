using System;
using System.Collections.Generic;
using System.Text;

namespace NetworkController.DataTransferStructures
{
    /// <summary>
    /// Used to retrieve AdditionalInfo when Handshake is omitted (e.g. due to restoring security keys from database)
    /// </summary>
    [Serializable]
    public class AdditionalInfoRequest : ConvertableToBytes<AdditionalInfoRequest>
    {
        /// <summary>
        /// Used for making sure that data can be decrypted
        /// </summary>
        public string SampleDataForEncryptionVerification { get; set; }
    }
}
