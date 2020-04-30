using System;
using System.IO;
using System.Xml;
using MWLib.Parsers;

namespace MWLib.IO
{
    public class PageDataReaderAdapter : ReaderAdapter, IPageDataReader
    {
        public static PageDataReaderAdapter Create(string location)
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
                        var impl = new MySqlConnectionPageDataReader(UriToMySqlConnectionString(uri));
                        string[] pathComponents = uri.AbsolutePath.Split('/');
                        if (pathComponents.Length > 2)
                        {
                            impl.PageName = String.Join("/", pathComponents, 2, pathComponents.Length - 2);
                        }
                        return new PageDataReaderAdapter(impl);

                    default:
                        throw new NotImplementedException(String.Format("Unsupported URL scheme {0}", uri.Scheme));
                }
            }
            else
            {
                Stream stream = null;
                XmlReader xml = null;
                try
                {
                    stream = DataFileTools.OpenInputFile(location);
                    xml = XmlReader.Create(stream, new XmlReaderSettings {XmlResolver = null});
                    return new PageDataReaderAdapter(new RevisionXmlDumpParser(xml), stream, xml);
                }
                catch
                {
                    if (xml != null) xml.Close();
                    if (stream != null) stream.Dispose();
                    throw;
                }
            }
        }

        private IPageDataReader impl;

        private PageDataReaderAdapter(IPageDataReader impl, Stream stream, XmlReader xmlReader)
            : this(impl, new IDisposable[] { stream, xmlReader })
        {
            this.impl = impl;
            UnderlyingStream = stream;
            UnderlyingXmlReader = xmlReader;
        }

        private PageDataReaderAdapter(IPageDataReader impl, params IDisposable[] subfields)
            : base(impl, subfields)
        {
            this.impl = impl;
        }

        public event EventHandler<PageEventArgs> PageStart
        {
            add { impl.PageStart += value; }
            remove { impl.PageStart -= value; }
        }

        public event EventHandler<PageEventArgs> PageComplete
        {
            add { impl.PageComplete += value; }
            remove { impl.PageComplete -= value; }
        }

        public event EventHandler<RevisionEventArgs> RevisionComplete
        {
            add { impl.RevisionComplete += value; }
            remove { impl.RevisionComplete -= value; }
        }

        public event EventHandler<EventArgs> SiteInfoProcessed
        {
            add { impl.SiteInfoProcessed += value; }
            remove { impl.SiteInfoProcessed -= value; }
        }
    }
}
