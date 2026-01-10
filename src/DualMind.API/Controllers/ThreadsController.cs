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
    [ApiController]
    public class ThreadsController : ControllerBase
    {
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

                var threads = await ThreadsService.GetThreadsAsync(userId, limit);

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

                var thread = await ThreadsService.CreateThreadAsync(request?.Title, userId);

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
                var thread = await ThreadsService.GetThreadAsync(threadId);

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
                string token = null;
                var messages = await ThreadMessagesService.GetThreadMessagesAsync(threadId, token);

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
    }
}
