// MWLib
// Copyright (c) 2007–2008  Petr Kadlec <mormegil@centrum.cz>
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
using System.Globalization;
using System.Runtime.Serialization;
using MWLib.IO;

namespace MWLib
{
    /// <summary>
    /// Exception during command line parsing
    /// </summary>
    [Serializable]
    public class CommandLineParameterException : Exception
    {
        /// <summary>
        /// Constructor
        /// </summary>
        public CommandLineParameterException() { }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="message">Exception message</param>
        public CommandLineParameterException(string message) : base(message) { }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="message">Exception message</param>
        /// <param name="innerException">Inner exception</param>
        public CommandLineParameterException(string message, Exception innerException) : base(message, innerException) { }

        /// <summary>
        /// Deserialization constructor
        /// </summary>
        /// <param name="info">Serialized data</param>
        /// <param name="context">Contextual information about the data source</param>
        protected CommandLineParameterException(SerializationInfo info, StreamingContext context) : base(info, context) { }
    }

    /// <summary>
    /// A parameter on command line
    /// </summary>
    public abstract class CommandLineParameter
    {
        /// <summary>
        /// Name of the parameter (without the two initial dashes)
        /// </summary>
        public string ParameterName { get; private set; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="parameterName">Name of the parameter (without the two initial dashes)</param>
        protected CommandLineParameter(string parameterName)
        {
            if (String.IsNullOrEmpty(parameterName)) throw new ArgumentException(LibraryResources.ParameterNameCannotBeEmpty, "parameterName");
            this.ParameterName = parameterName;
        }

        /// <summary>
        /// Process a matching argument from the command line
        /// </summary>
        /// <param name="argName">Name of the argument on the command line</param>
        /// <param name="argValue">Value given to the argument (after an equal sign), or <c>null</c></param>
        protected abstract void Process(string argName, string argValue);

        /// <summary>
        /// Try to parse an argument from the command line (check for match against this argument, and process it, if it matches)
        /// </summary>
        /// <param name="args">Array of arguments</param>
        /// <param name="index">Index of the argument to be processed</param>
        /// <returns>Number of elements in <paramref name="args"/> that have been processed and should be skipped (0 if none)</returns>
        protected internal virtual int TryParse(string[] args, int index)
        {
            string arg = args[index];
            if (arg.StartsWith("--"))
            {
                int equalSign = arg.IndexOf('=');
                if (equalSign >= 0)
                {
                    string argName = arg.Substring(2, equalSign - 2);
                    string argValue = arg.Substring(equalSign + 1);
                    if (argName == ParameterName)
                    {
                        Process(argName, argValue);
                        return 1;
                    }
                }
                else
                {
                    string argName = arg.Substring(2);
                    if (argName == ParameterName)
                    {
                        Process(argName, null);
                        return 1;
                    }
                }
            }
            return 0;
        }
    }

    /// <summary>
    /// A handler function for a command-line parameter
    /// </summary>
    /// <param name="argName">Name of the argument on the command line</param>
    /// <param name="argValue">Value given to the argument (after an equal sign), or <c>null</c></param>
    public delegate void CommandLineParameterHandler(string argName, string argValue);

    /// <summary>
    /// Command line parameter that should be processed by executing a given function
    /// </summary>
    public class CommandLineParameterCallback : CommandLineParameter
    {
        /// <summary>
        /// The callback function
        /// </summary>
        private readonly CommandLineParameterHandler callback;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="parameterName">Name of the parameter (without the two initial dashes)</param>
        /// <param name="callback">The function that should be called when the parameter is found</param>
        public CommandLineParameterCallback(string parameterName, CommandLineParameterHandler callback)
            : base(parameterName)
        {
            if (callback == null) throw new ArgumentNullException("callback");
            this.callback = callback;
        }

        /// <summary>
        /// Process a matching argument from the command line
        /// </summary>
        /// <param name="argName">Name of the argument on the command line</param>
        /// <param name="argValue">Value given to the argument (after an equal sign), or <c>null</c></param>
        /// <exception cref="CommandLineParameterException">When the parameter received an argument after an equal sign</exception>
        protected override void Process(string argName, string argValue)
        {
            if (argValue != null) throw new CommandLineParameterException(LibraryResources.ArgumentNotExpectedFor(argName));
            callback(argName, argValue);
        }
    }

    /// <summary>
    /// A command line parameter that may or may not be present
    /// </summary>
    public class CommandLineParameterFlag : CommandLineParameter
    {
        /// <summary>
        /// Has the flag been found on the command line?
        /// </summary>
        public bool Present { get; private set; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="parameterName">Name of the parameter (without the two initial dashes)</param>
        public CommandLineParameterFlag(string parameterName)
            : base(parameterName)
        {
        }

        /// <summary>
        /// Process a matching argument from the command line
        /// </summary>
        /// <param name="argName">Name of the argument on the command line</param>
        /// <param name="argValue">Value given to the argument (after an equal sign), or <c>null</c></param>
        /// <exception cref="CommandLineParameterException">When the parameter has an argument after an equal sign -or- the flag has been already specified</exception>
        protected override void Process(string argName, string argValue)
        {
            if (argValue != null) throw new CommandLineParameterException(LibraryResources.UnexpectedArgumentFor(argName));
            if (Present) throw new CommandLineParameterException(LibraryResources.DuplicateArgument(argName));
            Present = true;
        }
    }

    /// <summary>
    /// A command line parameter that has arguments given after an equal sign
    /// </summary>
    public class CommandLineParameterWithArguments : CommandLineParameter
    {
        /// <summary>
        /// List of arguments to the parameter
        /// </summary>
        /// <remarks>
        /// For <c>--arg=XYZ --arg=ABC --arg=UUU</c> this list contains <c>{"XYZ", "ABC", "UUU"}</c>.
        /// </remarks>
        public IList<string> Arguments
        {
            get { return arguments; }
        }
        private IList<string> arguments;

        /// <summary>
        /// Maximum allowed number of occurrences of this parameter
        /// </summary>
        public int MaxOccurrences { get; private set; }

        /// <summary>
        /// Number of occurrences of this parameter on the command line
        /// </summary>
        public int Occurrences
        {
            get { return arguments == null ? 0 : arguments.Count; }
        }

        /// <summary>
        /// Constructor, initializes the parameter for at most single occurrence
        /// </summary>
        /// <param name="parameterName">Name of the parameter (without the two initial dashes)</param>
        public CommandLineParameterWithArguments(string parameterName)
            : this(parameterName, 1)
        {
        }

        /// <summary>
        /// Constructor, initializes the parameter for at most single occurrence
        /// </summary>
        /// <param name="parameterName">Name of the parameter (without the two initial dashes)</param>
        /// <param name="maxOccurrences">Maximum allowed number of occurrences of this parameter</param>
        public CommandLineParameterWithArguments(string parameterName, int maxOccurrences)
            : base(parameterName)
        {
            if (maxOccurrences <= 0) throw new ArgumentOutOfRangeException("maxOccurrences", maxOccurrences, LibraryResources.ValueMustBePositive);
            this.MaxOccurrences = maxOccurrences;
        }

        /// <summary>
        /// Process a matching argument from the command line
        /// </summary>
        /// <param name="argName">Name of the argument on the command line</param>
        /// <param name="argValue">Value given to the argument (after an equal sign), or <c>null</c></param>
        /// <exception cref="CommandLineParameterException">When the parameter did not receive an argument after an equal sign</exception>
        protected override void Process(string argName, string argValue)
        {
            if (argValue == null) throw new CommandLineParameterException(LibraryResources.MissingArgumentRequiredFor(argName));
            ParseArgument(argName, argValue);
        }

        /// <summary>
        /// Process the argument value
        /// </summary>
        /// <param name="argName">Name of the argument on the command line</param>
        /// <param name="arg">Value given to the argument (after an equal sign)</param>
        /// <exception cref="CommandLineParameterException">When the parameter has been specified more times than <see cref="MaxOccurrences"/></exception>
        protected virtual void ParseArgument(string argName, string arg)
        {
            if (arguments == null) arguments = new List<string>();
            if (Occurrences > 0)
            {
                if (MaxOccurrences == 1) throw new CommandLineParameterException(LibraryResources.DuplicateArgument(argName));
                if (arguments.Count >= MaxOccurrences) throw new CommandLineParameterException(LibraryResources.TooManyArguments(argName));
            }
            arguments.Add(arg);
        }
    }

    /// <summary>
    /// A command line parameter that has an integer-valued argument given after an equal sign
    /// </summary>
    public class CommandLineParameterWithIntArgument : CommandLineParameterWithArguments
    {
        /// <summary>
        /// The specified argument value
        /// </summary>
        public int ArgumentValue
        {
            get { return argumentValue; }
        }
        private int argumentValue;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="parameterName">Name of the parameter (without the two initial dashes)</param>
        public CommandLineParameterWithIntArgument(string parameterName)
            : base(parameterName)
        {
        }

        /// <summary>
        /// Process the argument value
        /// </summary>
        /// <param name="argName">Name of the argument on the command line</param>
        /// <param name="arg">Value given to the argument (after an equal sign)</param>
        protected override void ParseArgument(string argName, string arg)
        {
            base.ParseArgument(argName, arg);
            argumentValue = Int32.Parse(arg, CultureInfo.InvariantCulture);
        }
    }

    /// <summary>
    /// A command line parameter that has a date argument given after an equal sign
    /// </summary>
    public class CommandLineParameterWithDateArgument : CommandLineParameterWithArguments
    {
        /// <summary>
        /// The specified argument value
        /// </summary>
        public DateTime ArgumentValue
        {
            get { return argumentValue; }
        }
        private DateTime argumentValue;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="parameterName">Name of the parameter (without the two initial dashes)</param>
        public CommandLineParameterWithDateArgument(string parameterName)
            : base(parameterName)
        {
        }

        /// <summary>
        /// Process the argument value
        /// </summary>
        /// <param name="argName">Name of the argument on the command line</param>
        /// <param name="arg">Value given to the argument (after an equal sign)</param>
        protected override void ParseArgument(string argName, string arg)
        {
            base.ParseArgument(argName, arg);
            argumentValue = DataFileTools.ParseDateTime(arg);
        }
    }

    /// <summary>
    /// Parser for command line arguments
    /// </summary>
    public class CommandLineParser
    {
        /// <summary>
        /// The command line arguments
        /// </summary>
        private string[] args;

        /// <summary>
        /// List of defined parameters
        /// </summary>
        private IEnumerable<CommandLineParameter> knownParameters;

        /// <summary>
        /// List of received positional arguments
        /// </summary>
        public IList<string> PositionalArguments
        {
            get
            {
                return positionalArguments;
            }
        }
        private List<string> positionalArguments;

        /// <summary>
        /// Constructor, defines supported parameters
        /// </summary>
        /// <param name="args">Arguments received on the command line</param>
        /// <param name="knownParameters">List of supported parameters</param>
        public CommandLineParser(string[] args, IEnumerable<CommandLineParameter> knownParameters)
        {
            this.args = args;
            this.knownParameters = knownParameters;
        }

        /// <summary>
        /// Parse the command line
        /// </summary>
        /// <exception cref="CommandLineParameterException">When the command line has invalid syntax</exception>
        public void Parse()
        {
            int i = 0;
            while (i < args.Length)
            {
                string arg = args[i];
                if (arg == "--")
                {
                    ++i;
                    break;
                }
                if (!arg.StartsWith("--")) break;

                bool found = false;
                foreach(CommandLineParameter param in knownParameters)
                {
                    int delta = param.TryParse(args, i);
                    if (delta != 0)
                    {
                        i += delta;
                        found = true;
                        break;
                    }
                }
                if (!found) throw new CommandLineParameterException(LibraryResources.UnknownParameter(arg));
            }
            if (i > args.Length) throw new CommandLineParameterException(LibraryResources.InvalidParameters);

            positionalArguments = new List<string>(args.Length - i);
            // positional arguments
            while (i < args.Length)
            {
                positionalArguments.Add(args[i]);
                ++i;
            }
        }
    }
}
