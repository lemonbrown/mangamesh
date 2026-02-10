using System;

namespace MangaMesh.Peer.Core.Exceptions
{
    public class TrackerAuthenticationException : Exception
    {
        public TrackerAuthenticationException(string message) : base(message) { }
    }
}
