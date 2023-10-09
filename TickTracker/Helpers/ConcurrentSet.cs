using System;
using System.Linq;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace TickTracker.Helpers
{
    public class ConcurrentSet<T> : ISet<T> where T : notnull
    {
        private readonly ConcurrentDictionary<T, byte> dictionary = new();

        /// <summary>
        /// Returns an enumerator that iterates through the collection.
        /// </summary>
        public IEnumerator<T> GetEnumerator()
        {
            return dictionary.Keys.GetEnumerator();
        }

        /// <summary>
        /// Returns an enumerator that iterates through a collection.
        /// </summary>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        /// <summary>
        /// Removes the first occurrence of a specific object from the <see cref="ICollection"/>.
        /// </summary>
        /// <returns>
        /// <see langword="true"/> if <paramref name="item"/> was successfully removed from the <see cref="ICollection"/>; otherwise, <see langword="false"/>. This method also returns <see langword="false"/> if <paramref name="item"/> is not found in the original <see cref="ICollection"/>.
        /// </returns>
        /// <param name="item">the object to remove from the <see cref="ICollection"/>.</param><exception cref="NotSupportedException">the <see cref="ICollection"/> is read-only.</exception>
        public bool Remove(T item)
        {
            return TryRemoveInternal(item);
        }

        /// <summary>
        /// Gets the number of elements in the set.
        /// </summary>
        public int Count
        {
            get { return dictionary.Count; }
        }

        /// <summary>
        /// Gets a value indicating whether the <see cref="ICollection"/> is read-only.
        /// </summary>
        /// <returns>
        /// <see langword="true"/> if the <see cref="IsReadOnly"/>; otherwise, <see langword="false"/>.
        /// </returns>
        public bool IsReadOnly { get { return false; } }

        /// <summary>
        /// Gets a value that indicates if the set is empty.
        /// </summary>
        public bool IsEmpty
        {
            get { return dictionary.IsEmpty; }
        }

        public ICollection<T> values
        {
            get { return dictionary.Keys; }
        }

        /// <summary>
        /// Adds an item to the <see cref="ICollection"/>.
        /// </summary>
        /// <param name="item">the object to add to the <see cref="ICollection"/>.</param><exception cref="NotSupportedException">the <see cref="ICollection"/> is read-only.</exception>
        void ICollection<T>.Add(T item)
        {
            if (!Add(item))
                throw new ArgumentException("item already exists in set.",nameof(item));
        }

        /// <summary>
        /// Modifies the current set so that it contains all elements that are present in both the current set and in the specified collection.
        /// </summary>
        /// <param name="other">the collection to compare to the current set.</param><exception cref="ArgumentNullException"><paramref name="other"/> is <see langword="null"/>.</exception>
        public void UnionWith(IEnumerable<T> other)
        {
            foreach (var item in other)
                TryAddInternal(item);
        }

        /// <summary>
        /// Modifies the current set so that it contains only elements that are also in a specified collection.
        /// </summary>
        /// <param name="other">the collection to compare to the current set.</param><exception cref="ArgumentNullException"><paramref name="other"/> is null.</exception>
        public void IntersectWith(IEnumerable<T> other)
        {
            var enumerable = other as IList<T> ?? other.ToArray();
            foreach (var item in this)
            {
                if (!enumerable.Contains(item))
                    TryRemoveInternal(item);
            }
        }

        /// <summary>
        /// Removes all elements in the specified collection from the current set.
        /// </summary>
        /// <param name="other">the collection of items to remove from the set.</param><exception cref="ArgumentNullException"><paramref name="other"/> is null.</exception>
        public void ExceptWith(IEnumerable<T> other)
        {
            foreach (var item in other)
                TryRemoveInternal(item);
        }

        /// <summary>
        /// Modifies the current set so that it contains only elements that are present either in the current set or in the specified collection, but not both. 
        /// </summary>
        /// <param name="other">the collection to compare to the current set.</param><exception cref="ArgumentNullException"><paramref name="other"/> is null.</exception>
        public void SymmetricExceptWith(IEnumerable<T> other)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Determines whether a set is a subset of a specified collection.
        /// </summary>
        /// <returns>
        /// <see langword="true"/> if the current set is a subset of <paramref name="other"/>; otherwise, <see langword="false"/>.
        /// </returns>
        /// <param name="other">the collection to compare to the current set.</param><exception cref="ArgumentNullException"><paramref name="other"/> is null.</exception>
        public bool IsSubsetOf(IEnumerable<T> other)
        {
            var enumerable = other as IList<T> ?? other.ToArray();
            return this.AsParallel().All(enumerable.Contains);
        }

        /// <summary>
        /// Determines whether the current set is a superset of a specified collection.
        /// </summary>
        /// <returns>
        /// <see langword="true"/> if the current set is a superset of <paramref name="other"/>; otherwise, <see langword="false"/>.
        /// </returns>
        /// <param name="other">the collection to compare to the current set.</param><exception cref="ArgumentNullException"><paramref name="other"/> is null.</exception>
        public bool IsSupersetOf(IEnumerable<T> other)
        {
            return other.AsParallel().All(Contains);
        }

        /// <summary>
        /// Determines whether the current set is a correct superset of a specified collection.
        /// </summary>
        /// <returns>
        /// <see langword="true"/> if the <see cref="ISet{T}"/> object is a correct superset of <paramref name="other"/>; otherwise, <see langword="false"/>.
        /// </returns>
        /// <param name="other">the collection to compare to the current set. </param><exception cref="ArgumentNullException"><paramref name="other"/> is null.</exception>
        public bool IsProperSupersetOf(IEnumerable<T> other)
        {
            var enumerable = other as IList<T> ?? other.ToArray();
            return this.Count != enumerable.Count && IsSupersetOf(enumerable);
        }

        /// <summary>
        /// Determines whether the current set is a property (strict) subset of a specified collection.
        /// </summary>
        /// <returns>
        /// <see langword="true"/> if the current set is a correct subset of <paramref name="other"/>; otherwise, <see langword="false"/>.
        /// </returns>
        /// <param name="other">the collection to compare to the current set.</param><exception cref="ArgumentNullException"><paramref name="other"/> is null.</exception>
        public bool IsProperSubsetOf(IEnumerable<T> other)
        {
            var enumerable = other as IList<T> ?? other.ToArray();
            return Count != enumerable.Count && IsSubsetOf(enumerable);
        }

        /// <summary>
        /// Determines whether the current set overlaps with the specified collection.
        /// </summary>
        /// <returns>
        /// <see langword="true"/> if the current set and <paramref name="other"/> share at least one common element; otherwise, <see langword="false"/>.
        /// </returns>
        /// <param name="other">the collection to compare to the current set.</param><exception cref="ArgumentNullException"><paramref name="other"/> is null.</exception>
        public bool Overlaps(IEnumerable<T> other)
        {
            return other.AsParallel().Any(Contains);
        }

        /// <summary>
        /// Determines whether the current set and the specified collection contain the same elements.
        /// </summary>
        /// <returns>
        /// <see langword="true"/> if the current set is equal to <paramref name="other"/>; otherwise, <see langword="false"/>.
        /// </returns>
        /// <param name="other">the collection to compare to the current set.</param><exception cref="ArgumentNullException"><paramref name="other"/> is null.</exception>
        public bool SetEquals(IEnumerable<T> other)
        {
            var enumerable = other as IList<T> ?? other.ToArray();
            return Count == enumerable.Count && enumerable.AsParallel().All(Contains);
        }

        /// <summary>
        /// Adds an element to the current set and returns a value to indicate if the element was successfully added. 
        /// </summary>
        /// <returns>
        /// <see langword="true"/> if the element is added to the set; <see langword="false"/> if the element is already in the set.
        /// </returns>
        /// <param name="item">the element to add to the set.</param>
        public bool Add(T item)
        {
            return TryAddInternal(item);
        }

        public void Clear()
        {
            dictionary.Clear();
        }

        public bool Contains(T item)
        {
            return dictionary.ContainsKey(item);
        }

        /// <summary>
        /// Copies the elements of the <see cref="ICollection"/> to an <see cref="Array"/>, starting at a particular <see cref="Array"/> index.
        /// </summary>
        /// <param name="array">The one-dimensional <see cref="Array"/> that is the destination of the elements copied from <see cref="ICollection"/>. the <see cref="Array"/> must have zero-based indexing.</param><param name="arrayIndex">the zero-based index in <paramref name="array"/> at which copying begins.</param><exception cref="ArgumentNullException"><paramref name="array"/> is null.</exception><exception cref="ArgumentOutOfRangeException"><paramref name="arrayIndex"/> is less than 0.</exception><exception cref="ArgumentException"><paramref name="array"/> is multidimensional.-or-the number of elements in the source <see cref="ICollection"/> is greater than the available space from <paramref name="arrayIndex"/> to the end of the destination <paramref name="array"/>.-or-type <paramref name="t"/> cannot be cast automatically to the type of the destination <paramref name="array"/>.</exception>
        public void CopyTo(T[] array, int arrayIndex)
        {
            values.CopyTo(array, arrayIndex);
        }

        public T[] ToArray()
        {
            return dictionary.Keys.ToArray();
        }

        private bool TryAddInternal(T item)
        {
            return dictionary.TryAdd(item, default);
        }

        private bool TryRemoveInternal(T item)
        {
            return dictionary.TryRemove(item, out _);
        }
    }
}
