using AnthroDispatch.Application.Abstractions;
using AnthroDispatch.Domain.Entities.Dispatch;

namespace AnthroDispatch.Application.DataPreparation;

public interface IDispatchInputBuilder
{
    DispatchProblem Build(AnthroDispatchDataset dataset, DispatchBuildOptions? options = null);
}