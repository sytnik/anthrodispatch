using AnthroDispatch.Application.Abstractions;
using AnthroDispatch.Infrastructure.MockData;
using FluentAssertions;

namespace AnthroDispatch.Tests.Algorithms;

[TestFixture]
public class MockDatasetGeneratorTests
{
    [Test]
    public async Task MockDatasetGenerator_ShouldBeDeterministicForSameSeed()
    {
        var gen = new MockDatasetGenerator();
        var req = new DatasetGenerationRequest(42, 6, 120, 10, 10, 8);

        var r1 = await gen.GenerateAsync(req);
        var r2 = await gen.GenerateAsync(req);

        r1.Groups.Count.Should().Be(r2.Groups.Count);
        r1.Instructors.Count.Should().Be(r2.Instructors.Count);
        r1.Disciplines.Count.Should().Be(r2.Disciplines.Count);

        // Names should match (deterministic Bogus)
        for (var i = 0; i < r1.Instructors.Count; i++)
            r1.Instructors[i].FullName.Should().Be(r2.Instructors[i].FullName);
    }

    [Test]
    public async Task MockDatasetGenerator_ShouldGenerateRequestedCounts()
    {
        var gen = new MockDatasetGenerator();
        var req = new DatasetGenerationRequest(99, 6, 100, 10, 10, 8);
        var r = await gen.GenerateAsync(req);

        r.Groups.Count.Should().Be(6);
        r.Instructors.Count.Should().Be(10);
        r.Disciplines.Count.Should().Be(10);
        r.Rooms.Count.Should().Be(8);
        r.Assignments.Count.Should().BeGreaterThan(0);
    }

    [Test]
    public async Task MockDatasetGenerator_CompatibilityScoresShouldBeWithinRange()
    {
        var gen = new MockDatasetGenerator();
        var req = new DatasetGenerationRequest(7, 3, 60, 5, 8, 5);
        var r = await gen.GenerateAsync(req);

        foreach (var c in r.Compatibilities)
            c.Score.Should().BeInRange(-1.0, 1.0);
    }
}