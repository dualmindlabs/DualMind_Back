using System;
using System.Net;
using System.Threading.Tasks;
using System.Web.Http;
using DualMind_Back.Models;
using DualMind_Back.Services;

namespace DualMind_Back.Controllers
{
    [RoutePrefix("api/threads")]
    [DualMind_Back.App_Start.SupabaseAuth]
    public class ThreadsController : ApiController
    {
        [HttpGet]
        [Route("")]
        public async Task<IHttpActionResult> GetThreads([FromUri] int limit = 20, [FromUri] Guid? userId = null)
        {
            try
            {
                if (!userId.HasValue && Request.Properties.ContainsKey("UserId"))
                {
                    userId = (Guid)Request.Properties["UserId"];
                }

                var threads = await ThreadsService.GetThreadsAsync(userId, limit);

                return Ok(new { items = threads });
            }
            catch (Exception ex)
            {
                var error = ResponseFormatter.FormatErrorResponse(ex, "THREADS_ERROR");
                return Content(HttpStatusCode.InternalServerError, error);
            }
        }

        [HttpPost]
        [Route("")]
        public async Task<IHttpActionResult> CreateThread([FromBody] CreateThreadRequest request)
        {
            try
            {
                Guid? userId = request?.UserId;
                if (!userId.HasValue && Request.Properties.ContainsKey("UserId"))
                {
                    userId = (Guid)Request.Properties["UserId"];
                }

                var thread = await ThreadsService.CreateThreadAsync(request?.Title, userId);

                return Ok(thread);
            }
            catch (Exception ex)
            {
                var error = ResponseFormatter.FormatErrorResponse(ex, "THREAD_CREATE_ERROR");
                return Content(HttpStatusCode.InternalServerError, error);
            }
        }

        [HttpGet]
        [Route("{threadId:guid}")]
        public async Task<IHttpActionResult> GetThread(Guid threadId)
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
                var error = ResponseFormatter.FormatErrorResponse(ex, "THREAD_ERROR");
                return Content(HttpStatusCode.InternalServerError, error);
            }
        }

        [HttpGet]
        [Route("{threadId:guid}/messages")]
        public async Task<IHttpActionResult> GetThreadMessages(Guid threadId)
        {
            try
            {
                var token = Request.Headers.Authorization?.Parameter;
                var messages = await ThreadMessagesService.GetThreadMessagesAsync(threadId, token);

                return Ok(new { items = messages });
            }
            catch (Exception ex)
            {
                var error = ResponseFormatter.FormatErrorResponse(ex, "MESSAGES_ERROR");
                return Content(HttpStatusCode.InternalServerError, error);
            }
        }
    }
}
