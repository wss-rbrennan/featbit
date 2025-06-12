using System;
using System.Collections;
using System.Collections.Generic;

namespace Infrastructure.Scaling.Types
{
    public class QueryCollection : IEnumerable<KeyValuePair<string, List<string>>>
    {
        private readonly Dictionary<string, List<string>> _query;

        public QueryCollection(string rawQuery)
        {
            _query = ParseQuery(rawQuery);
        }

        public string? this[string key] =>
            _query.TryGetValue(key, out var values) ? values[0] : null;

        public List<string>? GetAll(string key) =>
            _query.TryGetValue(key, out var values) ? values : null;

        public bool ContainsKey(string key) => _query.ContainsKey(key);

        public int Count => _query.Count;

        public IEnumerable<string> Keys => _query.Keys;

        private static Dictionary<string, List<string>> ParseQuery(string query)
        {
            var result = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            if (string.IsNullOrWhiteSpace(query))
                return result;

            if (query.StartsWith("?"))
                query = query.Substring(1);

            var pairs = query.Split('&', StringSplitOptions.RemoveEmptyEntries);

            foreach (var pair in pairs)
            {
                var kv = pair.Split('=', 2);
                var key = Uri.UnescapeDataString(kv[0]);
                var value = kv.Length > 1 ? Uri.UnescapeDataString(kv[1]) : "";

                if (!result.TryGetValue(key, out var list))
                    result[key] = list = new List<string>();

                list.Add(value);
            }

            return result;
        }

        public IEnumerator<KeyValuePair<string, List<string>>> GetEnumerator() => _query.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => _query.GetEnumerator();
    }
}