using System;
using System.Collections.Generic;
using System.Text;

namespace NetworkController.DataTransferStructures
{

    /// <summary>
    /// Use when connection is lost, but you've still got security keys to restore it
    /// </summary>
    [Serializable]
    public class ConnectionRestoreRequest : ConvertableToJSONBytes<ConnectionRestoreRequest>
    {
        /// <summary>
        /// Used for making sure that data can be decrypted
        /// </summary>
        public string SampleDataForEncryptionVerification { get; set; }
    }
}
