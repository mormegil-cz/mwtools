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
using System.Globalization;
using System.IO;
using System.Text;
using ICSharpCode.SharpZipLib.GZip;

namespace MWLib.IO
{
    /// <summary>
    /// Tools for various file operations in MWLib
    /// </summary>
    public static class DataFileTools
    {
        /// <summary>
        /// Detected file type
        /// </summary>
        private enum FileType
        {
            /// <summary>
            /// No known type detected (or file does not exist)
            /// </summary>
            None = 0,

            /// <summary>
            /// Plain text SQL script
            /// </summary>
            SQL = 1,

            /// <summary>
            /// XML
            /// </summary>
            XML = 2,

            /// <summary>
            /// GZipped data
            /// </summary>
            GZip = 101,

            /// <summary>
            /// A .7z file
            /// </summary>
            SevenZip = 102,
        }

        /// <summary>
        /// Detects type of file
        /// </summary>
        /// <param name="fileName">Filename of the file to be tested</param>
        /// <returns>One of detected file types or <see cref="FileType.None"/> if the file does not exist or has unknown type</returns>
        private static FileType DetectFileType(string fileName)
        {
            try
            {
                using (var stream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read, 8))
                {
                    int b1, b2;
                    b1 = stream.ReadByte();
                    b2 = stream.ReadByte();
                    if (b1 == 31 && b2 == 139) return FileType.GZip;
                    if (b1 == 0x37 && b2 == 0x7a) return FileType.SevenZip;
                    if (b1 == 0x2d && b2 == 0x2d) return FileType.SQL;
                    if (b1 == 0x3c && b2 == 0x6d) return FileType.XML;
                    return FileType.None;
                }
            }
            catch (IOException)
            {
                return FileType.None;
            }
        }

        /// <summary>
        /// Supported extensions automatically appended to file names
        /// </summary>
        private static readonly string[] suffixes = {"", ".sql.gz", ".sql", ".gz", ".xml", ".xml.7z"};

        /// <summary>
        /// Open a file with optional automatic decompression
        /// </summary>
        /// <param name="fileName">Filename</param>
        /// <returns>Input stream for the file contents (after decompression, if required)</returns>
        /// <remarks>
        /// This method tries detects the file type and if the file is compressed, it automatically supplies
        /// the required decompressor. If the file does not exist, it tries the same using a set of supported
        /// extensions.
        /// </remarks>
        public static Stream OpenInputFile(string fileName)
        {
            // empty string means standard input
            if (String.IsNullOrEmpty(fileName))
            {
                return Console.OpenStandardInput();
            }

            foreach (string suffix in suffixes)
            {
                string fname = fileName + suffix;
                Stream compressedStream = null;
                switch (DetectFileType(fname))
                {
                    case FileType.None:
                        break;

                    case FileType.GZip:
                        try
                        {
                            compressedStream = new FileStream(fname, FileMode.Open, FileAccess.Read, FileShare.Read);
                            return new GZipInputStream(compressedStream);
                        }
                        catch
                        {
                            if (compressedStream != null) compressedStream.Dispose();
                            throw;
                        }

                    case FileType.SevenZip:
                        try
                        {
                            compressedStream = new FileStream(fname, FileMode.Open, FileAccess.Read, FileShare.Read);
                            return new SevenZipFileStream(compressedStream);
                        }
                        catch
                        {
                            if (compressedStream != null) compressedStream.Dispose();
                            throw;
                        }

                    case FileType.SQL:
                    case FileType.XML:
                        return new FileStream(fname, FileMode.Open, FileAccess.Read, FileShare.Read);

                    default:
                        throw new InvalidDataException(LibraryResources.UnsupportedFileType);
                }
            }
            throw new IOException(String.Format(CultureInfo.CurrentCulture, LibraryResources.CannotFindUsableFile, fileName));
        }

        public static string DecompressGZippedString(string gzipped)
        {
            byte[] data = Encoding.Default.GetBytes(gzipped);
            using (var decompressor = new GZipInputStream(new MemoryStream(data, false)))
            {
                var result = new StringBuilder();
                var buffer = new byte[10240];

                while (true)
                {
                    int read = decompressor.Read(buffer, 0, buffer.Length);
                    if (read <= 0) break;
                    result.Append(Encoding.Default.GetChars(buffer, 0, read));
                }

                return result.ToString();
            }
        }

        /// <summary>
        /// Parse date/time in formats YYYYMMDD, or YYYYMMDDHHMMSS
        /// </summary>
        /// <param name="value">Date/time string</param>
        /// <returns>Parsed date/time</returns>
        /// <exception cref="FormatException">When <paramref name="value"/> is not properly formatted</exception>
        public static DateTime ParseDateTime(string value)
        {
            if (value == null) throw new ArgumentNullException("value");
            if (value.Length != 8 && value.Length != 14) throw new FormatException(LibraryResources.UnsupportedDateTimeFormat);

            int y = Convert.ToInt32(value.Substring(0, 4), CultureInfo.InvariantCulture);
            int m = Convert.ToInt32(value.Substring(4, 2), CultureInfo.InvariantCulture);
            int d = Convert.ToInt32(value.Substring(6, 2), CultureInfo.InvariantCulture);
            if (value.Length > 8)
            {
                int hh = Convert.ToInt32(value.Substring(8, 2), CultureInfo.InvariantCulture);
                int mm = Convert.ToInt32(value.Substring(10, 2), CultureInfo.InvariantCulture);
                int ss = Convert.ToInt32(value.Substring(12, 2), CultureInfo.InvariantCulture);
                return new DateTime(y, m, d, hh, mm, ss);
            }
            return new DateTime(y, m, d);
        }
    }
}