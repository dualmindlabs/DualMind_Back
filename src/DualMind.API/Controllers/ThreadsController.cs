using System;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using DualMind.API.Core.Models;
using DualMind.API.Core.Services;

namespace DualMind.API.Controllers
{
    [Route("api/threads")]
    [Authorize]
    public class ThreadsController : ControllerBase
    {
        private readonly IThreadsService _threadsService;
        private readonly IThreadMessagesService _threadMessagesService;
        private readonly IUserSyncService _userSyncService;
        private readonly ISystemSettingsService _systemSettingsService;

        public ThreadsController(
            IThreadsService threadsService, 
            IThreadMessagesService threadMessagesService,
            IUserSyncService userSyncService,
            ISystemSettingsService systemSettingsService)
        {
            _threadsService = threadsService;
            _threadMessagesService = threadMessagesService;
            _userSyncService = userSyncService;
            _systemSettingsService = systemSettingsService;
        }

        [HttpGet]
        [Route("")]
        public async Task<IActionResult> GetThreads([FromQuery] int limit = 20)
        {
            try
            {
                Guid? userId = null;
                var sub = User.FindFirst("sub")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (Guid.TryParse(sub, out var parsedId))
                {
                    userId = parsedId;
                }

                if (!userId.HasValue)
                {
                    return Unauthorized(new { error = "User ID claim missing" });
                }

                var threads = await _threadsService.GetThreadsAsync(userId, limit);

                return Ok(new { items = threads });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    error = ex.Message,
                    code = "THREADS_ERROR"
                });
            }
        }

        [HttpPost]
        [Route("")]
        public async Task<IActionResult> CreateThread([FromBody] CreateThreadRequest request)
        {
            try
            {
                Guid? userId = null;
                var sub = User.FindFirst("sub")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (Guid.TryParse(sub, out var parsedId))
                {
                    userId = parsedId;
                }

                if (!userId.HasValue)
                {
                    return Unauthorized(new { error = "User ID claim missing" });
                }

                // 🚨 BLOCKER FIX: Ensure public.users row exists before creating thread
                // Extract email/name from claims if available
                var email = User.FindFirst("email")?.Value 
                    ?? User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;
                    
                var name = User.FindFirst("full_name")?.Value 
                    ?? User.FindFirst("name")?.Value 
                    ?? User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value;
                
                await _userSyncService.EnsureUserExistsAsync(userId.Value, email, name);

                var thread = await _threadsService.CreateThreadAsync(request?.Title, userId);

                return Ok(thread);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    error = ex.Message,
                    code = "THREAD_CREATE_ERROR"
                });
            }
        }

        /// <summary>
        /// Get a single thread by ID.
        /// Supports public sharing: if public_sharing feature flag is enabled AND thread visibility is public/unlisted,
        /// the endpoint will return the thread without requiring authentication.
        /// </summary>
        [HttpGet]
        [Route("{threadId:guid}")]
        [AllowAnonymous] // Allow both authenticated and anonymous requests
        public async Task<IActionResult> GetThread(Guid threadId)
        {
            try
            {
                var thread = await _threadsService.GetThreadAsync(threadId);

                if (thread == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        error = "Thread not found",
                        code = "NOT_FOUND"
                    });
                }

                // Check if public sharing is enabled
                var publicSharingEnabled = await _systemSettingsService.GetFeatureFlagAsync("public_sharing");

                // Determine if user is authenticated
                Guid? userId = null;
                var sub = User.FindFirst("sub")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (Guid.TryParse(sub, out var parsedId))
                {
                    userId = parsedId;
                }

                var isAuthenticated = User.Identity?.IsAuthenticated == true && userId.HasValue;

                // PUBLIC SHARING LOGIC:
                // If public_sharing is enabled AND visibility is public/unlisted → allow access without auth
                // Otherwise → require auth and user_id match (for private threads)
                
                if (publicSharingEnabled && 
                    (thread.Visibility == "public" || thread.Visibility == "unlisted"))
                {
                    // Public/unlisted thread with feature enabled - allow access
                    return Ok(thread);
                }

                // For private threads OR when public_sharing is disabled, require authentication
                if (!isAuthenticated)
                {
                    return Unauthorized(new
                    {
                        success = false,
                        error = "Authentication required to access this thread",
                        code = "UNAUTHORIZED"
                    });
                }

                // For private threads, verify ownership
                if (thread.Visibility == "private" && thread.UserId != userId)
                {
                    return StatusCode(403, new
                    {
                        success = false,
                        error = "You do not have access to this thread",
                        code = "FORBIDDEN"
                    });
                }

                return Ok(thread);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    error = ex.Message,
                    code = "THREAD_ERROR"
                });
            }
        }

        /// <summary>
        /// Get messages for a thread.
        /// Supports public sharing: follows the same rules as GetThread for public/unlisted threads.
        /// </summary>
        [HttpGet]
        [Route("{threadId:guid}/messages")]
        [AllowAnonymous] // Allow both authenticated and anonymous requests
        public async Task<IActionResult> GetThreadMessages(Guid threadId)
        {
            try
            {
                // First check thread access (same logic as GetThread)
                var thread = await _threadsService.GetThreadAsync(threadId);

                if (thread == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        error = "Thread not found",
                        code = "NOT_FOUND"
                    });
                }

                var publicSharingEnabled = await _systemSettingsService.GetFeatureFlagAsync("public_sharing");

                Guid? userId = null;
                var sub = User.FindFirst("sub")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (Guid.TryParse(sub, out var parsedId))
                {
                    userId = parsedId;
                }

                var isAuthenticated = User.Identity?.IsAuthenticated == true && userId.HasValue;

                // Check access permissions
                if (!(publicSharingEnabled && (thread.Visibility == "public" || thread.Visibility == "unlisted")))
                {
                    if (!isAuthenticated)
                    {
                        return Unauthorized(new
                        {
                            success = false,
                            error = "Authentication required to access this thread",
                            code = "UNAUTHORIZED"
                        });
                    }

                    if (thread.Visibility == "private" && thread.UserId != userId)
                    {
                        return StatusCode(403, new
                        {
                            success = false,
                            error = "You do not have access to this thread",
                            code = "FORBIDDEN"
                        });
                    }
                }

                var messages = await _threadMessagesService.GetThreadMessagesAsync(threadId, userId);

                return Ok(new { items = messages });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    error = ex.Message,
                    code = "MESSAGES_ERROR"
                });
            }
        }

        /// <summary>
        /// Update thread title. Requires authentication and ownership.
        /// </summary>
        [HttpPatch]
        [Route("{threadId:guid}")]
        public async Task<IActionResult> UpdateThread(Guid threadId, [FromBody] UpdateThreadRequest request)
        {
            try
            {
                // Get user ID from token
                Guid? userId = null;
                var sub = User.FindFirst("sub")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (Guid.TryParse(sub, out var parsedId))
                {
                    userId = parsedId;
                }

                if (!userId.HasValue)
                {
                    return Unauthorized(new { error = "User ID claim missing" });
                }

                // Verify ownership
                var thread = await _threadsService.GetThreadAsync(threadId);
                if (thread == null)
                {
                    return NotFound(new { success = false, error = "Thread not found", code = "NOT_FOUND" });
                }

                if (thread.UserId != userId)
                {
                    return StatusCode(403, new
                    {
                        success = false,
                        error = "You do not have permission to update this thread",
                        code = "FORBIDDEN"
                    });
                }

                if (string.IsNullOrWhiteSpace(request?.Title))
                {
                    return BadRequest(new
                    {
                        success = false,
                        error = "Title is required",
                        code = "INVALID_REQUEST"
                    });
                }

                await _threadsService.UpdateThreadAsync(threadId, request.Title);

                return Ok(new
                {
                    success = true,
                    message = "Thread updated successfully"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    error = ex.Message,
                    code = "THREAD_UPDATE_ERROR"
                });
            }
        }

        /// <summary>
        /// Update thread visibility. Requires authentication and ownership.
        /// Only the thread owner can change visibility.
        /// </summary>
        [HttpPatch]
        [Route("{threadId:guid}/visibility")]
        public async Task<IActionResult> UpdateThreadVisibility(Guid threadId, [FromBody] UpdateThreadVisibilityRequest request)
        {
            try
            {
                // Get user ID from token
                Guid? userId = null;
                var sub = User.FindFirst("sub")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (Guid.TryParse(sub, out var parsedId))
                {
                    userId = parsedId;
                }

                if (!userId.HasValue)
                {
                    return Unauthorized(new { error = "User ID claim missing" });
                }

                // Verify thread exists
                var thread = await _threadsService.GetThreadAsync(threadId);
                if (thread == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        error = "Thread not found",
                        code = "NOT_FOUND"
                    });
                }

                // Verify ownership - only the owner can change visibility
                if (thread.UserId != userId)
                {
                    return StatusCode(403, new
                    {
                        success = false,
                        error = "Only the thread owner can change visibility",
                        code = "FORBIDDEN"
                    });
                }

                // Validate visibility value
                if (string.IsNullOrWhiteSpace(request?.Visibility))
                {
                    return BadRequest(new
                    {
                        success = false,
                        error = "Visibility is required",
                        code = "INVALID_REQUEST"
                    });
                }

                var validVisibilities = new[] { "private", "public", "unlisted" };
                if (!Array.Exists(validVisibilities, v => v == request.Visibility.ToLowerInvariant()))
                {
                    return BadRequest(new
                    {
                        success = false,
                        error = $"Invalid visibility. Must be one of: {string.Join(", ", validVisibilities)}",
                        code = "INVALID_REQUEST"
                    });
                }

                await _threadsService.UpdateThreadVisibilityAsync(threadId, request.Visibility);

                return Ok(new
                {
                    success = true,
                    message = "Thread visibility updated successfully",
                    visibility = request.Visibility.ToLowerInvariant()
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    error = ex.Message,
                    code = "VISIBILITY_UPDATE_ERROR"
                });
            }
        }

        [HttpDelete]
        [Route("{threadId:guid}")]
        public async Task<IActionResult> DeleteThread(Guid threadId)
        {
            try
            {
                // Get user ID from token
                Guid? userId = null;
                var sub = User.FindFirst("sub")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (Guid.TryParse(sub, out var parsedId))
                {
                    userId = parsedId;
                }

                if (!userId.HasValue)
                {
                    return Unauthorized(new { error = "User ID claim missing" });
                }

                // Verify ownership
                var thread = await _threadsService.GetThreadAsync(threadId);
                if (thread == null)
                {
                    return NotFound(new { success = false, error = "Thread not found", code = "NOT_FOUND" });
                }

                if (thread.UserId != userId)
                {
                    return StatusCode(403, new
                    {
                        success = false,
                        error = "You do not have permission to delete this thread",
                        code = "FORBIDDEN"
                    });
                }

                await _threadsService.DeleteThreadAsync(threadId);

                return Ok(new
                {
                    success = true,
                    message = "Thread deleted successfully"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    error = ex.Message,
                    code = "THREAD_DELETE_ERROR"
                });
            }
        }
    }

    public class UpdateThreadRequest
    {
        public string Title { get; set; }
    }
}
