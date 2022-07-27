using Tenray.ZoneTree.Exceptions.WAL;

namespace Tenray.ZoneTree.WAL;

public static class WriteAheadLogEntryReader
{
    public delegate void LogEntryReaderDelegate<TLogEntry>(BinaryReader reader, ref TLogEntry logEntry);

    public delegate (bool isValid, TKey key, TValue value) LogEntryDeserializerDelegate<TKey, TValue, TLogEntry>(in TLogEntry logEntry);

    public static WriteAheadLogReadLogEntriesResult<TKey, TValue> ReadLogEntries<TKey, TValue, TLogEntry>(
        Stream stream,
        bool stopReadOnException,
        bool stopReadOnChecksumFailure,
        LogEntryReaderDelegate<TLogEntry> logEntryReader,
        LogEntryDeserializerDelegate<TKey, TValue, TLogEntry> logEntryDeserializer)
    {
        var result = new WriteAheadLogReadLogEntriesResult<TKey, TValue>
        {
            Success = true
        };
        stream.Seek(0, SeekOrigin.Begin);
        var binaryReader = new BinaryReader(stream);
        TLogEntry entry = default;

        var keyList = new List<TKey>();
        var valuesList = new List<TValue>();
        var i = 0;
        var length = stream.Length;
        while (true)
        {
            var logEntryPosition = stream.Position;
            try
            {
                if (stream.Position == length)
                    break;
                logEntryReader(binaryReader, ref entry);
            }
            catch (EndOfStreamException e)
            {
                var ex = new IncompleteTailRecordFoundException(e)
                {
                    FileLength = length,
                    RecordPosition = logEntryPosition,
                    RecordIndex = i
                };
                result.Exceptions.Add(i, ex);
                result.Success = false;
                break;
            }
            catch (ObjectDisposedException e)
            {
                result.Exceptions.Add(i, new ObjectDisposedException($"ReadLogEntry failed. Index={i}", e));
                result.Success = false;
                break;
            }
            catch (IOException e)
            {
                result.Exceptions.Add(i, new IOException($"ReadLogEntry failed. Index={i}", e));
                result.Success = false;
                if (stopReadOnException) break;
            }
            catch (Exception e)
            {
                result.Exceptions.Add(i, new InvalidOperationException($"ReadLogEntry failed. Index={i}", e));
                result.Success = false;
                if (stopReadOnException) break;
            }
            (var isValid, var key, var value) = logEntryDeserializer(in entry);
            if (!isValid)
            {
                if (!result.Exceptions.ContainsKey(i))
                    result.Exceptions.Add(i, new InvalidDataException($"Checksum failed. Index={i}"));
                result.Success = false;
                if (stopReadOnChecksumFailure) break;
            }

            keyList.Add(key);
            valuesList.Add(value);
            ++i;
        }
        result.Keys = keyList;
        result.Values = valuesList;
        stream.Seek(0, SeekOrigin.End);
        return result;
    }
}
