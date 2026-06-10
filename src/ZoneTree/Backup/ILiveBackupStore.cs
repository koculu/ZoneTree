using ZoneTree.Segments.Disk;

namespace ZoneTree.Backup;

/// <summary>
/// Receives live backup generation operations.
/// Implement this interface to write backup files to local disk, object
/// storage, a remote service, or any other destination.
/// </summary>
public interface ILiveBackupStore
{
  /// <summary>
  /// Allocates the next backup generation id.
  /// </summary>
  Task<long> GetNextGenerationIdAsync(CancellationToken cancellationToken);

  /// <summary>
  /// Called when a new backup generation starts.
  /// </summary>
  Task BeginGenerationAsync(
      long generationId,
      DateTime startedAtUtc,
      CancellationToken cancellationToken);

  /// <summary>
  /// Adds an immutable disk segment to the current generation.
  /// Returns true when ZoneTree must upload this segment file.
  /// </summary>
  Task<bool> UseSegmentAsync(
      long generationId,
      DiskSegmentFile file,
      CancellationToken cancellationToken);

  /// <summary>
  /// Uploads one immutable disk segment file.
  /// </summary>
  Task UploadSegmentFileAsync(
      long generationId,
      DiskSegmentFile file,
      Stream source,
      CancellationToken cancellationToken);

  /// <summary>
  /// Opens a writer for one logical in-memory record batch.
  /// </summary>
  Task<ILiveBackupRecordWriter> OpenRecordWriterAsync(
      long generationId,
      LiveBackupRecordBatch batch,
      CancellationToken cancellationToken);

  /// <summary>
  /// Called when the generation is complete.
  /// </summary>
  Task CompleteGenerationAsync(
      long generationId,
      long lastOpIndex,
      CancellationToken cancellationToken);
}
