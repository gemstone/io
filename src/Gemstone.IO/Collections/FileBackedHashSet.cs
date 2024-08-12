﻿//******************************************************************************************************
//  FileBackedHashSet.cs - Gbtc
//
//  Copyright © 2014, Grid Protection Alliance.  All Rights Reserved.
//
//  Licensed to the Grid Protection Alliance (GPA) under one or more contributor license agreements. See
//  the NOTICE file distributed with this work for additional information regarding copyright ownership.
//  The GPA licenses this file to you under the MIT License (MIT), the "License"; you may
//  not use this file except in compliance with the License. You may obtain a copy of the License at:
//
//      http://www.opensource.org/licenses/MIT
//
//  Unless agreed to in writing, the subject software distributed under the License is distributed on an
//  "AS-IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. Refer to the
//  License for the specific language governing permissions and limitations.
//
//  Code Modification History:
//  ----------------------------------------------------------------------------------------------------
//  12/02/2014 - Stephen C. Wills
//       Generated original version of source code.
//
//******************************************************************************************************

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Gemstone.GuidExtensions;

namespace Gemstone.IO.Collections;

/// <summary>
/// Represents a lookup table backed by a file, with very little memory overhead.
/// </summary>
/// <typeparam name="T">The type of the items in the lookup table.</typeparam>
public sealed class FileBackedHashSet<T> : FileBackedHashSet<T, object> where T : notnull
{
    /// <inheritdoc />
    public FileBackedHashSet() { }

    /// <inheritdoc />
    public FileBackedHashSet(string filePath) : 
        base(filePath) { }

    /// <inheritdoc />
    public FileBackedHashSet(IEqualityComparer<T>? comparer) :
        base(comparer) { }

    /// <inheritdoc />
    public FileBackedHashSet(IEnumerable<T> enumerable) :
        base(enumerable) { }

    /// <inheritdoc />
    public FileBackedHashSet(string filePath, IEqualityComparer<T>? comparer) :
        base(filePath, comparer) { }

    /// <inheritdoc />
    public FileBackedHashSet(string filePath, IEnumerable<T> enumerable) : 
        base(filePath, enumerable) { }

    /// <inheritdoc />
    public FileBackedHashSet(IEnumerable<T> enumerable, IEqualityComparer<T>? comparer) : 
        base(enumerable, comparer) { }

    /// <inheritdoc />
    public FileBackedHashSet(string filePath, IEnumerable<T> enumerable, IEqualityComparer<T>? comparer) :
        base(filePath, enumerable, comparer) { }
}

/// <summary>
/// Represents a lookup table backed by a file, with very little memory overhead.
/// </summary>
/// <typeparam name="T">The type of the items in the lookup table.</typeparam>
/// <typeparam name="TElem">The element type of <typeparamref name="T"/> when it is a <see cref="IList"/> type; otherwise, <see cref="object"/>.</typeparam>
public class FileBackedHashSet<T, TElem> : ISet<T>, IDisposable where T : notnull
{
    #region [ Members ]

    // Fields
    private readonly FileBackedLookupTable<T, object, TElem, object> m_lookupTable;

    #endregion

    #region [ Constructors ]

    /// <summary>
    /// Creates a new instance of the <see cref="FileBackedHashSet{T}"/> class.
    /// </summary>
    /// <exception cref="InvalidOperationException"><typeparamref name="T"/> cannot be serialized.</exception>
    /// <remarks>
    /// This constructor uses the default equality comparer for file backed lookup tables,
    /// which is not the same as the default equality comparer for <typeparamref name="T"/>
    /// objects. This is because the default implementation of <see cref="object.GetHashCode"/>
    /// does not provide guarantees about consistency across platforms, or even implementations
    /// of the CLR. Instead, the default equality comparer uses a byte-for-byte comparison to
    /// determine equality between keys and a CRC-32 for its hash code implementation. This
    /// means the performance of the hashing function is dependent on the performance of the
    /// serialization function.
    /// </remarks>
    public FileBackedHashSet()
        : this(Path.GetTempFileName(), (IEqualityComparer<T>?)default)
    {
    }

    /// <summary>
    /// Creates a new instance of the <see cref="FileBackedHashSet{T}"/> class.
    /// </summary>
    /// <param name="filePath">The path to the file used to store the lookup table.</param>
    /// <exception cref="ArgumentException"><paramref name="filePath"/> is a zero-length string, contains only white space, or contains one or more invalid characters as defined by <see cref="Path.GetInvalidPathChars"/>.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="filePath"/> is null.</exception>
    /// <exception cref="InvalidOperationException"><typeparamref name="T"/> cannot be serialized.</exception>
    /// <remarks>
    /// This constructor uses the default equality comparer for file backed lookup tables,
    /// which is not the same as the default equality comparer for <typeparamref name="T"/>
    /// objects. This is because the default implementation of <see cref="object.GetHashCode"/>
    /// does not provide guarantees about consistency across platforms, or even implementations
    /// of the CLR. Instead, the default equality comparer uses a byte-for-byte comparison to
    /// determine equality between keys and a CRC-32 for its hash code implementation. This
    /// means the performance of the hashing function is dependent on the performance of the
    /// serialization function.
    /// </remarks>
    public FileBackedHashSet(string filePath)
        : this(filePath, (IEqualityComparer<T>?)default)
    {
    }

    /// <summary>
    /// Creates a new instance of the <see cref="FileBackedHashSet{T}"/> class.
    /// </summary>
    /// <param name="comparer">The equality comparer used to compare items in the hash set.</param>
    /// <exception cref="InvalidOperationException"><typeparamref name="T"/> cannot be serialized.</exception>
    public FileBackedHashSet(IEqualityComparer<T>? comparer)
        : this(Path.GetTempFileName(), comparer)
    {
    }

    /// <summary>
    /// Creates a new instance of the <see cref="FileBackedHashSet{T}"/> class.
    /// </summary>
    /// <param name="enumerable">The enumerable whose elements are copied to this hash set.</param>
    /// <exception cref="ArgumentNullException"><paramref name="enumerable"/> is null.</exception>
    /// <exception cref="InvalidOperationException"><typeparamref name="T"/> cannot be serialized.</exception>
    /// <remarks>
    /// This constructor uses the default equality comparer for file backed lookup tables,
    /// which is not the same as the default equality comparer for <typeparamref name="T"/>
    /// objects. This is because the default implementation of <see cref="object.GetHashCode"/>
    /// does not provide guarantees about consistency across platforms, or even implementations
    /// of the CLR. Instead, the default equality comparer uses a byte-for-byte comparison to
    /// determine equality between keys and a CRC-32 for its hash code implementation. This
    /// means the performance of the hashing function is dependent on the performance of the
    /// serialization function.
    /// </remarks>
    public FileBackedHashSet(IEnumerable<T> enumerable)
        : this(enumerable, default!)
    {
    }

    /// <summary>
    /// Creates a new instance of the <see cref="FileBackedHashSet{T}"/> class.
    /// </summary>
    /// <param name="filePath">The path to the file used to store the lookup table.</param>
    /// <param name="comparer">The equality comparer used to compare items in the hash set.</param>
    /// <exception cref="ArgumentException"><paramref name="filePath"/> is a zero-length string, contains only white space, or contains one or more invalid characters as defined by <see cref="Path.GetInvalidPathChars"/>.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="filePath"/> is null.</exception>
    /// <exception cref="InvalidOperationException"><typeparamref name="T"/> cannot be serialized.</exception>
    public FileBackedHashSet(string filePath, IEqualityComparer<T>? comparer)
    {
        m_lookupTable = new FileBackedLookupTable<T, object, TElem, object>(LookupTableType.HashSet, filePath, comparer);
    }

    /// <summary>
    /// Creates a new instance of the <see cref="FileBackedHashSet{T}"/> class.
    /// </summary>
    /// <param name="filePath">The path to the file used to store the lookup table.</param>
    /// <param name="enumerable">The enumerable whose elements are copied to this hash set.</param>
    /// <exception cref="ArgumentException"><paramref name="filePath"/> is a zero-length string, contains only white space, or contains one or more invalid characters as defined by <see cref="Path.GetInvalidPathChars"/>.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="filePath"/> is null or <paramref name="enumerable"/> is null.</exception>
    /// <exception cref="InvalidOperationException"><typeparamref name="T"/> cannot be serialized.</exception>
    /// <remarks>
    /// This constructor uses the default equality comparer for file backed lookup tables,
    /// which is not the same as the default equality comparer for <typeparamref name="T"/>
    /// objects. This is because the default implementation of <see cref="object.GetHashCode"/>
    /// does not provide guarantees about consistency across platforms, or even implementations
    /// of the CLR. Instead, the default equality comparer uses a byte-for-byte comparison to
    /// determine equality between keys and a CRC-32 for its hash code implementation. This
    /// means the performance of the hashing function is dependent on the performance of the
    /// serialization function.
    /// </remarks>
    public FileBackedHashSet(string filePath, IEnumerable<T> enumerable)
        : this(filePath, enumerable, default!)
    {
    }

    /// <summary>
    /// Creates a new instance of the <see cref="FileBackedHashSet{T}"/> class.
    /// </summary>
    /// <param name="enumerable">The enumerable whose elements are copied to this hash set.</param>
    /// <param name="comparer">The equality comparer used to compare items in the hash set.</param>
    /// <exception cref="ArgumentNullException"><paramref name="enumerable"/> is null.</exception>
    /// <exception cref="InvalidOperationException"><typeparamref name="T"/> cannot be serialized.</exception>
    public FileBackedHashSet(IEnumerable<T> enumerable, IEqualityComparer<T>? comparer)
        : this(Path.GetTempFileName(), enumerable, comparer)
    {
    }

    /// <summary>
    /// Creates a new instance of the <see cref="FileBackedHashSet{T}"/> class.
    /// </summary>
    /// <param name="filePath">The path to the file used to store the lookup table.</param>
    /// <param name="enumerable">The enumerable whose elements are copied to this hash set.</param>
    /// <param name="comparer">The equality comparer used to compare items in the hash set.</param>
    /// <exception cref="ArgumentException"><paramref name="filePath"/> is a zero-length string, contains only white space, or contains one or more invalid characters as defined by <see cref="Path.GetInvalidPathChars"/>.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="filePath"/> is null or <paramref name="enumerable"/> is null.</exception>
    /// <exception cref="InvalidOperationException"><typeparamref name="T"/> cannot be serialized.</exception>
    public FileBackedHashSet(string filePath, IEnumerable<T> enumerable, IEqualityComparer<T>? comparer)
    {
        m_lookupTable = new FileBackedLookupTable<T, object, TElem, object>(LookupTableType.HashSet, filePath, comparer);

        foreach (T item in enumerable)
            Add(item);
    }

    #endregion

    #region [ Properties ]

    /// <summary>
    /// Gets or sets the path to the file backing this hash set.
    /// </summary>
    /// <exception cref="ArgumentException">FilePath is set and is a zero-length string, contains only white space, or contains one or more invalid characters as defined by <see cref="Path.GetInvalidPathChars"/>.</exception>
    /// <remarks>
    /// Changes to this property will cause the file to close if the file is already opened.
    /// Data will not be automatically written from the old file to the new file.
    /// </remarks>
    public string FilePath
    {
        get => m_lookupTable.FilePath;
        set => m_lookupTable.FilePath = value;
    }

    /// <summary>
    /// Gets the number of elements contained in the <see cref="FileBackedHashSet{T}"/>.
    /// </summary>
    /// <returns>
    /// The number of elements contained in the <see cref="FileBackedHashSet{T}"/>.
    /// </returns>
    public int Count => m_lookupTable.Count;

    /// <summary>
    /// Gets a value indicating whether the <see cref="FileBackedHashSet{T}"/> is read-only.
    /// </summary>
    /// <returns>
    /// true if the <see cref="FileBackedHashSet{T}"/> is read-only; otherwise, false.
    /// </returns>
    public bool IsReadOnly => m_lookupTable.IsReadOnly;

    /// <summary>
    /// Gets the default signature used by the <see cref="FileBackedHashSet{T}"/>
    /// if no user-defined signature is supplied.
    /// </summary>
    public byte[] DefaultSignature => new Guid(FileBackedLookupTable<T, object, TElem, object>.HashSetSignature).ToRfcBytes();

    /// <summary>
    /// Gets or sets the signature of the file backing the lookup table.
    /// </summary>
    /// <exception cref="ArgumentNullException">Attempt is made to set Signature to a null value.</exception>
    /// <exception cref="ArgumentException">Attempt is made to set Signature to a value larger than the maximum allowed size.</exception>
    /// <exception cref="NotSupportedException">Attempt is made to modify Signature of a read-only lookup table.</exception>
    public byte[] Signature
    {
        get => m_lookupTable.Signature;
        set => m_lookupTable.Signature = value;
    }

    /// <summary>
    /// Gets or sets the size of the cache used
    /// to store data from the file in memory.
    /// </summary>
    public long CacheSize
    {
        get => m_lookupTable.CacheSize;
        set => m_lookupTable.CacheSize = value;
    }

    /// <summary>
    /// Gets the number of operations that fragment the
    /// lookup table that have occurred since the last
    /// time the lookup table was compacted.
    /// </summary>
    /// <remarks>
    /// This value is not stored in the file and may therefore
    /// be inaccurate if the lookup table has not been compacted
    /// since the last time it was opened.
    /// </remarks>
    public int FragmentationCount => m_lookupTable.FragmentationCount;

    #endregion

    #region [ Methods ]

    /// <summary>
    /// Opens the file backing this hash set.
    /// </summary>
    /// <exception cref="InvalidOperationException">File is already open.</exception>
    public void Open() => m_lookupTable.Open();

    /// <summary>
    /// Opens the file backing this hash set in read-only mode.
    /// </summary>
    /// <exception cref="InvalidOperationException">File is already open.</exception>
    public void OpenRead() => m_lookupTable.OpenRead();

    /// <summary>
    /// Adds an element to the current set and returns a value to indicate if the element was successfully added. 
    /// </summary>
    /// <param name="item">The element to add to the set.</param>
    /// <returns>
    /// true if the element is added to the set; false if the element is already in the set.
    /// </returns>
    public bool Add(T item) => m_lookupTable.TryAdd(item, default!);

    /// <summary>
    /// Removes the first occurrence of a specific object from the <see cref="FileBackedHashSet{T}"/>.
    /// </summary>
    /// <param name="item">The object to remove from the <see cref="FileBackedHashSet{T}"/>.</param>
    /// <returns>
    /// true if <paramref name="item"/> was successfully removed from the <see cref="FileBackedHashSet{T}"/>; otherwise, false. This method also returns false if <paramref name="item"/> is not found in the original <see cref="FileBackedHashSet{T}"/>.
    /// </returns>
    /// <exception cref="NotSupportedException">The <see cref="FileBackedHashSet{T}"/> is read-only.</exception>
    public bool Remove(T item) => m_lookupTable.Remove(item);

    /// <summary>
    /// Removes all elements that match the conditions defined by the specified predicate from a <see cref="FileBackedHashSet{T}"/> collection.
    /// </summary>
    /// <param name="match">The <see cref="Predicate{T}"/> delegate that defines the conditions of the elements to remove.</param>
    /// <returns>The number of elements that were removed from the <see cref="FileBackedHashSet{T}"/> collection.</returns>
    /// <exception cref="ArgumentNullException">match is null</exception>
    /// <exception cref="NotSupportedException">The <see cref="FileBackedHashSet{T}"/> is read-only.</exception>
    public int RemoveWhere(Predicate<T> match)
    {
        if (match is null)
            throw new ArgumentNullException(nameof(match));

        int removedCount = 0;
        m_lookupTable.UnmarkAll();

        foreach (T item in this)
        {
            if (!match(item))
                continue;

            m_lookupTable.TryMark(item);
            removedCount++;
        }

        m_lookupTable.RemoveMarked();

        return removedCount;
    }

    /// <summary>
    /// Determines whether the <see cref="FileBackedHashSet{T}"/> contains a specific value.
    /// </summary>
    /// <param name="item">The object to locate in the <see cref="FileBackedHashSet{T}"/>.</param>
    /// <returns>
    /// true if <paramref name="item"/> is found in the <see cref="FileBackedHashSet{T}"/>; otherwise, false.
    /// </returns>
    public bool Contains(T item) => m_lookupTable.ContainsKey(item);

    /// <summary>
    /// Modifies the current set so that it contains all elements that are present in either the current set or the specified collection.
    /// </summary>
    /// <param name="other">The collection to compare to the current set.</param>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is null.</exception>
    /// <exception cref="NotSupportedException">The <see cref="FileBackedHashSet{T}"/> is read-only.</exception>
    public void UnionWith(IEnumerable<T> other)
    {
        if (other is null)
            throw new ArgumentNullException(nameof(other));

        foreach (T item in other)
            Add(item);
    }

    /// <summary>
    /// Modifies the current set so that it contains only elements that are also in a specified collection.
    /// </summary>
    /// <param name="other">The collection to compare to the current set.</param>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is null.</exception>
    /// <exception cref="NotSupportedException">The <see cref="FileBackedHashSet{T}"/> is read-only.</exception>
    public void IntersectWith(IEnumerable<T> other)
    {
        if (other is null)
            throw new ArgumentNullException(nameof(other));

        m_lookupTable.UnmarkAll();

        foreach (T item in other)
            m_lookupTable.TryMark(item);

        m_lookupTable.RemoveUnmarked();
    }

    /// <summary>
    /// Removes all elements in the specified collection from the current set.
    /// </summary>
    /// <param name="other">The collection of items to remove from the set.</param>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is null.</exception>
    /// <exception cref="NotSupportedException">The <see cref="FileBackedHashSet{T}"/> is read-only.</exception>
    public void ExceptWith(IEnumerable<T> other)
    {
        if (other is null)
            throw new ArgumentNullException(nameof(other));

        foreach (T item in other)
            m_lookupTable.Remove(item);
    }

    /// <summary>
    /// Modifies the current set so that it contains only elements that are present
    /// either in the current set or in the specified collection, but not both. 
    /// </summary>
    /// <param name="other">The collection to compare to the current set.</param>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is null.</exception>
    /// <exception cref="NotSupportedException">The <see cref="FileBackedHashSet{T}"/> is read-only.</exception>
    public void SymmetricExceptWith(IEnumerable<T> other)
    {
        if (other is null)
            throw new ArgumentNullException(nameof(other));

        m_lookupTable.UnmarkAll();

        List<T> list = other.Where(item => !m_lookupTable.TryMark(item)).ToList();

        m_lookupTable.RemoveMarked();

        foreach (T item in list)
            Add(item);
    }

    /// <summary>
    /// Determines whether a set is a subset of a specified collection.
    /// </summary>
    /// <param name="other">The collection to compare to the current set.</param>
    /// <returns>
    /// true if the current set is a subset of <paramref name="other"/>; otherwise, false.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is null.</exception>
    /// <exception cref="NotSupportedException">The <see cref="FileBackedHashSet{T}"/> is read-only.</exception>
    public bool IsSubsetOf(IEnumerable<T> other)
    {
        if (other is null)
            throw new ArgumentNullException(nameof(other));

        m_lookupTable.UnmarkAll();

        foreach (T item in other)
            m_lookupTable.TryMark(item);

        return m_lookupTable.AllMarked();
    }

    /// <summary>
    /// Determines whether the current set is a superset of a specified collection.
    /// </summary>
    /// <param name="other">The collection to compare to the current set.</param>
    /// <returns>
    /// true if the current set is a superset of <paramref name="other"/>; otherwise, false.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is null.</exception>
    public bool IsSupersetOf(IEnumerable<T> other)
    {
        if (other is null)
            throw new ArgumentNullException(nameof(other));

        return other.All(Contains);
    }

    /// <summary>
    /// Determines whether the current set is a proper (strict) superset of a specified collection.
    /// </summary>
    /// <param name="other">The collection to compare to the current set. </param>
    /// <returns>
    /// true if the current set is a proper superset of <paramref name="other"/>; otherwise, false.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is null.</exception>
    /// <exception cref="NotSupportedException">The <see cref="FileBackedHashSet{T}"/> is read-only.</exception>
    public bool IsProperSupersetOf(IEnumerable<T> other)
    {
        if (other is null)
            throw new ArgumentNullException(nameof(other));

        m_lookupTable.UnmarkAll();

        if (other.Any(item => !m_lookupTable.TryMark(item)))
            return false;

        return !m_lookupTable.AllMarked();
    }

    /// <summary>
    /// Determines whether the current set is a proper (strict) subset of a specified collection.
    /// </summary>
    /// <param name="other">The collection to compare to the current set.</param>
    /// <returns>
    /// true if the current set is a proper subset of <paramref name="other"/>; otherwise, false.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is null.</exception>
    /// <exception cref="NotSupportedException">The <see cref="FileBackedHashSet{T}"/> is read-only.</exception>
    public bool IsProperSubsetOf(IEnumerable<T> other)
    {
        if (other is null)
            throw new ArgumentNullException(nameof(other));
            
        bool canBeProperSubset = false;
        m_lookupTable.UnmarkAll();

        foreach (T item in other)
        {
            if (!m_lookupTable.TryMark(item))
                canBeProperSubset = true;
        }

        return canBeProperSubset && m_lookupTable.AllMarked();
    }

    /// <summary>
    /// Determines whether the current set overlaps with the specified collection.
    /// </summary>
    /// <param name="other">The collection to compare to the current set.</param>
    /// <returns>
    /// true if the current set and <paramref name="other"/> share at least one common element; otherwise, false.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is null.</exception>
    public bool Overlaps(IEnumerable<T> other)
    {
        if (other is null)
            throw new ArgumentNullException(nameof(other));

        return other.Any(Contains);
    }

    /// <summary>
    /// Determines whether the current set and the specified collection contain the same elements.
    /// </summary>
    /// <param name="other">The collection to compare to the current set.</param>
    /// <returns>
    /// true if the current set is equal to <paramref name="other"/>; otherwise, false.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is null.</exception>
    /// <exception cref="NotSupportedException">The <see cref="FileBackedHashSet{T}"/> is read-only.</exception>
    public bool SetEquals(IEnumerable<T> other)
    {
        if (other is null)
            throw new ArgumentNullException(nameof(other));

        m_lookupTable.UnmarkAll();

        return other.All(item => m_lookupTable.TryMark(item)) && m_lookupTable.AllMarked();
    }

    /// <summary>
    /// Removes all items from the <see cref="FileBackedHashSet{T}"/>.
    /// </summary>
    /// <exception cref="NotSupportedException">The <see cref="FileBackedHashSet{T}"/> is read-only.</exception>
    public void Clear() => m_lookupTable.Clear();

    /// <summary>
    /// Copies the elements of the <see cref="FileBackedHashSet{T}"/> to an <see cref="Array"/>, starting at a particular <see cref="Array"/> index.
    /// </summary>
    /// <param name="array">The one-dimensional <see cref="Array"/> that is the destination of the elements copied from <see cref="FileBackedHashSet{T}"/>. The <see cref="Array"/> must have zero-based indexing.</param>
    /// <param name="arrayIndex">The zero-based index in <paramref name="array"/> at which copying begins.</param>
    /// <exception cref="ArgumentNullException"><paramref name="array"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="arrayIndex"/> is less than 0.</exception>
    /// <exception cref="ArgumentException">The number of elements in the source <see cref="FileBackedHashSet{T}"/> is greater than the available space from <paramref name="arrayIndex"/> to the end of the destination <paramref name="array"/>.</exception>
    public void CopyTo(T[] array, int arrayIndex)
    {
        if (array is null)
            throw new ArgumentNullException(nameof(array));

        if (arrayIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(arrayIndex));

        if (m_lookupTable.Count > array.Length - arrayIndex)
            throw new ArgumentException("Not enough available space in array to copy elements from hash set");

        foreach (T item in this)
            array[arrayIndex++] = item;
    }

    /// <summary>
    /// Returns an enumerator that iterates through the collection.
    /// </summary>
    /// <returns>
    /// A <see cref="IEnumerator{T}"/> that can be used to iterate through the collection.
    /// </returns>
    /// <filterpriority>1</filterpriority>
    public IEnumerator<T> GetEnumerator() => m_lookupTable.GetKeys().GetEnumerator();

    /// <summary>
    /// Defragments the item section of the hash set,
    /// which gets fragmented after removing items.
    /// </summary>
    public void Compact() => m_lookupTable.Compact();

    /// <summary>
    /// Closes the file backing this hash set.
    /// </summary>
    public void Close() => m_lookupTable.Close();

    /// <summary>
    /// Releases all the resources used by the <see cref="FileBackedHashSet{T}"/> object.
    /// </summary>
    public void Dispose() => m_lookupTable.Dispose();

    /// <summary>
    /// Adds an item to the <see cref="FileBackedHashSet{T}"/>.
    /// </summary>
    /// <param name="item">The object to add to the <see cref="FileBackedHashSet{T}"/>.</param>
    /// <exception cref="NotSupportedException">The <see cref="FileBackedHashSet{T}"/> is read-only.</exception>
    // ReSharper disable once AssignNullToNotNullAttribute
    void ICollection<T>.Add(T item) => Add(item);

    /// <summary>
    /// Returns an enumerator that iterates through a collection.
    /// </summary>
    /// <returns>
    /// An <see cref="IEnumerator"/> object that can be used to iterate through the collection.
    /// </returns>
    /// <filterpriority>2</filterpriority>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    #endregion
}
