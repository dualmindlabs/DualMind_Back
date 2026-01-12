using System;

namespace DualMind_Back.Core.Exceptions
{
    public class ProviderExhaustedException : Exception
    {
        public string ProviderName { get; }

        public ProviderExhaustedException(string providerName) 
            : base($"All active keys for provider '{providerName}' are exhausted or cooling down.")
        {
            ProviderName = providerName;
        }

        public ProviderExhaustedException(string providerName, string message) 
            : base(message)
        {
            ProviderName = providerName;
        }
    }
}
