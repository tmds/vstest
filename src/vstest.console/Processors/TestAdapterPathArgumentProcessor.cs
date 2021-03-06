// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.CommandLine.Processors
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Contracts;
    using System.Globalization;
    using System.IO;
    using System.Linq;

    using Microsoft.VisualStudio.TestPlatform.CommandLine.Processors.Utilities;
    using Microsoft.VisualStudio.TestPlatform.Common;
    using Microsoft.VisualStudio.TestPlatform.Common.Interfaces;
    using Microsoft.VisualStudio.TestPlatform.Utilities;
    using Microsoft.VisualStudio.TestPlatform.Utilities.Helpers;
    using Microsoft.VisualStudio.TestPlatform.Utilities.Helpers.Interfaces;

    using CommandLineResources = Microsoft.VisualStudio.TestPlatform.CommandLine.Resources.Resources;

    /// <summary>
    /// Allows the user to specify a path to load custom adapters from.
    /// </summary>
    internal class TestAdapterPathArgumentProcessor : IArgumentProcessor
    {
        #region Constants

        /// <summary>
        /// The name of the command line argument that the ListTestsArgumentExecutor handles.
        /// </summary>
        public const string CommandName = "/TestAdapterPath";

        #endregion

        private Lazy<IArgumentProcessorCapabilities> metadata;

        private Lazy<IArgumentExecutor> executor;

        /// <summary>
        /// Gets the metadata.
        /// </summary>
        public Lazy<IArgumentProcessorCapabilities> Metadata
        {
            get
            {
                if (this.metadata == null)
                {
                    this.metadata = new Lazy<IArgumentProcessorCapabilities>(() => new TestAdapterPathArgumentProcessorCapabilities());
                }

                return this.metadata;
            }
        }

        /// <summary>
        /// Gets or sets the executor.
        /// </summary>
        public Lazy<IArgumentExecutor> Executor
        {
            get
            {
                if (this.executor == null)
                {
                    this.executor = new Lazy<IArgumentExecutor>(() => new TestAdapterPathArgumentExecutor(CommandLineOptions.Instance, RunSettingsManager.Instance, ConsoleOutput.Instance, new FileHelper()));
                }

                return this.executor;
            }

            set
            {
                this.executor = value;
            }
        }
    }

    /// <summary>
    /// The argument capabilities.
    /// </summary>
    internal class TestAdapterPathArgumentProcessorCapabilities : BaseArgumentProcessorCapabilities
    {
        public override string CommandName => TestAdapterPathArgumentProcessor.CommandName;

        public override bool AllowMultiple => true;

        public override bool IsAction => false;

        public override ArgumentProcessorPriority Priority => ArgumentProcessorPriority.AutoUpdateRunSettings;

        public override string HelpContentResourceName => CommandLineResources.TestAdapterPathHelp;

        public override HelpContentPriority HelpPriority => HelpContentPriority.TestAdapterPathArgumentProcessorHelpPriority;
    }

    /// <summary>
    /// The argument executor.
    /// </summary>
    internal class TestAdapterPathArgumentExecutor : IArgumentExecutor
    {
        #region Fields

        /// <summary>
        /// Used for getting sources.
        /// </summary>
        private CommandLineOptions commandLineOptions;

        /// <summary>
        /// Run settings provider.
        /// </summary>
        private IRunSettingsProvider runSettingsManager;

        /// <summary>
        /// Used for sending output.
        /// </summary>
        private IOutput output;

        /// <summary>
        /// For file related operation
        /// </summary>
        private IFileHelper fileHelper;

        #endregion

        #region Constructor

        /// <summary>
        /// Default constructor.
        /// </summary>
        /// <param name="options"> The options. </param>
        /// <param name="testPlatform">The test platform</param>
        public TestAdapterPathArgumentExecutor(CommandLineOptions options, IRunSettingsProvider runSettingsManager, IOutput output, IFileHelper fileHelper)
        {
            Contract.Requires(options != null);

            this.commandLineOptions = options;
            this.runSettingsManager = runSettingsManager;
            this.output = output;
            this.fileHelper = fileHelper;
        }

        #endregion

        #region IArgumentExecutor

        /// <summary>
        /// Initializes with the argument that was provided with the command.
        /// </summary>
        /// <param name="argument">Argument that was provided with the command.</param>
        public void Initialize(string argument)
        {
            if (string.IsNullOrWhiteSpace(argument))
            {
                throw new CommandLineException(
                    string.Format(CultureInfo.CurrentCulture, CommandLineResources.TestAdapterPathValueRequired));
            }

            string customAdaptersPath;

            try
            {
                // Remove leading and trailing ' " ' chars...
                argument = argument.Trim().Trim(new char[] { '\"' });

                customAdaptersPath = Path.GetFullPath(argument);
                if (!fileHelper.DirectoryExists(customAdaptersPath))
                {
                    throw new DirectoryNotFoundException(CommandLineResources.TestAdapterPathDoesNotExist);
                }

                // Get testadapter paths from RunSettings.
                var testAdapterPathsInRunSettings = this.runSettingsManager.QueryRunSettingsNode("RunConfiguration.TestAdaptersPaths");

                if (!string.IsNullOrWhiteSpace(testAdapterPathsInRunSettings))
                {
                    var testAdapterFullPaths = new List<string>();
                    var testAdapterPathsInRunSettingsArray = testAdapterPathsInRunSettings.Split(
                        new[] { ';' },
                        StringSplitOptions.RemoveEmptyEntries);

                    foreach (var testadapterPath in testAdapterPathsInRunSettingsArray)
                    {
                        var testAdapterFullPath = Path.GetFullPath(testadapterPath);

                        if (!this.fileHelper.DirectoryExists(testAdapterFullPath))
                        {
                            throw new DirectoryNotFoundException(CommandLineResources.TestAdapterPathDoesNotExist);
                        }

                        testAdapterFullPaths.Add(testAdapterFullPath);
                    }

                    testAdapterFullPaths.Add(customAdaptersPath);
                    testAdapterFullPaths = testAdapterFullPaths.Distinct().ToList();
                    customAdaptersPath = string.Join(";", testAdapterFullPaths.ToArray());
                }

                this.runSettingsManager.UpdateRunSettingsNode("RunConfiguration.TestAdaptersPaths", customAdaptersPath);
            }
            catch (Exception e)
            {
                throw new CommandLineException(
                    string.Format(CultureInfo.CurrentCulture, CommandLineResources.InvalidTestAdapterPathCommand, argument, e.Message));
            }

            this.commandLineOptions.TestAdapterPath = customAdaptersPath;
        }

        /// <summary>
        /// Executes the argument processor.
        /// </summary>
        /// <returns> The <see cref="ArgumentProcessorResult"/>. </returns>
        public ArgumentProcessorResult Execute()
        {
            // Nothing to do since we updated the parameter during initialize parameter
            return ArgumentProcessorResult.Success;
        }

        #endregion
    }
}