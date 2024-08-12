//******************************************************************************************************
//  StaticTestSTEII.cs - Gbtc
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
//  08/12/2024 - Ritchie Carroll
//       Generated original version of source code.
//
//******************************************************************************************************
// ReSharper disable InconsistentNaming
// ReSharper disable IdentifierTypo

using System;
using System.Data;
using System.IO;
using System.Text;
using Gemstone.IO.Parsing;

namespace Gemstone.IO.UnitTests.Collections;

/// <summary>
/// Example class for manual static strongly-typed explicit interface implementation serialization.
/// </summary>
public class StaticTestSTEII : ISupportStreamSerialization<StaticTestSTEII>
{
    public StaticTestSTEII()
    {
    }

    public StaticTestSTEII(Guid id, string name, ConnectionState status)
    {
        ID = id;
        Name = name;
        Status = status;
    }

    public Guid ID { get; set; }

    public string Name { get; set; } = "";

    public ConnectionState Status { get; set; }

    /// <summary>
    /// Deserializes the <see cref="InstanceTest"/> from a <see cref="Stream"/>.
    /// </summary>
    /// <param name="stream">Source stream.</param>
    /// <returns>New deserialized instance.</returns>
    static StaticTestSTEII ISupportStreamSerialization<StaticTestSTEII>.ReadFrom(Stream stream)
    {
        BinaryReader reader = new(stream, Encoding.UTF8, true);

        return new StaticTestSTEII
        {
            ID = new Guid(reader.ReadBytes(16)),
            Name = reader.ReadString(),
            Status = (ConnectionState)reader.ReadByte()
        };
    }

    /// <summary>
    /// Serializes the <see cref="InstanceTest"/> to a <see cref="Stream"/>.
    /// </summary>
    /// <param name="stream">Target stream.</param>
    /// <param name="instance">Instance to serialize.</param>
    static void ISupportStreamSerialization<StaticTestSTEII>.WriteTo(Stream stream, StaticTestSTEII instance)
    {
        BinaryWriter writer = new(stream, Encoding.UTF8, true);

        writer.Write(instance.ID.ToByteArray());
        writer.Write(instance.Name);
        writer.Write((byte)instance.Status);
    }
}
