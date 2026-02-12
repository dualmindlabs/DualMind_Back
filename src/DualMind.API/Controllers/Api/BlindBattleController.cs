using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace DualMind.API.Controllers.Api
{
    [Route("api/arena/blind-battle")]
    [ApiController]
    public class BlindBattleController : ControllerBase
    {
        [HttpPost]
        [Route("")]
        public IActionResult StartBattle([FromBody] BlindBattleRequest request)
        {
            // MOCK IMPLEMENTATION - As requested for Phase 2 UI development
            var battleId = Guid.NewGuid();
            
            return Ok(new
            {
                battleId = battleId,
                response1 = new
                {
                    message = "This is a mock response from Model A. It is designed to test the streaming UI capabilities.",
                    modelName = "hidden-model-a"
                },
                response2 = new
                {
                    message = "This is a mock response from Model B. It helps verify that side-by-side rendering works correctly.",
                    modelName = "hidden-model-b"
                }
            });
        }
    }

    public class BlindBattleRequest
    {
        public string Prompt { get; set; }
    }
}
