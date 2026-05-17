using AnthroDispatch.Domain.Enums;

namespace AnthroDispatch.Domain.Entities;

public sealed class Room
{
    public Guid Id { get; init; }
    public string Code { get; init; } = "";
    public RoomType Type { get; init; }
    public int Capacity { get; init; }
}