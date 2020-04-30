using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml;

namespace MWLib.Parsers
{
    /// <summary>
    /// Parser of XML revision dumps (database dumps, XML exports)
    /// </summary>
    public class RevisionXmlDumpParser : PageDataReader
    {
        /// <summary>
        /// Sitename
        /// </summary>
        public string Sitename;

        /// <summary>
        /// Base URI
        /// </summary>
        public Uri Base;

        /// <summary>
        /// Database name
        /// </summary>
        public string DbName;

        /// <summary>
        /// Name of the software that generated the dump
        /// </summary>
        public string Generator;

        /// <summary>
        /// Case-sensitivity specification
        /// </summary>
        public string Case;

        /// <summary>
        /// Dictionary of namespaces (namespace index -> namespace title)
        /// </summary>
        public Dictionary<int, string> Namespaces;

        /// <summary>
        /// The XML reader providing the dump data
        /// </summary>
        private readonly XmlReader xml;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="xml">The reader providing the dump XML data</param>
        public RevisionXmlDumpParser(XmlReader xml)
        {
            this.xml = xml;
        }

        /// <summary>
        /// Process the dump
        /// </summary>
        /// <remarks>
        /// The method reads the dump, raising the events during the process. After the method call returns, the
        /// dump will have been read completely.
        /// </remarks>
        public override void Parse()
        {
            xml.ReadStartElement("mediawiki");
            xml.ReadStartElement("siteinfo");
            while (xml.Read() && xml.NodeType != XmlNodeType.EndElement)
            {
                if (xml.NodeType != XmlNodeType.Element) throw new XmlException("An element expected");
                switch (xml.Name)
                {
                    case "sitename":
                        if (Sitename != null) throw new XmlException("Duplicate <sitename> element");
                        Sitename = xml.ReadElementContentAsString();
                        break;
                    case "base":
                        if (Base != null) throw new XmlException("Duplicate <base> element");
                        Base = new Uri(xml.ReadElementContentAsString());
                        break;
                    case "dbname":
                        if (DbName != null) throw new XmlException("Duplicate <dbname> element");
                        DbName = xml.ReadElementContentAsString();
                        break;
                    case "generator":
                        if (Generator != null) throw new XmlException("Duplicate <generator> element");
                        Generator = xml.ReadElementContentAsString();
                        break;
                    case "case":
                        if (Case != null) throw new XmlException("Duplicate <generator> element");
                        Case = xml.ReadElementContentAsString();
                        break;
                    case "namespaces":
                        if (Namespaces != null) throw new XmlException("Duplicate <namespaces> element");
                        Namespaces = new Dictionary<int, string>();
                        while (xml.Read() && xml.IsStartElement("namespace"))
                        {
                            string key = xml.GetAttribute("key");
                            string value = xml.ReadElementContentAsString();
                            Namespaces.Add(Int32.Parse(key, CultureInfo.InvariantCulture), value);
                        }
                        xml.ReadEndElement();
                        break;
                    default:
                        throw new XmlException("Unexpected element: '" + xml.Name + "'");
                }
            }
            if (xml.Name != "siteinfo") throw new XmlException("</siteinfo> expected");
            OnSiteInfoProcessed();
            xml.Read();
            while (xml.MoveToContent() == XmlNodeType.Element && xml.IsStartElement("page"))
            {
                string pageTitle = null;
                string nsFromXml = null;
                string pageId = null;
                string pageSha1 = null;
                string restrictions = null;
                bool isRedirect = false;
                xml.Read();
                while (xml.MoveToContent() == XmlNodeType.Element && !xml.IsStartElement("revision"))
                {
                    switch (xml.Name)
                    {
                        case "title":
                            if (pageTitle != null) throw new XmlException("Duplicate <title> element");
                            pageTitle = xml.ReadElementContentAsString();
                            break;
                        case "ns":
                            if (nsFromXml != null) throw new XmlException("Duplicate <ns> element");
                            nsFromXml = xml.ReadElementContentAsString();
                            break;
                        case "id":
                            if (pageId != null) throw new XmlException("Duplicate <id> element");
                            pageId = xml.ReadElementContentAsString();
                            break;
                        case "sha1":
                            if (pageSha1 != null) throw new XmlException("Duplicate <sha1> element");
                            pageSha1 = xml.ReadElementContentAsString();
                            break;
                        case "restrictions":
                            if (restrictions != null) throw new XmlException("Duplicate <restrictions> element");
                            restrictions = xml.ReadElementContentAsString();
                            break;
                        case "redirect":
                            if (!xml.IsEmptyElement) throw new XmlException("Unexpected contents of <redirect>");
                            isRedirect = true;
                            break;
                        default:
                            throw new XmlException("Unexpected element: '" + xml.Name + "'");
                    }
                    xml.Read();
                }
                if (pageTitle == null) throw new XmlException("Required <title> element missing");
                if (pageId == null) throw new XmlException("Required <id> element missing");
                Namespace ns;
                string title;
                Page.ParseTitle(pageTitle, Namespaces, out ns, out title);
                var page = new Page(ns, title, Int32.Parse(pageId, CultureInfo.InvariantCulture), isRedirect);
                OnPageStart(page);
                while (xml.IsStartElement("revision"))
                {
                    int? revisionId = null;
                    int? parentId = null;
                    DateTime? timestamp = null;
                    bool minor = false;
                    User contributor = null;
                    string comment = null;
                    string sha1 = null;
                    string text = null;
                    string model = null;
                    string format = null;
                    xml.Read();
                    while (xml.MoveToContent() != XmlNodeType.EndElement)
                    {
                        if (xml.NodeType != XmlNodeType.Element) throw new XmlException("An element expected");
                        switch (xml.Name)
                        {
                            case "id":
                                if (revisionId != null) throw new XmlException("Duplicate <id> element");
                                revisionId = xml.ReadElementContentAsInt();
                                break;
                            case "parentid":
                                if (parentId != null) throw new XmlException("Duplicate <parentid> element");
                                parentId = xml.ReadElementContentAsInt();
                                break;
                            case "timestamp":
                                if (timestamp != null) throw new XmlException("Duplicate <timestamp> element");
                                timestamp = xml.ReadElementContentAsDateTime();
                                break;
                            case "minor":
                                if (minor) throw new XmlException("Duplicate <minor> element");
                                if (!xml.IsEmptyElement) throw new XmlException("Unexpected contents of <minor>");
                                minor = true;
                                break;
                            case "contributor":
                                if (contributor != null) throw new XmlException("Duplicate <contributor> element");
                                string userIp = null;
                                string userId = null;
                                string userName = null;
                                if (xml.HasAttributes && xml.GetAttribute("deleted") == "deleted")
                                {
                                    userIp = "#deleted#";
                                }
                                else
                                {
                                    xml.Read();
                                    while (xml.MoveToContent() != XmlNodeType.EndElement)
                                    {
                                        if (xml.NodeType != XmlNodeType.Element) throw new XmlException("An element expected");
                                        switch (xml.Name)
                                        {
                                            case "id":
                                                if (userId != null) throw new XmlException("Duplicate <id> element");
                                                userId = xml.ReadElementContentAsString();
                                                break;
                                            case "ip":
                                                if (userIp != null) throw new XmlException("Duplicate <ip> element");
                                                userIp = xml.ReadElementContentAsString();
                                                break;
                                            case "username":
                                                if (userName != null) throw new XmlException("Duplicate <username> element");
                                                userName = xml.ReadElementContentAsString();
                                                break;
                                        }
                                        xml.Read();
                                    }
                                    if (xml.Name != "contributor") throw new XmlException("</contributor> expected");
                                }
                                if (userIp != null)
                                {
                                    if (userId != null || userName != null) throw new XmlException("User cannot be both anonymous and registered");
                                    contributor = new AnonymousUser(userIp);
                                }
                                else
                                {
                                    if (userId == null || userName == null) throw new XmlException("Required contributor subelements missing");
                                    contributor = new RegisteredUser(userName, Int32.Parse(userId, CultureInfo.InvariantCulture));
                                }
                                break;
                            case "comment":
                                if (comment != null) throw new XmlException("Duplicate <comment> element");
                                comment = xml.ReadElementContentAsString();
                                break;
                            case "sha1":
                                if (sha1 != null) throw new XmlException("Duplicate <sha1> element");
                                sha1 = xml.ReadElementContentAsString();
                                break;
                            case "model":
                                if (model != null) throw new XmlException("Duplicate <model> element");
                                model = xml.ReadElementContentAsString();
                                break;
                            case "format":
                                if (format != null) throw new XmlException("Duplicate <format> element");
                                format = xml.ReadElementContentAsString();
                                break;
                            case "text":
                                if (text != null) throw new XmlException("Duplicate <text> element");
                                text = xml.ReadElementContentAsString();
                                break;
                            default:
                                throw new XmlException("Unexpected element: '" + xml.Name + "'");
                        }
                        xml.Read();
                    }
                    if (xml.Name != "revision") throw new XmlException("</revision> expected");
                    if (revisionId == null) throw new XmlException("Required <id> element missing");
                    if (contributor == null) throw new XmlException("Required <contributor> element missing");
                    if (text == null) throw new XmlException("Required <text> element missing");
                    if (timestamp == null) throw new XmlException("Required <timestamp> element missing");
                    OnRevisionComplete(new Revision(page, revisionId.Value, parentId ?? 0, (DateTime) timestamp, minor, contributor, comment, model, format, text));
                    xml.Read();
                }
                if (xml.NodeType != XmlNodeType.EndElement || xml.Name != "page") throw new XmlException("</page> expected");
                OnPageComplete(page);
                xml.Read();
            }
            if (xml.Name != "mediawiki" || xml.NodeType != XmlNodeType.EndElement) throw new XmlException("</mediawiki> expected");
            while (xml.Read())
            {
                if (xml.NodeType != XmlNodeType.Whitespace) throw new XmlException("Unexpected contents");
            }
        }
    }
}
