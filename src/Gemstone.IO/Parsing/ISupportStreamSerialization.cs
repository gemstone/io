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

#if !NET
using System;
#endif
using 
System.IO;

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
    /// <summary>
    /// Deserializes an instance of type <typeparamref name="T"/> from a <see cref="Stream"/>.
    /// </summary>
    /// <param name="stream">Source stream.</param>
    /// <returns>New deserialized instance.</returns>
#if NET
    static abstract object ReadFrom(Stream stream);
#else
    static object ReadFrom(Stream stream) => throw new NotImplementedException($"{nameof(ReadFrom)} undefined");
#endif

    /// <summary>
    /// Serializes an <paramref name="instance"/> of type <typeparamref name="T"/> to a <see cref="Stream"/>.
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
public interface ISupportStreamSerialization<T>
{
    /// <summary>
    /// Deserializes an instance of type <typeparamref name="T"/> from a <see cref="Stream"/>.
    /// </summary>
    /// <param name="stream">Source stream.</param>
    /// <returns>New deserialized instance.</returns>
#if NET
    static abstract T ReadFrom(Stream stream);
#else
    static T ReadFrom(Stream stream) => throw new NotImplementedException($"{nameof(ReadFrom)} undefined");
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
}
