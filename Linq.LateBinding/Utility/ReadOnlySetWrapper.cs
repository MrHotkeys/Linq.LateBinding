using System;
using System.Collections;
using System.Collections.Generic;

namespace MrHotkeys.Linq.LateBinding.Utility
{
    public sealed class ReadOnlySetWrapper<T> : IReadOnlyCollection<T>, ICollection<T>
    {
        private ISet<T> Set { get; }

        public int Count => Set.Count;

        bool ICollection<T>.IsReadOnly => true;

        public ReadOnlySetWrapper(ISet<T> set)
        {
            Set = set ?? throw new ArgumentNullException(nameof(set));
        }

        public bool Contains(T item) =>
            Set.Contains(item);

        public void CopyTo(T[] array, int arrayIndex) =>
            (this as IEnumerable<T>).CopyTo(array, arrayIndex);

        public IEnumerator<T> GetEnumerator() =>
            Set.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() =>
            GetEnumerator();

        void ICollection<T>.Add(T item) =>
            throw new NotSupportedException();

        bool ICollection<T>.Remove(T item) =>
            throw new NotSupportedException();

        void ICollection<T>.Clear() =>
            throw new NotSupportedException();
    }
}