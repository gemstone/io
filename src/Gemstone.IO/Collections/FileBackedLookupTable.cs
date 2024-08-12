//******************************************************************************************************
//  FileBackedLookupTableBase.cs - Gbtc
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
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Gemstone.GuidExtensions;
using Gemstone.IO.Checksums;

// ReSharper disable StaticFieldInGenericType
// ReSharper disable UnusedMember.Local
// ReSharper disable InconsistentNaming

namespace Gemstone.IO.Collections;

internal enum LookupTableType
{
    /// <summary>
    /// Dictionary with keys and values in the item nodes.
    /// </summary>
    Dictionary,

    /// <summary>
    /// HashSet with lookup node markers for set operations.
    /// </summary>
    HashSet
}

internal sealed class FileBackedLookupTable<TKey, TValue> : IEnumerable<KeyValuePair<TKey, TValue>>, IDisposable where TKey : notnull
{
    #region [ Members ]

    // Nested Types

    private class HeaderNode
    {
        public const int SignatureSize = 16;
        public const int FixedSize = SignatureSize + 4 * sizeof(long);

        public HeaderNode(LookupTableType type)
        {
            string signature = type == LookupTableType.Dictionary ? DictionarySignature : HashSetSignature;
            Signature = Guid.Parse(signature).ToRfcBytes();
        }

        public readonly byte[] Signature;
        public long Count;
        public long Capacity;
        public long ItemSectionPointer;
        public long EndOfFilePointer;
    }

    private class JournalNode
    {
        public const int FixedSize = sizeof(int) + 3 * sizeof(long) + sizeof(int);

        public const int None = 0;
        public const int Set = 1;
        public const int Delete = 2;
        public const int GrowLookupSection = 3;
        public const int RebuildLookupTable = 4;
        public const int WriteItemNodePointers = 5;
        public const int Truncate = 6;
        public const int Clear = 7;

        public int Operation;
        public long LookupPointer;
        public long ItemPointer;
        public long Sync;
        public int Checksum;
    }

    private class LookupNode
    {
        public long ItemPointer;
        public int Marker;
    }

    private class ItemNode
    {
        // Size of only the fixed size portion of
        // the item node (not including key and value)
        public const int FixedSize = 2 * sizeof(long) + sizeof(int);

        public long LookupPointer;
        public long NextItemPointer;
        public int HashCode;
        public TKey Key = default!;
        public TValue Value = default!;
    }

    private class CRCStream : Stream
    {
        #region [ Members ]

        // Fields
        private readonly Crc32 m_checksum = new();

        #endregion

        #region [ Properties ]

        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public uint Value => m_checksum.Value;

        #endregion

        #region [ Methods ]

        public override void Flush() { }

        public void Reset() => m_checksum.Reset();

        public override void Write(byte[] buffer, int offset, int count) => m_checksum.Update(buffer, offset, count);

        #endregion

        #region [ Not Supported ]

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        #endregion
    }

    // The default equality comparer for strings does not guarantee
    // consistency across platforms or versions of the CLR. This comparer
    // uses CRC-32 for its hashing function to provide that guarantee.
    private class DefaultKeyComparer : IEqualityComparer<TKey>
    {
        public bool Equals(TKey? x, TKey? y)
        {
            using MemoryStream xStream = new();
            using MemoryStream yStream = new();

            s_writeKeyAction(xStream, x);
            s_writeKeyAction(yStream, y);

            if (xStream.Length != yStream.Length)
                return false;

            byte[] xBuffer = xStream.GetBuffer();
            byte[] yBuffer = yStream.GetBuffer();

            for (int i = 0; i < xStream.Length; i++)
            {
                if (xBuffer[i] != yBuffer[i])
                    return false;
            }

            return true;
        }

        public int GetHashCode(TKey obj)
        {
            using CRCStream crcStream = new();

            s_writeKeyAction(crcStream, obj);

            return unchecked((int)crcStream.Value);
        }

        public static DefaultKeyComparer Default { get; } = new();
    }

    private class KeysEnumerable : IEnumerable<TKey>
    {
        private readonly Func<IEnumerator<TKey>> m_getEnumeratorFunc;

        public KeysEnumerable(Func<IEnumerator<TKey>> getEnumeratorFunc) => m_getEnumeratorFunc = getEnumeratorFunc;

        public IEnumerator<TKey> GetEnumerator() => m_getEnumeratorFunc();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    // Constants

    /// <summary>
    /// Defines signature for dictionaries.
    /// </summary>
    public const string DictionarySignature = "3165E4F9-203B-4741-A186-EA34659A94B7";

    /// <summary>
    /// Defines signature for hash sets.
    /// </summary>
    public const string HashSetSignature = "6527713F-78AE-43DA-8E37-718AFED99927";

    private const double MaximumLoadFactor = 0.7D;
    private const int MaximumChainedEmptyNodes = 3;

    // Fields
    private HeaderNode m_headerNode;
    private JournalNode m_journalNode;

    private CachedFileStream? m_fileStream;
    private BinaryWriter? m_fileWriter;
    private BinaryReader? m_fileReader;

    private string m_filePath;
    private readonly IEqualityComparer<TKey> m_keyComparer;
    private readonly LookupTableType m_lookupTableType;

    #endregion

    #region [ Constructors ]

    /// <summary>
    /// Creates a new instance of the <see cref="FileBackedLookupTable{TKey, TValue}"/> class.
    /// </summary>
    /// <param name="lookupTableType">Type of the lookup table used to tweak the file format.</param>
    /// <exception cref="InvalidOperationException">Either <typeparamref name="TKey"/> or <typeparamref name="TValue"/> cannot be serialized.</exception>
    public FileBackedLookupTable(LookupTableType lookupTableType)
        : this(lookupTableType, (IEqualityComparer<TKey>?)default)
    {
    }

    /// <summary>
    /// Creates a new instance of the <see cref="FileBackedLookupTable{TKey, TValue}"/> class.
    /// </summary>
    /// <param name="lookupTableType">Type of the lookup table used to tweak the file format.</param>
    /// <param name="filePath">The path to the file used to store the lookup table.</param>
    /// <exception cref="ArgumentException"><paramref name="filePath"/> is a zero-length string, contains only white space, or contains one or more invalid characters as defined by <see cref="Path.GetInvalidPathChars"/>.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="filePath"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Either <typeparamref name="TKey"/> or <typeparamref name="TValue"/> cannot be serialized.</exception>
    public FileBackedLookupTable(LookupTableType lookupTableType, string filePath)
        : this(lookupTableType, filePath, null)
    {
    }

    /// <summary>
    /// Creates a new instance of the <see cref="FileBackedLookupTable{TKey, TValue}"/> class.
    /// </summary>
    /// <param name="lookupTableType">Type of the lookup table used to tweak the file format.</param>
    /// <param name="keyComparer">The equality comparer used to compare keys in the lookup table.</param>
    /// <exception cref="InvalidOperationException">Either <typeparamref name="TKey"/> or <typeparamref name="TValue"/> cannot be serialized.</exception>
    public FileBackedLookupTable(LookupTableType lookupTableType, IEqualityComparer<TKey>? keyComparer)
        : this(lookupTableType, Path.GetTempFileName(), keyComparer)
    {
    }

    /// <summary>
    /// Creates a new instance of the <see cref="FileBackedLookupTable{TKey, TValue}"/> class.
    /// </summary>
    /// <param name="lookupTableType">Type of the lookup table used to tweak the file format.</param>
    /// <param name="filePath">The path to the file used to store the lookup table.</param>
    /// <param name="keyComparer">The equality comparer used to compare keys in the lookup table.</param>
    /// <exception cref="ArgumentException"><paramref name="filePath"/> is a zero-length string, contains only white space, or contains one or more invalid characters as defined by <see cref="Path.GetInvalidPathChars"/>.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="filePath"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Either <typeparamref name="TKey"/> or <typeparamref name="TValue"/> cannot be serialized.</exception>
    public FileBackedLookupTable(LookupTableType lookupTableType, string filePath, IEqualityComparer<TKey>? keyComparer)
    {
        if (s_writeKeyAction is null || s_readKeyFunc is null)
            throw new InvalidOperationException("Unable to create FileBackedLookupTable with keys that are not serializable");

        if (lookupTableType == LookupTableType.Dictionary && (s_writeValueAction is null || s_readValueFunc is null))
            throw new InvalidOperationException("Unable to create FileBackedLookupTable with values that are not serializable");

        if (filePath is null)
            throw new ArgumentNullException(nameof(filePath));

        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("Path is zero-length or contains only whitespace", nameof(filePath));

        m_lookupTableType = lookupTableType;
        m_filePath = string.Empty;
        m_keyComparer = keyComparer ?? DefaultKeyComparer.Default;

        m_headerNode = new HeaderNode(lookupTableType);
        m_journalNode = new JournalNode();

        FilePath = filePath;
    }

    #endregion

    #region [ Properties ]

    /// <summary>
    /// Gets or sets the path to the file backing this lookup table.
    /// </summary>
    /// <exception cref="ArgumentException">FilePath contains one or more invalid characters as defined by <see cref="Path.GetInvalidPathChars"/>.</exception>
    /// <remarks>
    /// Changes to this property will cause the file to close if the file is already opened.
    /// Data will not be automatically written from the old file to the new file.
    /// </remarks>
    public string FilePath
    {
        get
        {
            return m_filePath;
        }
        set
        {
            if (m_filePath == value)
                return;

            if (!string.IsNullOrWhiteSpace(value))
            {
                char[] invalidPathChars = Path.GetInvalidPathChars();

                if (value.Any(invalidPathChars.Contains))
                    throw new ArgumentException($"Path contains one or more invalid characters: {value}", nameof(value));
            }

            Close();
            m_filePath = value;
        }
    }

    /// <summary>
    /// Gets the number of elements contained in the <see cref="FileBackedLookupTable{TKey, TValue}"/>.
    /// </summary>
    /// <returns>
    /// The number of elements contained in the <see cref="FileBackedLookupTable{TKey, TValue}"/>.
    /// </returns>
    public int Count
    {
        get
        {
            if (m_fileStream is null)
                OpenImplicit();

            return (int)m_headerNode.Count;
        }
    }

    /// <summary>
    /// Gets a value indicating whether the <see cref="FileBackedLookupTable{TKey, TValue}"/> is read-only.
    /// </summary>
    /// <returns>
    /// true if the <see cref="FileBackedLookupTable{TKey, TValue}"/> is read-only; otherwise, false.
    /// </returns>
    public bool IsReadOnly
    {
        get
        {
            if (m_fileStream is null)
                OpenImplicit();

            return m_fileWriter is null;
        }
    }

    /// <summary>
    /// Gets or sets the signature of the file backing the lookup table.
    /// </summary>
    /// <exception cref="ArgumentNullException">Attempt is made to set Signature to a null value.</exception>
    /// <exception cref="ArgumentException">Attempt is made to set Signature to a value larger than the maximum allowed size.</exception>
    /// <exception cref="NotSupportedException">Attempt is made to modify Signature of a read-only lookup table.</exception>
    public byte[] Signature
    {
        get
        {
            if (m_fileStream is null)
                OpenImplicit();

            byte[] signature = new byte[HeaderNode.SignatureSize];
            Buffer.BlockCopy(m_headerNode.Signature, 0, signature, 0, HeaderNode.SignatureSize);

            return signature;
        }
        set
        {
            if (value is null)
                throw new ArgumentNullException(nameof(value));

            if (value.Length > HeaderNode.SignatureSize)
                throw new ArgumentException($"Attempt was made to set signature to a value larger than the maximum signature size of {HeaderNode.SignatureSize}");

            FailIfReadOnly();

            Buffer.BlockCopy(value, 0, m_headerNode.Signature, 0, value.Length);
            Array.Clear(m_headerNode.Signature, value.Length, HeaderNode.SignatureSize - value.Length);
            Write(m_headerNode);
        }
    }

    /// <summary>
    /// Gets or sets the element with the specified key.
    /// </summary>
    /// <returns>
    /// The element with the specified key.
    /// </returns>
    /// <param name="key">The key of the element to get or set.</param>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> is null.</exception>
    /// <exception cref="KeyNotFoundException">The property is retrieved and <paramref name="key"/> is not found.</exception>
    /// <exception cref="NotSupportedException">The property is set and the <see cref="FileBackedLookupTable{TKey, TValue}"/> is read-only.</exception>
    public TValue this[TKey key]
    {
        get
        {
            if (key is null)
                throw new ArgumentNullException(nameof(key));

            if (!TryGetValue(key, out TValue value))
                throw new KeyNotFoundException($"Item with the given key ({key}) was not found in the lookup table: {m_filePath}");

            return value;
        }
        set
        {
            long count;

            if (key is null)
                throw new ArgumentNullException(nameof(key));

            FailIfReadOnly();

            Find(key, out long lookupPointer, out long itemPointer);

            if (itemPointer >= m_headerNode.ItemSectionPointer)
                count = m_headerNode.Count;
            else
                count = m_headerNode.Count + 1;

            if (count > m_headerNode.Capacity * MaximumLoadFactor)
            {
                Grow();
                Find(key, out lookupPointer, out itemPointer);
            }

            ItemNode itemNode = new()
            {
                LookupPointer = lookupPointer, 
                HashCode = m_keyComparer.GetHashCode(key), 
                Key = key,
                Value = value
            };

            FileStream.Seek(m_headerNode.EndOfFilePointer, SeekOrigin.Begin);
            Write(itemNode);
            Set(lookupPointer, m_headerNode.EndOfFilePointer, count);

            if (itemPointer >= m_headerNode.ItemSectionPointer)
                FragmentationCount++;
        }
    }

    /// <summary>
    /// Gets or sets the size of the cache used
    /// to store data from the file in memory.
    /// </summary>
    public long CacheSize
    {
        get
        {
            if (m_fileStream is null)
                OpenImplicit();

            return FileStream.CacheSize;
        }
        set
        {
            if (m_fileStream is null)
                OpenImplicit();

            FileStream.CacheSize = value;
        }
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
    public int FragmentationCount { get; private set; }

    private CachedFileStream FileStream => m_fileStream
                                           ?? throw new InvalidOperationException("Attempt was made to dereference null file stream.");

    private BinaryWriter FileWriter => m_fileWriter
                                       ?? throw new InvalidOperationException("Attempt was made to dereference null file writer.");

    private BinaryReader FileReader => m_fileReader
                                       ?? throw new InvalidOperationException("Attempt was made to dereference null file reader.");

    private int LookupNodeSize
    {
        get
        {
            const int FixedSize = sizeof(long);
            int variableSize = m_lookupTableType == LookupTableType.HashSet ? sizeof(int) : 0;

            return FixedSize + variableSize;
        }
    }

    #endregion

    #region [ Methods ]

    /// <summary>
    /// Opens the file backing this lookup table.
    /// </summary>
    /// <exception cref="InvalidOperationException">File is already open.</exception>
    public void Open()
    {
        if (m_fileStream is not null)
            throw new InvalidOperationException($"File is already open: {m_filePath}");

        m_headerNode = new HeaderNode(m_lookupTableType);
        m_journalNode = new JournalNode();

        if (string.IsNullOrWhiteSpace(m_filePath))
            m_filePath = Path.GetTempFileName();

        string? directory = Path.GetDirectoryName(m_filePath);

        // Attempt to create the directory if it doesn't already exist
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        // Open the file in read/write mode
        m_fileStream = new CachedFileStream(m_filePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        m_fileWriter = new BinaryWriter(m_fileStream);
        m_fileReader = new BinaryReader(m_fileStream);

        if (m_fileStream.Length > HeaderNode.FixedSize + JournalNode.FixedSize)
        {
            // Read the header node and the journal
            // node from the start of the file
            Read(m_headerNode);
            Read(m_journalNode);

            // If the journal node has an indicates that an operation was interrupted
            // the last time this file was open, replay that operation to return the
            // file to a consistent state
            switch (m_journalNode.Operation)
            {
                case JournalNode.Set:
                    Set(m_journalNode.LookupPointer, m_journalNode.ItemPointer, m_journalNode.Sync);
                    break;

                case JournalNode.Delete:
                    Delete(m_journalNode.LookupPointer, m_journalNode.Sync);
                    break;

                case JournalNode.GrowLookupSection:
                    GrowLookupSection(m_journalNode.LookupPointer, m_journalNode.ItemPointer, m_journalNode.Sync);
                    break;

                case JournalNode.RebuildLookupTable:
                    RebuildLookupTable(m_journalNode.LookupPointer);
                    break;

                case JournalNode.WriteItemNodePointers:
                    WriteItemNodePointers(m_journalNode.LookupPointer, m_journalNode.ItemPointer, m_journalNode.Sync);
                    break;

                case JournalNode.Truncate:
                    Truncate(m_journalNode.ItemPointer);
                    break;

                case JournalNode.Clear:
                    Clear();
                    break;
            }
        }
        else
        {
            // This will bring the lookup table to its initial
            // state, with an empty lookup table and zero items
            Clear();
        }
    }

    /// <summary>
    /// Opens the file backing this lookup table in read-only mode.
    /// </summary>
    /// <exception cref="InvalidOperationException">File is already open or the file has a pending transaction that could not be completed.</exception>
    /// <exception cref="FormatException">The format of the file is invalid.</exception>
    public void OpenRead()
    {
        void internalOpenRead()
        {
            if (m_fileStream is not null)
                throw new InvalidOperationException($"File is already open: {m_filePath}");

            m_fileStream = new CachedFileStream(m_filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            m_fileReader = new BinaryReader(m_fileStream);

            // Validate that the file format is valid
            if (m_fileStream.Length <= HeaderNode.FixedSize + JournalNode.FixedSize)
                throw new FormatException($"The format of the file is invalid: {m_filePath}");

            // Read the header node from the start of the file
            m_headerNode = new HeaderNode(m_lookupTableType);
            m_journalNode = new JournalNode();
            Read(m_headerNode);
            Read(m_journalNode);
        }

        internalOpenRead();

        try
        {
            // There may be undefined behavior when reading from a lookup table with a
            // pending transaction so we aggressively attempt to complete the transaction
            // and then reopen the file in read-only mode
            while (m_journalNode.Operation != JournalNode.None)
            {
                Close();
                Open();
                Close();
                internalOpenRead();
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Unable to complete the pending transaction in [{m_filePath}]: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Adds an element with the provided key and value to the <see cref="FileBackedLookupTable{TKey, TValue}"/>.
    /// </summary>
    /// <param name="key">The object to use as the key of the element to add.</param>
    /// <param name="value">The object to use as the value of the element to add.</param>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> is null.</exception>
    /// <exception cref="ArgumentException">An element with the same key already exists in the <see cref="FileBackedLookupTable{TKey, TValue}"/>.</exception>
    /// <exception cref="NotSupportedException">The <see cref="FileBackedLookupTable{TKey, TValue}"/> is read-only.</exception>
    public void Add(TKey key, TValue value)
    {
        if (key is null)
            throw new ArgumentNullException(nameof(key));

        FailIfReadOnly();

        Find(key, out long lookupPointer, out long itemPointer);

        if (itemPointer >= m_headerNode.ItemSectionPointer)
            throw new ArgumentException($"An element with the same key ({key}) already exists: {m_filePath}");

        if (m_headerNode.Count + 1L > m_headerNode.Capacity * MaximumLoadFactor)
        {
            Grow();
            Find(key, out lookupPointer, out _);
        }

        ItemNode itemNode = new()
        {
            LookupPointer = lookupPointer, 
            HashCode = m_keyComparer.GetHashCode(key), 
            Key = key, 
            Value = value
        };

        FileStream.Seek(m_headerNode.EndOfFilePointer, SeekOrigin.Begin);

        Write(itemNode);
        Set(lookupPointer, m_headerNode.EndOfFilePointer, m_headerNode.Count + 1);
    }

    /// <summary>
    /// Adds an element with the provided key and value to the <see cref="FileBackedLookupTable{TKey, TValue}"/>.
    /// </summary>
    /// <param name="key">The object to use as the key of the element to add.</param>
    /// <param name="value">The object to use as the value of the element to add.</param>
    /// <returns>True if the item was successfully added; false otherwise.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> is null.</exception>
    /// <exception cref="ArgumentException">An element with the same key already exists in the <see cref="FileBackedLookupTable{TKey, TValue}"/>.</exception>
    /// <exception cref="NotSupportedException">The <see cref="FileBackedLookupTable{TKey, TValue}"/> is read-only.</exception>
    public bool TryAdd(TKey? key, TValue value)
    {
        if (key is null || IsReadOnly)
            return false;

        Find(key, out long lookupPointer, out long itemPointer);

        if (itemPointer >= m_headerNode.ItemSectionPointer)
            return false;

        if (m_headerNode.Count + 1 > m_headerNode.Capacity * MaximumLoadFactor)
        {
            Grow();
            Find(key, out lookupPointer, out _);
        }

        ItemNode itemNode = new()
        {
            LookupPointer = lookupPointer, 
            HashCode = m_keyComparer.GetHashCode(key), 
            Key = key, 
            Value = value
        };

        FileStream.Seek(m_headerNode.EndOfFilePointer, SeekOrigin.Begin);
        Write(itemNode);
        Set(lookupPointer, m_headerNode.EndOfFilePointer, m_headerNode.Count + 1);

        return true;
    }

    /// <summary>
    /// Removes the element with the specified key from the <see cref="FileBackedLookupTable{TKey, TValue}"/>.
    /// </summary>
    /// <returns>
    /// true if the element is successfully removed; otherwise, false.
    /// This method also returns false if <paramref name="key"/> was not
    /// found in the original <see cref="FileBackedLookupTable{TKey, TValue}"/>.
    /// </returns>
    /// <param name="key">The key of the element to remove.</param>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> is null.</exception>
    /// <exception cref="NotSupportedException">The <see cref="FileBackedLookupTable{TKey, TValue}"/> is read-only.</exception>
    public bool Remove(TKey key)
    {
        if (key is null)
            throw new ArgumentNullException(nameof(key));

        FailIfReadOnly();

        Find(key, out long lookupPointer, out long itemPointer);

        if (itemPointer < m_headerNode.ItemSectionPointer)
            return false;

        Delete(lookupPointer, m_headerNode.Count - 1);
        FragmentationCount++;

        return true;
    }

    /// <summary>
    /// Attempts to find the lookup node for the corresponding
    /// key and, if found, marks the lookup node.
    /// </summary>
    /// <param name="key">The key used to find the lookup node to be marked.</param>
    /// <exception cref="NotSupportedException">The <see cref="FileBackedLookupTable{TKey, TValue}"/> is read-only.</exception>
    public bool TryMark(TKey key)
    {
        if (m_lookupTableType != LookupTableType.HashSet)
            return false;

        FailIfReadOnly();

        Find(key, out long lookupPointer, out long itemPointer);

        if (itemPointer < m_headerNode.ItemSectionPointer)
            return false;

        FileStream.Seek(lookupPointer + sizeof(long), SeekOrigin.Begin);
        FileWriter.Write(1);

        return true;
    }

    /// <summary>
    /// Determines whether all occupied lookup nodes are marked.
    /// </summary>
    /// <returns>True if all occupied lookup nodes are marked; false otherwise.</returns>
    /// <exception cref="NotSupportedException">The <see cref="FileBackedLookupTable{TKey, TValue}"/> is read-only.</exception>
    public bool AllMarked()
    {
        if (m_lookupTableType != LookupTableType.HashSet)
            return true;

        FailIfReadOnly();

        long lookupPointer = HeaderNode.FixedSize + JournalNode.FixedSize;
        LookupNode lookupNode = new();

        for (int i = 0; i < m_headerNode.Capacity; i++)
        {
            FileStream.Seek(lookupPointer, SeekOrigin.Begin);
            Read(lookupNode);

            if (lookupNode.ItemPointer >= m_headerNode.ItemSectionPointer && lookupNode.Marker == 0)
                return false;

            lookupPointer += LookupNodeSize;
        }

        return true;
    }

    /// <summary>
    /// Removes all unmarked nodes from the hash set.
    /// </summary>
    /// <exception cref="NotSupportedException">The <see cref="FileBackedLookupTable{TKey, TValue}"/> is read-only.</exception>
    public void RemoveMarked()
    {
        if (m_lookupTableType != LookupTableType.HashSet)
            return;

        FailIfReadOnly();

        long lookupPointer = HeaderNode.FixedSize + JournalNode.FixedSize;

        for (int i = 0; i < m_headerNode.Capacity; i++)
        {
            FileStream.Seek(lookupPointer + sizeof(long), SeekOrigin.Begin);
            int mark = FileReader.ReadInt32();

            if (mark != 0)
            {
                Delete(lookupPointer, m_headerNode.Count - 1);
                FragmentationCount++;
            }

            lookupPointer += LookupNodeSize;
        }
    }

    /// <summary>
    /// Removes all unmarked nodes from the hash set.
    /// </summary>
    /// <exception cref="NotSupportedException">The <see cref="FileBackedLookupTable{TKey, TValue}"/> is read-only.</exception>
    public void RemoveUnmarked()
    {
        if (m_lookupTableType != LookupTableType.HashSet)
            return;

        FailIfReadOnly();

        long lookupPointer = HeaderNode.FixedSize + JournalNode.FixedSize;
        LookupNode lookupNode = new();

        for (int i = 0; i < m_headerNode.Capacity; i++)
        {
            FileStream.Seek(lookupPointer, SeekOrigin.Begin);
            Read(lookupNode);

            if (lookupNode.ItemPointer >= m_headerNode.ItemSectionPointer && lookupNode.Marker == 0)
            {
                Delete(lookupPointer, m_headerNode.Count - 1);
                FragmentationCount++;
            }

            lookupPointer += LookupNodeSize;
        }
    }

    /// <summary>
    /// Unmarks all lookup nodes in the hash set.
    /// </summary>
    /// <exception cref="NotSupportedException">The <see cref="FileBackedLookupTable{TKey, TValue}"/> is read-only.</exception>
    public void UnmarkAll()
    {
        if (m_lookupTableType != LookupTableType.HashSet)
            return;

        FailIfReadOnly();

        long lookupPointer = HeaderNode.FixedSize + JournalNode.FixedSize;

        for (int i = 0; i < m_headerNode.Capacity; i++)
        {
            FileStream.Seek(lookupPointer + sizeof(long), SeekOrigin.Begin);
            FileWriter.Write(0);
            lookupPointer += LookupNodeSize;
        }
    }

    /// <summary>
    /// Determines whether the <see cref="FileBackedLookupTable{TKey, TValue}"/> contains an element with the specified key.
    /// </summary>
    /// <returns>
    /// true if the <see cref="FileBackedLookupTable{TKey, TValue}"/> contains an element with the key; otherwise, false.
    /// </returns>
    /// <param name="key">The key to locate in the <see cref="FileBackedLookupTable{TKey, TValue}"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> is null.</exception>
    public bool ContainsKey(TKey key)
    {
        if (key is null)
            throw new ArgumentNullException(nameof(key));

        if (m_fileStream is null)
            OpenImplicit();

        Find(key, out long _, out long itemPointer);

        return itemPointer >= m_headerNode.ItemSectionPointer;
    }

    /// <summary>
    /// Gets the value associated with the specified key.
    /// </summary>
    /// <returns>
    /// true if the object that implements <see cref="FileBackedLookupTable{TKey, TValue}"/> contains an element with the specified key; otherwise, false.
    /// </returns>
    /// <param name="key">The key whose value to get.</param>
    /// <param name="value">When this method returns, the value associated with the specified key, if the key is found; otherwise, the default value for the type of the <paramref name="value"/> parameter. This parameter is passed uninitialized.</param>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> is null.</exception>
    public bool TryGetValue(TKey key, out TValue value)
    {
        if (key is null)
            throw new ArgumentNullException(nameof(key));

        if (m_fileStream is null)
            OpenImplicit();

        Find(key, out _, out long itemPointer);

        if (itemPointer >= m_headerNode.ItemSectionPointer)
        {
            value = ReadValue();

            return true;
        }

        value = default!;

        return false;
    }

    /// <summary>
    /// Removes all items from the <see cref="FileBackedLookupTable{TKey, TValue}"/>.
    /// </summary>
    /// <exception cref="NotSupportedException">The <see cref="FileBackedLookupTable{TKey, TValue}"/> is read-only. </exception>
    public void Clear()
    {
        FailIfReadOnly();

        if (m_journalNode.Operation != JournalNode.Clear)
        {
            m_journalNode.Operation = JournalNode.Clear;
            Write(m_journalNode);
        }

        // Truncate the file to eliminate the lookup table and item section
        FileStream.SetLength(HeaderNode.FixedSize + JournalNode.FixedSize);

        // Create a new header node and write it to the start of the file
        m_headerNode.Count = 0L;
        m_headerNode.Capacity = 16L;
        m_headerNode.ItemSectionPointer = HeaderNode.FixedSize + JournalNode.FixedSize + LookupNodeSize * m_headerNode.Capacity;
        m_headerNode.EndOfFilePointer = m_headerNode.ItemSectionPointer;
        Write(m_headerNode);

        // Create the new lookup table section
        FileStream.SetLength(m_headerNode.ItemSectionPointer);

        // Clearing the lookup table defragments the file
        FragmentationCount = 0;

        ClearJournalNode();
    }

    /// <summary>
    /// Returns an enumerator that iterates through the collection.
    /// </summary>
    /// <returns>
    /// A <see cref="IEnumerator{T}"/> that can be used to iterate through the collection.
    /// </returns>
    /// <filterpriority>1</filterpriority>
    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
    {
        if (m_fileStream is null)
            OpenImplicit();

        ItemNode itemNode = new();
        long lookupPointer = HeaderNode.FixedSize + JournalNode.FixedSize;
        long count = 0L;

        while (count < m_headerNode.Count)
        {
            long itemPointer = ReadItemPointer(lookupPointer);

            if (itemPointer >= m_headerNode.ItemSectionPointer)
            {
                FileStream.Seek(itemPointer, SeekOrigin.Begin);
                Read(itemNode);

                yield return new KeyValuePair<TKey, TValue>(itemNode.Key, itemNode.Value);

                count++;
            }

            lookupPointer += LookupNodeSize;
        }
    }

    /// <summary>
    /// Gets an enumerable used to iterate only the keys in the lookup table.
    /// </summary>
    /// <returns>An enumerable used to iterate only the keys in the lookup table.</returns>
    public IEnumerable<TKey> GetKeys() => new KeysEnumerable(GetKeysEnumerator);

    /// <summary>
    /// Defragments the item section of the lookup table,
    /// which gets fragmented after removing keys or updating values.
    /// </summary>
    public void Compact()
    {
        FailIfReadOnly();

        if (m_headerNode.ItemSectionPointer == m_headerNode.EndOfFilePointer)
            return;

        // Initialize item1, lookup1, and item2 pointers
        long item1 = m_headerNode.ItemSectionPointer;
        long lookup1 = ReadLookupPointer(item1);
        long item2 = FileReader.ReadInt64();

        // Determine whether item1 is an orphaned node
        bool orphan1 = ReadItemPointer(lookup1) != item1;

        // Byte array will be initialized to the
        // right size and reused during compaction
        byte[]? bytes = default;

        while (item2 < m_headerNode.EndOfFilePointer)
        {
            // Update lookup2 and item3 pointers
            long lookup2 = ReadLookupPointer(item2);
            long item3 = FileReader.ReadInt64();

            // Determine whether item2 is an orphaned node
            bool orphan2 = ReadItemPointer(lookup2) != item2;

            if (!orphan1)
            {
                // Skip nodes that are not orphaned at
                // the beginning of the item section
                item1 = item2;
                lookup1 = lookup2;
                orphan1 = orphan2;
            }
            else
            {
                if (orphan2)
                {
                    // Combine the orphaned nodes
                    WriteItemNodePointers(lookup1, item1, item3);
                }
                else
                {
                    // Determine the length of the two nodes
                    int length1 = (int)(item2 - item1);
                    int length2 = (int)(item3 - item2);

                    // Allocate enough memory to move item2
                    if (bytes is null || bytes.Length < length2)
                        bytes = new byte[length2];

                    // Read item2 into memory
                    FileStream.Seek(item2, SeekOrigin.Begin);
                    FileStream.Read(bytes, 0, length2);

                    // Check to see if item1 is at least large enough
                    // to fully contain item2 plus an orphaned node
                    if (length2 + 2 * sizeof(long) < length1)
                    {
                        // Write data to the body of the orphaned node
                        FileStream.Seek(item1 + 2 * sizeof(long), SeekOrigin.Begin);
                        FileStream.Write(bytes, 2 * sizeof(long), length2 - 2 * sizeof(long));

                        // Position nextItemPointer of the soon
                        // to be orphaned node, pointing at item2
                        FileWriter.Write(lookup1);
                        FileWriter.Write(item2);

                        // Update the pointers in the orphaned item node,
                        // then point the lookup node to the orphaned node
                        WriteItemNodePointers(lookup2, item1, item1 + length2);
                        Set(lookup2, item1, m_headerNode.Count);

                        // The left over empty space and the node at item2
                        // have become orphaned nodes so combine them
                        WriteItemNodePointers(lookup1, item1 + length2, item3);

                        // Update item1 for the next iteration
                        item1 += length2;
                    }
                    else
                    {
                        // Write item2 beyond the end of the item section
                        FileStream.Seek(m_headerNode.EndOfFilePointer, SeekOrigin.Begin);
                        FileStream.Write(bytes, 0, length2);

                        // Fix the nextItemPointer
                        FileStream.Seek(m_headerNode.EndOfFilePointer + sizeof(long), SeekOrigin.Begin);
                        FileWriter.Write(m_headerNode.EndOfFilePointer + length2);

                        // Update the lookup node to point to the new location for item2
                        Set(lookup2, m_headerNode.EndOfFilePointer, m_headerNode.Count);

                        // Combine the orphaned item1 and the newly orphaned item2
                        WriteItemNodePointers(lookup1, item1, item3);
                    }
                }
            }

            // Update item2 for the next iteration
            item2 = item3;
        }

        if (orphan1)
            Truncate(item1);

        // Set fragmentation count to zero
        // once the file has been compacted
        FragmentationCount = 0;
    }

    /// <summary>
    /// Closes the file backing this lookup table.
    /// </summary>
    public void Close()
    {
        using (m_fileStream)
        using (m_fileWriter)
        using (m_fileReader)
        {
            m_fileStream = null;
            m_fileWriter = null;
            m_fileReader = null;
        }

        // Set fragmentation count to
        // zero when closing the file
        FragmentationCount = 0;
    }

    /// <summary>
    /// Releases all the resources used by the <see cref="FileBackedLookupTable{TKey, TValue}"/> object.
    /// </summary>
    public void Dispose() => Close();

    private void OpenImplicit()
    {
        // When accessing the lookup table without explicitly opening the file,
        // we cannot determine the level of access required by all operations
        // that will be performed against the lookup table so we default to write
        // access and fall back on read access if necessary
        try
        {
            Open();
        }
        catch (UnauthorizedAccessException)
        {
            OpenRead();
        }
    }

    private void Grow()
    {
        ItemNode itemNode = new();

        long newCapacity = 2 * m_headerNode.Capacity;
        long lookupTableSize = newCapacity * LookupNodeSize;
        long minimumItemSectionPointer = HeaderNode.FixedSize + JournalNode.FixedSize + lookupTableSize;

        long itemPointer = m_headerNode.ItemSectionPointer;
        long copyPointer = m_headerNode.EndOfFilePointer;

        // If the lookup table is growing beyond the end of the file,
        // save time by writing a nextItemPointer beyond the end of the file.
        // This will allow us to begin copying item nodes directly to the new
        // item section so that we don't have to move item nodes multiple times
        if (minimumItemSectionPointer > m_headerNode.EndOfFilePointer + sizeof(long) * 2)
        {
            FileStream.Seek(m_headerNode.EndOfFilePointer, SeekOrigin.Begin);
            FileWriter.Write(0L);
            FileWriter.Write(minimumItemSectionPointer);
            copyPointer = minimumItemSectionPointer;
        }

        // Begin copying item nodes to the end of the file
        // to make room for the lookup table to grow
        while (itemPointer < minimumItemSectionPointer)
        {
            // Read the two pointers at the start of the item node
            FileStream.Seek(itemPointer, SeekOrigin.Begin);
            itemNode.LookupPointer = FileReader.ReadInt64();
            itemNode.NextItemPointer = FileReader.ReadInt64();
            itemNode.HashCode = FileReader.ReadInt32();

            // If the lookup node referenced by this item node does not point back
            // to the item node, then this is an orphaned node and may be skipped
            if (ReadItemPointer(itemNode.LookupPointer) != itemPointer)
            {
                itemPointer = itemNode.NextItemPointer;

                continue;
            }

            // Jump back to the item node and read the key and value,
            // then update the item pointer to point to the next item after this one
            FileStream.Seek(itemPointer + ItemNode.FixedSize, SeekOrigin.Begin);
            itemNode.Key = ReadKey();
            itemNode.Value = ReadValue();
            itemPointer = itemNode.NextItemPointer;

            // Write the item node to the end of the file,
            // then update the copy pointer to point to the
            // next available location at the end of the file
            FileStream.Seek(copyPointer, SeekOrigin.Begin);
            Write(itemNode);
            copyPointer = FileStream.Position;
        }

        // Use the GrowLookupSection operation to point lookup nodes
        // to the copied item nodes and move the item section pointer
        GrowLookupSection(itemPointer, m_headerNode.EndOfFilePointer, copyPointer);

        // This will compact the item section, clear the existing
        // lookup table, rebuild the lookup table from the lookup
        // pointers in the item section, and increase the capacity
        // of the lookup tables
        RebuildLookupTable(newCapacity);
    }

    private long FindEndOfChain(int hashCode, long capacity)
    {
        long position = GetPosition(GetFirstHash(hashCode), capacity);
        long collisionOffset = GetCollisionOffset(hashCode);
        long lookupPointer = GetLookupPointer(position);

        while (ReadItemPointer(lookupPointer) >= m_headerNode.ItemSectionPointer)
        {
            position = GetPosition(position + collisionOffset, capacity);
            lookupPointer = GetLookupPointer(position);
        }

        return lookupPointer;
    }

    private void Find(TKey key, out long lookupPointer, out long itemPointer)
    {
        int hashCode = m_keyComparer.GetHashCode(key);
        long firstHash = GetFirstHash(hashCode);
        long collisionOffset = GetCollisionOffset(hashCode);
        long position = GetPosition(firstHash);
        long emptyPointer = 0L;
        int emptyNodes = 0;

        lookupPointer = GetLookupPointer(position);
        itemPointer = ReadItemPointer(lookupPointer);

        // Loop until the item pointer
        // is pointing to the header node
        while (itemPointer > 0L)
        {
            if (itemPointer >= m_headerNode.ItemSectionPointer)
            {
                // Determine if the item pointed to by
                // lookupPointer is the one we are trying to find
                FileStream.Seek(itemPointer + ItemNode.FixedSize, SeekOrigin.Begin);

                if (m_keyComparer.Equals(key, ReadKey()))
                    return;
            }
            else if (emptyPointer == 0L)
            {
                // Because the item pointer is not pointing to the
                // item section of the file, this is an empty slot
                // that may be used for a new item
                emptyPointer = lookupPointer;
                emptyNodes++;
            }
            else
            {
                // Track the number of empty nodes in the chain to
                // determine whether the lookup tables need to be rebuilt
                emptyNodes++;
            }

            // If the lookup node has never been occupied,
            // it is the end of the chain
            if (itemPointer == 0L)
                break;

            if (emptyNodes > MaximumChainedEmptyNodes)
            {
                // Rebuild the lookup table and begin searching
                // again from the beginning of the chain
                RebuildLookupTable(m_headerNode.Capacity);
                position = GetPosition(firstHash);
                emptyPointer = 0L;
                emptyNodes = 0;
            }
            else
            {
                // Update the position using the collision offset
                // to find the next lookup node in the chain
                position = GetPosition(position + collisionOffset);
            }

            lookupPointer = GetLookupPointer(position);
            itemPointer = ReadItemPointer(lookupPointer);
        }

        // If we made it here, it means that the item wasn't found in the lookup tables
        //
        // If the chain is not empty, an empty node should have been found in it;
        // otherwise, lookupPointer should already be pointing to the first node in the chain
        if (emptyPointer > 0L)
            lookupPointer = emptyPointer;

        // Set itemPointer to point to the header
        // node to indicate the item was not found
        itemPointer = 0L;
    }

    private void Set(long lookupPointer, long itemPointer, long count)
    {
        // Write the set operation to the journal node
        m_journalNode.Operation = JournalNode.Set;
        m_journalNode.LookupPointer = lookupPointer;
        m_journalNode.ItemPointer = itemPointer;
        m_journalNode.Sync = count;
        Write(m_journalNode);

        // Perform the set operation
        WriteItemPointer(lookupPointer, itemPointer);
        long nextItemPointer = ReadNextItemPointer(itemPointer);

        if (nextItemPointer > m_headerNode.EndOfFilePointer)
            m_headerNode.EndOfFilePointer = nextItemPointer;

        m_headerNode.Count = count;
        Write(m_headerNode);
        ClearJournalNode();
    }

    private void Delete(long lookupPointer, long count)
    {
        // Write the delete operation to the journal node
        m_journalNode.Operation = JournalNode.Delete;
        m_journalNode.LookupPointer = lookupPointer;
        m_journalNode.Sync = count;
        Write(m_journalNode);

        // Perform the delete operation
        WriteItemPointer(lookupPointer, 1L);
        m_headerNode.Count = count;
        Write(m_headerNode);
        ClearJournalNode();
    }

    private void GrowLookupSection(long itemSectionPointer, long endOfFilePointer, long newEndOfFilePointer)
    {
        if (m_journalNode.Operation != JournalNode.GrowLookupSection)
        {
            // Write the grow operation to the journal node
            m_journalNode.Operation = JournalNode.GrowLookupSection;
            m_journalNode.ItemPointer = itemSectionPointer;
            Write(m_journalNode);
        }

        // Perform the grow operation
        long itemPointer = endOfFilePointer;

        while (itemPointer < newEndOfFilePointer)
        {
            long lookupPointer = ReadLookupPointer(itemPointer);
            WriteItemPointer(lookupPointer, itemPointer);
            FileStream.Seek(itemPointer + sizeof(long), SeekOrigin.Begin);
            itemPointer = FileReader.ReadInt64();
        }

        m_headerNode.ItemSectionPointer = itemSectionPointer;
        m_headerNode.EndOfFilePointer = newEndOfFilePointer;
        Write(m_headerNode);
        ClearJournalNode();
    }

    private void RebuildLookupTable(long capacity)
    {
        if (m_journalNode.Operation != JournalNode.RebuildLookupTable)
        {
            // Item section needs to be compacted
            // before this operation can work
            Compact();

            // Write the grow operation to the journal node
            m_journalNode.Operation = JournalNode.RebuildLookupTable;
            m_journalNode.LookupPointer = capacity;
            Write(m_journalNode);
        }

        // Perform the grow operation
        LookupNode emptyNode = new();
        FileStream.Seek(HeaderNode.FixedSize + JournalNode.FixedSize, SeekOrigin.Begin);

        for (int i = 0; i < capacity; i++)
            Write(emptyNode);

        long itemPointer = m_headerNode.ItemSectionPointer;

        while (itemPointer < m_headerNode.EndOfFilePointer)
        {
            FileStream.Seek(itemPointer + sizeof(long), SeekOrigin.Begin);
            long nextItemPointer = FileReader.ReadInt64();
            int hashCode = FileReader.ReadInt32();

            long lookupPointer = FindEndOfChain(hashCode, capacity);
            WriteLookupPointer(lookupPointer, itemPointer);
            WriteItemPointer(lookupPointer, itemPointer);

            itemPointer = nextItemPointer;
        }

        m_headerNode.Capacity = capacity;
        Write(m_headerNode);
        ClearJournalNode();
    }

    private void WriteItemNodePointers(long lookupPointer, long itemPointer, long nextItemPointer)
    {
        // Write the grow operation to the journal node
        m_journalNode.Operation = JournalNode.WriteItemNodePointers;
        m_journalNode.LookupPointer = lookupPointer;
        m_journalNode.ItemPointer = itemPointer;
        m_journalNode.Sync = nextItemPointer;
        Write(m_journalNode);

        // Perform the write operation
        FileStream.Seek(itemPointer, SeekOrigin.Begin);
        FileWriter.Write(lookupPointer);
        FileWriter.Write(nextItemPointer);

        ClearJournalNode();
    }

    private void Truncate(long itemPointer)
    {
        // Write the grow operation to the journal node
        m_journalNode.Operation = JournalNode.Truncate;
        m_journalNode.ItemPointer = itemPointer;
        Write(m_journalNode);

        // Perform the truncate operation
        m_headerNode.EndOfFilePointer = itemPointer;
        Write(m_headerNode);
        FileStream.SetLength(itemPointer);

        ClearJournalNode();
    }

    private void ClearJournalNode()
    {
        m_journalNode.Operation = JournalNode.None;
        m_journalNode.LookupPointer = 0L;
        m_journalNode.ItemPointer = 0L;
        m_journalNode.Sync = 0;
        Write(m_journalNode);
    }

    private long GetPosition(long hashCode) => GetPosition(hashCode, m_headerNode.Capacity);

    private long GetPosition(long hashCode, long capacity) => hashCode % capacity;

    private long GetLookupPointer(long position) => HeaderNode.FixedSize + JournalNode.FixedSize + LookupNodeSize * position;

    private void Write(HeaderNode node)
    {
        FileStream.Seek(0, SeekOrigin.Begin);
        FileStream.Write(m_headerNode.Signature, 0, HeaderNode.SignatureSize);
        FileWriter.Write(node.Count);
        FileWriter.Write(node.Capacity);
        FileWriter.Write(node.ItemSectionPointer);
        FileWriter.Write(node.EndOfFilePointer);
    }

    private void Write(JournalNode node)
    {
        Crc32 checksum = new();

        checksum.Update(node.Operation);
        checksum.Update((int)(node.LookupPointer >> sizeof(int)));
        checksum.Update((int)node.LookupPointer);
        checksum.Update((int)(node.ItemPointer >> sizeof(int)));
        checksum.Update((int)node.ItemPointer);
        checksum.Update((int)(node.Sync >> sizeof(int)));
        checksum.Update((int)node.Sync);

        node.Checksum = (int)checksum.Value;

        FileStream.Flush();

        FileStream.Seek(HeaderNode.FixedSize, SeekOrigin.Begin);
        FileWriter.Write(node.Operation);
        FileWriter.Write(node.LookupPointer);
        FileWriter.Write(node.ItemPointer);
        FileWriter.Write(node.Sync);
        FileWriter.Write(node.Checksum);

        FileStream.Flush();
    }

    private void Write(LookupNode node)
    {
        FileWriter.Write(node.ItemPointer);

        if (m_lookupTableType == LookupTableType.HashSet)
            FileWriter.Write(node.Marker);
    }

    private void Write(ItemNode node)
    {
        long start = FileStream.Position;
        FileWriter.Write(node.LookupPointer);
        FileStream.Seek(sizeof(long), SeekOrigin.Current);
        FileWriter.Write(node.HashCode);
        s_writeKeyAction(FileStream, node.Key);

        if (m_lookupTableType == LookupTableType.Dictionary)
            s_writeValueAction(FileStream, node.Value);

        long end = FileStream.Position;
        FileStream.Seek(start + sizeof(long), SeekOrigin.Begin);
        FileWriter.Write(end);
        FileStream.Seek(end, SeekOrigin.Begin);
    }

    private void WriteItemPointer(long lookupPointer, long itemPointer)
    {
        FileStream.Seek(lookupPointer, SeekOrigin.Begin);
        FileWriter.Write(itemPointer);
    }

    private void WriteLookupPointer(long lookupPointer, long itemPointer)
    {
        FileStream.Seek(itemPointer, SeekOrigin.Begin);
        FileWriter.Write(lookupPointer);
    }

    private void Read(HeaderNode node)
    {
        FileStream.Read(node.Signature, 0, HeaderNode.SignatureSize);
        node.Count = FileReader.ReadInt64();
        node.Capacity = FileReader.ReadInt64();
        node.ItemSectionPointer = FileReader.ReadInt64();
        node.EndOfFilePointer = FileReader.ReadInt64();
    }

    private void Read(JournalNode node)
    {
        Crc32 checksum = new();

        node.Operation = FileReader.ReadInt32();
        node.LookupPointer = FileReader.ReadInt64();
        node.ItemPointer = FileReader.ReadInt64();
        node.Sync = FileReader.ReadInt64();
        node.Checksum = FileReader.ReadInt32();

        checksum.Update(node.Operation);
        checksum.Update((int)(node.LookupPointer >> sizeof(int)));
        checksum.Update((int)node.LookupPointer);
        checksum.Update((int)(node.ItemPointer >> sizeof(int)));
        checksum.Update((int)node.ItemPointer);
        checksum.Update((int)(node.Sync >> sizeof(int)));
        checksum.Update((int)node.Sync);

        if (node.Checksum != (int)checksum.Value)
        {
            node.Operation = JournalNode.None;
            node.Sync = 0;
            node.LookupPointer = 0L;
            node.ItemPointer = 0L;

            if (!IsReadOnly)
                Write(node);
        }
    }

    private void Read(LookupNode node)
    {
        node.ItemPointer = FileReader.ReadInt64();

        if (m_lookupTableType == LookupTableType.HashSet)
            node.Marker = FileReader.ReadInt32();
    }

    private void Read(ItemNode node)
    {
        node.LookupPointer = FileReader.ReadInt64();
        node.NextItemPointer = FileReader.ReadInt64();
        node.HashCode = FileReader.ReadInt32();
        node.Key = ReadKey();
        node.Value = ReadValue();
    }

    private long ReadItemPointer(long lookupPointer)
    {
        if (lookupPointer >= m_headerNode.ItemSectionPointer)
            return 0L;

        FileStream.Seek(lookupPointer, SeekOrigin.Begin);

        return FileReader.ReadInt64();
    }

    private long ReadLookupPointer(long itemPointer)
    {
        FileStream.Seek(itemPointer, SeekOrigin.Begin);
        return FileReader.ReadInt64();
    }

    private long ReadNextItemPointer(long itemPointer)
    {
        FileStream.Seek(itemPointer + sizeof(long), SeekOrigin.Begin);
        return FileReader.ReadInt64();
    }

    private TKey ReadKey() => s_readKeyFunc(FileStream);

    private TValue ReadValue() => m_lookupTableType == LookupTableType.Dictionary ? s_readValueFunc(FileStream) : default!;

    private IEnumerator<TKey> GetKeysEnumerator()
    {
        if (m_fileStream is null)
            OpenImplicit();

        long lookupPointer = HeaderNode.FixedSize + JournalNode.FixedSize;
        long count = 0L;

        while (count < m_headerNode.Count)
        {
            long itemPointer = ReadItemPointer(lookupPointer);

            if (itemPointer >= m_headerNode.ItemSectionPointer)
            {
                FileStream.Seek(itemPointer + ItemNode.FixedSize, SeekOrigin.Begin);
                yield return ReadKey();
                count++;
            }

            lookupPointer += LookupNodeSize;
        }
    }

    private void FailIfReadOnly()
    {
        if (IsReadOnly)
            throw new NotSupportedException($"Unable to modify read-only lookup table: {m_filePath}");
    }

    /// <summary>
    /// Returns an enumerator that iterates through a collection.
    /// </summary>
    /// <returns>
    /// An <see cref="IEnumerator"/> object that can be used to iterate through the collection.
    /// </returns>
    /// <filterpriority>2</filterpriority>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    #endregion

    #region [ Static ]

    // Static Fields
    private const BindingFlags s_instanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private const BindingFlags s_staticFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private static readonly Action<Stream, TKey?> s_writeKeyAction;
    private static readonly Action<Stream, TValue> s_writeValueAction;
    private static readonly Func<Stream, TKey> s_readKeyFunc;
    private static readonly Func<Stream, TValue> s_readValueFunc;

    // Static Constructor

    static FileBackedLookupTable()
    {
        Action<Stream, TKey>? writeKeyAction = GetWriteMethod<TKey>();
        Action<Stream, TValue>? writeValueAction = GetWriteMethod<TValue>();
        Func<Stream, TKey>? readKeyFunc = GetReadMethod<TKey>();
        Func<Stream, TValue>? readValueFunc = GetReadMethod<TValue>();

        writeKeyAction ??= (_, _) => throw new InvalidOperationException($"Type of TKey ({typeof(TKey).FullName}) is not write serializable.");
        readKeyFunc ??= _ => throw new InvalidOperationException($"Type of TKey ({typeof(TKey).FullName}) is not read serializable.");
        writeValueAction ??= (_, _) => throw new InvalidOperationException($"Type of TValue ({typeof(TValue).FullName}) is not write serializable.");
        readValueFunc ??= _ => throw new InvalidOperationException($"Type of TValue ({typeof(TValue).FullName}) is not read serializable.");

        s_writeKeyAction = writeKeyAction!;
        s_writeValueAction = writeValueAction;
        s_readKeyFunc = readKeyFunc;
        s_readValueFunc = readValueFunc;
    }

    // Static Methods

    private static Action<Stream, object?>? GetWriteMethod(Type type)
    {
        // Create from instance-based method with following signature:
        // void WriteTo(Stream stream)
        MethodInfo? method = type.GetMethod("WriteTo", s_instanceFlags, null, [typeof(Stream)], null);

        if (method is not null)
        {
            ParameterExpression streamParam = Expression.Parameter(typeof(Stream), "stream");
            ParameterExpression objParam = Expression.Parameter(typeof(object), "obj");
            UnaryExpression objCast = Expression.Convert(objParam, type);
            MethodCallExpression methodCall = Expression.Call(objCast, method, streamParam);

            Expression<Action<Stream, object?>> lambda = Expression.Lambda<Action<Stream, object?>>(
                methodCall,
                streamParam,
                objParam
            );

            return lambda.Compile();
        }

        // Create from static-based method with object-typed parameter with following signature:
        // static void WriteTo(Stream stream, object obj)
        method = type.GetMethod("WriteTo", s_staticFlags, null, [typeof(Stream), typeof(object)], null);

        if (method is not null)
        {
            Action<Stream, object?> action = (Action<Stream, object?>)Delegate.CreateDelegate(typeof(Action<Stream, object?>), method);
            return (stream, obj) => action(stream, obj);
        }

        // Create from static-based method with strongly-typed parameter with following signature:
        // static void WriteTo(Stream stream, T instance)
        method = type.GetMethod("WriteTo", s_staticFlags, null, [typeof(Stream), type], null);

        if (method is not null)
        {
            ParameterExpression streamParam = Expression.Parameter(typeof(Stream), "stream");
            ParameterExpression objParam = Expression.Parameter(typeof(object), "obj");
            UnaryExpression objCast = Expression.Convert(objParam, type);
            MethodCallExpression methodCall = Expression.Call(method, streamParam, objCast);

            Expression<Action<Stream, object?>> lambda = Expression.Lambda<Action<Stream, object?>>(
                methodCall,
                streamParam,
                objParam
            );

            return lambda.Compile();
        }

        // Handle native types
        Action<BinaryWriter, object?>? binaryWriterAction = GetBinaryWriterMethod(type);

        if (binaryWriterAction is null)
            return null;

        return (stream, obj) =>
        {
            using BinaryWriter writer = new(stream, Encoding.UTF8, true);
            binaryWriterAction(writer, obj);
        };
    }

    private static Action<Stream, T>? GetWriteMethod<T>()
    {
        Type type = typeof(T);

        if (!IsListType(type))
        {
            Action<Stream, object?>? writeMethod = GetWriteMethod(type);
            return (stream, obj) => writeMethod?.Invoke(stream, obj!);
        }

        Type elementType = GetListTypeElement(type);
        Action<Stream, object>? method = GetWriteMethod(elementType);

        if (method is null)
            return null;

        // Handle list/array types
        return (stream, source) =>
        {
            if (source is not IList items)
                return;

            BinaryWriter writer = new(stream, Encoding.UTF8, true);
            writer.Write(items.Count);

            foreach (object item in items)
                method(stream, item);
        };
    }

    private static Func<Stream, object?>? GetReadMethod(Type type)
    {
        // Create from constructor with stream parameter
        ConstructorInfo? constructor = type.GetConstructor(s_instanceFlags, null, [typeof(Stream)], null);

        if (constructor is not null)
        {
            ParameterExpression streamParam = Expression.Parameter(typeof(Stream), "stream");
            NewExpression newExpression = Expression.New(constructor, streamParam);
            LambdaExpression lambda = Expression.Lambda(typeof(Func<Stream, object>), newExpression, streamParam);
            
            return (Func<Stream, object>)lambda.Compile();
        }

        MethodInfo? method;

        // Check for parameterless constructor
        constructor = type.GetConstructor(s_instanceFlags, null, Type.EmptyTypes, null);

        if (constructor is not null)
        {
            // Create from instance-based method with following signature:
            // void ReadFrom(Stream stream)
            method = type.GetMethod("ReadFrom", s_instanceFlags, null, [typeof(Stream)], null);

            if (method is not null)
            {
                NewExpression newExpression = Expression.New(type);
                ParameterExpression streamParam = Expression.Parameter(typeof(Stream), "stream");
                ParameterExpression instanceVar = Expression.Variable(type, "instance");
                BinaryExpression assignInstance = Expression.Assign(instanceVar, newExpression);
                MethodCallExpression methodCall = Expression.Call(instanceVar, method, streamParam);

                Expression<Func<Stream, object>> lambda = Expression.Lambda<Func<Stream, object>>(
                    Expression.Block(
                        [instanceVar],  // Declare the variable in the block
                        assignInstance, // Assign the new instance
                        methodCall,     // Call the method on the instance
                        Expression.Convert(instanceVar, typeof(object))
                    ),
                    streamParam
                );

                return lambda.Compile();
            }
        }

        // See if a static "ReadFrom" method exists
        method = type.GetMethod("ReadFrom", s_staticFlags, null, [typeof(Stream)], null);

        if (method is not null)
        {
            // Create from static-based method with object return with following signature:
            // static object ReadFrom(Stream stream)
            if (method.ReturnType == typeof(object))
            {
                Func<Stream, object?> action = (Func<Stream, object?>)Delegate.CreateDelegate(typeof(Func<Stream, object?>), method);
                return stream => action(stream);
            }

            // Create from static-based method with strongly-typed return with following signature:
            // static T ReadFrom(Stream stream)
            if (method.ReturnType == type)
            {
                ParameterExpression streamParam = Expression.Parameter(typeof(Stream), "stream");
                MethodCallExpression methodCall = Expression.Call(method, streamParam);
                UnaryExpression convertToObject = Expression.Convert(methodCall, typeof(object));

                Expression<Func<Stream, object>> lambda = Expression.Lambda<Func<Stream, object>>(
                    convertToObject,
                    streamParam
                );

                return lambda.Compile();
            }
        }

        // Handle native types
        Func<BinaryReader, object?>? binaryReaderFunc = GetBinaryReaderMethod(type);

        if (binaryReaderFunc is not null)
        {
            return stream =>
            {
                using BinaryReader reader = new(stream, Encoding.UTF8, true);
                return binaryReaderFunc(reader);
            };
        }

        return null;
    }

    private static Func<Stream, T>? GetReadMethod<T>()
    {
        Type type = typeof(T);

        if (!IsListType(type))
        {
            Func<Stream, object?>? readMethod = GetReadMethod(type);

            return stream =>
            {
                object? obj = readMethod?.Invoke(stream);
                return obj is null ? default! : (T)obj;
            };
        }

        Type elementType = GetListTypeElement(type);
        Func<Stream, object?>? method = GetReadMethod(elementType);

        if (method is null)
            return null;

        // Handle list/array types
        return stream =>
        {
            BinaryReader reader = new(stream, Encoding.UTF8, true);
            int count = reader.ReadInt32();
            IList items;

            if (type.IsArray)
            {
                items = (IList)Activator.CreateInstance(elementType.MakeArrayType(), count)!;

                for (int i = 0; i < count; i++)
                    items[i] = method(stream);
            }
            else
            {
                items = (IList)Activator.CreateInstance(type)!;

                for (int i = 0; i < count; i++)
                    items.Add(method(stream));
            }

            return (T)items;
        };
    }

    private static Action<BinaryWriter, object?>? GetBinaryWriterMethod(Type type)
    {
        TypeCode typeCode = GetTypeCode(type);

        return typeCode switch
        {
            TypeCode.Boolean => (writer, obj) => writer.Write(Convert.ToBoolean(obj)),
            TypeCode.Byte => (writer, obj) => writer.Write(Convert.ToByte(obj)),
            TypeCode.Char => (writer, obj) => writer.Write(Convert.ToChar(obj)),
            TypeCode.DateTime => (writer, obj) => writeDateTime(writer, Convert.ToDateTime(obj)),
            TypeCode.Decimal => (writer, obj) => writer.Write(Convert.ToDecimal(obj)),
            TypeCode.Double => (writer, obj) => writer.Write(Convert.ToDouble(obj)),
            TypeCode.Int16 => (writer, obj) => writer.Write(Convert.ToInt16(obj)),
            TypeCode.Int32 => (writer, obj) => writer.Write(Convert.ToInt32(obj)),
            TypeCode.Int64 => (writer, obj) => writer.Write(Convert.ToInt64(obj)),
            TypeCode.SByte => (writer, obj) => writer.Write(Convert.ToSByte(obj)),
            TypeCode.Single => (writer, obj) => writer.Write(Convert.ToSingle(obj)),
            TypeCode.String => (writer, obj) => writeString(writer, Convert.ToString(obj), obj is null),
            TypeCode.UInt16 => (writer, obj) => writer.Write(Convert.ToUInt16(obj)),
            TypeCode.UInt32 => (writer, obj) => writer.Write(Convert.ToUInt32(obj)),
            TypeCode.UInt64 => (writer, obj) => writer.Write(Convert.ToUInt64(obj)),
            _ => null
        };

        void writeDateTime(BinaryWriter writer, DateTime dt)
        {
            writer.Write((byte)dt.Kind);
            writer.Write(dt.Ticks);
        }

        // Note that Convert.ToString returns empty string for null inputs, hence the following logic
        void writeString(BinaryWriter writer, string? str, bool isNull)
        {
            writer.Write(str ?? string.Empty);

            if (string.IsNullOrEmpty(str))
                writer.Write(isNull);
        }
    }

    private static Func<BinaryReader, object?>? GetBinaryReaderMethod(Type type)
    {
        TypeCode typeCode = GetTypeCode(type);
        TypeConverter converter = TypeDescriptor.GetConverter(type);

        return typeCode switch
        {
            TypeCode.Boolean => reader => getConvertedValue(reader.ReadBoolean()),
            TypeCode.Byte => reader => getConvertedValue(reader.ReadByte()),
            TypeCode.Char => reader => getConvertedValue(reader.ReadChar()),
            TypeCode.DateTime => reader => getConvertedValue(readDateTime(reader)),
            TypeCode.Decimal => reader => getConvertedValue(reader.ReadDecimal()),
            TypeCode.Double => reader => getConvertedValue(reader.ReadDouble()),
            TypeCode.Int16 => reader => getConvertedValue(reader.ReadInt16()),
            TypeCode.Int32 => reader => getConvertedValue(reader.ReadInt32()),
            TypeCode.Int64 => reader => getConvertedValue(reader.ReadInt64()),
            TypeCode.SByte => reader => getConvertedValue(reader.ReadSByte()),
            TypeCode.Single => reader => getConvertedValue(reader.ReadSingle()),
            TypeCode.String => reader => getConvertedValue(readString(reader), true),
            TypeCode.UInt16 => reader => getConvertedValue(reader.ReadUInt16()),
            TypeCode.UInt32 => reader => getConvertedValue(reader.ReadUInt32()),
            TypeCode.UInt64 => reader => getConvertedValue(reader.ReadUInt64()),
            _ => null
        };

        // Note that converter.ConvertFrom returns empty string for null string inputs, hence the following logic
        object? getConvertedValue<TReader>(TReader readValue, bool retainNull = false)
        {
            if (retainNull && readValue is null)
                return null;

            return converter.CanConvertFrom(typeof(TReader)) ? 
                converter.ConvertFrom(readValue!) : 
                readValue;
        }

        static DateTime readDateTime(BinaryReader reader)
        {
            DateTimeKind kind = (DateTimeKind)reader.ReadByte();
            return new DateTime(reader.ReadInt64(), kind);
        }

        static string? readString(BinaryReader reader)
        {
            string str = reader.ReadString();
            bool isNull = str == string.Empty && reader.ReadBoolean();
            return isNull ? null : str;
        }
    }

    private static TypeCode GetTypeCode(Type type)
    {
        TypeCode typeCode = Type.GetTypeCode(type);

        if (typeCode != TypeCode.Object)
            return typeCode;

        try
        {
            // IConvertible types will provide their own type code
            if (Activator.CreateInstance(type) is IConvertible convertible)
                return convertible.GetTypeCode();
        }
        catch (Exception ex)
        {
            LibraryEvents.OnSuppressedException(typeof(FileBackedLookupTable<,>), ex);
        }

        return TypeCode.Object;
    }

    private static bool IsListType(Type type)
    {
        return typeof(IList).IsAssignableFrom(type);
    }

    private static Type GetListTypeElement(Type type)
    {
        return type.IsArray ? type.GetElementType()! : 
            type.IsGenericType ? type.GetGenericArguments()[0] : typeof(object);
    }

    private static long GetFirstHash(int hashCode)
    {
        uint i = (uint)hashCode;
        long hash = 17L;

        while (i > 0)
        {
            hash = hash * 23L + (i & 0xF);
            i >>= 4;
        }

        return hash;
    }

    private static long GetCollisionOffset(int hashCode)
    {
        uint i = (uint)hashCode;
        long hash = 13L;

        while (i > 0)
        {
            hash = hash * 29L + (i & 0xF);
            i >>= 4;
        }

        return hash | 1L;
    }

    #endregion
}
