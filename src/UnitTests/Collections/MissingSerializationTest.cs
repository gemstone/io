﻿//******************************************************************************************************
//  MissingSerializationTest.cs - Gbtc
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
//  02/21/2025 - C. Lackner
//       Generated original version of source code.
//
//******************************************************************************************************

using System;
using System.Collections.Generic;
using System.Data;

namespace Gemstone.IO.UnitTests.Collections;

/// <summary>
/// Defines an object without Serialization.
/// </summary>
public class MissingSerializationTest
{
    public MissingSerializationTest()
    {
    }

    public MissingSerializationTest(Guid id, string name, ConnectionState status)
    {
        ID = id;
        Name = name;
        Status = status;
    }

    public Guid ID { get; set; }

    public string Name { get; set; } = "";

    public ConnectionState Status { get; set; }
}
