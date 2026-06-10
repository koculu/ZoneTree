namespace ZoneTree.Backup;

public interface ILiveBackupRecordWriter : IAsyncDisposable
{
  /// <summary>
  /// Writes one logical key/value record from an in-memory backup pass.
  /// The key and value buffers in the record are already serialized with
  /// the ZoneTree serializers configured for the database.
  /// </summary>
  Task WriteAsync(
      LiveBackupRecord record,
      CancellationToken cancellationToken);
}
