using System;
using System.IO;
using System.Text;
using MWLib.Parsers;

namespace MWLib.IO
{
    public class TableReaderAdapter : ReaderAdapter, IDatabaseTableReader
    {
        public static TableReaderAdapter Create(string location)
        {
            if (location == null) throw new ArgumentNullException("location");

            if (location.Contains("://"))
            {
                var uri = new Uri(location);
                switch (uri.Scheme.ToUpperInvariant())
                {
                    case "MYSQL":
                    case "MYSQL-TCP":
                    case "MYSQL-MEMORY":
                    case "MYSQL-PIPE":
                        string[] pathComponents = uri.AbsolutePath.Split('/');
                        if (pathComponents.Length <= 2) throw new ArgumentException("Table name is required");
                        var impl = new MySqlConnectionTableReader(UriToMySqlConnectionString(uri), String.Join("/", pathComponents, 2, pathComponents.Length - 2));
                        return new TableReaderAdapter(impl);

                    default:
                        throw new NotImplementedException(String.Format("Unsupported URL scheme {0}", uri.Scheme));
                }
            }
            else
            {
                Stream stream = null;
                CountingStreamReader reader = null;
                try
                {
                    stream = DataFileTools.OpenInputFile(location);
                    reader = new CountingStreamReader(location, stream, Encoding.UTF8);
                    return new TableReaderAdapter(new MySqlDumpParser(reader), stream, reader);
                }
                catch
                {
                    if (reader != null) reader.Close();
                    if (stream != null) stream.Dispose();
                    throw;
                }
            }
        }

        private IDatabaseTableReader impl;

        private TableReaderAdapter(IDatabaseTableReader impl, Stream stream, CountingStreamReader streamReader)
            : this(impl, new IDisposable[] { stream, streamReader })
        {
            this.impl = impl;
            UnderlyingStream = stream;
            UnderlyingStreamReader = streamReader;
        }

        private TableReaderAdapter(IDatabaseTableReader impl, params IDisposable[] subfields)
            : base(impl, subfields)
        {
            this.impl = impl;
        }

        public event EventHandler<RowEventArgs> RowComplete
        {
            add { impl.RowComplete += value; }
            remove { impl.RowComplete -= value; }
        }
    }
}
