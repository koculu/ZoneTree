using Tenray;
using ZoneTree.Collections;
using ZoneTree.Core;
using ZoneTree.Serializers;

namespace ZoneTree.Transactional
{
    public sealed class OptimisticTransaction<TKey, TValue> : IDisposable
    {
        const string TxHistory = "txHistory";

        const string TxDependency = "txDep";

        public long TransactionId { get; }

        readonly DictionaryWithWAL<TKey, CombinedValue<TValue, long>> OldValueTable;
        
        readonly DictionaryWithWAL<long, bool> DependencyTable;

        readonly ZoneTreeOptions<TKey, TValue> Options;

        public OptimisticTransaction(
            long transactionId,
            ZoneTreeOptions<TKey, TValue> options)
        {
            TransactionId = transactionId;
            Options = options;
            OldValueTable = new(
                transactionId,
                TxHistory,
                options.WriteAheadLogProvider,
                options.KeySerializer,
                new CombinedSerializer<TValue, long>(
                    options.ValueSerializer,
                    new Int64Serializer()),
                options.Comparer,
                (in CombinedValue<TValue, long> x) => x.Value2 == 0,
                (ref CombinedValue<TValue, long> x) => x.Value2 = 0
                );

            DependencyTable = new(
                transactionId,
                TxDependency,
                options.WriteAheadLogProvider,
                new Int64Serializer(),
                new BooleanSerializer(),
                new Int64ComparerAscending(),
                (in bool x) => x == true,
                (ref bool x) => x = true
                );
        }

        public void AbortTransaction(TransactionResult result)
        {
            // abort dependent transactions
            // Undo changes
        }

        public void Drop()
        {
            DependencyTable.Drop();
            OldValueTable.Drop();
        }

        /// <summary>
        /// Marks read stamp, adds dependencies or aborts transaction.
        /// https://en.wikipedia.org/wiki/Timestamp-based_concurrency_control
        /// </summary>
        /// <param name="optRecord"></param>
        /// <exception cref="TransactionIsAbortedException"></exception>
        public void HandleReadKey(
            ref OptimisticRecord optRecord)
        {
            if (optRecord.WriteStamp > TransactionId)
            {
                AbortTransaction(TransactionResult.AbortedRetry);
                throw new TransactionIsAbortedException(TransactionId, TransactionResult.AbortedRetry);
            }

            if (optRecord.WriteStamp != 0)
                DependencyTable.Upsert(optRecord.WriteStamp, false);
            optRecord.ReadStamp = Math.Max(optRecord.ReadStamp, TransactionId);            
        }

        /// <summary>
        /// Returns true for skipping writes. (Thomas Write Rule)
        /// https://en.wikipedia.org/wiki/Thomas_Write_Rule
        /// </summary>
        /// <param name="optRecord"></param>
        /// <returns>false if write must be skipped, otherwise true.</returns>
        /// <exception cref="TransactionIsAbortedException"></exception>
        public bool HandleWriteKey(
            ref OptimisticRecord optRecord, 
            in TKey key,
            bool hasOldValue, 
            in TValue oldValue)
        {
            if (optRecord.ReadStamp > TransactionId)
            {
                AbortTransaction(TransactionResult.AbortedRetry);
                throw new TransactionIsAbortedException(TransactionId, TransactionResult.AbortedRetry);
            }

            if (optRecord.WriteStamp > TransactionId)
                return false;
            var value = oldValue;
            if (!hasOldValue)
                Options.MarkValueDeleted(ref value);

            var combinedValue = 
                new CombinedValue<TValue, long>(value, optRecord.WriteStamp);
            OldValueTable.Upsert(key, combinedValue);
            optRecord.WriteStamp = TransactionId;
            return true;
        }

        public void Dispose()
        {
            DependencyTable?.Dispose();
            OldValueTable?.Dispose();
        }
    }
}
