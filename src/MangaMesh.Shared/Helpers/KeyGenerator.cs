using NSec.Cryptography;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MangaMesh.Shared.Helpers
{
    public static class KeyGenerator
    {
        public static Key GenerateEd25519Key()
        {
            var creationParameters = new KeyCreationParameters
            {
                ExportPolicy = KeyExportPolicies.AllowPlaintextExport
            };

            return new Key(SignatureAlgorithm.Ed25519, creationParameters);
        }
    }
}
