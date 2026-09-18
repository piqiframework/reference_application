using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Newtonsoft.Json;
using PIQI.Components.Models;
using System.Net;
using System.Text.Json;
using CQLServiceProgram = Program;  // CQL.Service Program is in global namespace
using CQLTest.Service;
using Microsoft.Extensions.DependencyInjection;

namespace PIQI.Service.Test;

/// <summary>
/// Integration tests for CQL scoring functionality.
/// Starts both CQL.Service (as HTTP) and PIQI_Engine.Server for testing.
/// </summary>
public class CQLIntegrationTests : PIQITestBase, IClassFixture<WebApplicationFactory<CQLServiceProgram>>, IClassFixture<WebApplicationFactory<PIQI_Engine.Server.Program>>
{
    private readonly HttpClient _client;
    private readonly WebApplicationFactory<CQLServiceProgram> _cqlFactory;
    private readonly WebApplicationFactory<PIQI_Engine.Server.Program> _piqiFactory;

    public CQLIntegrationTests(
        WebApplicationFactory<CQLServiceProgram> cqlFactory,
        WebApplicationFactory<PIQI_Engine.Server.Program> piqiFactory) : base(CreatePiqiClient(cqlFactory, piqiFactory))
    {
        _cqlFactory = cqlFactory;
        _piqiFactory = piqiFactory;

        // Create PIQI Engine client with Test configuration and CQL service routing
        _client = CreatePiqiClient(_cqlFactory, _piqiFactory);
    }

    private static HttpClient CreatePiqiClient(
        WebApplicationFactory<CQLServiceProgram> cqlFactory,
        WebApplicationFactory<PIQI_Engine.Server.Program> piqiFactory)
    {
        // Create the CQL service test server
        var cqlServer = cqlFactory.Server;

        // Configure PIQI Engine to route CQL service calls to the test server
        var factory = piqiFactory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Test");

            // Replace the CQLServiceClient's HttpClient with one that uses the test server
            builder.ConfigureServices(services =>
            {
                // Remove the existing CQLServiceClient registration
                var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(CQLServiceClient));
                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }

                // Re-register with a custom HttpClient that routes to the test server
                services.AddHttpClient<CQLServiceClient>()
                    .ConfigurePrimaryHttpMessageHandler(() => new TestServerMessageHandler(cqlServer.CreateHandler()));
            });
        });

        return factory.CreateClient();
    }

    /// <summary>
    /// Custom HttpMessageHandler that intercepts HTTP requests and forwards them
    /// to a TestServer's HttpClient instead of making real network calls.
    /// </summary>
    private class TestServerMessageHandler : DelegatingHandler
    {
        private readonly HttpMessageHandler _innerHandler;

        public TestServerMessageHandler(HttpMessageHandler testServerHandler)
        {
            _innerHandler = testServerHandler ?? throw new ArgumentNullException(nameof(testServerHandler));
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            // Use the test server's handler to process the request
            var invoker = new HttpMessageInvoker(_innerHandler);
            return await invoker.SendAsync(request, cancellationToken);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _innerHandler?.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    #region Test Cases

    #region CQL Scoring Tests

    [Theory]
    [Trait("Category", "CQL_Scoring")]
    [InlineData("/PIQI/ScoreMessage")]
    public async Task CQL1_Scores1_ReturnsExpectedResponse(string endpoint)
    {
        // Arrange
        var piqiRequest = new PIQIRequest
        {
            ContributorID = "TestProvider",
            DataSourceID = "TestSource",
            PIQIModelMnemonic = "PAT_CLINICAL_V1",
            EvaluationRubricMnemonic = "CQL_LIBRARY_TEST",
            MessageID = "CQLMsg001",
            MessageData = File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestData/CQLTestData/Input/AnnualFluVaccine_Pass.json"))
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

        string? expectedOutputString = File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestData/CQLTestData/ExpectedOutput/AnnualFluVaccine_Pass.json"));
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
    [Trait("Category", "CQL_Scoring")]
    [InlineData("/PIQI/ScoreMessage")]
    public async Task CQL1_Scores2_ReturnsExpectedResponse(string endpoint)
    {
        // Arrange
        var piqiRequest = new PIQIRequest
        {
            ContributorID = "TestProvider",
            DataSourceID = "TestSource",
            PIQIModelMnemonic = "PAT_CLINICAL_V1",
            EvaluationRubricMnemonic = "CQL_LIBRARY_TEST",
            MessageID = "CQLMsg002",
            MessageData = File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestData/CQLTestData/Input/AnnualFluVaccine_Skip.json"))
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

        string? expectedOutputString = File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestData/CQLTestData/ExpectedOutput/AnnualFluVaccine_Skip.json"));
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
    [Trait("Category", "CQL_Scoring")]
    [InlineData("/PIQI/ScoreMessage")]
    public async Task CQL1_Scores3_ReturnsExpectedResponse(string endpoint)
    {
        // Arrange
        var piqiRequest = new PIQIRequest
        {
            ContributorID = "TestProvider",
            DataSourceID = "TestSource",
            PIQIModelMnemonic = "PAT_CLINICAL_V1",
            EvaluationRubricMnemonic = "CQL_LIBRARY_TEST",
            MessageID = "CQLMsg002",
            MessageData = File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestData/CQLTestData/Input/AnnualFluVaccine_Skip_DeathDate.json"))
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

        string? expectedOutputString = File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestData/CQLTestData/ExpectedOutput/AnnualFluVaccine_Skip_DeathDate.json"));
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
    [Trait("Category", "CQL_Scoring")]
    [InlineData("/PIQI/ScoreMessage")]
    public async Task CQL1_Scores4_ReturnsExpectedResponse(string endpoint)
    {
        // Arrange
        var piqiRequest = new PIQIRequest
        {
            ContributorID = "TestProvider",
            DataSourceID = "TestSource",
            PIQIModelMnemonic = "PAT_CLINICAL_V1",
            EvaluationRubricMnemonic = "CQL_LIBRARY_TEST",
            MessageID = "CQLMsg002",
            MessageData = File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestData/CQLTestData/Input/AnnualFluVaccine_Skip_Deceased.json"))
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

        string? expectedOutputString = File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestData/CQLTestData/ExpectedOutput/AnnualFluVaccine_Skip_Deceased.json"));
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
    [Trait("Category", "CQL_Scoring")]
    [InlineData("/PIQI/ScoreMessage")]
    public async Task CQL1_Scores5_ReturnsExpectedResponse(string endpoint)
    {
        // Arrange
        var piqiRequest = new PIQIRequest
        {
            ContributorID = "TestProvider",
            DataSourceID = "TestSource",
            PIQIModelMnemonic = "PAT_CLINICAL_V1",
            EvaluationRubricMnemonic = "CQL_LIBRARY_TEST",
            MessageID = "CQLMsg003",
            MessageData = File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestData/CQLTestData/Input/AnnualFluVaccine_Fail.json"))
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

        string? expectedOutputString = File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestData/CQLTestData/ExpectedOutput/AnnualFluVaccine_Fail.json"));
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
    [Trait("Category", "CQL_Scoring")]
    [InlineData("/PIQI/ScoreMessage")]
    public async Task CQL1_Scores6_ReturnsExpectedResponse(string endpoint)
    {
        // Arrange
        var piqiRequest = new PIQIRequest
        {
            ContributorID = "TestProvider",
            DataSourceID = "TestSource",
            PIQIModelMnemonic = "PAT_CLINICAL_V1",
            EvaluationRubricMnemonic = "CQL_LIBRARY_TEST",
            MessageID = "CQLMsg003",
            MessageData = File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestData/CQLTestData/Input/AnnualFluVaccine_Fail_FluNotInPeriod.json"))
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

        string? expectedOutputString = File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestData/CQLTestData/ExpectedOutput/AnnualFluVaccine_Fail_FluNotInPeriod.json"));
        if (expectedOutputString == null) Assert.Fail("Expected output result file not found.");

        PIQIResponse? expectedresult = System.Text.Json.JsonSerializer.Deserialize<PIQIResponse>(expectedOutputString, options);
        if (expectedresult == null) Assert.Fail("Failed to deserialize expected result file.");

        #region Scoring Data

        if (expectedresult.ScoringData == null) Assert.Fail("Missing or invalid scoring data in the expected result file.");
        ScoreDataCompare(expectedresult.ScoringData, result.ScoringData);

        #endregion

        #endregion
    }

    #endregion

    #region CQL Audit Tests

    [Theory]
    [Trait("Category", "CQL_Audit")]
    [InlineData("/PIQI/ScoreAuditMessage")]
    public async Task CQL1_Audit1_ReturnsExpectedResponse(string endpoint)
    {
        // Arrange
        var piqiRequest = new PIQIRequest
        {
            ContributorID = "TestProvider",
            DataSourceID = "TestSource",
            PIQIModelMnemonic = "PAT_CLINICAL_V1",
            EvaluationRubricMnemonic = "CQL_LIBRARY_TEST",
            MessageID = "CQLMsg001",
            MessageData = File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestData/CQLTestData/Input/AnnualFluVaccine_Pass.json"))
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

        string? expectedOutputString = File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestData/CQLTestData/ExpectedOutput/AnnualFluVaccine_Pass.json"));
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
    [Trait("Category", "CQL_Audit")]
    [InlineData("/PIQI/ScoreAuditMessage")]
    public async Task CQL1_Audit2_ReturnsExpectedResponse(string endpoint)
    {
        // Arrange
        var piqiRequest = new PIQIRequest
        {
            ContributorID = "TestProvider",
            DataSourceID = "TestSource",
            PIQIModelMnemonic = "PAT_CLINICAL_V1",
            EvaluationRubricMnemonic = "CQL_LIBRARY_TEST",
            MessageID = "CQLMsg002",
            MessageData = File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestData/CQLTestData/Input/AnnualFluVaccine_Skip.json"))
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

        string? expectedOutputString = File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestData/CQLTestData/ExpectedOutput/AnnualFluVaccine_Skip.json"));
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
    [Trait("Category", "CQL_Audit")]
    [InlineData("/PIQI/ScoreAuditMessage")]
    public async Task CQL1_Audit3_ReturnsExpectedResponse(string endpoint)
    {
        // Arrange
        var piqiRequest = new PIQIRequest
        {
            ContributorID = "TestProvider",
            DataSourceID = "TestSource",
            PIQIModelMnemonic = "PAT_CLINICAL_V1",
            EvaluationRubricMnemonic = "CQL_LIBRARY_TEST",
            MessageID = "CQLMsg002",
            MessageData = File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestData/CQLTestData/Input/AnnualFluVaccine_Skip_DeathDate.json"))
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

        string? expectedOutputString = File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestData/CQLTestData/ExpectedOutput/AnnualFluVaccine_Skip_DeathDate.json"));
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
    [Trait("Category", "CQL_Audit")]
    [InlineData("/PIQI/ScoreAuditMessage")]
    public async Task CQL1_Audit4_ReturnsExpectedResponse(string endpoint)
    {
        // Arrange
        var piqiRequest = new PIQIRequest
        {
            ContributorID = "TestProvider",
            DataSourceID = "TestSource",
            PIQIModelMnemonic = "PAT_CLINICAL_V1",
            EvaluationRubricMnemonic = "CQL_LIBRARY_TEST",
            MessageID = "CQLMsg002",
            MessageData = File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestData/CQLTestData/Input/AnnualFluVaccine_Skip_Deceased.json"))
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

        string? expectedOutputString = File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestData/CQLTestData/ExpectedOutput/AnnualFluVaccine_Skip_Deceased.json"));
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
    [Trait("Category", "CQL_Audit")]
    [InlineData("/PIQI/ScoreAuditMessage")]
    public async Task CQL1_Audit5_ReturnsExpectedResponse(string endpoint)
    {
        // Arrange
        var piqiRequest = new PIQIRequest
        {
            ContributorID = "TestProvider",
            DataSourceID = "TestSource",
            PIQIModelMnemonic = "PAT_CLINICAL_V1",
            EvaluationRubricMnemonic = "CQL_LIBRARY_TEST",
            MessageID = "CQLMsg003",
            MessageData = File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestData/CQLTestData/Input/AnnualFluVaccine_Fail.json"))
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

        string? expectedOutputString = File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestData/CQLTestData/ExpectedOutput/AnnualFluVaccine_Fail.json"));
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
    [Trait("Category", "CQL_Audit")]
    [InlineData("/PIQI/ScoreAuditMessage")] 
    public async Task CQL1_Audit6_ReturnsExpectedResponse(string endpoint)
    {
        // Arrange
        var piqiRequest = new PIQIRequest
        {
            ContributorID = "TestProvider",
            DataSourceID = "TestSource",
            PIQIModelMnemonic = "PAT_CLINICAL_V1",
            EvaluationRubricMnemonic = "CQL_LIBRARY_TEST",
            MessageID = "CQLMsg003",
            MessageData = File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestData/CQLTestData/Input/AnnualFluVaccine_Fail_FluNotInPeriod.json"))
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

        string? expectedOutputString = File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestData/CQLTestData/ExpectedOutput/AnnualFluVaccine_Fail_FluNotInPeriod.json"));
        if (expectedOutputString == null) Assert.Fail("Expected output result file not found.");

        PIQIResponse? expectedresult = System.Text.Json.JsonSerializer.Deserialize<PIQIResponse>(expectedOutputString, options);
        if (expectedresult == null) Assert.Fail("Failed to deserialize expected result file.");

        #region Scoring Data

        if (expectedresult.ScoringData == null) Assert.Fail("Missing or invalid scoring data in the expected result file.");
        ScoreDataCompare(expectedresult.ScoringData, result.ScoringData);

        #endregion

        #endregion
    }

    #endregion

    #endregion
}
