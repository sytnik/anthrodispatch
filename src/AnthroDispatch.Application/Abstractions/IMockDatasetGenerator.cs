namespace AnthroDispatch.Application.Abstractions;

public interface IMockDatasetGenerator
{
    Task<DatasetGenerationResult> GenerateAsync(
        DatasetGenerationRequest request,
        CancellationToken cancellationToken = default);
}