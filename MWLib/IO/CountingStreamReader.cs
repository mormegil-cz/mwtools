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
using System.IO;
using System.Text;

namespace MWLib.IO
{
    /// <summary>
    /// A stream reader wrapper keeping current position in the stream (current line and column)
    /// </summary>
    public class CountingStreamReader : StreamReader
    {
        /// <summary>
        /// The name of the currently read file
        /// </summary>
        public String FileName
        {
            get { return fileName; }
        }
        private readonly string fileName;

        /// <summary>
        /// The current line number
        /// </summary>
        public int Line
        {
            get { return line; }
        }
        private int line;

        /// <summary>
        /// The current column number
        /// </summary>
        public int Column
        {
            get { return column; }
        }
        private int column;

        /// <summary>
        /// A linebreak has been detected, advance counters to another line
        /// </summary>
        private void AdvanceLine()
        {
            ++line;
            column = 1;
        }

        /// <summary>
        /// Process one read character
        /// </summary>
        /// <param name="ch">The character read from the stream</param>
        private void ProcessChar(char ch)
        {
            if (ch == '\n') AdvanceLine();
            else ++column;
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="fileName">Name of the file read</param>
        /// <param name="stream">Input stream</param>
        /// <param name="encoding">Encoding to be used for reading the stream</param>
        public CountingStreamReader(string fileName, Stream stream, Encoding encoding) : base(stream, encoding)
        {
            this.fileName = fileName;
            line = 1;
            column = 1;
        }

        /// <summary>
        /// Read one character
        /// </summary>
        /// <returns>The character read from the stream</returns>
        /// <seealso cref="TextReader.Read()"/>
        public override int Read()
        {
            int c = base.Read();
            if (c > 0) ProcessChar((char) c);
            return c;
        }

        /// <summary>
        /// Read a block of characters from the stream
        /// </summary>
        /// <param name="buffer">Buffer to be filled with read data</param>
        /// <param name="index">Index into buffer from which the data should be written</param>
        /// <param name="count">Number of characters to be read</param>
        /// <returns>Number of characters read</returns>
        /// <seealso cref="TextReader.ReadBlock"/>
        public override int ReadBlock(char[] buffer, int index, int count)
        {
            if (buffer == null) throw new ArgumentNullException("buffer");

            int result = base.ReadBlock(buffer, index, count);
            for (int i = 0; i < result; ++i)
            {
                ProcessChar(buffer[i + index]);
            }
            return result;
        }

        /// <summary>
        /// Read a line from the strea
        /// </summary>
        /// <returns>The line read, null if end of stream has been reached</returns>
        /// <seealso cref="TextReader.ReadLine"/>
        public override string ReadLine()
        {
            string s = base.ReadLine();
            if (s == null) return null;
            foreach (char c in s) ProcessChar(c);
            AdvanceLine();
            return s;
        }

        /// <summary>
        /// Reads all remaining data from the stream
        /// </summary>
        /// <returns>The remaining text in the stream</returns>
        /// <seealso cref="TextReader.ReadToEnd"/>
        public override string ReadToEnd()
        {
            string s = base.ReadToEnd();
            foreach (char c in s) ProcessChar(c);
            return s;
        }
    }
}