//******************************************************************************************************
//  StreamSerialization.cs - Gbtc
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
// ReSharper disable InconsistentNaming
// ReSharper disable IdentifierTypo

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace Gemstone.IO.Parsing;

/// <summary>
/// Defines stream serialization operations for natives types or classes that expose <c>ReadFrom</c> and
/// <c>WriteTo</c> methods. Instance and static options available. Arrays and lists of types are also
/// supported, so long as base type is a native type or supports required serialization methods.
/// </summary>
/// <typeparam name="T">Target type for stream serialization.</typeparam>
/// <remarks>
/// <para>
/// Deserialization Method Implementation Options:<br/>
/// <c>ReadFrom</c> Instance signature: <c>void ReadFrom(Stream)</c><br/>
/// <c>ReadFrom</c> Static object-based signature: <c>static object ReadFrom(Stream)</c><br/>
/// <c>ReadFrom</c> Static strongly-typed signature: <c>static T ReadFrom(Stream)</c><br/>
/// Note that deserialization of type also supports a constructor that accepts a standalone
/// <see cref="Stream"/> parameter.
/// </para>
/// <para>
/// Serialization Method Implementation Options:<br/>
/// <c>WriteTo</c> Instance signature: <c>void WriteTo(Stream)</c><br/>
/// <c>WriteTo</c> Static object-based signature: <c>static void WriteTo(Stream, object)</c><br/>
/// <c>WriteTo</c> Static strongly-typed signature: <c>static void WriteTo(Stream, T)</c><br/>
/// </para>
/// <para>
/// Note that proper static method signatures can be defined for a class by implementing the
/// <see cref="ISupportStreamSerialization"/> or <see cref="ISupportStreamSerialization{T}"/>
/// interface, implicitly or explicitly.
/// </para>
/// </remarks>
public static class StreamSerialization<T>
{
    private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private const BindingFlags StaticFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    private const string NamespacePrefix = $"{nameof(Gemstone)}.{nameof(IO)}.{nameof(Parsing)}.{nameof(ISupportStreamSerialization)}";
    
    private const string ReadFromMethod = nameof(ISupportStreamSerialization.ReadFrom);
    private const string ReadFromMethodEII = $"{NamespacePrefix}.{ReadFromMethod}";             // Explicit ISupportStreamSerialization implementation
    private const string ReadFromMethodEIOfTI = $"{NamespacePrefix}<{{0}}>.{ReadFromMethod}";   // Explicit ISupportStreamSerialization<T> implementation
    
    private const string WriteToMethod = nameof(ISupportStreamSerialization.WriteTo);
    private const string WriteToMethodEII = $"{NamespacePrefix}.{WriteToMethod}";               // Explicit ISupportStreamSerialization implementation
    private const string WriteToMethodEIOfTI = $"{NamespacePrefix}<{{0}}>.{WriteToMethod}";     // Explicit ISupportStreamSerialization<T> implementation

    /// <summary>
    /// Gets read deserialization method for type <typeparamref name="T"/>.
    /// </summary>
    /// <param name="elementType">
    /// Provides a type representing the encompassed elements when type <typeparamref name="T"/> is a list or array type, and element type cannot otherwise
    /// be ascertained, e.g., when element type is an <see cref="object"/>.
    /// </param>
    /// <returns>Read deserialization method.</returns>
    public static Func<Stream, T>? GetReadMethod(Type? elementType = null)
    {
        Type type = typeof(T);

        if (!IsListType(type))
        {
            Func<Stream, object?>? readMethod = GetReadMethodForType(type);

            return stream =>
            {
                object? obj = readMethod?.Invoke(stream);
                return obj is null ? default! : (T)obj;
            };
        }

        elementType = elementType is null || elementType == typeof(object) ? GetListTypeElement(type) : elementType;
        Func<Stream, object?>? method = GetReadMethodForType(elementType);

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

    private static Func<Stream, object?>? GetReadMethodForType(Type type)
    {
        // Create from constructor with stream parameter
        ConstructorInfo? constructor = type.GetConstructor(InstanceFlags, null, [typeof(Stream)], null);

        if (constructor is not null)
        {
            ParameterExpression streamParam = Expression.Parameter(typeof(Stream), "stream");
            NewExpression newExpression = Expression.New(constructor, streamParam);
            LambdaExpression lambda = Expression.Lambda(typeof(Func<Stream, object>), newExpression, streamParam);

            return (Func<Stream, object>)lambda.Compile();
        }

        MethodInfo? method;

        // Check for parameterless constructor
        constructor = type.GetConstructor(InstanceFlags, null, Type.EmptyTypes, null);

        if (constructor is not null)
        {
            // Create from instance-based method with following signature:
            // void ReadFrom(Stream stream)
            method = type.GetMethod(ReadFromMethod, InstanceFlags, null, [typeof(Stream)], null);

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
        method = type.GetMethod(ReadFromMethod, StaticFlags, null, [typeof(Stream)], null) ??
                 type.GetMethod(ReadFromMethodEII, StaticFlags, null, [typeof(Stream)], null) ??
                 type.GetMethod(string.Format(ReadFromMethodEIOfTI, type.FullName), StaticFlags, null, [typeof(Stream)], null);

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

    /// <summary>
    /// Gets write serialization method for type <typeparamref name="T"/>.
    /// </summary>
    /// <param name="elementType">
    /// Provides a type representing the encompassed elements when type <typeparamref name="T"/> is a list or array type, and element type cannot otherwise
    /// be ascertained, e.g., when element type is an <see cref="object"/>.
    /// </param>
    /// <returns>Write serialization method.</returns>
    public static Action<Stream, T>? GetWriteMethod(Type? elementType = null)
    {
        Type type = typeof(T);

        if (!IsListType(type))
        {
            Action<Stream, object?>? writeMethod = GetWriteMethodForType(type);
            return (stream, obj) => writeMethod?.Invoke(stream, obj!);
        }

        elementType = elementType is null || elementType == typeof(object) ? GetListTypeElement(type) : elementType;
        Action<Stream, object>? method = GetWriteMethodForType(elementType);

        if (method is null)
            return null;

        // Handle list/array types
        return (stream, obj) =>
        {
            if (obj is not IList items)
                return;

            BinaryWriter writer = new(stream, Encoding.UTF8, true);
            writer.Write(items.Count);

            foreach (object item in items)
                method(stream, item);
        };
    }

    private static Action<Stream, object?>? GetWriteMethodForType(Type type)
    {
        // Create from instance-based method with following signature:
        // void WriteTo(Stream stream)
        MethodInfo? method = type.GetMethod(WriteToMethod, InstanceFlags, null, [typeof(Stream)], null);

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
        method = type.GetMethod(WriteToMethod, StaticFlags, null, [typeof(Stream), typeof(object)], null) ??
                 type.GetMethod(WriteToMethodEII, StaticFlags, null, [typeof(Stream), typeof(object)], null);

        if (method is not null)
        {
            Action<Stream, object?> action = (Action<Stream, object?>)Delegate.CreateDelegate(typeof(Action<Stream, object?>), method);
            return (stream, obj) => action(stream, obj);
        }

        // Create from static-based method with strongly-typed parameter with following signature:
        // static void WriteTo(Stream stream, T instance)
        method = type.GetMethod(WriteToMethod, StaticFlags, null, [typeof(Stream), type], null) ??
                 type.GetMethod(string.Format(WriteToMethodEIOfTI, type.FullName), StaticFlags, null, [typeof(Stream), type], null);

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
            LibraryEvents.OnSuppressedException(typeof(StreamSerialization<>), ex);
        }

        return TypeCode.Object;
    }

    private static bool IsListType(Type type)
    {
        return typeof(IList).IsAssignableFrom(type);
    }

    private static Type GetListTypeElement(Type type)
    {
        if (type.IsArray)
            return type.GetElementType()!;

        if (type.IsGenericType)
            return type.GetGenericArguments()[0];

        Type[] interfaces = type.GetInterfaces();

        // Check if the type implements any generic IList<T> -- just getting first
        Type? enumerableType = interfaces.FirstOrDefault(interfaceType =>
            interfaceType.IsGenericType && interfaceType.GetGenericTypeDefinition() == typeof(IList<>));

        return enumerableType?.GetGenericArguments().FirstOrDefault() ?? typeof(object);
    }
}
