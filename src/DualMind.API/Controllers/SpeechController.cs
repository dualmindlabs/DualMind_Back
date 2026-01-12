using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using DualMind.API.AI.Providers;

namespace DualMind.API.Controllers
{
    [Route("api/speech")]
    public class SpeechController : ControllerBase
    {
        private readonly GroqService _groqService;

        public SpeechController(GroqService groqService)
        {
            _groqService = groqService;
        }

        [HttpPost]
        [Route("generate")]
        public async Task<IActionResult> Generate([FromBody] SpeechRequest request)
        {
            if (request == null || string.IsNullOrEmpty(request.Text))
            {
                return BadRequest(new
                {
                    success = false,
                    error = "Text is required",
                    code = "INVALID_REQUEST"
                });
            }

            try
            {
                var audioBytes = await _groqService.GenerateSpeechAsync(request.Text, request.Voice ?? "Celeste-PlayAI");

                return File(audioBytes, "audio/wav", "speech.wav");
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    error = ex.Message
                });
            }
        }
    }

    public class SpeechRequest
    {
        public string Text { get; set; }
        public string Voice { get; set; }
    }
}
