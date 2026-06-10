namespace ZoneTree.Backup;

/// <summary>
/// Reads live backup generations for restore.
/// </summary>
public interface ILiveBackupSource
{
  Task<LiveBackupGeneration> ReadLatestGenerationAsync(
      CancellationToken cancellationToken);

  Task<LiveBackupGeneration> ReadGenerationAsync(
      long generationId,
      CancellationToken cancellationToken);

  Task<Stream> OpenSegmentFileAsync(
      LiveBackupFile file,
      CancellationToken cancellationToken);

  Task<Stream> OpenRecordBatchAsync(
      LiveBackupRecordBatch batch,
      CancellationToken cancellationToken);
}
