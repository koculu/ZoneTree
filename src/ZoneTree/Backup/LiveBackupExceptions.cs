using ZoneTree.Exceptions;

namespace ZoneTree.Backup;

public class LiveBackupException : ZoneTreeException
{
  public LiveBackupException(
      string message,
      Exception innerException = null)
      : base(message, innerException)
  {
  }
}

public sealed class LiveBackupGenerationException : LiveBackupException
{
  public LiveBackupGenerationException(
      long generationId,
      string operation,
      Exception innerException)
      : base(
          $"Live backup generation {generationId} failed while {operation}.",
          innerException)
  {
    GenerationId = generationId;
    Operation = operation;
  }

  public long GenerationId { get; }

  public string Operation { get; }
}

public sealed class LiveBackupQueueException : LiveBackupException
{
  public LiveBackupQueueException(
      string operation,
      Exception innerException)
      : base($"Live backup failed while {operation}.", innerException)
  {
    Operation = operation;
  }

  public string Operation { get; }
}

public sealed class LiveBackupRestoreTargetAlreadyExistsException
    : LiveBackupException
{
  public LiveBackupRestoreTargetAlreadyExistsException()
      : base(
          "Live backup restore target already contains a ZoneTree database. " +
          "Restore into an empty data directory or open the existing database.")
  {
  }
}
