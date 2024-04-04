﻿//******************************************************************************************************
//  MultipleDestinationExporter.cs - Gbtc
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
//  07/29/2008 - J. Ritchie Carroll
//       Added "Initialize" method to enable user to reconnect to network shares.
//       Added more descriptive status messages to provide more detailed user feedback.
//  09/19/2008 - J. Ritchie Carroll
//       Converted to C#.
//  10/22/2008 - Pinal C. Patel
//       Edited code comments.
//  09/14/2009 - Stephen C. Wills
//       Added new header and license agreement.
//  01/27/2011 - J. Ritchie Carroll
//       Modified internal operation to minimize risk of file deadlock and/or memory overload.
//  09/22/2011 - J. Ritchie Carroll
//       Added Mono implementation exception regions.
//  12/14/2012 - Starlynn Danyelle Gilliam
//       Modified Header.
//
//******************************************************************************************************
// ReSharper disable NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Gemstone.ActionExtensions;
using Gemstone.Configuration;
using Gemstone.EventHandlerExtensions;
using Gemstone.Security.Cryptography;
using Gemstone.StringExtensions;
using Gemstone.Threading.SynchronizedOperations;
using Gemstone.Threading.WaitHandleExtensions;

namespace Gemstone.IO;

/// <summary>
/// Handles the exporting of a file to multiple destinations that are defined in the config file.
/// </summary>
/// <remarks>
/// This class is useful for updating the same file on multiple servers (e.g., load balanced web server).
/// </remarks>
/// <example>
/// This example shows the use <see cref="MultipleDestinationExporter"/> for exporting data to multiple locations:
/// <code>
/// using System;
/// using Gemstone.IO;
///
/// class Program
/// {
///     static MultipleDestinationExporter s_exporter;
///
///     static void Main(string[] args)
///     {
///         s_exporter = new MultipleDestinationExporter();
///         s_exporter.Initialized += s_exporter_Initialized;
///         ExportDestination[] defaultDestinations = new ExportDestination[] 
///         {
///             new ExportDestination(@"\\server1\share\exportFile.txt", false, "domain", "user1", "password1"), 
///             new ExportDestination(@"\\server2\share\exportFile.txt", false, "domain", "user2", "password2")
///         };
///         // Initialize with the destinations where data is to be exported.
///         s_exporter.Initialize(defaultDestinations);
///
///         Console.ReadLine();
///     }
///
///     static void s_exporter_Initialized(object sender, EventArgs e)
///     {
///         // Export data to all defined locations after initialization.
///         s_exporter.ExportData("TEST DATA");
///     }
/// }
/// </code>
/// This example shows the config file entry that can be used to specify the <see cref="ExportDestination"/> 
/// used by the <see cref="MultipleDestinationExporter"/> when exporting data:
/// <code>
/// <![CDATA[
/// [ExportDestinations]
/// ; Total allowed time for each export to execute, in milliseconds. Set to -1 for no specific timeout.
/// ExportTimeout=-1
///
/// ; Maximum number of retries that will be attempted during an export if the export fails. Set to zero to only attempt export once.
/// MaximumRetryAttempts=4
///
/// ; Interval to wait, in milliseconds, before retrying an export if the export fails.
/// RetryDelayInterval=1000
/// 
/// ; Total number of export files to produce.
/// ExportCount=2
///
/// ; Root path for export destination, e.g., drive letter or UNC share name. Use UNC path (\\server\share) with no trailing slash for network shares.
/// ExportDestination1=C:\
///
/// ; Boolean flag that determines whether to attempt network connection to share.
/// ExportDestination1_ConnectToShare=false
///
/// ; Path and file name of data export (do not include drive letter or UNC share). Prefix with slash when using UNC paths (\path\filename.txt).
/// ExportDestination1_FileName=Path\\FileName.txt
///
/// ; Root path for export destination, e.g., drive letter or UNC share name. Use UNC path (\\server\share) with no trailing slash for network shares.
/// ExportDestination2=\\server2\share
///
/// ; Boolean flag that determines whether to attempt network connection to share.
/// ExportDestination2_ConnectToShare=True
/// 
/// ; Domain used for authentication to network share (computer name for local accounts).
/// ExportDestination2_Domain=domain
/// 
/// ; User name used for authentication to network share.
/// ExportDestination2_UserName=user2
/// 
/// ; Password used for authentication to network share. Value supports encryption in the format of "KeyName:EncodedValue".
/// ExportDestination2_Password=config-cipher:l2qlAwAPihJjoThH+G53BYT6BXHQr13D6Asdibl0rDmlrgRXvJmCwcP8uvkFRHr9
///
/// ; Path and file name of data export (do not include drive letter or UNC share). Prefix with slash when using UNC paths (\path\filename.txt).
/// ExportDestination2_FileName=\\Path\FileName.txt
/// ]]>
/// </code>
/// </example>
/// <seealso cref="ExportDestination"/>
public class MultipleDestinationExporter : ISupportLifecycle, IProvideStatus, IPersistSettings
{
    #region [ Members ]

    // Nested Types

    /// <summary>
    /// Defines state information for an export.
    /// </summary>
    private sealed class ExportState : IDisposable
    {
        #region [ Members ]

        // Fields
        private bool m_disposed;

        #endregion

        #region [ Constructors ]

        /// <summary>
        /// Creates a new <see cref="ExportState"/>.
        /// </summary>
        public ExportState()
        {
            WaitHandle = new ManualResetEventSlim(false);
        }

        /// <summary>
        /// Releases the unmanaged resources before the <see cref="ExportState"/> object is reclaimed by <see cref="GC"/>.
        /// </summary>
        ~ExportState()
        {
            Dispose(false);
        }

        #endregion

        #region [ Properties ]

        /// <summary>
        /// Gets or sets the source file name for the <see cref="ExportState"/>.
        /// </summary>
        public string SourceFileName { get; init; } = default!;

        /// <summary>
        /// Gets or sets the destination file name for the <see cref="ExportState"/>.
        /// </summary>
        public string DestinationFileName { get; init; } = default!;

        /// <summary>
        /// Gets or sets the event wait handle for the <see cref="ExportState"/>.
        /// </summary>
        public ManualResetEventSlim? WaitHandle { get; }

        /// <summary>
        /// Gets or sets a flag that is used to determine if export process has timed out.
        /// </summary>
        public bool Timeout { get; set; }

        #endregion

        #region [ Methods ]

        /// <summary>
        /// Releases all the resources used by the <see cref="ExportState"/> object.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases the unmanaged resources used by the <see cref="ExportState"/> object and optionally releases the managed resources.
        /// </summary>
        /// <param name="disposing">true to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
        private void Dispose(bool disposing)
        {
            if (m_disposed)
                return;

            try
            {
                if (disposing)
                    WaitHandle?.Dispose();
            }
            finally
            {
                m_disposed = true;  // Prevent duplicate dispose.
            }
        }

        #endregion
    }

    // Constants

    /// <summary>
    /// Specifies the default value for the <see cref="ExportTimeout"/> property.
    /// </summary>
    public const int DefaultExportTimeout = Timeout.Infinite;

    /// <summary>
    /// Specifies the default value for the <see cref="PersistSettings"/> property.
    /// </summary>
    public const bool DefaultPersistSettings = true;

    /// <summary>
    /// Specifies the default value for the <see cref="SettingsCategory"/> property.
    /// </summary>
    public const string DefaultSettingsCategory = "ExportDestinations";

    /// <summary>
    /// Specifies the default value for the <see cref="MaximumRetryAttempts"/> property.
    /// </summary>
    public const int DefaultMaximumRetryAttempts = 4; // That is 4 retries plus the original attempt for a total of 5 attempts

    /// <summary>
    /// Specifies the default value for the <see cref="RetryDelayInterval"/> property.
    /// </summary>
    public const int DefaultRetryDelayInterval = 1000;

    // Events

    /// <summary>
    /// Occurs when the <see cref="MultipleDestinationExporter"/> object has been initialized.
    /// </summary>
    public event EventHandler? Initialized;

    /// <summary>
    /// Occurs when status information for the <see cref="MultipleDestinationExporter"/> object is being reported.
    /// </summary>
    /// <remarks>
    /// <see cref="EventArgs{T}.Argument"/> is the status message being reported by the <see cref="MultipleDestinationExporter"/>.
    /// </remarks>
    public event EventHandler<EventArgs<string>>? StatusMessage;

    /// <summary>
    /// Event is raised when there is an exception encountered while processing.
    /// </summary>
    /// <remarks>
    /// <see cref="EventArgs{T}.Argument"/> is the exception that was thrown.
    /// </remarks>
    public event EventHandler<EventArgs<Exception>>? ProcessException;

    /// <inheritdoc />
    public event EventHandler? Disposed;

    // Fields
    private long m_failedExportAttempts;
    private volatile byte[]? m_fileData;
    private Encoding m_textEncoding;
    private readonly List<ExportDestination> m_exportDestinations;
    private readonly object m_exportDestinationsLock;
    private readonly LongSynchronizedOperation m_exportOperation;
    private int m_maximumRetryAttempts;
    private int m_retryDelayInterval;

    #endregion

    #region [ Constructors ]

    /// <summary>
    /// Initializes a new instance of the <see cref="MultipleDestinationExporter"/> class.
    /// </summary>
    public MultipleDestinationExporter()
        : this(DefaultSettingsCategory)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MultipleDestinationExporter"/> class.
    /// </summary>
    /// <param name="settingsCategory">The config file settings category under which the export destinations are defined.</param>
    /// <param name="exportTimeout">The total allowed time in milliseconds for each export to execute.</param>
    public MultipleDestinationExporter(string settingsCategory, int exportTimeout = DefaultExportTimeout)
    {
        ExportTimeout = exportTimeout;
        Name = settingsCategory;
        PersistSettings = DefaultPersistSettings;
        m_maximumRetryAttempts = DefaultMaximumRetryAttempts;
        m_retryDelayInterval = DefaultRetryDelayInterval;
        m_textEncoding = Encoding.Default; // We use default ANSI page encoding for text based exports...
        m_exportDestinations = new List<ExportDestination>();
        m_exportDestinationsLock = new object();
        m_exportOperation = new LongSynchronizedOperation(ExecuteExports, OnProcessException)
        {
            IsBackground = true
        };            
    }

    #endregion

    #region [ Properties ]

    /// <summary>
    /// Gets or sets the total allowed time in milliseconds for each export to execute.
    /// </summary>
    /// <remarks>
    /// Set to Timeout.Infinite (-1) for no timeout.
    /// </remarks>
    public int ExportTimeout { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of retries that will be attempted during an export if the export fails.
    /// </summary>
    /// <remarks>
    /// Total file export attempts = 1 + <see cref="MaximumRetryAttempts"/>. Set to zero to only attempt export once.
    /// </remarks>
    public int MaximumRetryAttempts
    {
        get => m_maximumRetryAttempts;
        set
        {
            m_maximumRetryAttempts = value;

            if (m_maximumRetryAttempts < 0)
                m_maximumRetryAttempts = 0;
        }
    }

    /// <summary>
    /// Gets or sets the interval to wait, in milliseconds, before retrying an export if the export fails.
    /// </summary>
    public int RetryDelayInterval
    {
        get => m_retryDelayInterval;
        set
        {
            m_retryDelayInterval = value;

            if (m_retryDelayInterval <= 0)
                m_retryDelayInterval = DefaultRetryDelayInterval;
        }
    }

    /// <summary>
    /// Gets or sets a boolean value that indicates whether the settings of <see cref="MultipleDestinationExporter"/> object are 
    /// to be saved to the config file.
    /// </summary>
    public bool PersistSettings { get; init; }

    /// <summary>
    /// Gets or sets the category under which the settings of <see cref="MultipleDestinationExporter"/> object are to be saved
    /// to the config file if the <see cref="PersistSettings"/> property is set to true.
    /// </summary>
    /// <exception cref="ArgumentNullException">The value being assigned is null or empty string.</exception>
    public string SettingsCategory
    {
        get => Name;
        init
        {
            if (string.IsNullOrEmpty(value))
                throw new ArgumentNullException(nameof(value));

            Name = value;
        }
    }

    /// <summary>
    /// Gets or sets the <see cref="Encoding"/> to be used to encode text data being exported.
    /// </summary>
    public virtual Encoding TextEncoding
    {
        get => m_textEncoding;
        set => m_textEncoding = value ?? Encoding.Default;
    }

    /// <summary>
    /// Gets or sets a boolean value that indicates whether the <see cref="MultipleDestinationExporter"/> object is currently enabled.
    /// </summary>
    public bool Enabled { get; set; }

    /// <inheritdoc />
    public bool IsDisposed { get; private set; }

    /// <summary>
    /// Gets the total number exports performed successfully.
    /// </summary>
    public long TotalExports { get; private set; }

    /// <summary>
    /// Gets a list of currently defined <see cref="ExportDestination"/>.
    /// </summary>
    /// <remarks>
    /// Use the <see cref="Initialize(IEnumerable{ExportDestination})"/> method to change the export destination collection.
    /// </remarks>
    public ReadOnlyCollection<ExportDestination> ExportDestinations
    {
        get
        {
            lock (m_exportDestinationsLock)
                return m_exportDestinations.AsReadOnly();
        }
    }

    /// <summary>
    /// Gets the unique identifier of the <see cref="MultipleDestinationExporter"/> object.
    /// </summary>
    public string Name { get; private set; }

    /// <summary>
    /// Gets the descriptive status of the <see cref="MultipleDestinationExporter"/> object.
    /// </summary>
    public string Status
    {
        get
        {
            StringBuilder status = new();

            status.Append("     Configuration section: ");
            status.Append(Name);
            status.AppendLine();
            status.Append("            Export enabled: ");
            status.Append(Enabled);
            status.AppendLine();
            status.Append("       Temporary file path: ");
            status.Append(FilePath.TrimFileName(Path.GetTempPath(), 51));
            status.AppendLine();
            status.AppendLine("       Export destinations: ");
            status.AppendLine();

            lock (m_exportDestinationsLock)
            {
                int count = 1;

                foreach (ExportDestination export in m_exportDestinations)
                {
                    status.AppendFormat("         {0}: {1}\r\n", count.ToString().PadLeft(2, '0'), FilePath.TrimFileName(export.DestinationFile, 65));
                    count++;
                }
            }

            status.AppendLine();
            status.Append("       File export timeout: ");
            status.Append(ExportTimeout == Timeout.Infinite ? "Infinite" : $"{ExportTimeout} milliseconds");
            status.AppendLine();
            status.Append("    Maximum retry attempts: ");
            status.Append(m_maximumRetryAttempts);
            status.AppendLine();
            status.Append("      Retry delay interval: ");
            status.Append($"{m_retryDelayInterval} milliseconds");
            status.AppendLine();
            status.Append("    Failed export attempts: ");
            status.Append(m_failedExportAttempts);
            status.AppendLine();
            status.Append("      Total exports so far: ");
            status.Append(TotalExports);
            status.AppendLine();

            return status.ToString();
        }
    }

    #endregion

    #region [ Methods ]

    /// <summary>
    /// Releases all the resources used by the <see cref="ExportState"/> object.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases the unmanaged resources used by the <see cref="MultipleDestinationExporter"/> object and optionally releases the managed resources.
    /// </summary>
    /// <param name="disposing">true to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
    private void Dispose(bool disposing)
    {
        if (IsDisposed)
            return;

        try
        {
            // This will be done regardless of whether the object is finalized or disposed.
            if (!disposing)
                return;

            // This will be done only when the object is disposed by calling Dispose().
            Shutdown();
            SaveSettings();
        }
        finally
        {
            IsDisposed = true;          // Prevent duplicate dispose.
            Disposed?.SafeInvoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Initializes (or re-initializes) <see cref="MultipleDestinationExporter"/> from configuration settings.
    /// </summary>
    /// <remarks>
    /// If not being used as a component (i.e., user creates their own instance of this class), this method
    /// must be called in order to initialize exports.  Event if used as a component this method can be
    /// called at anytime to reinitialize the exports with new configuration information.
    /// </remarks>
    public void Initialize()
    {
        // We provide a simple default set of export destinations since no others are specified.
        Initialize(new[] { new ExportDestination(Common.IsPosixEnvironment ? "/usr/share/filename.txt" : "C:\\filename.txt", false, "domain", "username", "password") });
    }

    /// <summary>
    /// Initializes (or re-initializes) <see cref="MultipleDestinationExporter"/> from configuration settings.
    /// </summary>
    /// <param name="defaultDestinations">Provides a default set of export destinations if none exist in configuration settings.</param>
    /// <remarks>
    /// If not being used as a component (i.e., user creates their own instance of this class), this method
    /// must be called in order to initialize exports.  Even if used as a component this method can be
    /// called at anytime to reinitialize the exports with new configuration information.
    /// </remarks>
    public void Initialize(IEnumerable<ExportDestination> defaultDestinations)
    {
        // To not delay calling thread due to share authentication, we perform initialization on another thread...
        Thread initializeThread = new(InitializeExporter) { IsBackground = true };
        initializeThread.Start(defaultDestinations.ToList());
    }

    private void InitializeExporter(object? state)
    {
        // In case we are reinitializing class, we shut down any prior queue operations and close any existing network connections...
        Shutdown();

        // Retrieve any specified default export destinations
        lock (m_exportDestinationsLock)
        {
            m_exportDestinations.Clear();
            m_exportDestinations.AddRange(state as List<ExportDestination> ?? new List<ExportDestination>());
        }

        // Load export destinations from the config file - if nothing is in config file yet,
        // the default settings (passed in via state) will be used instead. Consumers
        // wishing to dynamically change export settings in code will need to make sure
        // PersistSettings is false in order to load specified code settings instead of
        // those that may be saved in the configuration file
        LoadSettings();

        ExportDestination[] destinations;

        lock (m_exportDestinationsLock)
        {
            // Cache a local copy of export destinations to reduce lock time,
            // network share authentication may take some time
            destinations = m_exportDestinations.ToArray();
        }

        foreach (ExportDestination destination in destinations)
        {
            // Connect to network shares if necessary
            if (!destination.ConnectToShare)
                continue;

            if (destination.Domain is null)
            {
                OnProcessException(new InvalidOperationException($"Network share authentication to {destination.Share} failed due to missing domain."));
                continue;
            }

            if (destination.UserName is null)
            {
                OnProcessException(new InvalidOperationException($"Network share authentication to {destination.Share} failed due to missing username."));
                continue;
            }

            if (destination.Password is null)
            {
                OnProcessException(new InvalidOperationException($"Network share authentication to {destination.Share} failed due to missing password."));
                continue;
            }

            if (Common.IsPosixEnvironment)
            {
                // TODO: Implement network share authentication for POSIX environment (use "mount" API call)
                OnStatusMessage("Network share authentication not currently available under POSIX environment...");
            }
            else
            {
                // Attempt connection to external network share
                try
                {
                    OnStatusMessage("Attempting network share authentication for user {0}\\{1} to {2}...", destination.Domain, destination.UserName, destination.Share);

                    FilePath.ConnectToNetworkShare(destination.Share, destination.UserName, destination.Password.ConfigDecrypt(), destination.Domain);

                    OnStatusMessage("Network share authentication to {0} succeeded.", destination.Share);
                }
                catch (Exception ex)
                {
                    // Something unexpected happened during attempt to connect to network share - so we'll report it...
                    OnProcessException(new IOException($"Network share authentication to {destination.Share} failed due to exception: {ex.Message}", ex));
                }
            }
        }

        Enabled = true;

        // Notify that initialization is complete.
        OnInitialized();
    }

    // This is all the needed dispose functionality, but since the class can be re-initialized this is a separate method
    private void Shutdown()
    {
        Enabled = false;

        lock (m_exportDestinationsLock)
        {
            // We'll be nice and disconnect network shares when this class is disposed...
            foreach (ExportDestination export in m_exportDestinations)
            {
                // TODO: Implement network share authentication for POSIX environment
                if (!export.ConnectToShare || Common.IsPosixEnvironment)
                    continue;

                try
                {
                    FilePath.DisconnectFromNetworkShare(export.Share);
                }
                catch (Exception ex)
                {
                    // Something unexpected happened during attempt to disconnect from network share - so we'll report it...
                    OnProcessException(new IOException($"Network share disconnect from {export.Share} failed due to exception: {ex.Message}", ex));
                }
            }
        }
    }

    /// <summary>
    /// Raises the <see cref="Initialized"/> event.
    /// </summary>
    protected virtual void OnInitialized()
    {
        Initialized?.SafeInvoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Raises the <see cref="StatusMessage"/> event.
    /// </summary>
    /// <param name="status">Status message to report.</param>
    /// <param name="args"><see cref="string.Format(string,object[])"/> parameters used for status message.</param>
    protected virtual void OnStatusMessage(string status, params object[] args)
    {
        StatusMessage?.SafeInvoke(this, new EventArgs<string>(string.Format(status, args)));
    }

    /// <summary>
    /// Raises <see cref="ProcessException"/> event.
    /// </summary>
    /// <param name="ex">Processing <see cref="Exception"/>.</param>
    protected virtual void OnProcessException(Exception ex)
    {
        ProcessException?.SafeInvoke(this, new EventArgs<Exception>(ex));
    }

    /// <summary>
    /// Start multiple file export.
    /// </summary>
    /// <param name="fileData">Text based data to export to each destination.</param>
    /// <remarks>
    /// This is assumed to be the full content of the file to export. This class does not queue data since
    /// the export is not intended to append to an existing file but rather replace an existing one.
    /// </remarks>
    public void ExportData(string fileData)
    {
        ExportData(m_textEncoding.GetBytes(fileData));
    }

    /// <summary>
    /// Start multiple file export.
    /// </summary>
    /// <param name="fileData">Binary data to export to each destination.</param>
    /// <remarks>
    /// This is assumed to be the full content of the file to export. This class does not queue data since
    /// the export is not intended to append to an existing file but rather replace an existing one.
    /// </remarks>
    public void ExportData(byte[] fileData)
    {
        if (Enabled)
        {
            // Ensure that only one export will be queued and exporting at once
            m_fileData = fileData;
            m_exportOperation.RunAsync();
        }
        else
        {
            throw new InvalidOperationException("Export failed: exporter is not currently enabled.");
        }
    }       

    private void ExecuteExports()
    {
        byte[]? fileData = m_fileData;

        if (!Enabled || fileData is null || m_exportDestinations.Count <= 0)
            return;

        string fileName = default!;
        ExportState[]? exportStates = null;

        try
        {
            ExportDestination[]? destinations;

            // Get a temporary file name
            fileName = Path.GetTempFileName();

            // Export data to the temporary file
            File.WriteAllBytes(fileName, fileData);

            lock (m_exportDestinationsLock)
            {
                // Cache a local copy of export destinations to reduce lock time
                destinations = m_exportDestinations.ToArray();
            }

            // Define a new export state for each export destination
            exportStates = new ExportState[destinations.Length];

            for (int i = 0; i < exportStates.Length; i++)
            {
                exportStates[i] = new ExportState
                {
                    SourceFileName = fileName,
                    DestinationFileName = destinations[i].DestinationFile
                };
            }

            // Spool threads to attempt copy of export files
            for (int i = 0; i < destinations.Length; i++)
                ThreadPool.QueueUserWorkItem(CopyFileToDestination, exportStates[i]);

            // Wait for exports to complete - even if user specifies to wait indefinitely spooled copy routines
            // will eventually return since there is a specified maximum retry count
            if (!exportStates.Select(exportState => exportState.WaitHandle)!.WaitAll(ExportTimeout))
            {
                // Exports failed to complete in specified allowed time, set timeout flag for each export state
                Array.ForEach(exportStates, exportState => exportState.Timeout = true);
                OnStatusMessage("Timed out attempting export, waited for {0}.", Ticks.FromMilliseconds(ExportTimeout).ToElapsedTimeString(2).ToLower());
            }
        }
        catch (Exception ex)
        {
            OnProcessException(new InvalidOperationException($"Exception encountered during export preparation: {ex.Message}", ex));
        }
        finally
        {
            // Dispose the export state wait handles
            if (exportStates is not null)
            {
                foreach (ExportState exportState in exportStates)
                    exportState.Dispose();
            }

            // Delete the temporary file - wait for the specified retry time in case the export threads may still be trying
            // their last copy attempt. This is important if the timeouts are synchronized and there is one more export
            // about to be attempted before the timeout flag is checked.
            new Action(() => DeleteTemporaryFile(fileName)).DelayAndExecute(m_retryDelayInterval);
        }
    }

    private void CopyFileToDestination(object? state)
    {
        ExportState? exportState = null;
        Exception? exportException = null;
        int failedExportCount = 0;

        try
        {
            exportState = state as ExportState;

            if (exportState is null)
                return;

            // File copy may fail if destination is locked, so we set up to retry this operation
            // waiting the specified period between attempts
            for (int attempt = 0; attempt < 1 + m_maximumRetryAttempts; attempt++)
            {
                try
                {
                    // Attempt to copy file to destination, overwriting if it already exists
                    File.Copy(exportState.SourceFileName, exportState.DestinationFileName, true);
                }
                catch (Exception ex)
                {
                    // Stack exception history to provide a full inner exception failure log for each export attempt
                    exportException = exportException is null ? ex : new IOException($"Attempt {attempt + 1} exception: {ex.Message}", exportException);
                    failedExportCount++;

                    // Abort retry attempts if export has timed out or maximum exports have been attempted
                    if (!Enabled || exportState.Timeout || attempt >= m_maximumRetryAttempts)
                        throw exportException;

                    Thread.Sleep(m_retryDelayInterval);
                }
            }

            // Track successful exports
            TotalExports++;
        }
        catch (Exception ex)
        {
            string? destinationFileName = null;
            bool timeout = false;

            if (exportState is not null)
            {
                destinationFileName = exportState.DestinationFileName;
                timeout = exportState.Timeout;
            }

            OnProcessException(new InvalidOperationException($"Export attempt aborted {(timeout ? "due to timeout with" : "after")} {failedExportCount} exception{(failedExportCount > 1 ? "s" : "")} for \"{destinationFileName.ToNonNullString("[undefined]")}\" - {ex.Message}", ex));
        }
        finally
        {
            // Release waiting thread
            exportState?.WaitHandle?.Set();

            // Track total number of failed export attempts
            Interlocked.Add(ref m_failedExportAttempts, failedExportCount);
        }
    }

    private void DeleteTemporaryFile(string filename)
    {
        if (string.IsNullOrEmpty(filename))
            return;

        try
        {
            // Delete the temporary file
            if (File.Exists(filename))
                File.Delete(filename);
        }
        catch (Exception ex)
        {
            // Although errors are not expected from deleting the temporary file, we report any that may occur
            OnProcessException(new InvalidOperationException($"Exception encountered while trying to remove temporary file: {ex.Message}", ex));
        }
    }

    /// <inheritdoc />
    public void SaveSettings()
    {
        if (!PersistSettings)
            return;

        // Ensure that settings category is specified.
        if (string.IsNullOrEmpty(SettingsCategory))
            throw new InvalidOperationException("SettingsCategory property has not been set");

        // Save settings under the specified category.
        dynamic settings = Settings.Instance[SettingsCategory];

        settings.ExportTimeout = ExportTimeout;
        settings.MaximumRetryAttempts = MaximumRetryAttempts;
        settings.RetryDelayInterval = RetryDelayInterval;

        lock (m_exportDestinationsLock)
        {
            settings["ExportCount"] = m_exportDestinations.Count;
            
            for (int i = 0; i < m_exportDestinations.Count; i++)
            {
                settings[$"ExportDestination{i + 1}"] = m_exportDestinations[i].Share;
                settings[$"ExportDestination{i + 1}_ConnectToShare"] = m_exportDestinations[i].ConnectToShare;
                settings[$"ExportDestination{i + 1}_Domain"] = m_exportDestinations[i].Domain;
                settings[$"ExportDestination{i + 1}_UserName"] = m_exportDestinations[i].UserName;
                settings[$"ExportDestination{i + 1}_Password"] = m_exportDestinations[i].Password;
                settings[$"ExportDestination{i + 1}_FileName"] = m_exportDestinations[i].FileName;
            }
        }
    }

    /// <inheritdoc />
    public void LoadSettings()
    {
        if (!PersistSettings)
            return;

        // Ensure that settings category is specified.
        if (string.IsNullOrEmpty(SettingsCategory))
            throw new InvalidOperationException("SettingsCategory property has not been set");

        // Load settings from the specified category.
        dynamic settings = Settings.Instance[SettingsCategory];

        int count = settings.ExportCount;

        if (count == 0)
            return;

        lock (m_exportDestinationsLock)
        {
            m_exportDestinations.Clear();

            for (int i = 0; i < count; i++)
            {
                string entryRoot = $"ExportDestination{i + 1}";

                // Load export destination from configuration entries
                ExportDestination destination = new()
                {
                    DestinationFile = $"{settings[entryRoot]}{settings[$"{entryRoot}_FileName"]}",
                    ConnectToShare = settings[$"{entryRoot}_ConnectToShare"],
                    Domain = settings[$"{entryRoot}_Domain"],
                    UserName = settings[$"{entryRoot}_UserName"],
                    Password = settings[$"{entryRoot}_Password"]
                };

                // Save new export destination if destination file name has been defined and is valid
                if (FilePath.IsValidFileName(destination.DestinationFile))
                    m_exportDestinations.Add(destination);
            }
        }
    }

    #endregion

    #region [ Static ]

    /// <inheritdoc cref="IDefineSettings.DefineSettings" />
    public static void DefineSettings(Settings settings, string settingsCategory = DefaultSettingsCategory)
    {
        dynamic section = settings[settingsCategory];

        section.ExportTimeout = (DefaultExportTimeout, "Total allowed time for each export to execute, in milliseconds. Set to -1 for no specific timeout.");
        section.MaximumRetryAttempts = (DefaultMaximumRetryAttempts, "Maximum number of retries that will be attempted during an export if the export fails. Set to zero to only attempt export once.");
        section.RetryDelayInterval = (DefaultRetryDelayInterval, "Interval to wait, in milliseconds, before retrying an export if the export fails.");
        section.ExportCount = (0, "Total number of export files to produce.");

        // Define example export destination settings
        section.ExportDestination1 = ("C:\\", "Root path for export destination, e.g., drive letter or UNC share name. Use UNC path (\\server\\share) with no trailing slash for network shares.");
        section.ExportDestination1_ConnectToShare = (false, "Boolean flag that determines whether to attempt network connection to share.");
        section.ExportDestination1_Domain = ("", "Domain used for authentication to network share (computer name for local accounts).");
        section.ExportDestination1_UserName = ("", "User name used for authentication to network share.");
        section.ExportDestination1_Password = ("", "Password used for authentication to network share. Value supports encryption in the format of \"KeyName:EncodedValue\".");
        section.ExportDestination1_FileName = ("Path\\FileName.txt", "Path and file name of data export (do not include drive letter or UNC share). Prefix with slash when using UNC paths (\\path\\filename.txt).");

        section.ExportDestination2 = ("\\server\\share", "Root path for export destination, e.g., drive letter or UNC share name. Use UNC path (\\server\\share) with no trailing slash for network shares.");
        section.ExportDestination2_ConnectToShare = (false, "Boolean flag that determines whether to attempt network connection to share.");
        section.ExportDestination2_Domain = ("domain", "Domain used for authentication to network share (computer name for local accounts).");
        section.ExportDestination2_UserName = ("username", "User name used for authentication to network share.");
        section.ExportDestination2_Password = ("config-cipher:base64-encoded-password", "Password used for authentication to network share. Value supports encryption in the format of \"KeyName:EncodedValue\".");
        section.ExportDestination2_FileName = ("\\Path\\FileName.txt", "Path and file name of data export (do not include drive letter or UNC share). Prefix with slash when using UNC paths (\\path\\filename.txt).");
    }

    #endregion
}
