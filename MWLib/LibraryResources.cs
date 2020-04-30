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
using System.Reflection;
using System.Resources;

namespace MWLib
{
    /// <summary>
    /// Resources for MWLib
    /// </summary>
    internal static class LibraryResources
    {
        /// <summary>
        /// "Unsupported date/time format"
        /// </summary>
        public static string UnsupportedDateTimeFormat { get { return GetMessage("UnsupportedDateTimeFormat"); } }

        /// <summary>
        /// "Unsupported file type"
        /// </summary>
        public static string UnsupportedFileType { get { return GetMessage("UnsupportedFileType"); } }

        /// <summary>
        /// "Cannot find usable file for {0}"
        /// </summary>
        public static string CannotFindUsableFile { get { return GetMessage("CannotFindSuitableFile"); } }

        /// <summary>
        /// "Seeking not supported on this stream"
        /// </summary>
        public static string SeekingNotSupported { get { return GetMessage("SeekingNotSupported"); } }

        /// <summary>
        /// "Setting length not supported on this stream"
        /// </summary>
        public static string SettingLengthNotSupported { get { return GetMessage("SettingLengthNotSupported"); } }

        /// <summary>
        /// "Stream already closed"
        /// </summary>
        public static string StreamAlreadyClosed { get { return GetMessage("StreamAlreadyClosed"); } }

        /// <summary>
        /// "This stream is read-only"
        /// </summary>
        public static string StreamIsReadOnly { get { return GetMessage("StreamIsReadOnly"); } }

        /// <summary>
        /// "End of file reached"
        /// </summary>
        public static string EndOfFileReached { get { return GetMessage("EndOfFileReached"); } }

        /// <summary>
        /// "Unsupported format"
        /// </summary>
        public static string UnsupportedFormat { get { return GetMessage("UnsupportedFormat"); } }

        /// <summary>
        /// "Readable stream required"
        /// </summary>
        public static string ReadableStreamRequired { get { return GetMessage("ReadableStreamRequired"); } }

        /// <summary>
        /// "Writable stream required"
        /// </summary>
        public static string WritableStreamRequired { get { return GetMessage("WritableStreamRequired"); } }

        /// <summary>
        /// "Argument must be User"
        /// </summary>
        public static string ArgumentMustBeUser { get { return GetMessage("ArgumentMustBeUser"); } }

        /// <summary>
        /// "Parameter name cannot be empty"
        /// </summary>
        public static string ParameterNameCannotBeEmpty { get { return GetMessage("ParameterNameCannotBeEmpty"); } }

        /// <summary>
        /// "Argument not expected for {0}"
        /// </summary>
        public static string ArgumentNotExpectedFor(string arg0) { return GetMessage("ArgumentNotExpectedFor", arg0); }

        /// <summary>
        /// "Missing argument required for {0}"
        /// </summary>
        public static string MissingArgumentRequiredFor(string arg0) { return GetMessage("MissingArgumentRequiredFor", arg0); }

        /// <summary>
        /// "Duplicate {0} argument"
        /// </summary>
        public static string DuplicateArgument(string arg0) { return GetMessage("DuplicateArgument", arg0); }

        /// <summary>
        /// "Unexpected argument for {0}"
        /// </summary>
        public static string UnexpectedArgumentFor(string arg0) { return GetMessage("UnexpectedArgumentFor", arg0); }

        /// <summary>
        /// "Too many {0} arguments"
        /// </summary>
        public static string TooManyArguments(string arg0) { return GetMessage("TooManyArguments", arg0); }

        /// <summary>
        /// "Unknown parameter: {0}"
        /// </summary>
        public static string UnknownParameter(string arg0) { return GetMessage("UnknownParameter", arg0); }

        /// <summary>
        /// "Invalid parameters"
        /// </summary>
        public static string InvalidParameters { get { return GetMessage("InvalidParameters"); } }

        /// <summary>
        /// "Value must be positive"
        /// </summary>
        public static string ValueMustBePositive { get { return GetMessage("ValueMustBePositive"); } }

        /// <summary>
        /// Retrieves a single message from the resource manager
        /// </summary>
        /// <param name="name">Resource identifier</param>
        /// <returns>Resource text</returns>
        public static string GetMessage(string name)
        {
            if (resourceManager == null)
            {
                resourceManager = new ResourceManager("MWLib.MWLibResources", Assembly.GetExecutingAssembly());
            }
            return resourceManager.GetString(name);
        }

        /// <summary>
        /// Retrieves a single formatted message from the resource manager
        /// </summary>
        /// <param name="name">Resource identifier</param>
        /// <param name="args">Formatting arguments</param>
        /// <returns>Resource text</returns>
        public static string GetMessage(string name, params object[] args)
        {
            return String.Format(GetMessage(name), args);
        }

        /// <summary>
        /// Resource manager instance
        /// </summary>
        /// <seealso cref="GetMessage(string)"/>
        /// <seealso cref="GetMessage(string, object[])"/>
        private static ResourceManager resourceManager;
    }
}
