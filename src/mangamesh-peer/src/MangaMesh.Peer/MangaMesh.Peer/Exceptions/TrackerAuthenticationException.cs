using System;

namespace MangaMesh.Client.Exceptions
{
    public class TrackerAuthenticationException : Exception
    {
        public TrackerAuthenticationException(string message) : base(message) { }
    }
}
