using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace NetworkController.DataTransferStructures
{
    [Serializable]
    public class ConnectionInitPublicKey : ConvertableToBytes<ConnectionInitPublicKey>
    {
        public int? RespondWithThisId { get; set; }

        // TODO: remove manual RSAParamters creation

        // RSAParameters interior (couldn't make Serializable)
        public byte[] D;
        public byte[] DP;
        public byte[] DQ;
        public byte[] Exponent;
        public byte[] InverseQ;
        public byte[] Modulus;
        public byte[] P;
        public byte[] Q;

        public RSAParameters RsaParams
        {
            get
            {
                return new RSAParameters
                {
                    D = D,
                    DP = DP,
                    DQ = DQ,
                    Exponent = Exponent,
                    InverseQ = InverseQ,
                    Modulus = Modulus,
                    P = P,
                    Q = Q
                };
            }

            set
            {
                D = value.D;
                DP = value.DP;
                DQ = value.DQ;
                Exponent = value.Exponent;
                InverseQ = value.InverseQ;
                Modulus = value.Modulus;
                P = value.P;
                Q = value.Q;
            }
        }
    }
}
