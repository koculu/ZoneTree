using ZoneTree.AbstractFileStream;
using ZoneTree.Comparers;
using ZoneTree.Exceptions;
using ZoneTree.Logger;
using ZoneTree.Options;
using ZoneTree.Segments.RandomAccess;
using ZoneTree.Serializers;
using ZoneTree.WAL;

namespace ZoneTree.UnitTests;

public sealed class ZoneTreeOptionsValidationTests
{
  [Test]
  public void ValidOptionsPassValidation()
  {
    var options = CreateValidOptions();

    Assert.That(options.TryValidate(out var exception), Is.True);
    Assert.That(exception, Is.Null);
  }

  [TestCaseSource(nameof(MissingOptionCases))]
  public void MissingRequiredOptionsFailValidation(
      Action<ZoneTreeOptions<int, int>> configure,
      string option)
  {
    var options = CreateValidOptions();
    configure(options);

    var isValid = options.TryValidate(out var exception);

    Assert.That(isValid, Is.False);
    var missingOptionException = Assert.Throws<MissingOptionException>(() => options.Validate());
    Assert.That(exception, Is.TypeOf<MissingOptionException>());
    Assert.That(((MissingOptionException)exception).MissingOption, Is.EqualTo(option));
    Assert.That(missingOptionException.MissingOption, Is.EqualTo(option));
  }

  [TestCaseSource(nameof(InvalidOptionCases))]
  public void InvalidOptionValuesFailValidation(
      Action<ZoneTreeOptions<int, int>> configure,
      string option)
  {
    var options = CreateValidOptions();
    configure(options);

    var isValid = options.TryValidate(out var exception);

    Assert.That(isValid, Is.False);
    var invalidOptionException = Assert.Throws<InvalidOptionValueException>(() => options.Validate());
    Assert.That(exception, Is.TypeOf<InvalidOptionValueException>());
    Assert.That(((InvalidOptionValueException)exception).Option, Is.EqualTo(option));
    Assert.That(invalidOptionException.Option, Is.EqualTo(option));
  }

  [Test]
  public void InvalidCompressionLevelStillUsesCompressionLevelException()
  {
    var options = CreateValidOptions();
    options.DiskSegmentOptions.CompressionMethod = CompressionMethod.LZ4;
    options.DiskSegmentOptions.CompressionLevel = 2;

    var isValid = options.TryValidate(out var exception);

    Assert.That(isValid, Is.False);
    Assert.That(exception, Is.TypeOf<CompressionLevelIsOutOfRangeException>());
    Assert.Throws<CompressionLevelIsOutOfRangeException>(() => options.Validate());
  }

  [Test]
  public void AllowUnsafeOptionValuesSkipsNumericRanges()
  {
    var options = CreateValidOptions();
    options.AllowUnsafeOptionValues = true;
    options.MutableSegmentMaxItemCount = 1;
    options.DiskSegmentMaxItemCount = 1;
    options.BTreeNodeSize = 2;
    options.BTreeLeafSize = 2;
    options.WriteAheadLogOptions.CompressionBlockSize = 1;
    options.DiskSegmentOptions.CompressionBlockSize = 1;
    options.DiskSegmentOptions.MinimumRecordCount = 10;
    options.DiskSegmentOptions.MaximumRecordCount = 1;

    Assert.That(options.TryValidate(out var exception), Is.True);
    Assert.That(exception, Is.Null);
  }

  [Test]
  public void AllowUnsafeOptionValuesKeepsEnumAndCompressionValidation()
  {
    var options = CreateValidOptions();
    options.AllowUnsafeOptionValues = true;
    options.WriteAheadLogOptions.WriteAheadLogMode = (WriteAheadLogMode)99;

    Assert.That(options.TryValidate(out var exception), Is.False);
    Assert.That(exception, Is.TypeOf<InvalidOptionValueException>());
  }

  static IEnumerable<TestCaseData> MissingOptionCases()
  {
    yield return Missing(
        options => options.KeySerializer = null,
        nameof(ZoneTreeOptions<int, int>.KeySerializer));

    yield return Missing(
        options => options.ValueSerializer = null,
        nameof(ZoneTreeOptions<int, int>.ValueSerializer));

    yield return Missing(
        options => options.Comparer = null,
        nameof(ZoneTreeOptions<int, int>.Comparer));

    yield return Missing(
        options => options.IsDeleted = null,
        nameof(ZoneTreeOptions<int, int>.IsDeleted));

    yield return Missing(
        options => options.MarkValueDeleted = null,
        nameof(ZoneTreeOptions<int, int>.MarkValueDeleted));

    yield return Missing(
        options => options.Logger = null,
        nameof(ZoneTreeOptions<int, int>.Logger));

    yield return Missing(
        options => options.RandomAccessDeviceManager = null,
        nameof(ZoneTreeOptions<int, int>.RandomAccessDeviceManager));

    yield return Missing(
        options => options.WriteAheadLogProvider = null,
        nameof(ZoneTreeOptions<int, int>.WriteAheadLogProvider));

    yield return Missing(
        options => options.WriteAheadLogOptions = null,
        nameof(ZoneTreeOptions<int, int>.WriteAheadLogOptions));

    yield return Missing(
        options => options.DiskSegmentOptions = null,
        nameof(ZoneTreeOptions<int, int>.DiskSegmentOptions));

    yield return Missing(
        options => options.WriteAheadLogOptions.AsyncCompressedModeOptions = null,
        "WriteAheadLogOptions.AsyncCompressedModeOptions");

    yield return Missing(
        options => options.WriteAheadLogOptions.SyncCompressedModeOptions = null,
        "WriteAheadLogOptions.SyncCompressedModeOptions");
  }

  static IEnumerable<TestCaseData> InvalidOptionCases()
  {
    yield return Invalid(
        options => options.MutableSegmentMaxItemCount = 0,
        nameof(ZoneTreeOptions<int, int>.MutableSegmentMaxItemCount));

    yield return Invalid(
        options => options.DiskSegmentMaxItemCount = 0,
        nameof(ZoneTreeOptions<int, int>.DiskSegmentMaxItemCount));

    yield return Invalid(
        options => options.BTreeLockMode = (ZoneTree.Collections.BTree.Lock.BTreeLockMode)99,
        nameof(ZoneTreeOptions<int, int>.BTreeLockMode));

    yield return Invalid(
        options => options.BTreeNodeSize = 1,
        nameof(ZoneTreeOptions<int, int>.BTreeNodeSize));

    yield return Invalid(
        options => options.BTreeLeafSize = 1,
        nameof(ZoneTreeOptions<int, int>.BTreeLeafSize));

    yield return Invalid(
        options => options.WriteAheadLogOptions.WriteAheadLogMode = (WriteAheadLogMode)99,
        "WriteAheadLogOptions.WriteAheadLogMode");

    yield return Invalid(
        options => options.WriteAheadLogOptions.CompressionMethod = (CompressionMethod)99,
        "WriteAheadLogOptions.CompressionMethod");

    yield return Invalid(
        options => options.WriteAheadLogOptions.CompressionBlockSize = 0,
        "WriteAheadLogOptions.CompressionBlockSize");

    yield return Invalid(
        options => options.WriteAheadLogOptions.CompressionBlockSize = 16 * 1024 * 1024 + 1,
        "WriteAheadLogOptions.CompressionBlockSize");

    yield return Invalid(
        options => options.WriteAheadLogOptions.AsyncCompressedModeOptions.EmptyQueuePollInterval = -1,
        "WriteAheadLogOptions.AsyncCompressedModeOptions.EmptyQueuePollInterval");

    yield return Invalid(
        options => options.WriteAheadLogOptions.SyncCompressedModeOptions.TailWriterJobInterval = -1,
        "WriteAheadLogOptions.SyncCompressedModeOptions.TailWriterJobInterval");

    yield return Invalid(
        options => options.DiskSegmentOptions.DiskSegmentMode = (DiskSegmentMode)99,
        "DiskSegmentOptions.DiskSegmentMode");

    yield return Invalid(
        options => options.DiskSegmentOptions.CompressionMethod = (CompressionMethod)99,
        "DiskSegmentOptions.CompressionMethod");

    yield return Invalid(
        options => options.DiskSegmentOptions.CompressionBlockSize = 0,
        "DiskSegmentOptions.CompressionBlockSize");

    yield return Invalid(
        options => options.DiskSegmentOptions.CompressionBlockSize = 64 * 1024 * 1024 + 1,
        "DiskSegmentOptions.CompressionBlockSize");

    yield return Invalid(
        options => options.DiskSegmentOptions.MaximumRecordCount = 0,
        "DiskSegmentOptions.MaximumRecordCount");

    yield return Invalid(
        options => options.DiskSegmentOptions.MinimumRecordCount = 0,
        "DiskSegmentOptions.MinimumRecordCount");

    yield return Invalid(
        options => options.DiskSegmentOptions.MinimumRecordCount =
            options.DiskSegmentOptions.MaximumRecordCount,
        "DiskSegmentOptions.MinimumRecordCount");

    yield return Invalid(
        options => options.DiskSegmentOptions.KeyCacheSize = -1,
        "DiskSegmentOptions.KeyCacheSize");

    yield return Invalid(
        options => options.DiskSegmentOptions.ValueCacheSize = -1,
        "DiskSegmentOptions.ValueCacheSize");

    yield return Invalid(
        options => options.DiskSegmentOptions.KeyCacheRecordLifeTimeInMillisecond = -1,
        "DiskSegmentOptions.KeyCacheRecordLifeTimeInMillisecond");

    yield return Invalid(
        options => options.DiskSegmentOptions.ValueCacheRecordLifeTimeInMillisecond = -1,
        "DiskSegmentOptions.ValueCacheRecordLifeTimeInMillisecond");

    yield return Invalid(
        options => options.DiskSegmentOptions.DefaultSparseArrayStepSize = -1,
        "DiskSegmentOptions.DefaultSparseArrayStepSize");
  }

  static TestCaseData Missing(
      Action<ZoneTreeOptions<int, int>> configure,
      string option)
  {
    return new TestCaseData(configure, option).SetName($"Missing {option}");
  }

  static TestCaseData Invalid(
      Action<ZoneTreeOptions<int, int>> configure,
      string option)
  {
    return new TestCaseData(configure, option).SetName($"Invalid {option}");
  }

  static ZoneTreeOptions<int, int> CreateValidOptions()
  {
    var logger = new ConsoleLogger(LogLevel.Error);
    var options = new ZoneTreeOptions<int, int>
    {
      Comparer = new Int32ComparerAscending(),
      KeySerializer = new Int32Serializer(),
      ValueSerializer = new Int32Serializer(),
      Logger = logger,
      WriteAheadLogProvider = new NullWriteAheadLogProvider(),
      RandomAccessDeviceManager = new RandomAccessDeviceManager(
          logger,
          new InMemoryFileStreamProvider(),
          "data/ZoneTreeOptionsValidationTests")
    };
    options.DisableDeletion();
    return options;
  }
}
