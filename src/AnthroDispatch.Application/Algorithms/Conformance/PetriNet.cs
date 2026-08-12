namespace AnthroDispatch.Application.Algorithms.Conformance;

/// <summary>
/// Minimal place/transition Petri net: a marking (token count per place) and
/// consume/produce operations. Used to represent the regulatory model N of
/// hard constraints C_hard(x) (dissertation §3.4) — places correspond to
/// exclusive resources (teacher/group/room availability at a given slot),
/// each with capacity 1 (either free, 1 token, or taken, 0 tokens).
/// </summary>
public sealed class PetriNet
{
    private readonly Dictionary<string, int> _marking = new();

    public void AddPlace(string placeId, int initialTokens = 1)
    {
        _marking[placeId] = initialTokens;
    }

    public bool HasPlace(string placeId) => _marking.ContainsKey(placeId);

    public int TokensAt(string placeId) => _marking.GetValueOrDefault(placeId, 0);

    /// <summary>Attempts to consume 1 token from the place. Returns false
    /// (a "missing token" in token-replay terms) if none is available.</summary>
    public bool TryConsume(string placeId)
    {
        if (!_marking.TryGetValue(placeId, out var tokens) || tokens <= 0) return false;
        _marking[placeId] = tokens - 1;
        return true;
    }

    public void Produce(string placeId)
    {
        _marking[placeId] = _marking.GetValueOrDefault(placeId, 0) + 1;
    }
}
