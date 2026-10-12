namespace TMHue.Core.Services;

/// <summary>
/// Janela deslizante de falhas: dispara quando <c>threshold</c> falhas caem dentro de
/// <c>window</c>. Serve de disjuntor para um handler global que engole exceções — uma falha
/// isolada segue tolerada, uma que se repete a cada frame não.
/// </summary>
public sealed class FailureBurstDetector
{
    private readonly int _threshold;
    private readonly long _windowMilliseconds;
    private readonly Queue<long> _failures;

    public FailureBurstDetector(int threshold, TimeSpan window)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(threshold, 1);
        _threshold = threshold;
        _windowMilliseconds = (long)window.TotalMilliseconds;
        _failures = new Queue<long>(threshold);
    }

    /// <summary>Registra uma falha ocorrida em <paramref name="timestampMilliseconds"/> (relógio
    /// monotônico, ex.: <see cref="Environment.TickCount64"/>) e informa se o limite estourou.</summary>
    public bool RegisterFailure(long timestampMilliseconds)
    {
        _failures.Enqueue(timestampMilliseconds);
        while (_failures.Count > _threshold)
            _failures.Dequeue();

        return _failures.Count == _threshold
            && timestampMilliseconds - _failures.Peek() <= _windowMilliseconds;
    }
}
