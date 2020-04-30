using System;
using System.Data;
using System.Linq;
using System.Text;
using MWLib.IO;
using MySql.Data.MySqlClient;

namespace MWLib.Parsers
{
    public class MySqlConnectionPageDataReader : PageDataReader
    {
        // private readonly MySqlClientFactory factory;
        private readonly string connectionString;

        public string PageName { get; set; }

        public DateTime? MinDateTime { get; set; }
        public DateTime? MaxDateTime { get; set; }

        public string DbTablePrefix { get; set; }

        public MySqlConnectionPageDataReader(/*string providerName, */string connectionString)
        {
            if (String.IsNullOrEmpty(connectionString)) throw new ArgumentException("Connection string required", "connectionString");
            /*
            if (String.IsNullOrEmpty(providerName))
            {
                factory = MySqlClientFactory.Instance;
            }
            else
            {
                factory = DbProviderFactories.GetFactory(providerName) as MySqlClientFactory;
                if (factory == null) throw new ArgumentException("Unsupported provider type", "providerName");
            }
            */
            this.connectionString = connectionString;
        }

        /// <summary>
        /// Parse the specified data
        /// </summary>
        public override void Parse()
        {
            OnSiteInfoProcessed();

            using (var pageConnection = new MySqlConnection(connectionString))
            {
                pageConnection.Open();

                using (var pageCommand = new MySqlCommand())
                {
                    pageCommand.Connection = pageConnection;

                    var pageCmdBuilder = new StringBuilder();
                    pageCmdBuilder.Append("SELECT page_id, page_namespace, page_title FROM ");
                    pageCmdBuilder.Append(DbTablePrefix);
                    pageCmdBuilder.Append("page");
                    if (!String.IsNullOrEmpty(PageName))
                    {
                        var where = new StringBuilder();
                        if (!String.IsNullOrEmpty(PageName))
                        {
                            Namespace ns;
                            string title;
                            Page.ParseTitle(PageName, out ns, out title);
                            where.Append("page_title=?pagename AND page_namespace=?namespace");
                            pageCommand.Parameters.AddWithValue("namespace", ns);
                            pageCommand.Parameters.AddWithValue("pagename", title);
                        }

                        pageCmdBuilder.Append(" WHERE ");
                        pageCmdBuilder.Append(where);
                    }
                    pageCommand.CommandText = pageCmdBuilder.ToString();

                    using (MySqlDataReader pageReader = pageCommand.ExecuteReader(CommandBehavior.SequentialAccess))
                    {
                        while (pageReader.Read())
                        {
                            int pageId = pageReader.GetInt32("page_id");
                            var ns = (Namespace)pageReader.GetInt32("page_namespace");
                            string title = pageReader.GetString("page_title");
                            bool isRedirect = pageReader.GetBoolean("page_is_redirect");
                            var page = new Page(ns, title, pageId, isRedirect);
                            OnPageStart(page);

                            using (var revisionConnection = new MySqlConnection(connectionString))
                            {
                                revisionConnection.Open();

                                using (var revisionCommand = new MySqlCommand())
                                {
                                    revisionCommand.Connection = revisionConnection;

                                    var revisionCmdBuilder = new StringBuilder();
                                    revisionCmdBuilder.Append("SELECT rev_id, rev_parent_id, rev_timestamp, rev_minor_edit, rev_user, rev_user_text, rev_comment, rev_content_model, rev_content_format, old_text, old_flags FROM ");
                                    revisionCmdBuilder.Append(DbTablePrefix);
                                    revisionCmdBuilder.Append("revision INNER JOIN ");
                                    revisionCmdBuilder.Append(DbTablePrefix);
                                    revisionCmdBuilder.Append("text ON old_id=rev_text_id WHERE rev_page=?pageid");
                                    revisionCommand.Parameters.AddWithValue("pageid", pageId);

                                    if (MinDateTime != null || MaxDateTime != null)
                                    {
                                        var where = new StringBuilder();
                                        if (MinDateTime != null)
                                        {
                                            where.Append("rev_timestamp>=?mintimestamp");
                                            revisionCommand.Parameters.AddWithValue("mintimestamp", MinDateTime.Value.ToString("yyyyMMddHHmmss"));
                                        }
                                        if (MaxDateTime != null)
                                        {
                                            if (where.Length != 0) where.Append(" AND ");
                                            where.Append("rev_timestamp<=?maxtimestamp");
                                            revisionCommand.Parameters.AddWithValue("maxtimestamp", MaxDateTime.Value.ToString("yyyyMMddHHmmss"));
                                        }

                                        revisionCmdBuilder.Append(" WHERE ");
                                        revisionCmdBuilder.Append(where);
                                    }

                                    revisionCommand.CommandText = revisionCmdBuilder.ToString();

                                    using (MySqlDataReader revisionReader = revisionCommand.ExecuteReader(CommandBehavior.SequentialAccess))
                                    {
                                        while (revisionReader.Read())
                                        {
                                            int revisionId = revisionReader.GetInt32("rev_id");
                                            int parentId = revisionReader.GetInt32("rev_parent_id");
                                            DateTime timestamp = DataFileTools.ParseDateTime(revisionReader.GetString("rev_timestamp"));
                                            bool minor = revisionReader.GetBoolean("rev_minor_edit");
                                            int userId = revisionReader.GetInt32("rev_user");
                                            string userText = revisionReader.GetString("rev_user_text");
                                            string comment = revisionReader.GetString("rev_comment");
                                            string model = revisionReader.GetString("rev_content_model");
                                            string format = revisionReader.GetString("rev_content_format");
                                            string text = revisionReader.GetString("old_text");
                                            string textFlags = revisionReader.GetString("old_flags");

                                            User contributor;
                                            if (userId == 0)
                                                contributor = new AnonymousUser(userText);
                                            else
                                                contributor = new RegisteredUser(userText, userId);

                                            if (!String.IsNullOrEmpty(textFlags))
                                            {
                                                string[] flags = textFlags.Split(',');

                                                if (flags.Contains("gzip"))
                                                {
                                                    text = DataFileTools.DecompressGZippedString(text);
                                                }

                                                if (flags.Contains("object"))
                                                {
                                                    throw new NotImplementedException("PHP serialized objects not supported");
                                                }

                                                if (flags.Contains("utf-8"))
                                                {
                                                    byte[] binary = Encoding.Default.GetBytes(text);
                                                    text = new string(Encoding.UTF8.GetChars(binary));
                                                }
                                                else
                                                {
                                                    throw new NotImplementedException("Legacy encoding not supported");
                                                }
                                            }

                                            OnRevisionComplete(new Revision(page, revisionId, parentId, timestamp, minor, contributor, comment, model, format, text));
                                        }
                                    }
                                }
                            }

                            OnPageComplete(page);
                        }
                    }
                }
            }
        }
    }
}
