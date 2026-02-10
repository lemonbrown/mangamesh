using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace MangaMesh.Client.Blob
{
    internal class BlobVerificationService
    {

        public static async Task<bool> VerifyBlobAsync(
            Stream blob,
            BlobHash expected)
        {
            using var sha = SHA256.Create();
            var actual = Convert.ToHexString(
                sha.ComputeHash(blob)).ToLowerInvariant();

            return actual == expected.Value;
        }

    }
}
