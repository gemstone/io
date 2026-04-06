//******************************************************************************************************
//  LogMessage.cs - Gbtc
//
//  Copyright © 2025, Grid Protection Alliance.  All Rights Reserved.
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
//  10/28/2025 - C. Lackner
//       Generated original version of source code.
//
//******************************************************************************************************

using System;
using Gemstone.Diagnostics;

namespace Gemstone.IO;

/// <summary>
/// Defines a log message send by the System
/// </summary>
/// 
public class UILogMessage
{
    /// <summary>
    /// The source of the log message. For Adapters this is the Adapter Name. For system messages it is an empty string.
    /// </summary>
    public string Source { get; set; }

    /// <summary>
    /// The message content.
    /// </summary>
    public string Message { get; set; }

    /// <summary>
    /// The Timestamp associated with the message.
    /// </summary>
    public DateTime TimeStamp { get; set; }

    /// <summary>
    /// The <see cref="MessageLevel"/> associates with this <see cref="UILogMessage"/>.
    /// </summary>
    public MessageLevel Level { get; set; }
}
