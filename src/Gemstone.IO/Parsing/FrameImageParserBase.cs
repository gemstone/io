﻿//******************************************************************************************************
//  FrameImageParserBase.cs - Gbtc
//
//  Copyright © 2012, Grid Protection Alliance.  All Rights Reserved.
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
//  03/28/2007 - Pinal C. Patel
//       Original version of source code generated.
//  11/20/2008 - J. Ritchie Carroll
//       Adapted for more generalized use via the following related base classes:
//          BinaryImageParserBase => FrameImageParserBase => MultiSourceFrameImageParserBase.
//  09/14/2009 - Stephen C. Wills
//       Added new header and license agreement.
//  11/23/2011 - J. Ritchie Carroll
//       Modified to support buffer optimized ISupportBinaryImage.
//  11/06/2012 - J. Ritchie Carroll
//       Modified to support queued publication processing.
//  12/14/2012 - Starlynn Danyelle Gilliam
//       Modified Header.
//
//******************************************************************************************************

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using System.Text;
using Gemstone.ArrayExtensions;
using Gemstone.Threading.Collections;
using Gemstone.TypeExtensions;

namespace Gemstone.IO.Parsing
{
    /// <summary>
    /// This class defines a basic implementation of parsing functionality suitable for automating the parsing of
    /// a binary data stream represented as frames with common headers and returning the parsed data via an event.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This parser is designed as a write-only stream such that data can come from any source.
    /// </para>
    /// <para>
    /// This class is more specific than the <see cref="BinaryImageParserBase"/> in that it can automate the parsing of
    /// a particular protocol that is formatted as a series of frames that have a common method of identification.
    /// Automation of type creation occurs by loading implementations of common types that implement the
    /// <see cref="ISupportFrameImage{TTypeIdentifier}"/> interface. The common method of identification is handled by
    /// creating a class derived from the <see cref="ICommonHeader{TTypeIdentifier}"/> which primarily includes a
    /// TypeID property, but also should include any state information needed to parse a particular frame if
    /// necessary. Derived classes simply override the <see cref="ParseCommonHeader"/> function in order to parse
    /// the <see cref="ICommonHeader{TTypeIdentifier}"/> from a provided binary image.
    /// </para>
    /// </remarks>
    /// <typeparam name="TTypeIdentifier">Type of identifier used to distinguish output types.</typeparam>
    /// <typeparam name="TOutputType">Type of the interface or class used to represent outputs.</typeparam>
    public abstract class FrameImageParserBase<TTypeIdentifier, TOutputType> : BinaryImageParserBase, IFrameImageParser<TTypeIdentifier, TOutputType> where TOutputType : ISupportFrameImage<TTypeIdentifier>
    {
        #region [ Members ]

        // Nested Types

        // Container for Type information.
        private class TypeInfo
        {
            public readonly Type RuntimeType;
            public readonly bool SupportsLifecycle;
            public TTypeIdentifier TypeID = default!;

            public TypeInfo(Type runtimeType)
            {
                RuntimeType = runtimeType;

                // If class implementation supports life cycle, automatically dispose of objects when we are done with them
                SupportsLifecycle = runtimeType.GetInterface("Gemstone.ISupportLifecycle") != null;
            }

            public TOutputType CreateNew() => (TOutputType)Activator.CreateInstance(RuntimeType);
        }

        // Events

        /// <summary>
        /// Occurs when a data image is deserialized successfully to one of the output types that the data
        /// image represents.
        /// </summary>
        /// <remarks>
        /// <see cref="EventArgs{T}.Argument"/> is the object that was deserialized from the binary image.
        /// </remarks>
        public event EventHandler<EventArgs<TOutputType>>? DataParsed;

        /// <summary>
        /// Occurs when matching an output type for deserializing the data image could not be found.
        /// </summary>
        /// <remarks>
        /// <see cref="EventArgs{T}.Argument"/> is the ID of the output type that could not be found.
        /// </remarks>
        public event EventHandler<EventArgs<TTypeIdentifier>>? OutputTypeNotFound;

        /// <summary>
        /// Occurs when more than one type has been defined that can deserialize the specified output type.
        /// </summary>
        /// <remarks>
        /// <see cref="EventArgs{T1,T2}.Argument1"/> is the <see cref="Type"/> that defines a type ID that has already been defined.<br/>
        /// <see cref="EventArgs{T1,T2}.Argument2"/> is the ID of the output type that was defined more than once.
        /// </remarks>
        public event EventHandler<EventArgs<Type, TTypeIdentifier>>? DuplicateTypeHandlerEncountered;

        // Fields
        private readonly Dictionary<TTypeIdentifier, TypeInfo> m_outputTypes;
        private readonly AsyncQueue<EventArgs<TOutputType>> m_outputQueue;
        private bool m_disposed;

        #endregion

        #region [ Constructors ]

        /// <summary>
        /// Creates a new instance of the <see cref="FrameImageParserBase{TTypeIdentifier,TOutputType}"/> class.
        /// </summary>
        protected FrameImageParserBase()
        {
            m_outputTypes = new Dictionary<TTypeIdentifier, TypeInfo>();
            m_outputQueue = new AsyncQueue<EventArgs<TOutputType>>
            {
                ProcessItemFunction = PublishParsedOutput
            };

            m_outputQueue.ProcessException += m_parsedOutputQueue_ProcessException;
        }

        #endregion

        #region [ Properties ]

        /// <summary>
        /// Gets the total number of parsed outputs that are currently queued for publication, if any.
        /// </summary>
        public virtual int QueuedOutputs => m_outputQueue?.Count ?? 0;

        /// <summary>
        /// Gets or sets a boolean value that indicates whether the frame image parser is currently enabled.
        /// </summary>
        public override bool Enabled
        {
            get => base.Enabled;
            set
            {
                base.Enabled = value;
                m_outputQueue.Enabled = value;
            }
        }

        /// <summary>
        /// Gets current status of <see cref="FrameImageParserBase{TTypeIdentifier,TOutputType}"/>.
        /// </summary>
        [Browsable(false)]
        public override string Status
        {
            get
            {
                StringBuilder status = new StringBuilder();

                status.Append(base.Status);
                status.AppendFormat("Total defined output types: {0}", m_outputTypes.Count);
                status.AppendLine();
                status.AppendFormat(" Parsed outputs to publish: {0}", QueuedOutputs);
                status.AppendLine();

                return status.ToString();
            }
        }

        #endregion

        #region [ Methods ]

        /// <summary>
        /// Releases the unmanaged resources used by the <see cref="FrameImageParserBase{TTypeIdentifier,TOutputType}"/> object and optionally releases the managed resources.
        /// </summary>
        /// <param name="disposing">true to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
        protected override void Dispose(bool disposing)
        {
            if (m_disposed)
                return;

            try
            {
                if (!disposing)
                    return;

                m_outputTypes.Clear();
            }
            finally
            {
                m_disposed = true;          // Prevent duplicate dispose.
                base.Dispose(disposing);    // Call base class Dispose().
            }
        }

        /// <summary>
        /// Start the data parser.
        /// </summary>
        /// <remarks>
        /// This overload loads public types from assemblies in the application binaries directory that implement the parser's output type.
        /// </remarks>
        public override void Start() => Start(typeof(TOutputType).LoadImplementations());

        /// <summary>
        /// Starts the data parser given the specified type implementations.
        /// </summary>
        /// <param name="implementations">Output type implementations to establish for the parser.</param>
        public virtual void Start(IEnumerable<Type> implementations)
        {
            // Call base class start method
            base.Start();

            List<TypeInfo> outputTypes = new List<TypeInfo>();  // Temporarily hold output types until their IDs are determined.

            foreach (Type type in implementations)
            {
                // See if a parameterless constructor is available for this type
                ConstructorInfo typeCtor = type.GetConstructor(Type.EmptyTypes);

                // Since user can call this overload with any list of types, we double check the type criteria.
                // If output type is a class, see if current type derives from it, else if output type is an
                // interface, see if current type implements it.
                if (typeCtor != null && !type.IsAbstract &&
                (
                   typeof(TOutputType).IsClass && type.IsSubclassOf(typeof(TOutputType)) ||
                   typeof(TOutputType).IsInterface && (object)type.GetInterface(typeof(TOutputType).Name) != null)
                )
                {
                    // The type meets the following criteria:
                    //      - has a default public constructor
                    //      - is not abstract and can be instantiated.
                    //      - type is related to class or interface specified for the output
                    TypeInfo outputType = new TypeInfo(type);

                    // We'll hold all of the matching types in this list temporarily until their IDs are determined.
                    outputTypes.Add(outputType);
                }
            }

            foreach (TypeInfo outputType in outputTypes)
            {
                // Now, we'll go though all of the output types we've found and instantiate an instance of each in order to get
                // the identifier for each of the type. This will help lookup of the type to be used when parsing the data.
                TOutputType instance = outputType.CreateNew();
                outputType.TypeID = instance.TypeID;

                if (!m_outputTypes.ContainsKey(outputType.TypeID))
                    m_outputTypes.Add(outputType.TypeID, outputType);
                else
                    OnDuplicateTypeHandlerEncountered(outputType.RuntimeType, outputType.TypeID);

                // Dispose of the object if it supports life-cycle management
                if (outputType.SupportsLifecycle)
                    ((IDisposable)instance).Dispose();
            }
        }

        /// <summary>
        /// Output type specific frame parsing algorithm.
        /// </summary>
        /// <param name="buffer">Buffer containing data to parse.</param>
        /// <param name="offset">Offset index into buffer that represents where to start parsing.</param>
        /// <param name="length">Maximum length of valid data from offset.</param>
        /// <returns>The length of the data that was parsed.</returns>
        protected override int ParseFrame(byte[] buffer, int offset, int length)
        {
            int parsedLength;

            // Extract the common header from the buffer image which includes the output type ID.
            // For any protocol data that is represented as frames of data in a stream, there will
            // be some set of common identification properties in the frame image, usually at the
            // top, that is common for all frame types.
            ICommonHeader<TTypeIdentifier> commonHeader = ParseCommonHeader(buffer, offset, length);

            // See if there was enough buffer to parse common header, if not exit and wait for more data
            if (commonHeader == null)
                return 0;

            // Lookup TypeID to see if it is a known type
            if (m_outputTypes.TryGetValue(commonHeader.TypeID, out TypeInfo outputType))
            {
                TOutputType instance = outputType.CreateNew();
                instance.CommonHeader = commonHeader;
                parsedLength = instance.ParseBinaryImage(buffer, offset, length);

                // Expose parsed type to consumer
                if (parsedLength > 0)
                    OnDataParsed(instance);
                else if (outputType.SupportsLifecycle)
                    ((IDisposable)instance).Dispose();
            }
            else
            {
                // Report unrecognized output type
                OnOutputTypeNotFound(commonHeader.TypeID);

                // We encountered an unrecognized data type that cannot be parsed
                if (ProtocolUsesSyncBytes)
                {
                    // Protocol uses synchronization bytes so we scan for them in the current buffer. This effectively
                    // scans through buffer to next frame...
                    int syncBytesPosition = buffer.IndexOfSequence(ProtocolSyncBytes, offset + 1, length - 1);

                    if (syncBytesPosition > -1)
                        return syncBytesPosition - offset;
                }

                // Without synchronization bytes we have no choice but to move onto the next buffer of data :(
                parsedLength = length;
                OnDataDiscarded(buffer.BlockCopy(offset, length));
            }

            return parsedLength;
        }

        /// <summary>
        /// Parses a common header instance that implements <see cref="ICommonHeader{TTypeIdentifier}"/> for the output type represented
        /// in the binary image.
        /// </summary>
        /// <param name="buffer">Buffer containing data to parse.</param>
        /// <param name="offset">Offset index into buffer that represents where to start parsing.</param>
        /// <param name="length">Maximum length of valid data from offset.</param>
        /// <returns>The <see cref="ICommonHeader{TTypeIdentifier}"/> which includes a type ID for the <see cref="Type"/> to be parsed.</returns>
        /// <remarks>
        /// <para>
        /// Derived classes need to provide a common header instance (i.e., class that implements <see cref="ICommonHeader{TTypeIdentifier}"/>)
        /// for the output types; this will primarily include an ID of the <see cref="Type"/> that the data image represents.  This parsing is
        /// only for common header information, actual parsing will be handled by output type via its <see cref="ISupportBinaryImage.ParseBinaryImage"/>
        /// method. This header image should also be used to add needed complex state information about the output type being parsed if needed.
        /// </para>
        /// <para>
        /// If there is not enough buffer available to parse common header (as determined by <paramref name="length"/>), return null.  Also, if
        /// the protocol allows frame length to be determined at the time common header is being parsed and there is not enough buffer to parse
        /// the entire frame, it will be optimal to prevent further parsing by returning null.
        /// </para>
        /// </remarks>
        protected abstract ICommonHeader<TTypeIdentifier> ParseCommonHeader(byte[] buffer, int offset, int length);

        /// <summary>
        /// Raises the <see cref="DataParsed"/> event.
        /// </summary>
        /// <param name="output">The object that was deserialized from binary image.</param>
        protected virtual void OnDataParsed(TOutputType output)
        {
            if (DataParsed == null)
                return;

            EventArgs<TOutputType> outputArgs = new EventArgs<TOutputType>(output);

            if (output.AllowQueuedPublication)
            {
                // Queue-up parsed output for publication
                m_outputQueue.Enqueue(outputArgs);
            }
            else
            {
                // Publish parsed output immediately
                DataParsed?.Invoke(this, outputArgs);
            }
        }

        /// <summary>
        /// <see cref="AsyncQueue{T}"/> handler used to publish queued outputs.
        /// </summary>
        /// <param name="outputArgs">Event args containing new output to publish.</param>
        protected virtual void PublishParsedOutput(EventArgs<TOutputType> outputArgs) => DataParsed?.Invoke(this, outputArgs);

        /// <summary>
        /// Raises the <see cref="OutputTypeNotFound"/> event.
        /// </summary>
        /// <param name="id">The ID of the output type that was not found.</param>
        protected virtual void OnOutputTypeNotFound(TTypeIdentifier id) => OutputTypeNotFound?.Invoke(this, new EventArgs<TTypeIdentifier>(id));

        /// <summary>
        /// Raises the <see cref="DuplicateTypeHandlerEncountered"/> event.
        /// </summary>
        /// <param name="duplicateType">The <see cref="Type"/> that defines a type ID that has already been defined.</param>
        /// <param name="id">The ID of the output type that was defined more than once.</param>
        protected virtual void OnDuplicateTypeHandlerEncountered(Type duplicateType, TTypeIdentifier id) => DuplicateTypeHandlerEncountered?.Invoke(this, new EventArgs<Type, TTypeIdentifier>(duplicateType, id));

        // Expose exceptions encountered via async queue processing to parsing exception event
        private void m_parsedOutputQueue_ProcessException(object sender, EventArgs<Exception> e) => OnParsingException(e.Argument);

        #endregion
    }
}
