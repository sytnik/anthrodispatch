namespace AnthroDispatch.Application.Abstractions;

public interface IAnthroDispatchMockDataGenerator
{
    Task<AnthroDispatchDataset> GenerateAsync(
        AnthroDispatchGenerationOptions options,
        CancellationToken cancellationToken = default);
}