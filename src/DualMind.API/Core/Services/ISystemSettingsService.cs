using System.Threading.Tasks;

namespace DualMind.API.Core.Services
{
    public interface ISystemSettingsService
    {
        /// <summary>
        /// Gets the boolean value of a feature flag from system_settings table
        /// </summary>
        /// <param name="key">The setting key (e.g., "public_sharing")</param>
        /// <returns>True if the feature is enabled, false otherwise</returns>
        Task<bool> GetFeatureFlagAsync(string key);

        /// <summary>
        /// Gets a raw setting value from system_settings table
        /// </summary>
        /// <param name="key">The setting key</param>
        /// <returns>The value as string, or null if not found</returns>
        Task<string?> GetSettingAsync(string key);
    }
}
