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
    [DualMind.API.Filters.SupabaseAuth]
    public class ThreadsController : ControllerBase
    {
        private readonly IThreadsService _threadsService;
        private readonly IThreadMessagesService _threadMessagesService;
        private readonly IUserSyncService _userSyncService;

        public ThreadsController(
            IThreadsService threadsService, 
            IThreadMessagesService threadMessagesService,
            IUserSyncService userSyncService)
        {
            _threadsService = threadsService;
            _threadMessagesService = threadMessagesService;
            _userSyncService = userSyncService;
        }

        [HttpGet]
        [Route("")]
        public async Task<IActionResult> GetThreads([FromQuery] int limit = 20, [FromQuery] Guid? userId = null)
        {
            try
            {
                if (!userId.HasValue && HttpContext.Items.ContainsKey("UserId"))
                {
                    userId = (Guid)HttpContext.Items["UserId"];
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
                Guid? userId = request?.UserId;
                if (!userId.HasValue && HttpContext.Items.ContainsKey("UserId"))
                {
                    userId = (Guid)HttpContext.Items["UserId"];
                }

                // 🚨 BLOCKER FIX: Ensure public.users row exists before creating thread
                if (userId.HasValue)
                {
                    var email = HttpContext.Items.ContainsKey("UserEmail") 
                        ? HttpContext.Items["UserEmail"]?.ToString() 
                        : null;
                    var name = HttpContext.Items.ContainsKey("UserName") 
                        ? HttpContext.Items["UserName"]?.ToString() 
                        : null;
                    
                    await _userSyncService.EnsureUserExistsAsync(userId.Value, email, name);
                }

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

        [HttpGet]
        [Route("{threadId:guid}")]
        public async Task<IActionResult> GetThread(Guid threadId)
        {
            try
            {
                var thread = await _threadsService.GetThreadAsync(threadId);

                if (thread == null)
                {
                    return NotFound();
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

        [HttpGet]
        [Route("{threadId:guid}/messages")]
        public async Task<IActionResult> GetThreadMessages(Guid threadId)
        {
            try
            {
                var token = Request.Headers["Authorization"].ToString().Replace("Bearer ", "");
                var messages = await _threadMessagesService.GetThreadMessagesAsync(threadId, token);

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

        [HttpPatch]
        [Route("{threadId:guid}")]
        public async Task<IActionResult> UpdateThread(Guid threadId, [FromBody] UpdateThreadRequest request)
        {
            try
            {
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

        [HttpDelete]
        [Route("{threadId:guid}")]
        public async Task<IActionResult> DeleteThread(Guid threadId)
        {
            try
            {
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
