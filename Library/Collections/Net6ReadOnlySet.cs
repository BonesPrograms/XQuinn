using System.Collections;
using System.Collections.Generic;
using System;
#if NET6_0_OR_GREATER
namespace XQuinn.Collections
{

    //Because Net6 has an IReadOnlySet interface, and not a concrete class to actually wrap with.
    
    public class Net6ReadOnlySet<T> : IReadOnlySet<T>
    {
        private readonly HashSet<T> _source;

        public Net6ReadOnlySet(HashSet<T> source)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
        }

        // Expose the count safely
        public int Count => _source.Count;

        // Standard lookup methods
        public bool Contains(T item) => _source.Contains(item);
        public bool IsProperSubsetOf(IEnumerable<T> other) => _source.IsProperSubsetOf(other);
        public bool IsProperSupersetOf(IEnumerable<T> other) => _source.IsProperSupersetOf(other);
        public bool IsSubsetOf(IEnumerable<T> other) => _source.IsSubsetOf(other);
        public bool IsSupersetOf(IEnumerable<T> other) => _source.IsSupersetOf(other);
        public bool Overlaps(IEnumerable<T> other) => _source.Overlaps(other);
        public bool SetEquals(IEnumerable<T> other) => _source.SetEquals(other);

        // Boilerplate enumerators
        public IEnumerator<T> GetEnumerator() => _source.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
#endif