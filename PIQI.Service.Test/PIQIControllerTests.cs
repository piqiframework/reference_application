using Microsoft.AspNetCore.Mvc.Testing;
using Newtonsoft.Json;
using PIQI.Components.Models;
using PIQI.Service.WebTesting.Rest;
using System.Net;
using System.Text.Json;

namespace PIQI.Service.Test;

public class PIQIControllerTests : PIQITestBase, IClassFixture<WebApplicationFactory<PIQI_Engine.Server.Program>>
{
    private readonly HttpClient _client;
    public PIQIControllerTests(WebApplicationFactory<PIQI_Engine.Server.Program> factory) : base(factory.CreateClient())
    {
        var application = new PIQIEngineService();
        _client = application.CreateClient();
    }

    #region Test Cases

    [Theory]
    [Trait("Category", "Scoring")]
    [InlineData("/PIQI/ScoreMessage")]
    public async Task ScoresMessage1_ReturnsExpectedResponse(string endpoint)
    {
        // Arrange
        var piqiRequest = new PIQIRequest
        {
            ContributorID = "TestProvider",
            DataSourceID = "TestSource",
            PIQIModelMnemonic = "PAT_CLINICAL_V1",
            EvaluationRubricMnemonic = "USCDI_V3",
            MessageID = "Msg001",
            MessageData = File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestData/Input/Test1_PIQI.json"))
        };
        var result = new PIQIResponse();
        var requestContent = new StringContent(JsonConvert.SerializeObject(piqiRequest), System.Text.Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync(endpoint, requestContent);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var contentType = response.Content.Headers.ContentType.MediaType;
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
        if (contentType == "text/plain" || contentType == "application/json")
        {
            var responseBody = await response.Content.ReadAsStringAsync();
            result = System.Text.Json.JsonSerializer.Deserialize<PIQIResponse>(responseBody, options);

            Assert.NotNull(result);
        }

        #region Check Results

        string? expectedOutputString = File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestData/ExpectedOutput/Test1_Result.json"));
        if (expectedOutputString == null) Assert.Fail("Expected output result file not found.");

        PIQIResponse? expectedresult = System.Text.Json.JsonSerializer.Deserialize<PIQIResponse>(expectedOutputString, options);
        if (expectedresult == null) Assert.Fail("Failed to deserialize expected result file.");

        #region Scoring Data

        if (expectedresult.ScoringData == null) Assert.Fail("Missing or invalid scoring data in the expected result file.");
        ScoreDataCompare(expectedresult.ScoringData, result.ScoringData);

        #endregion

        #endregion
    }

    [Theory] 
    [Trait("Category", "Audit")]
    [InlineData("/PIQI/ScoreAuditMessage")]
    public async Task ScoreAuditMessage1_ReturnsExpectedResponse(string endpoint)
    {
        // Arrange
        var piqiRequest = new PIQIRequest
        {
            ContributorID = "TestProvider",
            DataSourceID = "TestSource",
            PIQIModelMnemonic = "PAT_CLINICAL_V1",
            EvaluationRubricMnemonic = "USCDI_V3",
            MessageID = "Msg001",
            MessageData = File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestData/Input/Test1_PIQI.json"))
        };
        var result = new PIQIResponse();
        var requestContent = new StringContent(JsonConvert.SerializeObject(piqiRequest), System.Text.Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync(endpoint, requestContent);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var contentType = response.Content.Headers.ContentType.MediaType;
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
        if (contentType == "text/plain" || contentType == "application/json")
        {
            var responseBody = await response.Content.ReadAsStringAsync();
            result = System.Text.Json.JsonSerializer.Deserialize<PIQIResponse>(responseBody, options);

            Assert.NotNull(result);
        }

        #region Check Results

        string? expectedOutputString = File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestData/ExpectedOutput/Test1_Result.json"));
        if (expectedOutputString == null) Assert.Fail("Expected output result file not found.");

        PIQIResponse? expectedresult = System.Text.Json.JsonSerializer.Deserialize<PIQIResponse>(expectedOutputString, options);
        if (expectedresult == null) Assert.Fail("Failed to deserialize expected result file.");

        #region Scoring Data

        if (expectedresult.ScoringData == null) Assert.Fail("Missing or invalid scoring data in the expected result file.");
        ScoreDataCompare(expectedresult.ScoringData, result.ScoringData);

        #endregion

        #region Audit Results

        if (expectedresult.AuditedMessage == null) Assert.Fail("Missing or invalid audited message in the expected result file.");
        if (result.AuditedMessage == null) Assert.Fail("Missing or invalid audited message in the actual result.");
        AuditCompare(expectedresult.AuditedMessage, result.AuditedMessage);

        #endregion

        #endregion
    }

    [Theory]
    [Trait("Category", "Scoring")]
    [InlineData("/PIQI/ScoreMessage")]
    public async Task ScoresMessage2_ReturnsExpectedResponse(string endpoint)
    {
        // Arrange
        var piqiRequest = new PIQIRequest
        {
            ContributorID = "TestProvider",
            DataSourceID = "TestSource",
            PIQIModelMnemonic = "PAT_CLINICAL_V1",
            EvaluationRubricMnemonic = "USCDI_V3",
            MessageID = "Msg002",
            MessageData = File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestData/Input/Test2_PIQI.json"))
        };
        var result = new PIQIResponse();
        var requestContent = new StringContent(JsonConvert.SerializeObject(piqiRequest), System.Text.Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync(endpoint, requestContent);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var contentType = response.Content.Headers.ContentType.MediaType;
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
        if (contentType == "text/plain" || contentType == "application/json")
        {
            var responseBody = await response.Content.ReadAsStringAsync();
            result = System.Text.Json.JsonSerializer.Deserialize<PIQIResponse>(responseBody, options);

            Assert.NotNull(result);
        }

        #region Check Results

        string? expectedOutputString = File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestData/ExpectedOutput/Test2_Result.json"));
        if (expectedOutputString == null) Assert.Fail("Expected output result file not found.");

        PIQIResponse? expectedresult = System.Text.Json.JsonSerializer.Deserialize<PIQIResponse>(expectedOutputString, options);
        if (expectedresult == null) Assert.Fail("Failed to deserialize expected result file.");

        #region Scoring Data

        if (expectedresult.ScoringData == null) Assert.Fail("Missing or invalid scoring data in the expected result file.");
        ScoreDataCompare(expectedresult.ScoringData, result.ScoringData);

        #endregion

        #endregion
    }

    [Theory]
    [Trait("Category", "Audit")]
    [InlineData("/PIQI/ScoreAuditMessage")]
    public async Task ScoreAuditMessage2_ReturnsExpectedResponse(string endpoint)
    {
        // Arrange
        var piqiRequest = new PIQIRequest
        {
            ContributorID = "TestProvider",
            DataSourceID = "TestSource",
            PIQIModelMnemonic = "PAT_CLINICAL_V1",
            EvaluationRubricMnemonic = "USCDI_V3",
            MessageID = "Msg002",
            MessageData = File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestData/Input/Test2_PIQI.json"))
        };
        var result = new PIQIResponse();
        var requestContent = new StringContent(JsonConvert.SerializeObject(piqiRequest), System.Text.Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync(endpoint, requestContent);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var contentType = response.Content.Headers.ContentType.MediaType;
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
        if (contentType == "text/plain" || contentType == "application/json")
        {
            var responseBody = await response.Content.ReadAsStringAsync();
            result = System.Text.Json.JsonSerializer.Deserialize<PIQIResponse>(responseBody, options);

            Assert.NotNull(result);
        }

        #region Check Results

        string? expectedOutputString = File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestData/ExpectedOutput/Test2_Result.json"));
        if (expectedOutputString == null) Assert.Fail("Expected output result file not found.");

        PIQIResponse? expectedresult = System.Text.Json.JsonSerializer.Deserialize<PIQIResponse>(expectedOutputString, options);
        if (expectedresult == null) Assert.Fail("Failed to deserialize expected result file.");

        #region Scoring Data

        if (expectedresult.ScoringData == null) Assert.Fail("Missing or invalid scoring data in the expected result file.");
        ScoreDataCompare(expectedresult.ScoringData, result.ScoringData);

        #endregion

        #region Audit Results

        if (expectedresult.AuditedMessage == null) Assert.Fail("Missing or invalid audited message in the expected result file.");
        if (result.AuditedMessage == null) Assert.Fail("Missing or invalid audited message in the actual result.");
        AuditCompare(expectedresult.AuditedMessage, result.AuditedMessage);

        #endregion

        #endregion
    }

    [Theory]
    [Trait("Category", "Scoring")]
    [InlineData("/PIQI/ScoreMessage")]
    public async Task ScoresMessage3_ReturnsExpectedResponse(string endpoint)
    {
        // Arrange
        var piqiRequest = new PIQIRequest
        {
            ContributorID = "TestProvider",
            DataSourceID = "TestSource",
            PIQIModelMnemonic = "PAT_CLINICAL_V1",
            EvaluationRubricMnemonic = "USCDI_V3",
            MessageID = "Msg003",
            MessageData = File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestData/Input/Test3_PIQI.json"))
        };
        var result = new PIQIResponse();
        var requestContent = new StringContent(JsonConvert.SerializeObject(piqiRequest), System.Text.Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync(endpoint, requestContent);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var contentType = response.Content.Headers.ContentType.MediaType;
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
        if (contentType == "text/plain" || contentType == "application/json")
        {
            var responseBody = await response.Content.ReadAsStringAsync();
            result = System.Text.Json.JsonSerializer.Deserialize<PIQIResponse>(responseBody, options);

            Assert.NotNull(result);
        }

        #region Check Results

        string? expectedOutputString = File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestData/ExpectedOutput/Test3_Result.json"));
        if (expectedOutputString == null) Assert.Fail("Expected output result file not found.");

        PIQIResponse? expectedresult = System.Text.Json.JsonSerializer.Deserialize<PIQIResponse>(expectedOutputString, options);
        if (expectedresult == null) Assert.Fail("Failed to deserialize expected result file.");

        #region Scoring Data

        if (expectedresult.ScoringData == null) Assert.Fail("Missing or invalid scoring data in the expected result file.");
        ScoreDataCompare(expectedresult.ScoringData, result.ScoringData);

        #endregion

        #endregion
    }

    [Theory]
    [Trait("Category", "Audit")]
    [InlineData("/PIQI/ScoreAuditMessage")]
    public async Task ScoreAuditMessage3_ReturnsExpectedResponse(string endpoint)
    {
        // Arrange
        var piqiRequest = new PIQIRequest
        {
            ContributorID = "TestProvider",
            DataSourceID = "TestSource",
            PIQIModelMnemonic = "PAT_CLINICAL_V1",
            EvaluationRubricMnemonic = "USCDI_V3",
            MessageID = "Msg003",
            MessageData = File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestData/Input/Test3_PIQI.json"))
        };
        var result = new PIQIResponse();
        var requestContent = new StringContent(JsonConvert.SerializeObject(piqiRequest), System.Text.Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync(endpoint, requestContent);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var contentType = response.Content.Headers.ContentType.MediaType; 
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
        if (contentType == "text/plain" || contentType == "application/json")
        {
            var responseBody = await response.Content.ReadAsStringAsync();
            result = System.Text.Json.JsonSerializer.Deserialize<PIQIResponse>(responseBody, options);

            Assert.NotNull(result);
        }

        #region Check Results

        string? expectedOutputString = File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestData/ExpectedOutput/Test3_Result.json"));
        if (expectedOutputString == null) Assert.Fail("Expected output result file not found.");

        PIQIResponse? expectedresult = System.Text.Json.JsonSerializer.Deserialize<PIQIResponse>(expectedOutputString, options);
        if (expectedresult == null) Assert.Fail("Failed to deserialize expected result file.");

        #region Scoring Data

        if (expectedresult.ScoringData == null) Assert.Fail("Missing or invalid scoring data in the expected result file.");
        ScoreDataCompare(expectedresult.ScoringData, result.ScoringData);

        #endregion

        #region Audit Results

        if (expectedresult.AuditedMessage == null) Assert.Fail("Missing or invalid audited message in the expected result file.");
        if (result.AuditedMessage == null) Assert.Fail("Missing or invalid audited message in the actual result.");
        AuditCompare(expectedresult.AuditedMessage, result.AuditedMessage);

        #endregion

        #endregion
    }

    [Theory]
    [Trait("Category", "Scoring")]
    [InlineData("/PIQI/ScoreMessage")]
    public async Task ScoresMessage4_ReturnsExpectedResponse(string endpoint)
    {
        // Arrange
        var piqiRequest = new PIQIRequest
        {
            ContributorID = "TestProvider",
            DataSourceID = "TestSource",
            PIQIModelMnemonic = "PAT_CLINICAL_V1",
            EvaluationRubricMnemonic = "USCDI_V3",
            MessageID = "Msg004",
            MessageData = File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestData/Input/Test4_PIQI.json"))
        };
        var result = new PIQIResponse();
        var requestContent = new StringContent(JsonConvert.SerializeObject(piqiRequest), System.Text.Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync(endpoint, requestContent);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var contentType = response.Content.Headers.ContentType.MediaType; 
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
        if (contentType == "text/plain" || contentType == "application/json")
        {
            var responseBody = await response.Content.ReadAsStringAsync();
            result = System.Text.Json.JsonSerializer.Deserialize<PIQIResponse>(responseBody, options);

            Assert.NotNull(result);
        }

        #region Check Results

        string? expectedOutputString = File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestData/ExpectedOutput/Test4_Result.json"));
        if (expectedOutputString == null) Assert.Fail("Expected output result file not found.");

        PIQIResponse? expectedresult = System.Text.Json.JsonSerializer.Deserialize<PIQIResponse>(expectedOutputString, options);
        if (expectedresult == null) Assert.Fail("Failed to deserialize expected result file.");

        #region Scoring Data

        if (expectedresult.ScoringData == null) Assert.Fail("Missing or invalid scoring data in the expected result file.");
        ScoreDataCompare(expectedresult.ScoringData, result.ScoringData);

        #endregion

        #endregion
    }

    [Theory]
    [Trait("Category", "Audit")]
    [InlineData("/PIQI/ScoreAuditMessage")]
    public async Task ScoreAuditMessage4_ReturnsExpectedResponse(string endpoint)
    {
        // Arrange
        var piqiRequest = new PIQIRequest
        {
            ContributorID = "TestProvider",
            DataSourceID = "TestSource",
            PIQIModelMnemonic = "PAT_CLINICAL_V1",
            EvaluationRubricMnemonic = "USCDI_V3",
            MessageID = "Msg004",
            MessageData = File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestData/Input/Test4_PIQI.json"))
        };
        var result = new PIQIResponse();
        var requestContent = new StringContent(JsonConvert.SerializeObject(piqiRequest), System.Text.Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync(endpoint, requestContent);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var contentType = response.Content.Headers.ContentType.MediaType;
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
        if (contentType == "text/plain" || contentType == "application/json")
        {
            var responseBody = await response.Content.ReadAsStringAsync();
            result = System.Text.Json.JsonSerializer.Deserialize<PIQIResponse>(responseBody, options);

            Assert.NotNull(result);
        }

        #region Check Results

        string? expectedOutputString = File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestData/ExpectedOutput/Test4_Result.json"));
        if (expectedOutputString == null) Assert.Fail("Expected output result file not found.");

        PIQIResponse? expectedresult = System.Text.Json.JsonSerializer.Deserialize<PIQIResponse>(expectedOutputString, options);
        if (expectedresult == null) Assert.Fail("Failed to deserialize expected result file.");

        #region Scoring Data

        if (expectedresult.ScoringData == null) Assert.Fail("Missing or invalid scoring data in the expected result file.");
        ScoreDataCompare(expectedresult.ScoringData, result.ScoringData);

        #endregion

        #region Audit Results

        if (expectedresult.AuditedMessage == null) Assert.Fail("Missing or invalid audited message in the expected result file.");
        if (result.AuditedMessage == null) Assert.Fail("Missing or invalid audited message in the actual result.");
        AuditCompare(expectedresult.AuditedMessage, result.AuditedMessage);

        #endregion

        #endregion
    }

    [Theory]
    [Trait("Category", "Scoring")]
    [InlineData("/PIQI/ScoreMessage")]
    public async Task ScoresMessage5_ReturnsExpectedResponse(string endpoint)
    {
        // Arrange
        var piqiRequest = new PIQIRequest
        {
            ContributorID = "TestProvider",
            DataSourceID = "TestSource",
            PIQIModelMnemonic = "PAT_CLINICAL_V1",
            EvaluationRubricMnemonic = "USCDI_V3",
            MessageID = "Msg005",
            MessageData = File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestData/Input/Test5_PIQI.json"))
        };
        var result = new PIQIResponse();
        var requestContent = new StringContent(JsonConvert.SerializeObject(piqiRequest), System.Text.Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync(endpoint, requestContent);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var contentType = response.Content.Headers.ContentType.MediaType;
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
        if (contentType == "text/plain" || contentType == "application/json")
        {
            var responseBody = await response.Content.ReadAsStringAsync();
            result = System.Text.Json.JsonSerializer.Deserialize<PIQIResponse>(responseBody, options);

            Assert.NotNull(result);
        }

        #region Check Results

        string? expectedOutputString = File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestData/ExpectedOutput/Test5_Result.json"));
        if (expectedOutputString == null) Assert.Fail("Expected output result file not found.");

        PIQIResponse? expectedresult = System.Text.Json.JsonSerializer.Deserialize<PIQIResponse>(expectedOutputString, options);
        if (expectedresult == null) Assert.Fail("Failed to deserialize expected result file.");

        #region Scoring Data

        if (expectedresult.ScoringData == null) Assert.Fail("Missing or invalid scoring data in the expected result file.");
        ScoreDataCompare(expectedresult.ScoringData, result.ScoringData);

        #endregion

        #endregion
    }

    [Theory]
    [Trait("Category", "Audit")]
    [InlineData("/PIQI/ScoreAuditMessage")]
    public async Task ScoreAuditMessage5_ReturnsExpectedResponse(string endpoint)
    {
        // Arrange
        var piqiRequest = new PIQIRequest
        {
            ContributorID = "TestProvider",
            DataSourceID = "TestSource",
            PIQIModelMnemonic = "PAT_CLINICAL_V1",
            EvaluationRubricMnemonic = "USCDI_V3",
            MessageID = "Msg005",
            MessageData = File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestData/Input/Test5_PIQI.json"))
        };
        var result = new PIQIResponse();
        var requestContent = new StringContent(JsonConvert.SerializeObject(piqiRequest), System.Text.Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync(endpoint, requestContent);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var contentType = response.Content.Headers.ContentType.MediaType;
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
        if (contentType == "text/plain" || contentType == "application/json")
        {
            var responseBody = await response.Content.ReadAsStringAsync();
            result = System.Text.Json.JsonSerializer.Deserialize<PIQIResponse>(responseBody, options);

            Assert.NotNull(result);
        }

        #region Check Results

        string? expectedOutputString = File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestData/ExpectedOutput/Test5_Result.json"));
        if (expectedOutputString == null) Assert.Fail("Expected output result file not found.");

        PIQIResponse? expectedresult = System.Text.Json.JsonSerializer.Deserialize<PIQIResponse>(expectedOutputString, options);
        if (expectedresult == null) Assert.Fail("Failed to deserialize expected result file.");

        #region Scoring Data

        if (expectedresult.ScoringData == null) Assert.Fail("Missing or invalid scoring data in the expected result file.");
        ScoreDataCompare(expectedresult.ScoringData, result.ScoringData);

        #endregion

        #region Audit Results

        if (expectedresult.AuditedMessage == null) Assert.Fail("Missing or invalid audited message in the expected result file.");
        if (result.AuditedMessage == null) Assert.Fail("Missing or invalid audited message in the actual result.");
        AuditCompare(expectedresult.AuditedMessage, result.AuditedMessage);

        #endregion

        #endregion
    }

    #endregion

}
