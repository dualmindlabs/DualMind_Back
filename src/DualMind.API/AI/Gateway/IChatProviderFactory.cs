using DualMind.API.AI.Contracts;

namespace DualMind.API.AI.Gateway
{
    public interface IChatProviderFactory
    {
        IChatProvider GetProvider(string providerName);
        IChatProvider GetGroqProvider();
    }
}
