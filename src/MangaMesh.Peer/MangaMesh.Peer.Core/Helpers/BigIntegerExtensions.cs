using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MangaMesh.Peer.Core.Helpers
{
    public static class BigIntegerExtensions
    {
        public static int BitLength(this System.Numerics.BigInteger bi)
        {
            var bytes = bi.ToByteArray(isUnsigned: true, isBigEndian: true);
            if (bytes.Length == 0) return 0;
            int bits = (bytes.Length - 1) * 8;
            byte msb = bytes[0];
            while (msb != 0)
            {
                bits++;
                msb >>= 1;
            }
            return bits;
        }
    }
}
