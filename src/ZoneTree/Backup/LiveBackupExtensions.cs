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
    if (zoneTree == null)
      throw new ArgumentNullException(nameof(zoneTree));

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
    if (zoneTree == null)
      throw new ArgumentNullException(nameof(zoneTree));

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
    if (zoneTree == null)
      throw new ArgumentNullException(nameof(zoneTree));

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
    if (zoneTree == null)
      throw new ArgumentNullException(nameof(zoneTree));

    return new LiveBackup<TKey, TValue>(zoneTree, options);
  }

}
