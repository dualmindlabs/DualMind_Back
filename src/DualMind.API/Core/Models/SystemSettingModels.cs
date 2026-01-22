using System;

namespace DualMind.API.Core.Models
{
    /// <summary>
    /// DTO for system_settings table
    /// </summary>
    public class SystemSettingDto
    {
        public string Key { get; set; }
        public string Value { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    /// <summary>
    /// Response for feature flag queries
    /// </summary>
    public class FeatureFlagResponse
    {
        public string Key { get; set; }
        public bool Enabled { get; set; }
    }
}
