namespace AnthroDispatch.Domain.Entities.Operational;

public sealed class Degree
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string FullName { get; init; } = ""; // Bachelor, Master
    public string ShortName { get; init; } = "";
}