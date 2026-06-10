namespace ZoneTree.Backup;

/// <summary>
/// UTC schedule for automatic live backup generations.
/// </summary>
public sealed class LiveBackupSchedule
{
  readonly TimeOnly[] DailyUtcTimeArray;

  readonly LiveBackupWeeklyTime[] WeeklyUtcTimeArray;

  LiveBackupSchedule(
      LiveBackupScheduleKind kind,
      TimeSpan interval,
      TimeOnly[] dailyUtcTimes,
      LiveBackupWeeklyTime[] weeklyUtcTimes)
  {
    Kind = kind;
    Interval = interval;
    DailyUtcTimeArray = dailyUtcTimes;
    WeeklyUtcTimeArray = weeklyUtcTimes;
  }

  /// <summary>
  /// No scheduled backup generations.
  /// </summary>
  public static LiveBackupSchedule None { get; } = new(
      LiveBackupScheduleKind.None,
      TimeSpan.Zero,
      [],
      []);

  public LiveBackupScheduleKind Kind { get; }

  public TimeSpan Interval { get; }

  public TimeOnly[] DailyUtcTimes => DailyUtcTimeArray;

  public LiveBackupWeeklyTime[] WeeklyUtcTimes => WeeklyUtcTimeArray;

  /// <summary>
  /// Creates a schedule that runs after the specified interval.
  /// </summary>
  public static LiveBackupSchedule Every(TimeSpan interval)
  {
    ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(interval, TimeSpan.Zero);

    return new LiveBackupSchedule(
        LiveBackupScheduleKind.Every,
        interval,
        [],
        []);
  }

  /// <summary>
  /// Creates a schedule that runs every day at the specified UTC times.
  /// </summary>
  public static LiveBackupSchedule Daily(params TimeOnly[] utcTimes)
  {
    return new LiveBackupSchedule(
        LiveBackupScheduleKind.Daily,
        TimeSpan.Zero,
        NormalizeDailyTimes(utcTimes),
        []);
  }

  /// <summary>
  /// Creates a schedule that runs every week at the specified UTC times.
  /// </summary>
  public static LiveBackupSchedule Weekly(
      params LiveBackupWeeklyTime[] utcTimes)
  {
    return new LiveBackupSchedule(
        LiveBackupScheduleKind.Weekly,
        TimeSpan.Zero,
        [],
        NormalizeWeeklyTimes(utcTimes));
  }

  /// <summary>
  /// Creates one weekly UTC schedule point.
  /// </summary>
  public static LiveBackupWeeklyTime On(
      DayOfWeek dayOfWeek,
      TimeOnly utcTime)
  {
    return new LiveBackupWeeklyTime(dayOfWeek, utcTime);
  }

  internal DateTime GetNextUtc(DateTime utcNow)
  {
    utcNow = ToUtcDateTime(utcNow);
    return Kind switch
    {
      LiveBackupScheduleKind.None => DateTime.MaxValue,
      LiveBackupScheduleKind.Every => utcNow.Add(Interval),
      LiveBackupScheduleKind.Daily => GetNextDailyUtc(utcNow),
      LiveBackupScheduleKind.Weekly => GetNextWeeklyUtc(utcNow),
      _ => DateTime.MaxValue
    };
  }

  DateTime GetNextDailyUtc(DateTime utcNow)
  {
    var today = DateOnly.FromDateTime(utcNow);
    foreach (var time in DailyUtcTimeArray)
    {
      var candidate = ToUtcDateTime(today, time);
      if (candidate > utcNow)
        return candidate;
    }
    return ToUtcDateTime(today.AddDays(1), DailyUtcTimeArray[0]);
  }

  DateTime GetNextWeeklyUtc(DateTime utcNow)
  {
    var today = DateOnly.FromDateTime(utcNow);
    DateTime next = DateTime.MaxValue;
    foreach (var weeklyTime in WeeklyUtcTimeArray)
    {
      var daysToAdd = ((int)weeklyTime.DayOfWeek - (int)utcNow.DayOfWeek + 7) % 7;
      var candidate = ToUtcDateTime(
          today.AddDays(daysToAdd),
          weeklyTime.UtcTime);
      if (candidate <= utcNow)
      {
        candidate = ToUtcDateTime(
            today.AddDays(daysToAdd + 7),
            weeklyTime.UtcTime);
      }
      if (candidate < next)
        next = candidate;
    }
    return next;
  }

  static DateTime ToUtcDateTime(DateTime dateTime)
  {
    return dateTime.Kind == DateTimeKind.Utc
        ? dateTime
        : dateTime.ToUniversalTime();
  }

  static DateTime ToUtcDateTime(DateOnly date, TimeOnly time)
  {
    return new DateTime(
        date.Year,
        date.Month,
        date.Day,
        0,
        0,
        0,
        DateTimeKind.Utc).Add(time.ToTimeSpan());
  }

  static TimeOnly[] NormalizeDailyTimes(TimeOnly[] utcTimes)
  {
    ArgumentNullException.ThrowIfNull(utcTimes);
    var times = utcTimes
        .Distinct()
        .OrderBy(x => x)
        .ToArray();
    if (times.Length == 0)
      throw new ArgumentException(
          "Daily schedule requires at least one UTC time.",
          nameof(utcTimes));
    return times;
  }

  static LiveBackupWeeklyTime[] NormalizeWeeklyTimes(
      LiveBackupWeeklyTime[] utcTimes)
  {
    ArgumentNullException.ThrowIfNull(utcTimes);
    var times = utcTimes
        .Distinct()
        .OrderBy(x => x.DayOfWeek)
        .ThenBy(x => x.UtcTime)
        .ToArray();
    if (times.Length == 0)
      throw new ArgumentException(
          "Weekly schedule requires at least one UTC time.",
          nameof(utcTimes));
    return times;
  }
}

public enum LiveBackupScheduleKind
{
  None,
  Every,
  Daily,
  Weekly
}

public readonly struct LiveBackupWeeklyTime : IEquatable<LiveBackupWeeklyTime>
{
  public LiveBackupWeeklyTime(
      DayOfWeek dayOfWeek,
      TimeOnly utcTime)
  {
    DayOfWeek = dayOfWeek;
    UtcTime = utcTime;
  }

  public DayOfWeek DayOfWeek { get; }

  public TimeOnly UtcTime { get; }

  public bool Equals(LiveBackupWeeklyTime other)
  {
    return DayOfWeek == other.DayOfWeek &&
        UtcTime.Equals(other.UtcTime);
  }

  public override bool Equals(object obj)
  {
    return obj is LiveBackupWeeklyTime other && Equals(other);
  }

  public override int GetHashCode()
  {
    return HashCode.Combine(DayOfWeek, UtcTime);
  }

  public static bool operator ==(LiveBackupWeeklyTime left, LiveBackupWeeklyTime right)
  {
    return left.Equals(right);
  }

  public static bool operator !=(LiveBackupWeeklyTime left, LiveBackupWeeklyTime right)
  {
    return !(left == right);
  }
}
