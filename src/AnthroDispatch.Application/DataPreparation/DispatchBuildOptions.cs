namespace AnthroDispatch.Application.DataPreparation;

public sealed record DispatchBuildOptions(int Term = 1, bool ValidateRoomCapacity = true);