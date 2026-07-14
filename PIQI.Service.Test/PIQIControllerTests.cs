using Microsoft.AspNetCore.Mvc.Testing;
using Newtonsoft.Json;
using PIQI.Components.Models;
using PIQI.Service.WebTesting.Rest;
using System.Net;
using System.Text.Json;

namespace PIQI.Service.Test;

public class PIQIControllerTests : RestClient, IClassFixture<WebApplicationFactory<PIQI_Engine.Server.Program>>
{
    private readonly HttpClient _client;
    public PIQIControllerTests(WebApplicationFactory<PIQI_Engine.Server.Program> factory) : base(factory.CreateClient())
    {
        var application = new PIQIEngineService();
        _client = application.CreateClient();
    }

    #region Test Cases

    [Theory]
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

    #region Helper Methods

    private bool JsonCompare(object? obj, object? another)
    {
        if (ReferenceEquals(obj, another)) return true;
        if (obj == null && another == null) return true;
        if ((obj == null) || (another == null)) return false;
        if (obj.GetType() != another.GetType()) return false;

        var objJson = JsonConvert.SerializeObject(obj);
        var anotherJson = JsonConvert.SerializeObject(another);

        return objJson == anotherJson;
    }

    private void ScoreDataCompare(PIQIStatResponse expectedScore, PIQIStatResponse actualScore)
    {
        // Compare full scoring block first to see if there are any differences at all before drilling down to specific properties for easier debugging
        if (!JsonCompare(actualScore, expectedScore))
        {
            // Check overall message score
            if (!JsonCompare(actualScore.MessageResults, expectedScore.MessageResults))
            {
                var auditProperties = expectedScore.MessageResults.GetType().GetProperties();
                foreach (var prop in auditProperties)
                {
                    var resultValue = prop.GetValue(actualScore.MessageResults);
                    var expectedValue = prop.GetValue(expectedScore.MessageResults);
                    Assert.True(
                        expectedValue?.Equals(resultValue),
                        $"Failure. Values do not match." +
                        $"\nScoring block: Message Results\tProperty: {prop.Name}" +
                        $"\nExpected: '{expectedValue}'\nActual: '{resultValue}'"
                    );
                }
            }

            // Check each data class score
            foreach (var expectedDataClassResult in expectedScore.DataClassResults)
            {
                var actualDataClassResult = actualScore.DataClassResults.FirstOrDefault(dcr => dcr.DataClassName == expectedDataClassResult.DataClassName);
                if (actualDataClassResult == null) Assert.Fail($"Failure. Missing or invalid class in actual result." +
                    $"\nExpected: DataClassResults - {expectedDataClassResult.DataClassName}");

                if (!JsonCompare(expectedDataClassResult, actualDataClassResult))
                {
                    var dataClassProperties = expectedDataClassResult.GetType().GetProperties();
                    foreach (var prop in dataClassProperties)
                    {
                        var resultValue = prop.GetValue(actualDataClassResult);
                        var expectedValue = prop.GetValue(expectedDataClassResult);
                        Assert.True(
                            expectedValue.Equals(resultValue),
                            $"Failure. Values do not match." +
                            $"\nScoring block: DataClassResults\nClass: {expectedDataClassResult.DataClassName}\tProperty: {prop.Name}" +
                            $"\nExpected: '{expectedValue}'\nActual: '{resultValue}'"
                        );
                    }
                }
            }

            // Check each informational result
            foreach (var expectedInformationalResult in expectedScore.InformationalResults)
            {
                var actualInformationalResult = actualScore.InformationalResults.FirstOrDefault(ir => ir.DataClassName == expectedInformationalResult.DataClassName);
                if (actualInformationalResult == null) Assert.Fail($"Failure. Missing or invalid informational item in actual result." +
                    $"\nExpected: InformationalResults - {expectedInformationalResult.DataClassName}");

                // Check each class's informational evaluations to see if the overall class informational result doesn't match
                if (!JsonCompare(expectedInformationalResult, actualInformationalResult))
                {
                    // Check each specific informational evaluation in the class evaluation list
                    foreach (var expectedEvaluation in expectedInformationalResult.EvaluationList)
                    {
                        var actualEvaluation = actualInformationalResult.EvaluationList.FirstOrDefault(e => e.EvaluationName == expectedEvaluation.EvaluationName && e.EntityName == expectedEvaluation.EntityName);
                        if (actualEvaluation == null) Assert.Fail($"Failure. Missing or invalid informational evaluation in actual result." +
                            $"\nExpected: InformationalResults - {expectedInformationalResult.DataClassName}.{expectedEvaluation.EntityName}\tEvaluation: {expectedEvaluation.EvaluationName}");

                        // Check each specific informational evaluation property if the overall evaluation
                        if (!JsonCompare(expectedEvaluation, actualEvaluation))
                        {
                            var evaluationProperties = expectedEvaluation.GetType().GetProperties();
                            foreach (var prop in evaluationProperties)
                            {
                                var resultValue = prop.GetValue(actualEvaluation);
                                var expectedValue = prop.GetValue(expectedEvaluation);
                                Assert.True(
                                    expectedValue?.Equals(resultValue),
                                    $"Failure. Values do not match." +
                                    $"\nScoring block: InformationalResults\nEntity: {expectedInformationalResult.DataClassName}.{expectedEvaluation.EntityName}\tEvaulation: {expectedEvaluation.EvaluationName}\tProperty: {prop.Name}" +
                                    $"\nExpected: '{expectedValue}'\nActual: '{resultValue}'"
                                );
                            }
                        }
                    }
                }
            }
        }
    }

    private void AuditCompare(PIQIAuditResponse expectedAudit, PIQIAuditResponse actualMessage)
    {
        if (!JsonCompare(actualMessage, expectedAudit))
        {
            // Check overall audit score
            if (actualMessage.Audit == null) Assert.Fail("Failure. Missing audit in actual result.");
            if (expectedAudit.Audit == null) Assert.Fail("Failure. Missing audit in expected result.");
            if (!JsonCompare(actualMessage.Audit, expectedAudit.Audit))
            {
                var auditProperties = expectedAudit.Audit.GetType().GetProperties();
                foreach (var prop in auditProperties)
                {
                    var resultValue = prop.GetValue(actualMessage.Audit);
                    var expectedValue = prop.GetValue(expectedAudit.Audit);
                    Assert.True(
                        expectedValue?.Equals(resultValue),
                        $"Failure. Values do not match." +
                        $"\nScoring block: Audit\tProperty: {prop.Name}" +
                        $"\nExpected: '{expectedValue}'\nActual: '{resultValue}'"
                    );
                }
            }

            // Check audit root
            if (actualMessage.Root == null) Assert.Fail("Failure. Missing audit root in actual result.");
            if (expectedAudit.Root == null) Assert.Fail("Failure. Missing audit root in expected result.");
            if (!JsonCompare(actualMessage.Root, expectedAudit.Root))
            {
                // Assert matching class count
                Assert.True(
                    expectedAudit.Root.Classes?.Count == actualMessage.Root?.Classes?.Count,
                    $"Failure. Values do not match." +
                    $"\n{actualMessage.Root?.RootName}.Classes\tProperty: Count" +
                    $"\nExpected: '{expectedAudit.Root.Classes?.Count}'\nActual: '{actualMessage.Root?.Classes?.Count}'"
                );

                // Check each class in the audit root
                foreach (var expectedClassValue in expectedAudit.Root.Classes ?? [])
                {
                    var actualClassValue = actualMessage.Root?.Classes?.FirstOrDefault(c => c.ClassName == expectedClassValue.ClassName);
                    if (actualClassValue == null) Assert.Fail($"Failure. Missing or invalid class in actual result." +
                        $"\nExpected: {expectedAudit.Root.RootName}.{expectedClassValue.ClassName}");

                    if (!JsonCompare(expectedClassValue, actualClassValue))
                    {
                        // If the classes don't match, check each element within the class
                        // Assert matching element count
                        Assert.True(
                            actualClassValue.Elements?.Count == expectedClassValue.Elements?.Count,
                            $"Failure. Values do not match." +
                            $"\n{actualMessage.Root?.RootName}.{actualClassValue.ClassName}.Elements\tProperty: Count" +
                            $"\nExpected: '{expectedClassValue.Elements?.Count}'\nActual: '{actualClassValue.Elements?.Count}'"
                        );

                        // Check each element in the class
                        var elementindex = 0;
                        while (elementindex < expectedClassValue.Elements?.Count)
                        {
                            var expectedElementValue = expectedClassValue.Elements[elementindex];
                            var actualElementValue = actualClassValue.Elements?[elementindex];

                            // Check the elements at matching index positions
                            if (!JsonCompare(actualElementValue, expectedElementValue))
                            {
                                // Check the elements are matching at the attribute level
                                // Assert matching attribute count
                                Assert.True(
                                    expectedElementValue.Attributes?.Count == actualElementValue?.Attributes?.Count,
                                    $"Failure. Values do not match." +
                                    $"\n{actualMessage.Root?.RootName}.{actualClassValue.ClassName}[{elementindex}].Attributes\tProperty: Count" +
                                    $"\nExpected: '{expectedElementValue.Attributes?.Count}'\nActual: '{actualElementValue?.Attributes?.Count}'"
                                );

                                // Check each attribute in the element
                                foreach (var expectedAttributeValue in expectedElementValue.Attributes ?? [])
                                {
                                    var actualAttributeValue = actualElementValue?.Attributes?.FirstOrDefault(a => a.AttributeName == expectedAttributeValue.AttributeName);
                                    if (actualAttributeValue == null) 
                                        Assert.Fail($"Failure. Missing or invalid attribute in actual result." +
                                            $"\nExpected: {expectedAudit.Root?.RootName}.{expectedClassValue.ClassName}[{elementindex}].{expectedAttributeValue.AttributeName}");

                                    // If the attributes don't match, check each property within the attribute's assessmentItems and InformationalItems
                                    if (!JsonCompare(actualAttributeValue, expectedAttributeValue))
                                    {
                                        // Compare AssessmentItems
                                        foreach (var expectedAssessment in expectedAttributeValue.AttributeAudit?.AssessmentItems ?? [])
                                        {
                                            var actualAssessment = actualAttributeValue.AttributeAudit?.AssessmentItems?.FirstOrDefault(a => a.Assessment == expectedAssessment.Assessment);
                                            if (actualAssessment == null)
                                                Assert.Fail($"Failure. Missing or invalid attribute assessment in actual result." +
                                                    $"\nExpected: {expectedAudit.Root?.RootName}.{expectedClassValue.ClassName}[{elementindex}].{expectedAttributeValue.AttributeName}\tAssessment: {expectedAssessment.Assessment}");

                                            if (!JsonCompare(actualAssessment, expectedAssessment))
                                            {
                                                var assessmentProperties = expectedAssessment.GetType().GetProperties();
                                                foreach (var prop in assessmentProperties)
                                                {
                                                    var resultValue = prop.GetValue(actualAssessment);
                                                    var expectedValue = prop.GetValue(expectedAssessment);
                                                    Assert.True(
                                                       expectedValue?.Equals(resultValue),
                                                       $"Failure. Values do not match." +
                                                       $"\n{actualMessage.Root?.RootName}.{actualClassValue.ClassName}[{elementindex}].{actualAttributeValue.AttributeName} - AssessmentItems" +
                                                       $"\nAssessment: {actualAssessment.Assessment}\tProperty: {prop.Name}" +
                                                       $"\nExpected: '{expectedValue}'\nActual: '{resultValue}'"
                                                   );
                                                }
                                            }
                                        }

                                        // Compare InformationalItems
                                        foreach (var expectedInformationalAssessment in expectedAttributeValue.AttributeAudit?.InformationalItems ?? [])
                                        {
                                            var actualInformationalAssessment = actualAttributeValue.AttributeAudit?.InformationalItems?.FirstOrDefault(a => a.Assessment == expectedInformationalAssessment.Assessment);
                                            if (actualInformationalAssessment == null)
                                                Assert.Fail($"Failure. Missing or invalid attribute informational assessment in actual result." +
                                                    $"\nExpected: {expectedAudit.Root?.RootName}.{expectedClassValue.ClassName}[{elementindex}].{expectedAttributeValue.AttributeName}\tAssessment: {expectedInformationalAssessment.Assessment}");

                                            if (!JsonCompare(actualInformationalAssessment, expectedInformationalAssessment))
                                            {
                                                var informationalProperties = expectedInformationalAssessment.GetType().GetProperties();
                                                foreach (var prop in informationalProperties)
                                                {
                                                    var resultValue = prop.GetValue(actualInformationalAssessment);
                                                    var expectedValue = prop.GetValue(expectedInformationalAssessment);
                                                    Assert.True(
                                                       expectedValue?.Equals(resultValue),
                                                       $"Failure. Values do not match." +
                                                       $"\n{actualMessage.Root?.RootName}.{actualClassValue.ClassName}[{elementindex}].{actualAttributeValue.AttributeName} - InformationalItems" +
                                                       $"\nInformational Assessment: {actualInformationalAssessment.Assessment}\tProperty: {prop.Name}" +
                                                       $"\nExpected: '{expectedValue}'\nActual: '{resultValue}'"
                                                   );
                                                }
                                            }
                                        }

                                        // Compare ScoringData
                                        if (!JsonCompare(actualAttributeValue.AttributeAudit?.ScoringData, expectedAttributeValue.AttributeAudit?.ScoringData))
                                        {
                                            var scoringProperties = expectedAttributeValue.AttributeAudit?.ScoringData.GetType().GetProperties();
                                            foreach (var prop in scoringProperties ?? [])
                                            {
                                                var resultValue = prop.GetValue(actualAttributeValue.AttributeAudit?.ScoringData);
                                                var expectedValue = prop.GetValue(expectedAttributeValue.AttributeAudit?.ScoringData);
                                                Assert.True(
                                                   expectedValue?.Equals(resultValue),
                                                   $"Failure. Values do not match." +
                                                   $"\n{actualMessage.Root?.RootName}.{actualClassValue.ClassName}[{elementindex}].{actualAttributeValue.AttributeName} - ScoringData\tProperty: {prop.Name}" +
                                                   $"\nExpected: '{expectedValue}'\nActual: '{resultValue}'"
                                               );
                                            }
                                        }

                                        // Compare Attribute Data
                                        if (!JsonCompare(actualAttributeValue.Data, expectedAttributeValue.Data))
                                        {
                                            var dataProperties = expectedAttributeValue.Data?.GetType().GetProperties();
                                            foreach (var prop in dataProperties ?? [])
                                            {
                                                var resultValue = prop.GetValue(actualAttributeValue.Data);
                                                var expectedValue = prop.GetValue(expectedAttributeValue.Data);
                                                Assert.True(
                                                    JsonCompare(expectedValue, resultValue),
                                                    $"Failure. Values do not match." +
                                                    $"\n{actualMessage.Root?.RootName}.{actualClassValue.ClassName}[{elementindex}].{actualAttributeValue.AttributeName} - Data\tProperty: {prop.Name}" +
                                                    $"\nExpected: '{(expectedValue.GetType().IsPrimitive || expectedValue is string ? expectedValue.ToString()! : JsonConvert.SerializeObject(expectedValue))}'" +
                                                    $"\nActual: '{(resultValue.GetType().IsPrimitive || resultValue is string ? resultValue.ToString()! : JsonConvert.SerializeObject(resultValue))}'"
                                                );
                                            }
                                        }
                                    }
                                }

                                // If there are still no fails, check if the element level audits match
                                var elementAuditProperties = expectedElementValue.ElementAudit?.GetType().GetProperties();
                                foreach (var prop in elementAuditProperties ?? [])
                                {
                                    var resultValue = prop.GetValue(actualElementValue?.ElementAudit);
                                    var expectedValue = prop.GetValue(expectedElementValue.ElementAudit);
                                    Assert.True(
                                       expectedValue?.Equals(resultValue),
                                       $"Failure. Values do not match." +
                                       $"\n{actualMessage.Root?.RootName}.{actualClassValue.ClassName}[{elementindex}] - ElementAudit\tProperty: {prop.Name}" +
                                       $"\nExpected: '{expectedValue}'\nActual: '{resultValue}'"
                                   );
                                }
                            }

                            // Increment index
                            elementindex++;
                        }
                    }
                }
            }
        }
    }

    #endregion
}
