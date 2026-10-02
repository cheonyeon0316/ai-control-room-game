using System;
using System.Collections.Generic;

namespace ControlRoom
{
    /// <summary>The collected case file. A later verification replaces the earlier entry with the same ID.</summary>
    public sealed class EvidenceDatabase
    {
        private readonly List<EvidenceRecord> entries = new List<EvidenceRecord>();
        public IReadOnlyList<EvidenceRecord> Entries => entries.AsReadOnly();

        public void AddRange(IEnumerable<EvidenceRecord> records)
        {
            if (records == null) return;
            foreach (var record in records)
            {
                if (record == null || string.IsNullOrWhiteSpace(record.id)) continue;
                var copy = Copy(record);
                var index = entries.FindIndex(item => string.Equals(item.id, copy.id, StringComparison.Ordinal));
                if (index < 0) entries.Add(copy);
                else entries[index] = copy;
            }
        }

        public void Clear() => entries.Clear();

        public static EvidenceRecord Copy(EvidenceRecord value)
        {
            return new EvidenceRecord
            {
                id = value.id, title = value.title, content = value.content, sourceId = value.sourceId,
                timestamp = value.timestamp, locationId = value.locationId, imagePath = value.imagePath, reliability = value.reliability
            };
        }
    }
}
