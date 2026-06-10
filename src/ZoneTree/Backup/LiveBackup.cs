using ZoneTree.AbstractFileStream;
using ZoneTree.Options;
using ZoneTree.Segments;
using ZoneTree.Segments.Disk;
using ZoneTree.Segments.NullDisk;

namespace ZoneTree.Backup;

/// <summary>
/// Coordinates live backup of immutable ZoneTree disk segments and in-memory
/// records without changing the live ZoneTree state.
/// </summary>
public sealed class LiveBackup<TKey, TValue> : IDisposable
{
  readonly IZoneTree<TKey, TValue> ZoneTree;

  readonly IZoneTreeMaintenance<TKey, TValue> Maintenance;

  readonly Core.ZoneTree<TKey, TValue> CoreZoneTree;

  readonly LiveBackupOptions Options;

  readonly ZoneTreeOptions<TKey, TValue> ZoneTreeOptions;

  readonly object SyncRoot = new();

  readonly IFileStreamProvider SourceDataFileStreamProvider;

  readonly ILiveBackupStore Store;

  TaskCompletionSource<bool> StateChanged = CreateTaskSource();

  [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2213:Disposable fields should be disposed", Justification = "SchedulerCancellation is dispsed correctly.")]
  CancellationTokenSource SchedulerCancellation;

  int RunningSchedulerCount;

  long RecordBatchId;

  long CurrentBackupGenerationId;

  bool IsLiveBackupStarted;

  bool IsGenerationRunning;

  public LiveBackup(
      IZoneTree<TKey, TValue> zoneTree,
      LiveBackupOptions options)
  {
    ZoneTree = zoneTree ?? throw new ArgumentNullException(nameof(zoneTree));
    CoreZoneTree = zoneTree as Core.ZoneTree<TKey, TValue> ??
        throw new NotSupportedException(
            "Live backup requires the built-in ZoneTree implementation.");

    Options = options ?? throw new ArgumentNullException(nameof(options));
    Options.Normalize();

    if (Options.Store == null)
      throw new ArgumentException(
          "Live backup store is required.",
          nameof(options));

    Maintenance = zoneTree.Maintenance;
    ZoneTreeOptions = Maintenance.CloneOptions();
    SourceDataFileStreamProvider =
        ZoneTreeOptions.RandomAccessDeviceManager.FileStreamProvider;
    Store = options.Store;
    Maintenance.OnZoneTreeIsDisposing += OnZoneTreeIsDisposing;
  }

  void OnZoneTreeIsDisposing(IZoneTreeMaintenance<TKey, TValue> zoneTree)
  {
    Dispose();
  }

  public long CurrentGenerationId
  {
    get
    {
      lock (SyncRoot)
        return CurrentBackupGenerationId;
    }
  }

  /// <summary>
  /// Starts live backup by attaching merge-triggered backup handling and
  /// starting the configured scheduler.
  /// </summary>
  public void Start()
  {
    lock (SyncRoot)
    {
      if (IsLiveBackupStarted)
        return;

      IsLiveBackupStarted = true;
      Maintenance.OnMergeOperationEnded += OnMergeOperationEnded;
      StartScheduler();
      SignalStateChanged();
    }
  }

  /// <summary>
  /// Stops live backup by detaching merge-triggered backup handling and
  /// requesting scheduler cancellation.
  /// </summary>
  public void Stop()
  {
    lock (SyncRoot)
    {
      if (IsLiveBackupStarted)
      {
        Maintenance.OnMergeOperationEnded -= OnMergeOperationEnded;
        IsLiveBackupStarted = false;
      }

      var schedulerCancellation = SchedulerCancellation;
      schedulerCancellation?.Cancel();
      SchedulerCancellation = null;

      SignalStateChanged();
    }
  }

  /// <summary>
  /// Creates a live backup generation immediately.
  /// </summary>
  /// <remarks>
  /// Manual generations are not queued and do not require Start().
  /// If another generation is already running, this method throws.
  /// </remarks>
  public void CreateGeneration()
  {
    CreateGenerationAsync().GetAwaiter().GetResult();
  }

  /// <summary>
  /// Creates a live backup generation immediately.
  /// </summary>
  /// <remarks>
  /// Manual generations are not queued and do not require Start().
  /// If another generation is already running, this method throws.
  /// </remarks>
  public async Task CreateGenerationAsync(
      CancellationToken cancellationToken = default)
  {
    cancellationToken.ThrowIfCancellationRequested();

    BeginBackupGeneration(
        skipIfBusy: false,
        requireLiveBackupStarted: false);

    try
    {
      await BackupGenerationAsync(cancellationToken)
          .ConfigureAwait(false);
    }
    finally
    {
      CompleteBackupGenerationActivity();
    }
  }

  /// <summary>
  /// Waits until live backup is stopped and all live backup activity has
  /// finished.
  /// </summary>
  /// <remarks>
  /// This method waits while Start() is active. Call Stop() to detach automatic
  /// triggers and request scheduler shutdown. The method also waits for the
  /// scheduler to exit and for any running generation to complete.
  /// </remarks>
  public void WaitForLiveBackup()
  {
    WaitForLiveBackupAsync().GetAwaiter().GetResult();
  }

  /// <summary>
  /// Waits until live backup is stopped and all live backup activity has
  /// finished.
  /// </summary>
  /// <remarks>
  /// This method waits while Start() is active. Call Stop() to detach automatic
  /// triggers and request scheduler shutdown. The method also waits for the
  /// scheduler to exit and for any running generation to complete.
  /// </remarks>
  public async Task WaitForLiveBackupAsync(
      CancellationToken cancellationToken = default)
  {
    while (true)
    {
      Task waitTask;

      lock (SyncRoot)
      {
        if (!IsLiveBackupStarted &&
            !IsGenerationRunning &&
            RunningSchedulerCount == 0)
        {
          return;
        }

        waitTask = StateChanged.Task;
      }

      await waitTask.WaitAsync(cancellationToken)
          .ConfigureAwait(false);
    }
  }

  void StartScheduler()
  {
    if (Options.Schedule.Kind == LiveBackupScheduleKind.None)
      return;

    SchedulerCancellation?.Cancel();

    var schedulerCancellation = new CancellationTokenSource();
    SchedulerCancellation = schedulerCancellation;
    ++RunningSchedulerCount;

    _ = RunSchedulerAsync(schedulerCancellation);
  }

  async Task RunSchedulerAsync(
      CancellationTokenSource schedulerCancellation)
  {
    var cancellationToken = schedulerCancellation.Token;

    try
    {
      var nextUtc = Options.Schedule.GetNextUtc(DateTime.UtcNow);

      while (true)
      {
        await DelayUntilUtcAsync(nextUtc, cancellationToken)
            .ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();

        StartBackgroundBackupGeneration();

        nextUtc = Options.Schedule.GetNextUtc(DateTime.UtcNow);
      }
    }
    catch (OperationCanceledException)
        when (cancellationToken.IsCancellationRequested)
    {
    }
    catch (Exception e)
    {
      LogBackupError(e);
    }
    finally
    {
      lock (SyncRoot)
      {
        --RunningSchedulerCount;

        if (ReferenceEquals(SchedulerCancellation, schedulerCancellation))
          SchedulerCancellation = null;

        schedulerCancellation.Dispose();

        SignalStateChanged();
      }
    }
  }

  static async Task DelayUntilUtcAsync(
      DateTime utcTime,
      CancellationToken cancellationToken)
  {
    while (true)
    {
      var delay = utcTime - DateTime.UtcNow;

      if (delay <= TimeSpan.Zero)
        return;

      if (delay > TimeSpan.FromHours(1))
        delay = TimeSpan.FromHours(1);

      await Task.Delay(delay, cancellationToken)
          .ConfigureAwait(false);
    }
  }

  void OnMergeOperationEnded(
      IZoneTreeMaintenance<TKey, TValue> zoneTree,
      MergeResult mergeResult)
  {
    StartBackupGenerationOnSuccessfulMerge(mergeResult);
  }

  void StartBackupGenerationOnSuccessfulMerge(MergeResult mergeResult)
  {
    try
    {
      if (Options.BackupAfterMerge &&
          mergeResult == MergeResult.SUCCESS)
      {
        StartBackgroundBackupGeneration();
      }
    }
    catch (Exception e)
    {
      LogBackupError(e);
    }
  }

  void StartBackgroundBackupGeneration()
  {
    if (!BeginBackupGeneration(
        skipIfBusy: true,
        requireLiveBackupStarted: true))
    {
      return;
    }

    _ = Task.Run(RunBackgroundBackupGenerationAsync);
  }

  async Task RunBackgroundBackupGenerationAsync()
  {
    try
    {
      await BackupGenerationAsync(CancellationToken.None)
          .ConfigureAwait(false);
    }
    catch (Exception e)
    {
      LogBackupError(e);
    }
    finally
    {
      CompleteBackupGenerationActivity();
    }
  }

  bool BeginBackupGeneration(
      bool skipIfBusy,
      bool requireLiveBackupStarted)
  {
    lock (SyncRoot)
    {
      if (requireLiveBackupStarted &&
          !IsLiveBackupStarted)
      {
        return false;
      }

      if (IsGenerationRunning)
      {
        if (skipIfBusy)
          return false;

        throw new InvalidOperationException(
            "A live backup generation is already running.");
      }

      IsGenerationRunning = true;
      SignalStateChanged();

      return true;
    }
  }

  void CompleteBackupGenerationActivity()
  {
    lock (SyncRoot)
    {
      if (!IsGenerationRunning)
        return;

      IsGenerationRunning = false;
      SignalStateChanged();
    }
  }

  void SignalStateChanged()
  {
    var completion = StateChanged;
    StateChanged = CreateTaskSource();
    completion.TrySetResult(true);
  }

  async Task BackupGenerationAsync(CancellationToken cancellationToken)
  {
    long generationId = 0;
    try
    {
      cancellationToken.ThrowIfCancellationRequested();

      var moveMutableSegmentForward =
          Options.InMemoryMode == LiveBackupInMemoryMode.Snapshot;

      using var segments = CoreZoneTree.CollectBackupSegments(
          moveMutableSegmentForward,
          Options.IncludeInMemoryRecords);

      var startedAtUtc = DateTime.UtcNow;

      generationId = await Store.GetNextGenerationIdAsync(
          cancellationToken).ConfigureAwait(false);

      if (generationId <= 0)
        throw new InvalidOperationException(
            "Live backup store returned an invalid generation id.");

      await Store.BeginGenerationAsync(
          generationId,
          startedAtUtc,
          cancellationToken).ConfigureAwait(false);

      if (Options.IncludeInMemoryRecords &&
          segments.InMemoryIterator != null)
      {
        await BackupInMemoryRecordsAsync(
            generationId,
            segments.InMemoryIterator,
            cancellationToken).ConfigureAwait(false);
      }

      await BackupDiskSegmentAsync(
          segments.DiskSegment,
          generationId,
          order: 0,
          cancellationToken).ConfigureAwait(false);

      if (segments.BottomSegments != null)
      {
        var order = 1;

        foreach (var bottomSegment in segments.BottomSegments)
        {
          await BackupDiskSegmentAsync(
              bottomSegment,
              generationId,
              order++,
              cancellationToken).ConfigureAwait(false);
        }
      }

      await CompleteGenerationAsync(
          generationId,
          CoreZoneTree.LastOpIndex,
          cancellationToken).ConfigureAwait(false);
    }
    catch (OperationCanceledException)
        when (cancellationToken.IsCancellationRequested)
    {
      throw;
    }
    catch (LiveBackupException)
    {
      throw;
    }
    catch (Exception e)
    {
      throw new LiveBackupGenerationException(
          generationId,
          "creating generation",
          e);
    }
  }

  async Task BackupDiskSegmentAsync(
      IDiskSegment<TKey, TValue> diskSegment,
      long generationId,
      int order,
      CancellationToken cancellationToken)
  {
    if (diskSegment == null ||
        diskSegment is NullDiskSegment<TKey, TValue> ||
        diskSegment.SegmentId == 0)
    {
      return;
    }

    var files = GetExistingSegmentFiles(diskSegment, order);

    if (files.Length == 0)
      return;

    foreach (var file in files)
    {
      if (await Store.UseSegmentAsync(
          generationId,
          file,
          cancellationToken).ConfigureAwait(false))
        await CopySegmentFileAsync(
            file,
            generationId,
            cancellationToken).ConfigureAwait(false);
    }
  }

  DiskSegmentFile[] GetExistingSegmentFiles(
      IDiskSegment<TKey, TValue> diskSegment,
      int order)
  {
    var diskSegmentFiles = diskSegment.GetFiles();
    var files = new DiskSegmentFile[diskSegmentFiles.Length];
    var index = 0;

    foreach (var file in diskSegmentFiles)
    {
      if (string.IsNullOrWhiteSpace(file.Path))
      {
        throw new FileNotFoundException(
            $"Disk segment {diskSegment.SegmentId} returned an empty file path.");
      }

      if (!SourceDataFileStreamProvider.FileExists(file.Path))
      {
        throw new FileNotFoundException(
            $"Disk segment file was not found for segment {diskSegment.SegmentId}.",
            file.Path);
      }

      files[index++] = file with { Order = order };
    }

    return files;
  }

  async Task CopySegmentFileAsync(
      DiskSegmentFile file,
      long generationId,
      CancellationToken cancellationToken)
  {
    if (string.IsNullOrWhiteSpace(file.Path))
      throw new FileNotFoundException(
          $"Disk segment {file.SegmentId} returned an empty file path.");

    if (!SourceDataFileStreamProvider.FileExists(file.Path))
    {
      throw new FileNotFoundException(
          $"Disk segment file was not found for segment {file.SegmentId}.",
          file.Path);
    }

    using var source = SourceDataFileStreamProvider.CreateFileStream(
        file.Path,
        FileMode.Open,
        FileAccess.Read,
        FileShare.ReadWrite);

    await Store.UploadSegmentFileAsync(
        generationId,
        file,
        source.ToStream(),
        cancellationToken).ConfigureAwait(false);
  }

  async Task BackupInMemoryRecordsAsync(
      long generationId,
      IZoneTreeIterator<TKey, TValue> iterator,
      CancellationToken cancellationToken)
  {
    var batchId = Interlocked.Increment(ref RecordBatchId);
    var batch = new LiveBackupRecordBatch
    {
      BatchId = batchId,
      CompressionMethod = Options.RecordBatchCompression.Method,
      CompressionLevel = Options.RecordBatchCompression.Level,
      CompressionBlockSize = Options.RecordBatchCompression.BlockSize
    };

    await using var writer = await Store.OpenRecordWriterAsync(
        generationId,
        batch,
        cancellationToken).ConfigureAwait(false);

    long recordIndex = 0;

    while (iterator.Next())
    {
      cancellationToken.ThrowIfCancellationRequested();
      var key = iterator.CurrentKey;
      var value = iterator.CurrentValue;
      var keyBytes = ZoneTree.KeySerializer.Serialize(in key);
      var valueBytes = ZoneTree.ValueSerializer.Serialize(in value);

      ++recordIndex;

      var record = new LiveBackupRecord
      {
        RecordIndex = recordIndex,
        IsDeleted = ZoneTreeOptions.IsDeleted(in key, in value),
        Key = keyBytes,
        Value = valueBytes
      };

      await writer.WriteAsync(
          record,
          cancellationToken).ConfigureAwait(false);
    }

    batch.RecordCount = recordIndex;
  }

  async Task CompleteGenerationAsync(
      long generationId,
      long lastOpIndex,
      CancellationToken cancellationToken)
  {
    await Store.CompleteGenerationAsync(
        generationId,
        lastOpIndex,
        cancellationToken).ConfigureAwait(false);

    lock (SyncRoot)
      CurrentBackupGenerationId = generationId;
  }

  static TaskCompletionSource<bool> CreateTaskSource()
  {
    return new TaskCompletionSource<bool>(
        TaskCreationOptions.RunContinuationsAsynchronously);
  }

  void LogBackupError(Exception exception)
  {
    ZoneTree.Logger.LogError(exception);
  }

  public void Dispose()
  {
    Maintenance.OnZoneTreeIsDisposing -= OnZoneTreeIsDisposing;
    Stop();
    WaitForLiveBackup();
    SchedulerCancellation?.Dispose();
    SchedulerCancellation = null;
  }
}
