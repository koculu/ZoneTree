namespace ZoneTree.Core;

/// <summary>
/// Provides monotonically increasing ids for segment ids or operation indexes.
/// </summary>
/// <remarks>
/// When used as an operation-index provider, the monotonic sequence is the
/// producer high-water mark restored after restart and WAL replay.
/// </remarks>
public interface IIncrementalIdProvider
{
  /// <summary>
  /// Returns the next id in the sequence.
  /// </summary>
  long NextId();

  /// <summary>
  /// Sets the next id that will be returned by <see cref="NextId"/>.
  /// </summary>
  void SetNextId(long id);

  /// <summary>
  /// The last id returned by <see cref="NextId"/>.
  /// </summary>
  long LastId { get; }
}
