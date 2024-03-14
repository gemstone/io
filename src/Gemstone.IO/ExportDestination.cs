//******************************************************************************************************
//  ExportDestination.cs - Gbtc
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
//  02/13/2008 - J. Ritchie Carroll
//       Initial version of source generated.
//  09/19/2008 - J. Ritchie Carroll
//       Converted to C#.
//  10/23/2008 - Pinal C. Patel
//       Edited code comments.
//  09/14/2009 - Stephen C. Wills
//       Added new header and license agreement.
//  09/22/2011 - J. Ritchie Carroll
//       Added Mono implementation exception regions.
//  12/14/2012 - Starlynn Danyelle Gilliam
//       Modified Header.
//
//******************************************************************************************************

using System.IO;

namespace Gemstone.IO;

/// <summary>
/// Represents a destination location when exporting data using <see cref="MultipleDestinationExporter"/>.
/// </summary>
/// <seealso cref="MultipleDestinationExporter"/>
public class ExportDestination
{
    #region [ Constructors ]

    /// <summary>
    /// Constructs a new <see cref="ExportDestination"/>.
    /// </summary>
    public ExportDestination()
    {
    }

    /// <summary>
    /// Constructs a new <see cref="ExportDestination"/> given the specified parameters.
    /// </summary>
    /// <param name="destinationFile">Path and file name of export destination.</param>
    /// <param name="connectToShare">Determines whether or not to attempt network connection to share specified in <paramref name="destinationFile"/>.</param>
    /// <param name="domain">Domain used to authenticate network connection if <paramref name="connectToShare"/> is true.</param>
    /// <param name="userName">User name used to authenticate network connection if <paramref name="connectToShare"/> is true.</param>
    /// <param name="password">Password used to authenticate network connection if <paramref name="connectToShare"/> is true.</param>
    public ExportDestination(string destinationFile, bool connectToShare, string domain = "", string userName = "", string password = "")
    {
        DestinationFile = destinationFile;
        ConnectToShare = connectToShare;
        Domain = domain;
        UserName = userName;
        Password = password;
    }

    #endregion

    #region [ Properties ]

    /// <summary>
    /// Path and file name of export destination.
    /// </summary>
    public string DestinationFile { get; set; } = default!;

    /// <summary>
    /// Determines whether or not to attempt network connection to share specified in <see cref="ExportDestination.DestinationFile"/>.
    /// </summary>
    /// <remarks>
    /// This option is ignored under Mono deployments.
    /// </remarks>
    public bool ConnectToShare { get; set; }

    /// <summary>
    /// Domain used to authenticate network connection if <see cref="ExportDestination.ConnectToShare"/> is true.
    /// </summary>
    /// <remarks>
    /// This option is ignored under Mono deployments.
    /// </remarks>
    public string Domain { get; set; } = default!;

    /// <summary>
    /// User name used to authenticate network connection if <see cref="ExportDestination.ConnectToShare"/> is true.
    /// </summary>
    /// <remarks>
    /// This option is ignored under Mono deployments.
    /// </remarks>
    public string UserName { get; set; } = default!;

    /// <summary>
    /// Password used to authenticate network connection if <see cref="ExportDestination.ConnectToShare"/> is true.
    /// </summary>
    /// <remarks>
    /// This option is ignored under Mono deployments.
    /// </remarks>
    public string Password { get; set; } = default!;

    /// <summary>
    /// Path root of <see cref="ExportDestination.DestinationFile"/> (e.g., E:\ or \\server\share).
    /// </summary>
    public string Share => Path.GetPathRoot(DestinationFile);

    /// <summary>
    /// Path and filename of <see cref="ExportDestination.DestinationFile"/> without drive or server share prefix.
    /// </summary>
    public string FileName => DestinationFile.Substring(Share.Length);

    #endregion

    #region [ Methods ]

    /// <summary>
    /// Returns a <see cref="string"/> that represents the current <see cref="ExportDestination"/>.
    /// </summary>
    /// <returns>A <see cref="string"/> that represents the current <see cref="ExportDestination"/>.</returns>
    public override string ToString()
    {
        return DestinationFile;
    }

    #endregion
}
