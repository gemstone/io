//******************************************************************************************************
//  ISupportStreamSerialization.cs - Gbtc
//
//  Copyright © 2024, Grid Protection Alliance.  All Rights Reserved.
//
//  Licensed to the Grid Protection Alliance (GPA) under one or more contributor license agreements. See
//  the NOTICE file distributed with this work for additional information regarding copyright ownership.
//  The GPA licenses this file to you under the MIT License (MIT), the "License"; you may not use this
//  file except in compliance with the License. You may obtain a copy of the License at:
//
//      http://opensource.org/licenses/MIT
//
//  Unless agreed to in writing, the subject software distributed under the License is distributed on an
//  "AS-IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. Refer to the
//  License for the specific language governing permissions and limitations.
//
//  Code Modification History:
//  ----------------------------------------------------------------------------------------------------
//  08/12/2024 - J. Ritchie Carroll
//       Generated original version of source code.
//
//******************************************************************************************************
// ReSharper disable StaticMemberInGenericType
// ReSharper disable InconsistentNaming

using System;
using System.Collections;
using System.IO;
using System.Reflection;

namespace Gemstone.IO.Parsing;

/// <summary>
/// Specifies that an object supports serialization via static <see cref="Stream"/> operations
/// using object-typed <see cref="ReadFrom"/> and <see cref="WriteTo"/> methods.
/// </summary>
/// <remarks>
/// This interface exists to allow classes to properly define the needed method signatures for using
/// <see cref="StreamSerialization{T}"/> operations. However, as long as the <see cref="ReadFrom"/>
/// and <see cref="WriteTo"/> methods exist on a class with the proper signature, actual implementation
/// of this interface is optional.
/// </remarks>
public interface ISupportStreamSerialization
{
    internal const string NamespacePrefix = $"{nameof(Gemstone)}.{nameof(IO)}.{nameof(Parsing)}.{nameof(ISupportStreamSerialization)}";
    internal const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    internal const BindingFlags StaticFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    internal const string ReadFromMethod = nameof(ReadFrom);
    internal const string ReadFromMethodEII = $"{NamespacePrefix}.{ReadFromMethod}";             // Explicit ISupportStreamSerialization implementation
    internal const string ReadFromMethodEIOfTI = $"{NamespacePrefix}<{{0}}>.{ReadFromMethod}";   // Explicit ISupportStreamSerialization<T> implementation

    internal const string WriteToMethod = nameof(WriteTo);
    internal const string WriteToMethodEII = $"{NamespacePrefix}.{WriteToMethod}";               // Explicit ISupportStreamSerialization implementation
    internal const string WriteToMethodEIOfTI = $"{NamespacePrefix}<{{0}}>.{WriteToMethod}";     // Explicit ISupportStreamSerialization<T> implementation

    /// <summary>
    /// Gets flag that determines if type implementing <see cref="ISupportStreamSerialization"/> is a list-type and
    /// supports its own list serialization handling, i.e., if automated list count and items serialization should
    /// be skipped by <see cref="StreamSerialization{T}"/> operations.
    /// </summary>
    /// <remarks>
    /// More commonly, if a type is assignable from an <see cref="IList"/>, it would be its element type that would
    /// implement <see cref="ISupportStreamSerialization"/> and the list serialization would be handled automatically by
    /// <see cref="StreamSerialization{T}"/> operations. However, if a type is assignable from an <see cref="IList"/> and
    /// implements <see cref="ISupportStreamSerialization"/> directly, then setting this property to <c>true</c> allows
    /// the list type to override default behavior and handle its own list serialization using the <see cref="ReadFrom"/>
    /// and <see cref="WriteTo"/> methods.
    /// </remarks>
#if NET
    static virtual bool UseCustomListSerialization => false;
#else
    static bool UseCustomListSerialization => false;
#endif

    /// <summary>
    /// Deserializes an object from a <see cref="Stream"/>.
    /// </summary>
    /// <param name="stream">Source stream.</param>
    /// <returns>New deserialized instance.</returns>
#if NET
    static abstract object ReadFrom(Stream stream);
#else
    static object ReadFrom(Stream stream) => throw new NotImplementedException($"{nameof(ReadFrom)} undefined");
#endif

    /// <summary>
    /// Serializes an object to a <see cref="Stream"/>.
    /// </summary>
    /// <param name="stream">Target stream.</param>
    /// <param name="instance">Instance to serialize.</param>
#if NET
    static abstract void WriteTo(Stream stream, object instance);
#else
    static void WriteTo(Stream stream, object instance) => throw new NotImplementedException($"{nameof(WriteTo)} undefined");
#endif
}

/// <summary>
/// Specifies that an object supports serialization via static <see cref="Stream"/> operations
/// using strongly-typed <see cref="ReadFrom"/> and <see cref="WriteTo"/> methods.
/// </summary>
/// <typeparam name="T">Type that implements stream serialization.</typeparam>
/// <remarks>
/// This interface exists to allow classes to properly define the needed method signatures for using
/// <see cref="StreamSerialization{T}"/> operations. However, as long as the <see cref="ReadFrom"/>
/// and <see cref="WriteTo"/> methods exist on a class with the proper signature, actual implementation
/// of this interface is optional.
/// </remarks>
public interface ISupportStreamSerialization<T> : ISupportStreamSerialization
{
    /// <summary>
    /// Deserializes an instance of type <typeparamref name="T"/> from a <see cref="Stream"/>.
    /// </summary>
    /// <param name="stream">Source stream.</param>
    /// <returns>New deserialized instance.</returns>
#if NET
    new static abstract T ReadFrom(Stream stream);
#else
    new static T ReadFrom(Stream stream) => throw new NotImplementedException($"{nameof(ReadFrom)} undefined");
#endif

    /// <summary>
    /// Serializes an <paramref name="instance"/> of type <typeparamref name="T"/> to a <see cref="Stream"/>.
    /// </summary>
    /// <param name="stream">Target stream.</param>
    /// <param name="instance">Instance to serialize.</param>
#if NET
    static abstract void WriteTo(Stream stream, T instance);
#else
    static void WriteTo(Stream stream, T instance) => throw new NotImplementedException($"{nameof(WriteTo)} undefined");
#endif

#if NET
    private static MethodInfo? s_readFromMethod;
    private static MethodInfo? s_writeToMethod;

    static object ISupportStreamSerialization.ReadFrom(Stream stream)
    {
        s_readFromMethod ??= 
            typeof(T).GetMethod(ReadFromMethod, StaticFlags, null, [typeof(Stream)], null) ?? 
            typeof(T).GetMethod(string.Format(ReadFromMethodEIOfTI, typeof(T).FullName), StaticFlags, null, [typeof(Stream)], null) ?? 
            throw new NullReferenceException($"Failed to find '{ReadFromMethod}' implementation.");

        return s_readFromMethod.Invoke(null, [stream])!;
    }

    static void ISupportStreamSerialization.WriteTo(Stream stream, object instance)
    {
        s_writeToMethod ??= 
            typeof(T).GetMethod(WriteToMethod, StaticFlags, null, [typeof(Stream), typeof(T)], null) ??
            typeof(T).GetMethod(string.Format(WriteToMethodEIOfTI, typeof(T).FullName), StaticFlags, null, [typeof(Stream), typeof(T)], null) ?? 
            throw new NullReferenceException($"Failed to find '{WriteToMethod}' implementation.");

        s_writeToMethod.Invoke(null, [stream, instance]);
    }
#endif
}
