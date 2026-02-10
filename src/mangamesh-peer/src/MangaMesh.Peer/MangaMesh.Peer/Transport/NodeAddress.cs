using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MangaMesh.Client.Transport
{
    public record NodeAddress(string Host, int Port, string? OnionAddress = null);

}
