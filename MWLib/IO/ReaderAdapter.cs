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
using System.IO;
using System.Text;
using System.Xml;
using MWLib.Parsers;

namespace MWLib.IO
{
    /// <summary>
    /// An abstract superclass for adapters able to decide which parser implementation to choose
    /// </summary>
    /// <seealso cref="IParser"/>
    public abstract class ReaderAdapter : IParser, IDisposable
    {
        private readonly IParser impl;
        private readonly IDisposable[] subfields;

        /// <summary>
        /// Underlying stream from which this reader reads, or <c>null</c> if this adapter does not use a stream
        /// </summary>
        public Stream UnderlyingStream { get; protected set; }

        /// <summary>
        /// Underlying stream reader from which this reader reads, or <c>null</c> if this adapter does not use a stream reader
        /// </summary>
        public CountingStreamReader UnderlyingStreamReader { get; protected set; }

        /// <summary>
        /// Underlying XML reader from which this reader reads, or <c>null</c> if this adapter does not use a XML reader
        /// </summary>
        public XmlReader UnderlyingXmlReader { get; protected set; }

        /// <summary>
        /// Initialization
        /// </summary>
        /// <param name="impl">Implementation of the parser to be used by this adapter</param>
        /// <param name="subfields">List of owned disposable items to be disposed during finalization of this object</param>
        protected ReaderAdapter(IParser impl, params IDisposable[] subfields)
        {
            this.impl = impl;
            this.subfields = subfields;
        }

        /// <summary>
        /// Parse the respective data
        /// </summary>
        /// <seealso cref="IParser.Parse"/>
        public virtual void Parse()
        {
            impl.Parse();
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                var disposableImpl = impl as IDisposable;
                if (disposableImpl != null) disposableImpl.Dispose();
                if (subfields != null) foreach (IDisposable subfield in subfields) subfield.Dispose();
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected static string UriToMySqlConnectionString(Uri uri)
        {
            var connectionString = new StringBuilder();

            connectionString.Append("Allow Batch=false;Database=");
            connectionString.Append(uri.AbsolutePath.Split('/')[1]);
            if (!String.IsNullOrEmpty(uri.UserInfo))
            {
                string[] info = uri.UserInfo.Split(':');
                switch (info.Length)
                {
                    case 1:
                        connectionString.Append(";Username=");
                        connectionString.Append(uri.UserInfo);
                        break;
                    case 2:
                        connectionString.Append(";Username=");
                        connectionString.Append(info[0]);
                        connectionString.Append(";Password=");
                        connectionString.Append(info[1]);
                        break;
                    default:
                        throw new FormatException("Invalid syntax of user credentials");
                }
            }

            switch (uri.Scheme.ToUpperInvariant())
            {
                case "MYSQL":
                case "MYSQL-TCP":
                    connectionString.Append(";Server=");
                    connectionString.Append(uri.Host);
                    if (uri.Port > 0)
                    {
                        connectionString.Append(";Port=");
                        connectionString.Append(uri.Port);
                    }
                    break;

                case "MYSQL-MEMORY":
                    connectionString.Append(";Shared Memory Name=");
                    connectionString.Append(uri.Host);
                    break;

                case "MYSQL-PIPE":
                    connectionString.Append(";Pipe=");
                    connectionString.Append(uri.Host);
                    break;

                default:
                    throw new NotImplementedException(String.Format("Unsupported URL scheme {0}", uri.Scheme));
            }

            return connectionString.ToString();
        }
    }
}