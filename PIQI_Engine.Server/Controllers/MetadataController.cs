using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PIQI.Components.CustomExceptionClasses;
using PIQI.Components.Models;
using PIQI_Engine.Server.Engines;

namespace PIQI_Engine.Server.Controllers;

/// <summary>
/// Provides endpoints for accessing reference data used across the application.
/// </summary>
[Authorize]
[Route("[controller]")]
[ApiController]
public class MetadataController : ControllerBase
{
    #region Properties
    private readonly ReferenceDataEngine _referenceDataEngine;

    /// <summary>
    /// Initializes a new instance of the <see cref="MetadataController"/> class.
    /// </summary>
    /// <param name="referenceDataEngine">The PIQI engine used to process requests.</param>
    public MetadataController(ReferenceDataEngine referenceDataEngine) => _referenceDataEngine = referenceDataEngine;

    #endregion

    #region Get Requests
    /// <summary>
    /// Loads all registered evaluation rubrics.
    /// </summary>
    /// <returns>A list of evaluation profiles.</returns>
    /// <response code="200">The evaluation rubrics were successfully loaded.</response>
    /// <response code="500">An internal server error occurred during the load operation.</response>
    [HttpGet("Evaluations")] 
    public ActionResult<List<ReferenceDataDto>> LoadAllEvaluationRubrics()
    {
        try
        {
            List<ReferenceDataDto> response = _referenceDataEngine.LoadAllEvaluationRubrics();
            if (response == null) throw new CustomPIQIException(500, "REFERENCE_DATA_NOT_FOUND", "Failed to load evaluation rubrics.");
            return Ok(response);
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
    /// Loads a specific evaluation rubric by mnemonic. 
    /// </summary>
    /// <param name="mnemonic">The mnemonic of the evaluation rubric to load.</param>
    /// <returns>The evaluation rubric.</returns>
    /// <response code="200">The evaluation rubric was successfully loaded.</response>
    /// <response code="400">The request was invalid or malformed.</response>
    /// <response code="500">An internal server error occurred during the load operation.</response>
    [HttpGet("Evaluation/{mnemonic}")]
    public ActionResult<EvaluationRubric> LoadEvaluationRubric(string mnemonic)
    {
        try
        {
            EvaluationRubric? response = _referenceDataEngine.LoadEvaluationRubric(mnemonic, null);
            if (response == null) throw new CustomPIQIException(500, "REFERENCE_DATA_NOT_FOUND", "Failed to load evaluation rubric.");
            return Ok(response);
        }
        catch (CustomPIQIException ex)
        {
            return StatusCode((ex.HTTPStatusCode == 422 ? 400 : ex.HTTPStatusCode) ?? 500, new { Error = ex.Error });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Error = new { Message = ex.Message } });
        }
    }

    /// <summary>
    /// Loads all registered models.
    /// </summary>
    /// <returns>A list of models.</returns>
    /// <response code="200">The models were successfully loaded.</response>
    /// <response code="500">An internal server error occurred during the load operation.</response>
    [HttpGet("Models")]
    public ActionResult<List<ReferenceDataDto>> LoadAllModels()
    {
        try
        {
            List<ReferenceDataDto> response = _referenceDataEngine.LoadAllModels();
            if (response == null) throw new CustomPIQIException(500, "REFERENCE_DATA_NOT_FOUND", "Failed to load models.");
            return Ok(response);
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
    /// Loads a specific model by mnemonic.
    /// </summary>
    /// <param name="mnemonic">The mnemonic of the model to load.</param>
    /// <returns>The model.</returns>
    /// <response code="200">The model was successfully loaded.</response>
    /// <response code="400">The request was invalid or malformed.</response>
    /// <response code="500">An internal server error occurred during the load operation.</response>
    [HttpGet("Model/{mnemonic}")]
    public ActionResult<Model> LoadModel(string mnemonic)
    {
        try
        {
            Model? response = _referenceDataEngine.LoadModel(mnemonic);
            if (response == null) throw new CustomPIQIException(500, "REFERENCE_DATA_NOT_FOUND", "Failed to load model.");
            return Ok(response);
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
    /// Loads all registered SAMs.
    /// </summary>
    /// <returns>A list of SAMs.</returns>
    /// <response code="200">The SAMs were successfully loaded.</response>
    /// <response code="500">An internal server error occurred during the load operation.</response>
    [HttpGet("SAMs")]
    public ActionResult<List<SAM>> LoadAllSAMs()
    {
        try
        {
            List<SAM> response = _referenceDataEngine.LoadSAMs();
            if (response == null) throw new CustomPIQIException(500, "REFERENCE_DATA_NOT_FOUND", "Failed to load SAMs.");
            return Ok(response);
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
    /// Loads a specific SAM by mnemonic.
    /// </summary>
    /// <param name="mnemonic">The mnemonic of the SAM to load.</param>
    /// <returns>The SAM.</returns>
    /// <response code="200">The SAM was successfully loaded.</response>
    /// <response code="400">The request was invalid or malformed.</response>
    /// <response code="500">An internal server error occurred during the load operation.</response>
    [HttpGet("SAM/{mnemonic}")]
    public ActionResult<SAM> LoadSAM(string mnemonic)
    {
        try
        {
            SAM? response = _referenceDataEngine.LoadSAM(mnemonic);
            if (response == null) throw new CustomPIQIException(500, "REFERENCE_DATA_NOT_FOUND", "Failed to load SAM.");
            return Ok(response);
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

    #region Post Methods

    /// <summary>
    /// Imports a new evaluation rubric into the system.
    /// </summary>
    /// <param name="evaluationName">The name of the evaluation rubric.</param>
    /// <param name="evaluationMnemonic">The unique mnemonic identifier for the evaluation rubric.</param>
    /// <param name="evaluation">The evaluation rubric file to import.</param>
    /// <returns>Status indicating success or failure of the import operation.</returns>
    /// <response code="200">The evaluation rubric was accepted and persisted.</response>
    /// <response code="400">Request body is missing or malformed.</response>
    /// <response code="409">The mnemonic + version already exists with different content.</response>
    /// <response code="422">Required related reference data not found.</response>
    /// <response code="500">An internal server error occurred during the import operation.</response>
    [HttpPost("ImportEvaluation")]
    public IActionResult ImportEvaluation([FromForm] string evaluationName, [FromForm] string evaluationMnemonic, IFormFile evaluation)
    {
        try
        {
            _referenceDataEngine.ImportEvaluation(evaluationName, evaluationMnemonic, evaluation);
            return Ok();
        }
        catch(CustomPIQIException ex)
        {
            return StatusCode(ex.HTTPStatusCode ?? 500, new { Error = ex.Error });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Error = new { Message = ex.Message } });
        }
    }

    /// <summary>
    /// Imports a new model into the system.
    /// </summary>
    /// <param name="modelName">The name of the model.</param>
    /// <param name="modelMnemonic">The unique mnemonic identifier for the model.</param>
    /// <param name="model">The model file to import.</param>
    /// <returns>Status indicating success or failure of the import operation.</returns>
    /// <response code="200">The model was accepted and persisted.</response>
    /// <response code="400">Request body is missing or malformed.</response>
    /// <response code="409">The mnemonic + version already exists with different content.</response>
    /// <response code="422">Schema validation failure.</response>
    /// <response code="500">An internal server error occurred during the import operation.</response>
    [HttpPost("ImportModel")]
    public IActionResult ImportModel([FromForm] string modelName, [FromForm] string modelMnemonic, IFormFile model)
    {
        try
        {
            _referenceDataEngine.ImportModel(modelName, modelMnemonic, model);
            return Ok();
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

    #region Delete Methods

    /// <summary>
    /// Removes an evaluation rubric from the system.
    /// </summary>
    /// <param name="evaluationMnemonic">The unique mnemonic identifier of the evaluation rubric to remove.</param>
    /// <returns>Status indicating success or failure of the removal operation.</returns>
    /// <response code="200">The evaluation rubric was successfully removed.</response>
    /// <response code="400">The request was invalid or malformed.</response>
    /// <response code="405">The evaluation rubric cannot be removed because it is in the configuration file.</response>
    /// <response code="500">An internal server error occurred during the removal operation.</response>
    [HttpDelete("RemoveEvaluation/{evaluationMnemonic}")]
    public IActionResult RemoveEvaluation(string evaluationMnemonic)
    {
        try
        {
            _referenceDataEngine.RemoveEvaluation(evaluationMnemonic);
            return Ok();
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
    /// Removes a model from the system.
    /// </summary>
    /// <param name="evaluationMnemonic">The unique mnemonic identifier of the model to remove.</param>
    /// <returns>Status indicating success or failure of the removal operation.</returns>
    /// <response code="200">The model was successfully removed.</response>
    /// <response code="400">The request was invalid or malformed.</response>
    /// <response code="405">The model cannot be removed because it is in the configuration file.</response>
    /// <response code="500">An internal server error occurred during the removal operation.</response>
    [HttpDelete("RemoveModel/{modelMnemonic}")]
    public IActionResult RemoveModel(string evaluationMnemonic)
    {
        try
        {
            _referenceDataEngine.RemoveModel(evaluationMnemonic);
            return Ok();
        }
        catch (CustomPIQIException ex)
        {
            return StatusCode(ex.HTTPStatusCode ?? 500, new { Error = ex.Error });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Error = new { Message = ex.Message }});
        }
    }

    #endregion
}