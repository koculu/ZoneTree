namespace ZoneTree.Backup;

public static class LiveBackupExtensions
{
  /// <summary>
  /// Creates a live backup coordinator that writes into a local directory.
  /// </summary>
  public static LiveBackup<TKey, TValue> CreateLiveBackup<TKey, TValue>(
      this IZoneTree<TKey, TValue> zoneTree,
      string directory)
  {
    ArgumentNullException.ThrowIfNull(zoneTree);

    return new LiveBackup<TKey, TValue>(
        zoneTree,
        new LiveBackupOptions
        {
          Store = new LocalLiveBackupProvider(directory)
        });
  }

  /// <summary>
  /// Creates a live backup coordinator that writes into a local directory.
  /// </summary>
  public static LiveBackup<TKey, TValue> CreateLiveBackup<TKey, TValue>(
      this IZoneTree<TKey, TValue> zoneTree,
      LocalLiveBackupOptions localOptions)
  {
    ArgumentNullException.ThrowIfNull(zoneTree);

    return new LiveBackup<TKey, TValue>(
        zoneTree,
        new LiveBackupOptions
        {
          Store = new LocalLiveBackupProvider(localOptions)
        });
  }

  /// <summary>
  /// Creates a live backup coordinator that writes through the specified store.
  /// </summary>
  public static LiveBackup<TKey, TValue> CreateLiveBackup<TKey, TValue>(
      this IZoneTree<TKey, TValue> zoneTree,
      ILiveBackupStore store)
  {
    ArgumentNullException.ThrowIfNull(zoneTree);

    return new LiveBackup<TKey, TValue>(
        zoneTree,
        new LiveBackupOptions { Store = store });
  }

  /// <summary>
  /// Creates a live backup coordinator for this ZoneTree.
  /// </summary>
  public static LiveBackup<TKey, TValue> CreateLiveBackup<TKey, TValue>(
      this IZoneTree<TKey, TValue> zoneTree,
      LiveBackupOptions options)
  {
    ArgumentNullException.ThrowIfNull(zoneTree);

    return new LiveBackup<TKey, TValue>(zoneTree, options);
  }

}
