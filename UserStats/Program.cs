using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Permissions;
using System.Xml;
using MWLib;
using MWLib.IO;
using MWLib.Parsers;

[assembly: CLSCompliant(true)]
[assembly: SecurityPermission(SecurityAction.RequestMinimum, Execution = true)]
namespace UserStats
{
    /// <summary>
    /// Record storing data about one group of user’s edits (e.g. edits in the main namespace).
    /// </summary>
    internal struct UserDataColumn
    {
        /// <summary>
        /// Number of edits
        /// </summary>
        public int Edits;

        /// <summary>
        /// Number of revisions
        /// </summary>
        public int Revisions;

        /// <summary>
        /// Number of minor edits
        /// </summary>
        public int Minors;

        /// <summary>
        /// Total difference in size (bytes)
        /// </summary>
        public int DSize;

        /// <summary>
        /// Newly created pages
        /// </summary>
        public int NewPages;

        /// <summary>
        /// Number of edits in the recent time
        /// </summary>
        public int RecentEdits;

        /// <summary>
        /// Number of revisions in the recent time
        /// </summary>
        public int RecentRevisions;

        /// <summary>
        /// Number of minor edits in the recent time
        /// </summary>
        public int RecentMinors;

        /// <summary>
        /// Total difference in size in the recent time (bytes)
        /// </summary>
        public int RecentDSize;

        /// <summary>
        /// Number of pages created in the recent time
        /// </summary>
        public int RecentNewPages;

        /// <summary>
        /// Date/time of the first edit
        /// </summary>
        public DateTime FirstEdit;

        /// <summary>
        /// Date/time of the last edit
        /// </summary>
        public DateTime LastEdit;

        /// <summary>
        /// Sum of span size (for evaluating average span size)
        /// </summary>
        private int SumSpanSize;

        /// <summary>
        /// Sum of edit summary length (for evaluating average span size)
        /// </summary>
        private int SumCommentLen;

        /// <summary>
        /// Current length of the edit span in the current edit
        /// </summary>
        private int CurrSpan;

        /// <summary>
        /// Has this user have an edit recently?
        /// </summary>
        private bool HasRecentEdit;

        /// <summary>
        /// Average edit span (i.e. number of revisions in an edit)
        /// </summary>
        public decimal AverageSpan
        {
            get { return Edits == 0 ? 0 : (decimal)SumSpanSize / Edits; }
        }

        /// <summary>
        /// Average length of an edit summary (bytes)
        /// </summary>
        public decimal AverageComment
        {
            get { return Revisions == 0 ? 0 : (decimal)SumCommentLen / Revisions; }
        }

        /// <summary>
        /// Register a revision
        /// </summary>
        /// <param name="timestamp">Date/time of the revision</param>
        /// <param name="textDiff">Size of the text difference in the revision</param>
        /// <param name="commentLen">Length of the edit summary</param>
        /// <param name="minor">Is this edit marked as minor?</param>
        /// <param name="recentEdit">Is this edit recent?</param>
        /// <param name="newPage">Is this a new page creation?</param>
        public void AddRevision(DateTime timestamp, int textDiff, int commentLen, bool minor, bool recentEdit, bool newPage)
        {
            if (Revisions == 0 || FirstEdit.CompareTo(timestamp) > 0)
            {
                FirstEdit = timestamp;
            }
            if (Revisions == 0 || LastEdit.CompareTo(timestamp) < 0)
            {
                LastEdit = timestamp;
            }

            ++Revisions;
            ++CurrSpan;
            DSize += textDiff;
            if (minor) ++Minors;
            SumCommentLen += commentLen;
            if (recentEdit)
            {
                HasRecentEdit = true;
                ++RecentRevisions;
                RecentDSize += textDiff;
                if (minor) ++RecentMinors;
            }
            if (newPage)
            {
                ++NewPages;
                if (recentEdit) ++RecentNewPages;
            }
        }

        /// <summary>
        /// Register end of the current edit
        /// </summary>
        public void EndOfEdit()
        {
            if (CurrSpan == 0) throw new InvalidOperationException("Unexpected end of edit");

            ++Edits;
            SumSpanSize += CurrSpan;
            if (HasRecentEdit) ++RecentEdits;

            CurrSpan = 0;
            HasRecentEdit = false;
        }

        /// <summary>
        /// Write the data of this group to XML
        /// </summary>
        /// <param name="writer">XML writer the data should be written to</param>
        /// <param name="groupName">Name of this group</param>
        public void WriteXml(XmlWriter writer, string groupName)
        {
            writer.WriteStartElement("group");
            writer.WriteAttributeString("name", groupName);
            writer.WriteStartElement("firstedit");
            writer.WriteValue(FirstEdit);
            writer.WriteEndElement();
            writer.WriteStartElement("lastedit");
            writer.WriteValue(LastEdit);
            writer.WriteEndElement();
            writer.WriteElementString("edits", Edits.ToString(CultureInfo.InvariantCulture));
            writer.WriteElementString("revisions", Revisions.ToString(CultureInfo.InvariantCulture));
            writer.WriteElementString("minors", Minors.ToString(CultureInfo.InvariantCulture));
            writer.WriteElementString("dsize", DSize.ToString(CultureInfo.InvariantCulture));
            writer.WriteElementString("newpages", NewPages.ToString(CultureInfo.InvariantCulture));
            writer.WriteElementString("recentedits", RecentEdits.ToString(CultureInfo.InvariantCulture));
            writer.WriteElementString("recentrevisions", RecentRevisions.ToString(CultureInfo.InvariantCulture));
            writer.WriteElementString("recentminors", RecentMinors.ToString(CultureInfo.InvariantCulture));
            writer.WriteElementString("recentdsize", RecentDSize.ToString(CultureInfo.InvariantCulture));
            writer.WriteElementString("recentnewpages", RecentNewPages.ToString(CultureInfo.InvariantCulture));
            writer.WriteElementString("averagespan", AverageSpan.ToString("n5", CultureInfo.InvariantCulture));
            writer.WriteElementString("averagecomment", AverageComment.ToString("n5", CultureInfo.InvariantCulture));
            writer.WriteEndElement();
        }
    }

    /// <summary>
    /// Statistical data about a user
    /// </summary>
    internal class UserData
    {
        /// <summary>
        /// User definition
        /// </summary>
        public User User;

        /// <summary>
        /// Is this user a bot?
        /// </summary>
        public bool Bot;

        /// <summary>
        /// Per-namespace user data
        /// </summary>
        public UserDataColumn[] PerNamespace;

        /// <summary>
        /// Summary (total) statistics
        /// </summary>
        public UserDataColumn Total;

        /// <summary>
        /// Statistics for the main namespace
        /// </summary>
        public UserDataColumn Main;

        /// <summary>
        /// Statistics for talk namespaces
        /// </summary>
        public UserDataColumn Talk;

        /// <summary>
        /// Statistics for non-talk, non-main namespaces
        /// </summary>
        public UserDataColumn Others;

        /// <summary>
        /// Last namespace in which the user edited
        /// </summary>
        private Namespace LastNs;

        /// <summary>
        /// Index of the last namespace in which the user edited
        /// </summary>
        private int LastNsIndex;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="user">User</param>
        /// <param name="nsCount">Number of namespaces in the current dump that will be analysed</param>
        public UserData(User user, int nsCount)
        {
            if (user == null) throw new ArgumentNullException("user");
            if (nsCount <= 0) throw new ArgumentOutOfRangeException("nsCount", nsCount, "Namespace count must be greater than zero");
            User = user;
            PerNamespace = new UserDataColumn[nsCount];
            LastNsIndex = -1;
        }

        /// <summary>
        /// Register a revision made by this user
        /// </summary>
        /// <param name="timestamp">Date/time of the revision</param>
        /// <param name="ns">Namespace in which the edit has been made</param>
        /// <param name="nsIndex">Index of the namespace</param>
        /// <param name="textDiff">Size of the text difference (bytes)</param>
        /// <param name="commentLen">Length of the edit comment</param>
        /// <param name="minor">Is this edit marked as minor?</param>
        /// <param name="recentEdit">Is this edit recent?</param>
        /// <param name="newPage">Is this a new page creation?</param>
        public void AddRevision(DateTime timestamp, Namespace ns, int nsIndex, int textDiff, int commentLen, bool minor, bool recentEdit, bool newPage)
        {
            Total.AddRevision(timestamp, textDiff, commentLen, minor, recentEdit, newPage);
            PerNamespace[nsIndex].AddRevision(timestamp, textDiff, commentLen, minor, recentEdit, newPage);
            if (((int)ns) % 2 == 0)
            {
                if (ns == Namespace.Main)
                    Main.AddRevision(timestamp, textDiff, commentLen, minor, recentEdit, newPage);
                else
                    Others.AddRevision(timestamp, textDiff, commentLen, minor, recentEdit, newPage);
            }
            else
            {
                Talk.AddRevision(timestamp, textDiff, commentLen, minor, recentEdit, newPage);
            }
            LastNs = ns;
            LastNsIndex = nsIndex;
        }

        /// <summary>
        /// Register end of the current edit
        /// </summary>
        public void EndOfEdit()
        {
            if (LastNsIndex < 0) throw new InvalidOperationException("Unexpected end of edit");

            Total.EndOfEdit();
            PerNamespace[LastNsIndex].EndOfEdit();
            if (((int)LastNs) % 2 == 0)
            {
                if (LastNs == Namespace.Main)
                    Main.EndOfEdit();
                else
                    Others.EndOfEdit();
            }
            else
            {
                Talk.EndOfEdit();
            }

            LastNsIndex = -1;
        }
    }

    /// <summary>
    /// Exception for signalling an abort of the current process
    /// </summary>
    internal class AbortException : Exception
    {
    }

    /// <summary>
    /// Main class of the program
    /// </summary>
    class UserStatsMain
    {
        /// <summary>
        /// About line for the program
        /// </summary>
        private const string AboutLine = "MediaWiki User Statistics  v0.1  Copyright (c) Petr Kadlec, 2005-2007";

        /// <summary>
        /// XML namespace used in the resulting XML file
        /// </summary>
        private const string UserStatsXmlNamespace = "http://mormegil.wz.cz/prog/mwtools/namespace/userstats";

        /// <summary>
        /// Size of the title field in the progress text
        /// </summary>
        private const int ProgressTitleWidth = 40;

        /// <summary>
        /// Limit for two consecutive edits by the same user to be considered a repeated edit [minutes]
        /// </summary>
        private static int RepeatLimit = -1;

        /// <summary>
        /// Name of the input file
        /// </summary>
        private static string inputFilename;

        /// <summary>
        /// Input stream
        /// </summary>
        private static Stream stream;

        /// <summary>
        /// The processed XML revision dump
        /// </summary>
        private static RevisionXmlDumpParser dump;

        /// <summary>
        /// Time the analysis started
        /// </summary>
        private static DateTime start;

        /// <summary>
        /// Time the analysis finished
        /// </summary>
        private static DateTime stop;

        /// <summary>
        /// Length of the input file
        /// </summary>
        private static long length;

        /// <summary>
        /// Timestamp of the last progress update
        /// </summary>
        private static long lastProgressUpdate;

        /// <summary>
        /// Set of usernames of bot users
        /// </summary>
        private static HashSet<string> botList;

        /// <summary>
        /// Suppress some output of the program
        /// </summary>
        private static bool quiet;

        /// <summary>
        /// Should we do per-namespace statistics?
        /// </summary>
        private static bool perNamespace;

        /// <summary>
        /// Time representing beginning of the “recent” period
        /// </summary>
        private static DateTime monthAgo;

        /// <summary>
        /// Map of namespace → index
        /// </summary>
        private static SortedDictionary<Namespace, int> namespaceMap;

        /// <summary>
        /// Currently processed page
        /// </summary>
        private static Page currPage;

        /// <summary>
        /// Title of the currently processed page (length-limited to <see cref="ProgressTitleWidth"/>)
        /// </summary>
        private static string currPageTitle;

        /// <summary>
        /// Number of pages processed
        /// </summary>
        private static int pageCount;

        /// <summary>
        /// Total number of revisions
        /// </summary>
        private static int totalRevisionCount;

        /// <summary>
        /// Number of revisions in the current page
        /// </summary>
        private static int revisionCount;

        /// <summary>
        /// Text of the previous revision of this page
        /// </summary>
        private static string previousVersionText;

        /// <summary>
        /// User owning the previous revision of this page
        /// </summary>
        private static UserData previousUser;

        /// <summary>
        /// Timestamp of the previous revision of this page
        /// </summary>
        private static DateTime lastTimestamp;

        /// <summary>
        /// Collection of all users
        /// </summary>
        private static readonly Dictionary<User, UserData> users = new Dictionary<User, UserData>();

        /// <summary>
        /// Program entry point
        /// </summary>
        /// <param name="args">Command-line arguments</param>
        private static void Main(string[] args)
        {
            try
            {
                int i = 0;
                DateTime? date0 = null;
                try
                {
                    for (i = 0; i < args.Length; ++i)
                    {
                        if (!args[i].StartsWith("-")) break;
                        if (args[i] == "--")
                        {
                            ++i;
                            break;
                        }
                        else if (args[i] == "--quiet" || args[i] == "-q")
                        {
                            if (quiet) ArgumentError("Duplicate --quiet argument.");
                            quiet = true;
                        }
                        else if (args[i] == "--pernamespace")
                        {
                            if (perNamespace) ArgumentError("Duplicate --pernamespace argument.");
                            perNamespace = true;
                        }
                        else if (args[i].StartsWith("--date0="))
                        {
                            if (date0 != null) ArgumentError("Duplicate --date0 argument.");
                            string arg = args[i].Substring(8);
                            date0 = DataFileTools.ParseDateTime(arg);
                        }
                        else if (args[i].StartsWith("--botlist="))
                        {
                            if (botList != null) ArgumentError("Duplicate --botlist argument.");
                            string arg = args[i].Substring(10);
                            botList = LoadBotList(arg);
                        }
                        else if (args[i].StartsWith("--repeatlimit="))
                        {
                            if (RepeatLimit >= 0) ArgumentError("Duplicate --repeatlimit argument.");
                            string arg = args[i].Substring(14);
                            RepeatLimit = Int32.Parse(arg);
                            if (RepeatLimit < 0) ArgumentError("Invalid value of repeat limit");
                        }
                        else if (args[i] == "-V" || args[i] == "--version")
                        {
                            Console.WriteLine(AboutLine);
                            return;
                        }
                        else if (args[i] == "-h" || args[i] == "-?" || args[i] == "--help")
                        {
                            Console.WriteLine("Usage: UserStats [options...] filename");
                            Console.WriteLine("Options:");
                            Console.WriteLine("\t--date0=DATE\tDate of dump creation");
                            Console.WriteLine("\t--botlist=FILENAME\tFile with bot usernames");
                            Console.WriteLine("\t--pernamespace\tOutput per-namespace statistics");
                            Console.WriteLine("\t--repeatlimit=LIMIT\tLimit for repeated edits [minutes]");
                            Console.WriteLine("\t--quiet\tSuppress some output");
                            return;
                        }
                        else ArgumentError("Unknown argument");
                    }
                }
                catch (Exception e)
                {
                    ArgumentError(e.Message);
                }

                if (i != args.Length - 2) ArgumentError("");
                if (date0 == null) date0 = DateTime.Now;
                if (RepeatLimit < 0) RepeatLimit = 20;
                if (botList == null) botList = new HashSet<string>();

                monthAgo = date0.Value.AddDays(-30);
                inputFilename = args[i];
                string outputFilename = args[i + 1];

                Analyze(inputFilename);
                SaveResults(outputFilename);
            }
            catch(Exception e)
            {
                Console.Error.WriteLine("Error: {0}", e);
            }
        }

        /// <summary>
        /// Perform the analysis
        /// </summary>
        /// <param name="filename">Filename of the input file</param>
        private static void Analyze(string filename)
        {
            using (stream = DataFileTools.OpenInputFile(filename))
            {
                var settings = new XmlReaderSettings {XmlResolver = null};
                using (XmlReader xml = XmlReader.Create(stream, settings))
                {
                    dump = new RevisionXmlDumpParser(xml);
                    dump.SiteInfoProcessed += SiteInfoProcessed;
                    dump.PageStart += PageStart;
                    dump.PageComplete += PageComplete;
                    dump.RevisionComplete += RevisionComplete;
                    length = stream.Length;
                    start = DateTime.Now;
                    Console.TreatControlCAsInput = true;
                    try
                    {
                        dump.Parse();
                    }
                    catch(AbortException)
                    {
                        if (!quiet) Console.Error.Write("\nAborted");
                    }
                    stop = DateTime.Now;
                    Console.Error.WriteLine();
                    if (!quiet)
                    {
                        Console.Error.WriteLine("{0} pages ({1} revisions) by {2} users analyzed in {3:n0} s", pageCount, totalRevisionCount, users.Count, (stop - start).TotalSeconds);
                    }
                }
            }
        }

        /// <summary>
        /// Store the results of the analysis
        /// </summary>
        /// <param name="filename">Name of the output file</param>
        private static void SaveResults(string filename)
        {
            if (!quiet) Console.Write("Writing resulting data...");

            var settings = new XmlWriterSettings
                               {
                                   Indent = true,
                                   IndentChars = "\t",
                                   CloseOutput = true
                               };
            using (XmlWriter writer = XmlWriter.Create(filename, settings))
            {
                writer.WriteStartElement("userstats", UserStatsXmlNamespace);

                writer.WriteComment("Generated automatically by: " + AboutLine);
                writer.WriteComment("   on: " + DateTime.Now);
                writer.WriteComment("   from: '" + inputFilename + "'");

                foreach(UserData user in users.Values)
                {
                    writer.WriteStartElement("user");
                    var registered = user.User as RegisteredUser;
                    var anonymous = user.User as AnonymousUser;
                    if (registered != null)
                    {
                        writer.WriteAttributeString("id", registered.Id.ToString(CultureInfo.InvariantCulture));
                        writer.WriteAttributeString("name", registered.UserName);
                    }
                    if (anonymous != null)
                    {
                        writer.WriteAttributeString("ip", anonymous.Ip);
                    }
                    if (user.Bot) writer.WriteElementString("bot", "");

                    user.Main.WriteXml(writer, "main");
                    user.Others.WriteXml(writer, "others");
                    user.Talk.WriteXml(writer, "talk");
                    user.Total.WriteXml(writer, "total");
                    if (perNamespace)
                    {
                        foreach (KeyValuePair<Namespace, int> ns in namespaceMap)
                        {
                            user.PerNamespace[ns.Value].WriteXml(writer, "ns" + ((int) ns.Key).ToString(CultureInfo.InvariantCulture));
                        }
                    }

                    writer.WriteEndElement();
                }

                writer.WriteEndElement();
            }

            if (!quiet) Console.WriteLine("OK");
        }

        /// <summary>
        /// Load the list of bot users from a plaintext file
        /// </summary>
        /// <param name="filename">Name of the file with bot usernames</param>
        /// <returns>Set of bot usernames</returns>
        private static HashSet<string> LoadBotList(string filename)
        {
            var result = new HashSet<string>();
            using (TextReader reader = new StreamReader(filename))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    line = line.Trim();
                    if (line.Length > 0) result.Add(line);
                }
            }
            return result;
        }

        /// <summary>
        /// Report error in program arguments and terminate program
        /// </summary>
        private static void ArgumentError(string msg)
        {
            Console.Error.WriteLine("Invalid arguments. {0}", msg);
            Environment.Exit(2);
        }

        /// <summary>
        /// Event raised after the global site information has been read and processed
        /// </summary>
        private static void SiteInfoProcessed(object sender, EventArgs e)
        {
            var tempMap = new SortedDictionary<Namespace, int>();
            foreach(int ns in dump.Namespaces.Keys)
            {
                if (ns >= 0) tempMap.Add((Namespace) ns, 0);
            }
            namespaceMap = new SortedDictionary<Namespace, int>();
            int idx = 0;
            foreach(Namespace ns in tempMap.Keys)
            {
                namespaceMap.Add(ns, idx++);
            }
        }

        /// <summary>
        /// Event raised after one page has been completed (and all revisions read)
        /// </summary>
        private static void PageComplete(object sender, PageEventArgs e)
        {
            if (currPage == null) throw new InvalidOperationException("Unexpected page complete event");
            //Console.WriteLine("{0}: {1} revisions", currPage, revisionCount);

            if (previousUser != null)
            {
                previousUser.EndOfEdit();
            }

            previousUser = null;
            currPage = null;
            ++pageCount;
            totalRevisionCount += revisionCount;
        }

        /// <summary>
        /// Event raised when the parser has entered a new page record
        /// </summary>
        private static void PageStart(object sender, PageEventArgs e)
        {
            if (currPage != null) throw new InvalidOperationException("Unexpected page nesting");
            currPage = e.Page;
            currPageTitle = currPage.ToString();
            if (currPageTitle.Length > ProgressTitleWidth) currPageTitle = currPageTitle.Substring(0, ProgressTitleWidth);
            if (currPageTitle.Length < ProgressTitleWidth) currPageTitle = currPageTitle.PadRight(ProgressTitleWidth);

            revisionCount = 0;
            previousVersionText = "";
            previousUser = null;
        }

        /// <summary>
        /// Event raised when one revision has been read
        /// </summary>
        private static void RevisionComplete(object sender, RevisionEventArgs e)
        {
            if (currPage == null) throw new InvalidOperationException("Unexpected revision outside of page");

            Revision revision = e.Revision;
            UserData user = FindUser(revision.Contributor);
            if (user == null) throw new InvalidOperationException();

            // ignore null edits
            if (previousUser != user || previousVersionText != revision.Text)
            {
                bool isRecentEdit = monthAgo.CompareTo(revision.Timestamp) <= 0;
                bool isNewPage = previousUser == null;
                int nsIndex;
                if (!namespaceMap.TryGetValue(currPage.Namespace, out nsIndex))
                {
                    Console.Error.WriteLine("\nERROR: Unknown namespace {0} in page '{1}' (ID = {2})", currPage.Namespace, currPage, currPage.Id);
                    return;
                }

                if (previousUser == null || !RepeatedEdit(user, previousUser, revision.Timestamp, lastTimestamp))
                {
                    // not a repeated edit by the same user
                    if (previousUser != null)
                    {
                        previousUser.EndOfEdit();
                    }

                    previousUser = user;
                }

                user.AddRevision(revision.Timestamp, currPage.Namespace, namespaceMap[currPage.Namespace], revision.Text.Length - previousVersionText.Length, revision.Comment.Length, revision.Minor, isRecentEdit, isNewPage);
            }

            previousVersionText = revision.Text;
            lastTimestamp = revision.Timestamp;

            ++revisionCount;
            UpdateProgress(currPageTitle);
        }

        /// <summary>
        /// Evaluate wheter a revision should be considered a repeated edit
        /// </summary>
        /// <param name="user">User making this edit</param>
        /// <param name="previousUser">User the previous edit</param>
        /// <param name="timestamp">Timestamp of this edit</param>
        /// <param name="previousTimestamp">Timestamp of the previous edit</param>
        /// <returns><c>true</c> if this is a repeated edit, <c>false</c> otherwise</returns>
        /// <remarks>
        /// Repeated edits by the same user in some short time (<see cref="RepeatLimit"/>) are considered
        /// parts of a single edit and counted as such. This function tests whether this revision is
        /// continuation of the previous edit.
        ///
        /// That means this function checks whether both edits to the page have been made by the same user
        /// and are not split too much by time.
        /// </remarks>
        private static bool RepeatedEdit(UserData user, UserData previousUser, DateTime timestamp, DateTime previousTimestamp)
        {
            if (user == null) throw new ArgumentNullException("user");
            if (previousUser == null) throw new ArgumentNullException("previousUser");

            if (user.User != previousUser.User) return false;
            if (RepeatLimit == 0 || previousTimestamp.AddMinutes(RepeatLimit).CompareTo(timestamp) < 0) return false;
            return true;
        }

        /// <summary>
        /// Find user data for the given user (or create a new user data record if it does not already exist)
        /// </summary>
        /// <param name="user">Specification of the user</param>
        /// <returns>User data for the given user</returns>
        private static UserData FindUser(User user)
        {
            if (user == null) throw new ArgumentNullException("user");

            UserData result;
            if (users.TryGetValue(user, out result)) return result;
            result = new UserData(user, namespaceMap.Count);
            var registeredUser = user as RegisteredUser;
            if (registeredUser != null && botList.Contains(registeredUser.UserName))
            {
                result.Bot = true;
            }
            users.Add(user, result);
            return result;
        }

        /// <summary>
        /// Function to update the user with the progress
        /// </summary>
        /// <param name="title">Title of the currently processed page (length-limited to <see cref="ProgressTitleWidth"/>)</param>
        private static void UpdateProgress(string title)
        {
            DateTime now = DateTime.Now;
            if (now.Ticks - lastProgressUpdate > 10000000)
            {
                decimal done = 100.0m * stream.Position / length;
                TimeSpan elapsed = now - start;
                if (done > 0 && done <= 100)
                {
                    TimeSpan remaining = done > 0 ? new TimeSpan((long) Math.Round(elapsed.Ticks * (100.0m - done) / done)) : new TimeSpan(0);
                    Console.Write("{0:f1} % in {1:f1} s, ET: {2:d2}:{3:d2}:{4:d2}; {5}\r", done, elapsed.TotalSeconds, remaining.Hours, remaining.Minutes, remaining.Seconds, title);
                }
                else
                {
                    Console.Write("{0} revs ({1} pages) in {2:f1} s; {3}\r", totalRevisionCount, pageCount, elapsed.TotalSeconds, title);
                }
                lastProgressUpdate = now.Ticks;
            }

            bool abort = false;
            while (Console.KeyAvailable)
            {
                ConsoleKeyInfo key = Console.ReadKey(true);
                if (key.Key == ConsoleKey.C && key.Modifiers == ConsoleModifiers.Control) abort = true;
            }
            if (abort) throw new AbortException();
        }
    }
}