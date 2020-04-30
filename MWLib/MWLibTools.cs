// MWLib
// Copyright (c) 2007  Petr Kadlec <mormegil@centrum.cz>
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.IO;
using MWLib.IO;

namespace MWLib
{
    /// <summary>
    /// Common infrastructure for all command-line tools
    /// </summary>
    public abstract class CommandLineTool
    {
        #region ------ Functionality to be defined by the concrete subclasses ---------------------

        /// <summary>
        /// About line: a string containing a short description of the program, its version, and copyright information
        /// </summary>
        protected abstract string AboutLine { get; }

        /// <summary>
        /// List of input filenames
        /// </summary>
        protected abstract IEnumerable<string> InputFilenames { get; }

        /// <summary>
        /// Name of the output file
        /// </summary>
        protected abstract string OutputFilename { get; }

        /// <summary>
        /// Process the given input file
        /// </summary>
        /// <param name="inputStream">Input stream</param>
        protected abstract void Process(Stream inputStream);

        /// <summary>
        /// Save the results to the given file
        /// </summary>
        /// <param name="outputStream">Stream into which the output should be written</param>
        protected abstract void SaveOutput(Stream outputStream);

        /// <summary>
        /// This method is called when the command line has been parsed.
        /// </summary>
        /// <param name="commandLineParser">The parser used to parse the command line (it contains positional arguments)</param>
        /// <remarks>Subclasses should probably override this method</remarks>
        protected virtual void CommandLineParsed(CommandLineParser commandLineParser)
        {
            // subclasses might want to override this method
        }

        /// <summary>
        /// Print command-line help
        /// </summary>
        /// <remarks>Subclasses should probably override this method</remarks>
        protected virtual void PrintHelp()
        {
            Console.WriteLine(AboutLine);
            // subclasses should override this method
        }

        #endregion

        /// <summary>
        /// List of known parameters
        /// </summary>
        /// <seealso cref="AddParameter"/>
        private List<CommandLineParameter> parameters = new List<CommandLineParameter>();

        /// <summary>
        /// Constructor
        /// </summary>
        protected CommandLineTool()
        {
            AddParameter(new CommandLineParameterCallback("version", new CommandLineParameterHandler(delegate { PrintVersionInfo(); })));
            AddParameter(new CommandLineParameterCallback("help", new CommandLineParameterHandler(delegate { PrintHelp(); })));
        }

        /// <summary>
        /// Add a parameter definition
        /// </summary>
        /// <param name="parameter">Definition of a parameter supported by this program</param>
        protected void AddParameter(CommandLineParameter parameter)
        {
            parameters.Add(parameter);
        }

        /// <summary>
        /// Main method: executes all required steps
        /// </summary>
        /// <param name="args">Command-line arguments</param>
        /// <remarks>This method parses the supplied command-line arguments, reads all input files and creates the output file</remarks>
        protected void Execute(string[] args)
        {
            CommandLineParser parser = new CommandLineParser(args, parameters);
            parser.Parse();
            CommandLineParsed(parser);

            foreach (string input in InputFilenames)
            {
                using (Stream inputStream = DataFileTools.OpenInputFile(input))
                {
                    Process(inputStream);
                }
            }

            string outputFilename = OutputFilename;
            Stream outputStream = String.IsNullOrEmpty(outputFilename) ? Console.OpenStandardInput() : new FileStream(outputFilename, FileMode.Create, FileAccess.Write, FileShare.Read);
            try
            {
                SaveOutput(outputStream);
            }
            finally
            {
                outputStream.Dispose();
            }
        }

        /// <summary>
        /// Print GNU GPL information
        /// </summary>
        protected static void PrintGnuGplInfo()
        {
            Console.WriteLine("This program is distributed in the hope that it will be useful,");
            Console.WriteLine("but WITHOUT ANY WARRANTY; without even the implied warranty of");
            Console.WriteLine("MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the");
            Console.WriteLine("GNU General Public License for more details.");
            Environment.Exit(0);
        }

        /// <summary>
        /// Print version info
        /// </summary>
        protected void PrintVersionInfo()
        {
            Console.WriteLine(AboutLine);
            Console.WriteLine();
            PrintGnuGplInfo();
        }
    }
}
