using System;
using System.Collections.Generic;

namespace MrHotkeys.Linq.LateBinding
{
    internal static class EnumerableExtensions
    {
        public static void CopyTo<T>(this IEnumerable<T> items, T[] array, int arrayIndex)
        {
            if (items is null)
                throw new ArgumentNullException(nameof(items));
            if (array is null)
                throw new ArgumentNullException(nameof(array));
            if (arrayIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(arrayIndex), arrayIndex, "Must be > 0!");

            var offset = 0;
            foreach (var pair in items)
            {
                var index = arrayIndex + offset;
                if (index >= array.Length)
                    throw new ArgumentException($"Given array does not contain enough room for all items starting from index {arrayIndex}!", nameof(array));

                array[arrayIndex + offset] = pair;
                offset++;
            }
        }
    }
}