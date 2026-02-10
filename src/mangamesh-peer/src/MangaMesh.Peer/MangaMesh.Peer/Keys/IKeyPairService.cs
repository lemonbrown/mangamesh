using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MangaMesh.Client.Keys
{
    public interface IKeyPairService
    {
        public Task<KeyPairResult> GenerateKeyPairBase64Async();

        string SolveChallenge(string nonceBase64, string privateKeyBase64);

        bool Verify(string publicKeyBase64, string signatureBase64, string nonceBase64);
    }
}
