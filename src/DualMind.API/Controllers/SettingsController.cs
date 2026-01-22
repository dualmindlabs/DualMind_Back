using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using DualMind.API.Core.Services;
using DualMind.API.Core.Models;

namespace DualMind.API.Controllers
{
    /// <summary>
    /// Controller for system settings and feature flags.
    /// </summary>
    [Route("api/settings")]
    public class SettingsController : ControllerBase
    {
        private readonly ISystemSettingsService _settingsService;

        public SettingsController(ISystemSettingsService settingsService)
        {
            _settingsService = settingsService;
        }

        /// <summary>
        /// Get the status of a feature flag.
        /// No authentication required - feature flags are public.
        /// </summary>
        /// <param name="key">The feature flag key (e.g., 'public_sharing')</param>
        /// <returns>The feature flag status</returns>
        [HttpGet]
        [Route("feature-flag/{key}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetFeatureFlag(string key)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(key))
                {
                    return BadRequest(new
                    {
                        success = false,
                        error = "Feature flag key is required",
                        code = "INVALID_REQUEST"
                    });
                }

                var enabled = await _settingsService.GetFeatureFlagAsync(key);

                return Ok(new FeatureFlagResponse
                {
                    Key = key,
                    Enabled = enabled
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    error = ex.Message,
                    code = "SETTINGS_ERROR"
                });
            }
        }
    }
}
