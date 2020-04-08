using System;

namespace ARMeilleure.Tests.Assemblers
{
    class KeystoneException : Exception
    {
        public KeystoneError Error { get; }

        public KeystoneException(KeystoneError error)
        {
            Error = error;
        }

        // TODO: Get error message from keystone.
    }
}
