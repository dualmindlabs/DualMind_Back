using System;
using System.Threading.Tasks;
using System.Web.Http;
using DualMind_Back.Services;

namespace DualMind_Back.Controllers
{
    [RoutePrefix("api/speech")]
    public class SpeechController : ApiController
    {
        private readonly GroqService _groqService;

        public SpeechController()
        {
            _groqService = new GroqService();
        }

        [HttpPost]
        [Route("generate")]
        public async Task<IHttpActionResult> Generate([FromBody] SpeechRequest request)
        {
            if (string.IsNullOrEmpty(request.Text))
            {
                return BadRequest("Text is required");
            }

            try
            {
                var audioBytes = await _groqService.GenerateSpeechAsync(request.Text, request.Voice ?? "Celeste-PlayAI");

                var result = new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new System.Net.Http.ByteArrayContent(audioBytes)
                };

                result.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("audio/wav");
                result.Content.Headers.ContentDisposition = new System.Net.Http.Headers.ContentDispositionHeaderValue("attachment")
                {
                    FileName = "speech.wav"
                };

                return ResponseMessage(result);
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }
    }

    public class SpeechRequest
    {
        public string Text { get; set; }
        public string Voice { get; set; }
    }
}
