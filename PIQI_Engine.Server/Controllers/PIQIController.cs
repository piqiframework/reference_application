using Microsoft.AspNetCore.Mvc;
using PIQI.Components.CustomExceptionClasses;
using PIQI.Components.Models;
using PIQI_Engine.Server.Engines;

namespace PIQI_Engine.Server.Controllers;

/// <summary>
/// Provides endpoints for processing, scoring, and auditing PIQI messages.
/// Supports both JSON and form-data inputs.
/// </summary>
[Route("[controller]")]
[ApiController]
public class PIQIController : ControllerBase
{
    private readonly PIQIEngine _piqiEngine;

    /// <summary>
    /// Initializes a new instance of the <see cref="PIQIController"/> class.
    /// </summary>
    /// <param name="piqiEngine">The PIQI engine used to process requests.</param> 
    public PIQIController(PIQIEngine piqiEngine) => _piqiEngine = piqiEngine;

    #region Post Requests
    /// <summary> 
    /// Processes and scores a PIQI message.
    /// </summary>
    /// <param name="piqiRequest">The request containing the PIQI message to score.</param>
    /// <returns>
    /// An <see cref="ActionResult{PIQIResponse}"/> containing the scoring information and data class counts.
    /// </returns>
    /// <response code="200">Message evaluated successfully.</response>
    /// <response code="400">Request body is missing or malformed.</response>
    /// <response code="422">PIQIModelMnemonic does not match the model bound to the rubric, or EvaluationRubricMnemonic cannot be resolved.</response>
    /// <response code="500">Unexpected processing failure — response body includes error detail.</response>
    [HttpPost("ScoreMessage")]
    public async Task<ActionResult<PIQIResponse>> ScoreMessage([FromBody] PIQIRequest piqiRequest)
    {
        PIQIResponse result = new PIQIResponse();
        try
        {
            if (piqiRequest == null)
                return BadRequest("PIQIRequest cannot be null.");

            result = await _piqiEngine.PiqiRequestAsync(piqiRequest, false);
            if (!result.Succeeded)
                return StatusCode(500, result);

            return Ok(result);
        }
        catch (CustomPIQIException ex)
        {
            return StatusCode(ex.HTTPStatusCode ?? 500, new { Error = ex.Error });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Error = new { Message = ex.Message } });
        }
    }


    /// <summary>
    /// Processes, scores, and audits a PIQI message.
    /// </summary>
    /// <param name="piqiRequest">The request containing the PIQI message to score and audit.</param>
    /// <returns>
    /// An <see cref="ActionResult{PIQIResponse}"/> with scoring information, data class counts, and the audited message.
    /// </returns>
    /// <response code="200">Message evaluated successfully.</response>
    /// <response code="400">Request body is missing or malformed.</response>
    /// <response code="422">PIQIModelMnemonic does not match the model bound to the rubric, or EvaluationRubricMnemonic cannot be resolved.</response>
    /// <response code="500">Unexpected processing failure — response body includes error detail.</response>
    [HttpPost("ScoreAuditMessage")]
    public async Task<ActionResult<PIQIResponse>> ScoreAuditMessage([FromBody] PIQIRequest piqiRequest)
    {
        PIQIResponse result = new PIQIResponse();
        try
        {
            if (piqiRequest == null)
                return BadRequest("PIQIRequest cannot be null.");

            result = await _piqiEngine.PiqiRequestAsync(piqiRequest, true);
            if (!result.Succeeded)
                return StatusCode(500, result);

            return Ok(result);
        }
        catch (CustomPIQIException ex)
        {
            return StatusCode(ex.HTTPStatusCode ?? 500, new { Error = ex.Error });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Error = new { Message = ex.Message } });
        }
    }

    #endregion
}
