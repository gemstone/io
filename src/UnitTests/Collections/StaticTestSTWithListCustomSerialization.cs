//******************************************************************************************************
//  StaticTestSTWithListCustomSerialization.cs - Gbtc
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

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Gemstone.IO.Parsing;

namespace Gemstone.IO.UnitTests.Collections;

/// <summary>
/// Defines a serializable collection of <see cref="StaticTestST"/> objects
/// using a custom list serialization implementation.
/// </summary>
public class StaticTestSTWithListCustomSerialization : List<StaticTestST>, ISupportStreamSerialization<StaticTestSTWithListCustomSerialization>
{
    public string CustomData { get; set; }

    // Mark list-type as supporting custom serialization
    static bool ISupportStreamSerialization.UseCustomListSerialization => true;

    public static StaticTestSTWithListCustomSerialization ReadFrom(Stream stream)
    {
        BinaryReader reader = new(stream, Encoding.UTF8, true);
        StaticTestSTWithListCustomSerialization result = [];

        bool customDataIsNull = reader.ReadBoolean();
        result.CustomData = reader.ReadString();

        if (customDataIsNull)
            result.CustomData = null;

        int count = reader.ReadInt32();

        for (int i = 0; i < count; i++)
            result.Add(StaticTestST.ReadFrom(stream));

        return result;
    }

    public static void WriteTo(Stream stream, StaticTestSTWithListCustomSerialization instance)
    {
        BinaryWriter writer = new(stream, Encoding.UTF8, true);

        writer.Write(instance.CustomData is null);
        writer.Write(instance.CustomData ?? string.Empty);

        writer.Write(instance.Count);

        foreach (StaticTestST item in instance)
            StaticTestST.WriteTo(stream, item);
    }
}
