using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

using MrHotkeys.Linq.LateBinding.Utility;

namespace MrHotkeys.Linq.LateBinding.Dto
{
    public abstract class DtoDictionaryBase : IDictionary<string, object?>
    {
        private ReadOnlySetWrapper<string> _keys { get; }

        public IReadOnlyCollection<string> Keys => _keys;

        public ICollection<object?> Values =>
            (this as IEnumerable<KeyValuePair<string, object?>>)
            .Select(pair => pair.Value)
            .ToArray();

        public int Count => Keys.Count;

        public bool IsReadOnly => true;

        ICollection<string> IDictionary<string, object?>.Keys => _keys;

        bool ICollection<KeyValuePair<string, object?>>.IsReadOnly => true;

        protected DtoDictionaryBase(ReadOnlySetWrapper<string> keys)
        {
            _keys = keys ?? throw new ArgumentNullException(nameof(keys));
        }

        public abstract bool TryGetValue(string key, [NotNullWhen(true)] out object? value);

        public abstract bool TrySetValue(string key, object? value);

        public object? this[string key]
        {
            get
            {
                if (key is null)
                    throw new ArgumentNullException(nameof(key));
                if (!TryGetValue(key, out var value))
                    throw new KeyNotFoundException($"No member found for key \"{key}\"!");

                return value;
            }
            set
            {
                if (key is null)
                    throw new ArgumentNullException(nameof(key));
                if (!TrySetValue(key, value))
                    throw new KeyNotFoundException($"No member found for key \"{key}\"!");
            }
        }

        public bool ContainsKey(string key)
        {
            if (key is null)
                throw new ArgumentNullException(nameof(key));

            return Keys.Contains(key);
        }

        public bool Contains(KeyValuePair<string, object?> item) =>
            TryGetValue(item.Key, out var value) && value == item.Value;

        public void CopyTo(KeyValuePair<string, object?>[] array, int arrayIndex) =>
            (this as IEnumerable<KeyValuePair<string, object?>>).CopyTo(array, arrayIndex);

        public IEnumerator<KeyValuePair<string, object?>> GetEnumerator()
        {
            foreach (var key in Keys)
            {
                var value = this[key];
                yield return new KeyValuePair<string, object?>(key, value);
            }
        }

        IEnumerator IEnumerable.GetEnumerator() =>
            GetEnumerator();

        void IDictionary<string, object?>.Add(string key, object? value) =>
            throw new NotSupportedException();

        bool IDictionary<string, object?>.Remove(string key) =>
            throw new NotSupportedException();

        void ICollection<KeyValuePair<string, object?>>.Add(KeyValuePair<string, object?> item) =>
            throw new NotSupportedException();

        bool ICollection<KeyValuePair<string, object?>>.Remove(KeyValuePair<string, object?> item) =>
            throw new NotSupportedException();

        void ICollection<KeyValuePair<string, object?>>.Clear() =>
            throw new NotSupportedException();
    }
}