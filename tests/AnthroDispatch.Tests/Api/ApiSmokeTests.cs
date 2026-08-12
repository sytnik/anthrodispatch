using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace AnthroDispatch.Tests.Api;

[TestFixture]
public class ApiSmokeTests
{
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    [OneTimeSetUp]
    public void Setup()
    {
        _factory = new WebApplicationFactory<Program>();
        _client = _factory.CreateClient();
    }

    [OneTimeTearDown]
    public void Teardown()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    // Shared dataset+run for tests that need them
    private static string? _cachedDatasetId;
    private static string? _cachedRunId;

    private async Task<(string datasetId, string runId)> EnsureDatasetAndRun()
    {
        if (_cachedDatasetId != null && _cachedRunId != null)
            return (_cachedDatasetId, _cachedRunId);

        var genPayload = new StringContent(
            """{"seed":1,"groups":3,"studentsApprox":60,"instructors":6,"disciplines":6,"rooms":5}""",
            Encoding.UTF8, "application/json");
        var genResp = await _client.PostAsync("/api/datasets/generate", genPayload);
        genResp.EnsureSuccessStatusCode();
        var genBody = await genResp.Content.ReadAsStringAsync();
        _cachedDatasetId = JsonDocument.Parse(genBody).RootElement.GetProperty("datasetId").GetString()!;

        var optPayload = new StringContent(
            $$"""{"datasetId":"{{_cachedDatasetId}}","algorithm":"BaselineGA","seed":1,"populationSize":10,"maxGenerations":5}""",
            Encoding.UTF8, "application/json");
        var optResp = await _client.PostAsync("/api/optimization/run", optPayload);
        optResp.EnsureSuccessStatusCode();
        var optBody = await optResp.Content.ReadAsStringAsync();
        _cachedRunId = JsonDocument.Parse(optBody).RootElement.GetProperty("runId").GetString()!;

        return (_cachedDatasetId, _cachedRunId);
    }

    [Test]
    public async Task HealthEndpoint_ShouldReturnOk()
    {
        var response = await _client.GetAsync("/api/health");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("ok");
    }

    [Test]
    public async Task GenerateDataset_ShouldReturnDatasetId()
    {
        var payload = new StringContent(
            """{"seed":42,"groups":4,"studentsApprox":80,"instructors":8,"disciplines":8,"rooms":6}""",
            Encoding.UTF8, "application/json");
        var response = await _client.PostAsync("/api/datasets/generate", payload);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("datasetId");
    }

    [Test]
    public async Task OptimizationRun_ShouldReturnMetrics()
    {
        var (datasetId, _) = await EnsureDatasetAndRun();
        var optPayload = new StringContent(
            $$"""{"datasetId":"{{datasetId}}","algorithm":"BaselineGA","seed":99,"populationSize":10,"maxGenerations":5}""",
            Encoding.UTF8, "application/json");
        var optResp = await _client.PostAsync("/api/optimization/run", optPayload);
        optResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var optBody = await optResp.Content.ReadAsStringAsync();
        optBody.Should().Contain("bestFitness");
    }

    [Test]
    public async Task ExplanationEndpoint_ShouldReturnNonEmptyExplanation()
    {
        var (_, runId) = await EnsureDatasetAndRun();
        var resp = await _client.GetAsync($"/api/optimization/{runId}/explanation");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadAsStringAsync();
        body.Should().Contain("timetableId");
    }

    [Test]
    public async Task CandidatesEndpoint_ShouldReturnRankedCandidatesSortedByScoreIa()
    {
        var (_, runId) = await EnsureDatasetAndRun();
        var resp = await _client.GetAsync($"/api/optimization/{runId}/candidates");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadAsStringAsync();
        body.Should().Contain("scoreIa");

        var doc = JsonDocument.Parse(body);
        var candidates = doc.RootElement.GetProperty("candidates").EnumerateArray().ToList();
        candidates.Should().NotBeEmpty();

        var scores = candidates.Select(c => c.GetProperty("scoreIa").GetDouble()).ToList();
        scores.Should().BeInDescendingOrder();
        candidates[0].GetProperty("rank").GetInt32().Should().Be(1);
    }

    [Test]
    public async Task ConformanceEndpoint_ShouldReturnConformScore()
    {
        var (_, runId) = await EnsureDatasetAndRun();
        var resp = await _client.GetAsync($"/api/optimization/{runId}/conformance");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadAsStringAsync();
        body.Should().Contain("conform");

        var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("conform").GetDouble().Should().BeInRange(0.0, 1.0);
    }

    [Test]
    public async Task WhatIfEndpoint_ShouldReturnDeltaMetrics()
    {
        var (_, runId) = await EnsureDatasetAndRun(); // todo

        // We need a valid instructor ID — get it from the dataset summary
        var genPayload = new StringContent(
            """{"seed":1,"groups":3,"studentsApprox":60,"instructors":6,"disciplines":6,"rooms":5}""",
            Encoding.UTF8, "application/json");
        var genResp = await _client.PostAsync("/api/datasets/generate", genPayload);
        var genBody = await genResp.Content.ReadAsStringAsync();
        var datasetId = JsonDocument.Parse(genBody).RootElement.GetProperty("datasetId").GetString()!;

        // Create a run for this dataset to get an instructor id from the run
        var optPayload = new StringContent(
            $$"""{"datasetId":"{{datasetId}}","algorithm":"BaselineGA","seed":2,"populationSize":5,"maxGenerations":3}""",
            Encoding.UTF8, "application/json");
        var optResp = await _client.PostAsync("/api/optimization/run", optPayload);
        optResp.EnsureSuccessStatusCode();
        var optBody = await optResp.Content.ReadAsStringAsync();
        var localRunId = JsonDocument.Parse(optBody).RootElement.GetProperty("runId").GetString()!;

        // Use a random (but valid-format) instructor GUID — what-if handles not found gracefully
        var instrId = Guid.NewGuid();
        var whatIfPayload = new StringContent(
            $$"""{"runId":"{{localRunId}}","instructorId":"{{instrId}}","day":2}""",
            Encoding.UTF8, "application/json");
        var wiResp = await _client.PostAsync("/api/whatif/instructor-unavailable", whatIfPayload);
        wiResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var wiBody = await wiResp.Content.ReadAsStringAsync();
        wiBody.Should().Contain("deltaF");
    }
}