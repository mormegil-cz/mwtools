// MW Page Blame
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
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Permissions;
using System.Text;
using System.Xml;
using Differ;
using MWLib;
using MWLib.Parsers;
#if NUNIT
using NUnit.Framework;
#endif

[assembly: CLSCompliant(true)]
[assembly: SecurityPermission(SecurityAction.RequestMinimum, Execution = true)]
namespace PageBlame
{
    /// <summary>
    /// Exception for signalling an abort of the current process
    /// </summary>
    internal class AbortException : Exception
    {
    }

    /// <summary>
    /// Main class of the program
    /// </summary>
    internal class PageBlameMain : CommandLineTool
    {
        /// <summary>
        /// Output format
        /// </summary>
        private enum OutputFormat
        {
            /// <summary>
            /// Invalid value
            /// </summary>
            None = 0,

            /// <summary>
            /// Plain text output
            /// </summary>
            Text = 1,

            /// <summary>
            /// HTML formatted output
            /// </summary>
            Html = 2
        }

        /// <summary>
        /// About line for the program
        /// </summary>
        protected override string AboutLine { get { return "MW Page Blame  v0.1  Copyright (c) Petr Kadlec, 2007"; } }

        /// <summary>
        /// XML parser
        /// </summary>
        private IPageDataReader dump;

        /// <summary>
        /// Name of the input file
        /// </summary>
        private string inputFilename;

        /// <summary>
        /// Input stream
        /// </summary>
        private Stream inputStream;

        /// <summary>
        /// Name of the output file
        /// </summary>
        private string outputFilename;

        /// <summary>
        /// Length of the input file
        /// </summary>
        private long length;

        /// <summary>
        /// Timestamp of the last progress update
        /// </summary>
        private long lastProgressUpdate;

        /// <summary>
        /// Time of the program start
        /// </summary>
        private DateTime start;

        /// <summary>
        /// Time of finish
        /// </summary>
        private DateTime stop;

        /// <summary>
        /// Flag marking successful finish
        /// </summary>
        private bool done;

        /// <summary>
        /// Number of revisions found
        /// </summary>
        private int revisionCount;

        /// <summary>
        /// Information about the processed page
        /// </summary>
        private Page pageInfo;

        /// <summary>
        /// Text preprocessor to be used
        /// </summary>
        private static TextPreprocessor textPreprocessor;

        /// <summary>
        /// Index of revisions accessible by their text
        /// </summary>
        private Dictionary<string, RevisionData> revisionByText = new Dictionary<string, RevisionData>();

        /// <summary>
        /// List of all revisions
        /// </summary>
        private List<RevisionData> revisions = new List<RevisionData>();

        /// <summary>
        /// Suppress some output
        /// </summary>
        private bool quiet;

        /// <summary>
        /// Minimum date
        /// </summary>
        private DateTime fromDate;

        /// <summary>
        /// Maximum date
        /// </summary>
        private DateTime toDate;

        /// <summary>
        /// User-selected output format
        /// </summary>
        private OutputFormat selectedOutputFormat;

        /// <summary>
        /// Main entry point, contains only error-reporting wrapper
        /// </summary>
        /// <param name="args">Command-line arguments</param>
        /// <see cref="Start"/>
        [DebuggerNonUserCode]
        private static void Main(string[] args)
        {
            try
            {
                new PageBlameMain().Start(args);
            }
            catch(Exception e)
            {
                Console.Error.WriteLine("Error: {0}", e);
                Environment.Exit(1);
            }
        }

        /// <summary>
        /// The --quiet flag
        /// </summary>
        private CommandLineParameterFlag paramQuiet;

        /// <summary>
        /// The --from=DATE parameter
        /// </summary>
        private CommandLineParameterWithDateArgument paramFrom;

        /// <summary>
        /// The --to=DATE parameter
        /// </summary>
        private CommandLineParameterWithDateArgument paramTo;

        /// <summary>
        /// The --wrap=CHARS parameter
        /// </summary>
        private CommandLineParameterWithIntArgument paramWrap;

        /// <summary>
        /// The --format=FORMAT parameter
        /// </summary>
        private CommandLineParameterWithArguments paramFormat;

        /// <summary>
        /// Define the parameters and execute the program
        /// </summary>
        /// <param name="args">Command-line arguments</param>
        internal void Start(string[] args)
        {
            // register supported parameters
            AddParameter(paramQuiet = new CommandLineParameterFlag("quiet"));
            AddParameter(paramFrom = new CommandLineParameterWithDateArgument("from"));
            AddParameter(paramTo = new CommandLineParameterWithDateArgument("to"));
            AddParameter(paramWrap = new CommandLineParameterWithIntArgument("wrap"));
            AddParameter(paramFormat = new CommandLineParameterWithArguments("format"));

            // run!
            Execute(args);
        }

        /// <summary>
        /// Handler for processed command-line parameters
        /// </summary>
        /// <param name="commandLineParser">The command line parser used</param>
        protected override void CommandLineParsed(CommandLineParser commandLineParser)
        {
            base.CommandLineParsed(commandLineParser);

            if (commandLineParser.PositionalArguments.Count != 2) ArgumentError("");
            inputFilename = commandLineParser.PositionalArguments[0];
            outputFilename = commandLineParser.PositionalArguments[1];

            quiet = paramQuiet.Present;

            if (paramFormat.Occurrences != 0)
            {
                switch(paramFormat.Arguments[0].ToUpperInvariant())
                {
                    case "TEXT":
                    case "TXT":
                        selectedOutputFormat = OutputFormat.Text;
                        break;
                    case "HTML":
                        selectedOutputFormat = OutputFormat.Html;
                        break;
                    default:
                        throw new CommandLineParameterException("Unsupported output format");
                }
            }
            else
            {
                selectedOutputFormat = OutputFormat.Text;
            }

            fromDate = paramFrom.Occurrences == 0 ? DateTime.MinValue : paramFrom.ArgumentValue;
            toDate = paramTo.Occurrences == 0 ? DateTime.MaxValue : paramTo.ArgumentValue;
            int wrapLimit = paramWrap.Occurrences == 0 ? Int32.MaxValue : paramWrap.ArgumentValue;

            textPreprocessor = new TextPreprocessor(wrapLimit);
        }

        /// <summary>
        /// List of names of input files, in this case, there is only one input file
        /// </summary>
        protected override IEnumerable<string> InputFilenames
        {
            get { yield return inputFilename; }
        }

        /// <summary>
        /// The output filename
        /// </summary>
        protected override string OutputFilename
        {
            get { return outputFilename; }
        }

        /// <summary>
        /// Print command line help
        /// </summary>
        protected override void PrintHelp()
        {
            base.PrintHelp();
            Console.WriteLine("Usage: PageBlame [options] input output");
            Console.WriteLine("Options:");
            Console.WriteLine("\t--from=DATE\tIgnore revisions before DATE");
            Console.WriteLine("\t--to=DATE\tIgnore revisions after DATE");
            Console.WriteLine("\t--wrap=CHARS\tWrap the text at CHARS characters");
            Console.WriteLine("\t--format=FORMAT\tChoose output format (TEXT or HTML)");
            Console.WriteLine("\t--quiet\tSuppress some output");
            Environment.Exit(0);
        }

        /// <summary>
        /// Processing of a single input file
        /// </summary>
        /// <param name="inputStream">Input data stream</param>
        protected override void Process(Stream inputStream)
        {
            this.inputStream = inputStream;

            XmlReaderSettings settings = new XmlReaderSettings();
            settings.XmlResolver = null;
            using (XmlReader xml = XmlReader.Create(inputStream, settings))
            {
                dump = new RevisionXmlDumpParser(xml);
                dump.PageStart += PageStart;
                dump.PageComplete += PageComplete;
                dump.RevisionComplete += RevisionComplete;
                length = inputStream.Length;
                start = DateTime.Now;
                Console.TreatControlCAsInput = true;
                try
                {
                    dump.Parse();
                }
                catch (AbortException)
                {
                    if (!done)
                    {
                        if (!quiet) Console.Error.Write("\nAborted");
                    }
                }
                stop = DateTime.Now;
                Console.Error.WriteLine();
                if (!quiet)
                {
                    Console.Error.WriteLine("{0} revisions analyzed in {1:n0} s", revisionCount, (stop - start).TotalSeconds);
                }
            }
        }

        /// <summary>
        /// Save the results
        /// </summary>
        protected override void SaveOutput(Stream outputStream)
        {
            if (revisions.Count == 0)
            {
                Console.Error.WriteLine("No revisions to analyze...");
                return;
            }

            if (!quiet) Console.Error.Write("Writing resulting data...");

            using(StreamWriter writer = new StreamWriter(outputStream, Encoding.UTF8))
            {
                RevisionData lastRevision = revisions[revisions.Count - 1];
                switch(selectedOutputFormat)
                {
                    case OutputFormat.Text:
                        SaveOutputInText(writer, lastRevision);
                        break;
                    case OutputFormat.Html:
                        SaveOutputInHtml(writer, lastRevision);
                        break;
                }
            }

            if (!quiet) Console.Error.WriteLine("OK");
        }

        /// <summary>
        /// Output method for the text format.
        /// </summary>
        /// <param name="writer">Writer into which the output should be written</param>
        /// <param name="revision">The last revision</param>
        private static void SaveOutputInText(TextWriter writer, RevisionData revision)
        {
            foreach (LineInfo line in revision.LineInfo)
            {
                writer.WriteLine(line);
            }
        }

        /// <summary>
        /// Helper class for HTML output format
        /// </summary>
        private class ContributorOverview
        {
            public int Id;
            public string Name;
            public int EditCount;
            public DateTime FirstEdit;
            public DateTime LastEdit;

            public ContributorOverview(int id, string name, DateTime firstEdit)
            {
                Id = id;
                Name = name;
                FirstEdit = LastEdit = firstEdit;
            }
        }

        /// <summary>
        /// Output method for the HTML output format.
        /// </summary>
        /// <param name="writer">Writer into which should the output be written</param>
        /// <param name="revision">The last revision</param>
        private void SaveOutputInHtml(TextWriter writer, RevisionData revision)
        {
            var users = new Dictionary<User, ContributorOverview>();
            foreach (LineInfo line in revision.LineInfo)
            {
                User contributor = line.Revision.Contributor;
                ContributorOverview data;
                if (!users.TryGetValue(contributor, out data))
                {
                    data = new ContributorOverview(users.Count + 1, contributor.ToString(), line.Revision.Timestamp);
                    users.Add(contributor, data);
                }
                ++data.EditCount;
                data.LastEdit = line.Revision.Timestamp;
            }

            var sortedUsers = new SortedList<Pair<int, DateTime>, ContributorOverview>(users.Count);
            foreach (ContributorOverview user in users.Values)
            {
                sortedUsers.Add(new Pair<int, DateTime>(-user.EditCount, user.FirstEdit), user);
            }

            int counter = 0;
            foreach (ContributorOverview user in sortedUsers.Values)
            {
                ++counter;
                user.Id = counter;
            }

            writer.WriteLine(
@"<!DOCTYPE html PUBLIC ""-//W3C//DTD XHTML 1.0 Transitional//EN"" ""http://www.w3.org/TR/xhtml1/DTD/xhtml1-transitional.dtd"">
<html xmlns=""http://www.w3.org/1999/xhtml"">
 <head>
  <meta http-equiv='Content-Type' content='text/html; charset={2}' />
  <title>Blame for {0}</title>
  <meta name='generator' content='{1}' />
  <style type='text/css' media='screen'><!--

div.text {{ font-family: monospace; border: 1px blue dotted; color: black; background-color: #ffd; padding: 1em; white-space: pre-wrap; white-space: -moz-pre-wrap; }}
ul.legend {{ float: right; width: 150px; border: 1px black solid; color: black; background-color: #ddf; margin: 0.2em 0.2em 1em 1em; padding: 0.2em; list-style-type: none }}

span.u-1 {{ color: blue }}
span.u-2 {{ color: green }}
span.u-3 {{ color: red }}
span.u-4 {{ color: fuchsia }}
span.u-5 {{ color: coral }}
span.u-6 {{ color: darkseagreen }}
span.u-7 {{ color: slateblue }}
span.u-8 {{ color: olive }}
span.u-9 {{ color: royalblue }}

--></style>
   <!--[if IE]>
   <style type='text/css' media='screen'>
div.text {{ word-wrap: break-word; white-space: pre; }}
   </style>
   <![endif]--> 
    <script type='text/javascript'>/*<![CDATA[*/
    var highlightedUser = null;
    function highlightUser(userCls)
    {{
        if (highlightedUser == userCls)
        {{
            unhighlightUser(highlightedUser);
            return;
        }} else if (highlightedUser) unhighlightUser(highlightedUser);

        if (document.styleSheets[0].addRule)
            document.styleSheets[0].addRule('span.' + userCls, 'background-color: white; border: 1px dotted black;', 0);
        else
            document.styleSheets[0].insertRule('span.' + userCls + ' {{ background-color: white; border: 1px dotted black; }}', 0);

        highlightedUser = userCls;
    }}

    function unhighlightUser(userCls)
    {{
        if (document.styleSheets[0].removeRule)
            document.styleSheets[0].removeRule();
        else
            document.styleSheets[0].deleteRule(0);

        highlightedUser = null;
    }}
  /*]]>*/</script>
</head>

<body>", Path.GetFileNameWithoutExtension(inputFilename), AboutLine, writer.Encoding.HeaderName);

            writer.WriteLine("<ul id='legend' class='legend'>");
            foreach(ContributorOverview user in sortedUsers.Values)
            {
                writer.WriteLine("<li><span class='u-{0}' onclick='javascript:highlightUser(\"u-{0}\")'>{1}</span></li>", user.Id, user.Name);
            }
            writer.WriteLine("</ul>");

            writer.Write("<div id='content' class='text'>");

            int lastUser = -1;
            RevisionData lastRevision = null;
            foreach(LineInfo line in revision.LineInfo)
            {
                ContributorOverview user = users[line.Revision.Contributor];
                int userNumber = user.Id;
                if (line.Revision != lastRevision && lastRevision != null)
                {
                    writer.Write("</span>");
                }
                if (userNumber != lastUser)
                {
                    if (lastUser >= 0) writer.Write("</span>");
                    writer.Write("<span class='u-{0}'>", userNumber);
                    lastUser = userNumber;
                }
                else
                {
                    //writer.Write(' ');
                }
                if (line.Revision != lastRevision)
                {
                    writer.Write("<span title='{0} #{1} (r{3}) {2}'>", user.Name, line.Revision.Number, line.Revision.Timestamp, line.Revision.RevisionId);
                    lastRevision = line.Revision;
                }
                StringBuilder result = new StringBuilder(line.Line);
                result.Replace("&", "&amp;");
                result.Replace("<", "&lt;");
                result.Replace(">", "&gt;");
                writer.Write(result.ToString());
            }
            if (lastRevision != null) writer.Write("</span>");
            if (lastUser >= 0) writer.Write("</span>");

            writer.WriteLine(@"</div>
 </body>
</html>");
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
        /// Event raised after one page has been completed (and all revisions read)
        /// </summary>
        private void PageComplete(object sender, PageEventArgs e)
        {
            done = true;
        }

        /// <summary>
        /// Event raised when the parser has entered a new page record
        /// </summary>
        private void PageStart(object sender, PageEventArgs e)
        {
            if (done) throw new AbortException();
            pageInfo = e.Page;
        }

        /// <summary>
        /// Event raised when one revision has been read
        /// </summary>
        private void RevisionComplete(object sender, RevisionEventArgs e)
        {
            if (pageInfo == null) throw new InvalidOperationException("Unexpected revision outside of page");

            Revision revision = e.Revision;

            if (revision.Timestamp >= fromDate && revision.Timestamp <= toDate)
            {
                RevisionData thisRevision;
                if (revisions.Count == 0)
                {
                    // create the first revision
                    var lineInfo = new List<LineInfo>();
                    thisRevision = new RevisionData(1, revision.Text, revision.Contributor, revision.Id, revision.Timestamp, revision.Comment, revision.Minor, lineInfo);
                    foreach (string line in thisRevision.Lines) lineInfo.Add(new LineInfo(line, thisRevision));
                }
                else
                {
                    // check if this is not a revert to a prior revision
                    RevisionData revertedToRev = FindRevisionData(revision.Text);

                    if (revertedToRev == null)
                    {
                        // not a revert
                        RevisionData previousRevision = revisions[revisions.Count - 1];

                        var lineInfo = new List<LineInfo>(previousRevision.LineInfo);
                        thisRevision = new RevisionData(revisions.Count + 1, revision.Text, revision.Contributor, revision.Id, revision.Timestamp, revision.Comment, revision.Minor, lineInfo);
                        revisionByText.Add(revision.Text, thisRevision);

                        ProcessDiff(previousRevision, thisRevision, lineInfo, previousRevision.IntData, thisRevision.IntData);

                        /*
                        Diff.Item[] script = Diff.DiffInt(previousRevision.IntData, thisRevision.IntData);

                        foreach (Edit edit in script)
                        {
                            switch (edit.Type)
                            {
                                case EditType.Delete:
                                    lineInfo.RemoveRange(edit.StartB, edit.Length);
                                    break;
                                case EditType.Insert:
                                    for (int i = 0; i < edit.Length; ++i)
                                        lineInfo.Insert(edit.StartB + i, new LineInfo(thisRevision.Lines[edit.StartB + i], thisRevision));
                                    break;
                                case EditType.Change:
                                    for (int i = 0; i < edit.Length; ++i)
                                        lineInfo[edit.StartB + i] = new LineInfo(thisRevision.Lines[edit.StartB + i], thisRevision);
                                    break;
                            }
                        }
                        */
                    }
                    else
                    {
                        // just a revert
                        thisRevision = revertedToRev;
                    }
                }

                // add this revision
                revisions.Add(thisRevision);

                ++revisionCount;
            }

            UpdateProgress(revision.Timestamp + "  ");
        }

        private static void ProcessDiff(RevisionData previousRevision, RevisionData thisRevision, List<LineInfo> lineInfo, int[] prevData, int[] thisData)
        {
            Diff.Item[] script = Diff.DiffInt(prevData, thisData);

            foreach (Diff.Item edit in script)
            {
                // Console.WriteLine("-{0}+{1} @{2}/{3}", edit.deletedA, edit.insertedB, edit.StartA, edit.StartB);
                if (edit.deletedA > 0) lineInfo.RemoveRange(edit.StartB, edit.deletedA);
                for (int i = 0; i < edit.insertedB; ++i)
                    lineInfo.Insert(edit.StartB + i, new LineInfo(thisRevision.Lines[edit.StartB + i], thisRevision));
            }
        }

        /// <summary>
        /// Find revision object for the given text
        /// </summary>
        /// <param name="text">Text of the revision</param>
        /// <returns>Revision object corresponding to the given text, or <c>null</c> if this revision did not exist</returns>
        private RevisionData FindRevisionData(string text)
        {
            RevisionData result;
            revisionByText.TryGetValue(text, out result);
            return result;
        }

        /// <summary>
        /// Function to update the user with the progress
        /// </summary>
        /// <param name="info">Informative text (must be short)</param>
        private void UpdateProgress(string info)
        {
            DateTime now = DateTime.Now;
            if (now.Ticks - lastProgressUpdate > 10000000)
            {
                decimal percentDone = 100.0m * inputStream.Position / length;
                TimeSpan elapsed = now - start;
                TimeSpan remaining = percentDone > 0 ? new TimeSpan((long)Math.Round(elapsed.Ticks * (10000.0m / (percentDone * percentDone) - 1.0m))) : new TimeSpan(0);
                Console.Error.Write("{0:f1} % in {1:f1} s, ET: {2:d2}:{3:d2}:{4:d2}; {5}\r", percentDone, elapsed.TotalSeconds, remaining.Hours, remaining.Minutes, remaining.Seconds, info);
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

        /// <summary>
        /// Data stored for each revision
        /// </summary>
        private class RevisionData
        {
            private readonly int number;
            private readonly string text;
            private readonly string[] lines;
            private readonly int[] intData;
            private readonly User contributor;
            private readonly int revisionId;
            private readonly DateTime timestamp;
            private readonly string comment;
            private readonly bool minor;
            private readonly List<LineInfo> lineInfo;

            /// <summary>
            /// Number of the revision
            /// </summary>
            public int Number
            {
                get { return number; }
            }

            /// <summary>
            /// The revision text
            /// </summary>
            public string Text
            {
                get { return text; }
            }

            /// <summary>
            /// The revision text, split into lines
            /// </summary>
            public string[] Lines
            {
                get { return lines; }
            }

            /// <summary>
            /// Hashcodes of the lines, used in the diffing algorithm
            /// </summary>
            public int[] IntData
            {
                get { return intData; }
            }

            /// <summary>
            /// The user that saved this revision
            /// </summary>
            public User Contributor
            {
                get { return contributor; }
            }

            /// <summary>
            /// Revision database ID
            /// </summary>
            public int RevisionId
            {
                get { return revisionId; }
            }

            /// <summary>
            /// The timestamp when the edit has been made
            /// </summary>
            public DateTime Timestamp
            {
                get { return timestamp; }
            }

            /// <summary>
            /// Information about each line
            /// </summary>
            public List<LineInfo> LineInfo
            {
                get { return lineInfo; }
            }

            /// <summary>
            /// Constructor
            /// </summary>
            /// <param name="number">Revision number</param>
            /// <param name="text">Revision text</param>
            /// <param name="contributor">User who stored the revision</param>
            /// <param name="revisionId">Database revision identifier</param>
            /// <param name="timestamp">Timestamp of the revision</param>
            /// <param name="comment">Edit summary</param>
            /// <param name="minor">Is the revision minor?</param>
            /// <param name="lineInfo">Per-line revision information</param>
            public RevisionData(int number, string text, User contributor, int revisionId, DateTime timestamp, string comment, bool minor, List<LineInfo> lineInfo)
            {
                this.number = number;
                this.text = text;
                this.contributor = contributor;
                this.revisionId = revisionId;
                this.timestamp = timestamp;
                this.comment = comment;
                this.minor = minor;
                this.lineInfo = lineInfo;

                lines = textPreprocessor.Process(text);
                intData = new int[lines.Length];
                for (int i = 0; i < lines.Length; ++i)
                {
                    intData[i] = lines[i].Trim().GetHashCode();
                }
            }

            /// <summary>
            /// Returns the hash code of this instance, based only on the revision text
            /// </summary>
            /// <returns>Hash code of the revision text</returns>
            /// <seealso cref="Object.GetHashCode"/>
            public override int GetHashCode()
            {
                return Text.GetHashCode();
            }

            /// <summary>
            /// Checks whether this instance contains the same text as another one
            /// </summary>
            /// <param name="obj">Another instance of this class</param>
            /// <returns><c>true</c> if <paramref name="obj"/> contains the same text as this instance, <c>false</c> otherwise</returns>
            /// <seealso cref="Object.Equals(object)"/>
            public override bool Equals(object obj)
            {
                RevisionData other = obj as RevisionData;
                if (other == null) return false;
                return Text.Equals(other.Text);
            }

            /// <summary>
            /// Returns the description of this instance
            /// </summary>
            /// <returns>String containing the revision number, timestamp, minor flag, and contributor name</returns>
            public override string ToString()
            {
                return String.Format("{0,8}\t  {1,-20}\t{2} {3,-30}", Number, timestamp, minor ? 'm' : ' ', contributor);
            }
        }

        /// <summary>
        /// Data stored for every line
        /// </summary>
        private struct LineInfo
        {
            private readonly string line;
            private readonly RevisionData revision;

            /// <summary>
            /// Line text
            /// </summary>
            public string Line
            {
                get { return line; }
            }

            /// <summary>
            /// The revision this line has been modified last
            /// </summary>
            public RevisionData Revision
            {
                get { return revision; }
            }

            /// <summary>
            /// Constructor
            /// </summary>
            /// <param name="line">Line text</param>
            /// <param name="revision">The revision this line has been modified last</param>
            public LineInfo(string line, RevisionData revision)
            {
                this.line = line;
                this.revision = revision;
            }

            /// <summary>
            /// Returns the description of this instance
            /// </summary>
            /// <returns>Information about the revision followed by the line text</returns>
            public override string ToString()
            {
                return String.Format("{0}\t{1}", Revision, Line.Trim());
            }
        }

#if NUNIT
        [TestFixture]
        public class UnitTests
        {
            [SetUp]
            public void Setup()
            {
                textPreprocessor = new TextPreprocessor(1);
            }

            private static void DoDiffTest(string from, string to)
            {
                var lineInfo = new List<LineInfo>();
                RevisionData firstRevision = new RevisionData(1, from, null, 1, DateTime.Now, "first!", false, lineInfo);
                foreach(string word in from.Split(' '))
                {
                    lineInfo.Add(new LineInfo(word, firstRevision));
                }
                var secondLineInfo = new List<LineInfo>(lineInfo);
                RevisionData secondRevision = new RevisionData(2, to, null, 2, DateTime.Now, "second", false, secondLineInfo);
                // Console.WriteLine("--- diffing '{0}' and '{1}' ---", from, to);
                ProcessDiff(firstRevision, secondRevision, secondLineInfo, firstRevision.IntData, secondRevision.IntData);
                string[] words = to.Split(' ');
                Assert.AreEqual(words.Length, secondLineInfo.Count);
                for (int i = 0; i < words.Length; ++i)
                {
                    Assert.AreEqual(words[i], secondLineInfo[i].Line.Trim(), "Difference at word {0}", i);
                }
            }

            [Test]
            public void TestDiffs()
            {
                DoDiffTest("a b c d", "e a b c d");
                DoDiffTest("a b c d", "a b c d e");
                DoDiffTest("a b c d", "a b c e d");
                DoDiffTest("a b c d", "x b c d");
                DoDiffTest("a b c d", "a b c x");
                DoDiffTest("a b c d", "a b x d");
                DoDiffTest("a b c d", "b c d");
                DoDiffTest("a b c d", "a b c");
                DoDiffTest("a b c d", "a b d");
                DoDiffTest("a b c d", "e a");
                DoDiffTest("a b - c d e f f", "a b x c e f");
                DoDiffTest("lorem ipsum dolor sit", "lorem ipsum johnny sit amet");
                DoDiffTest("lorem ipsum ipsum ipsum dolor sit", "lorem ipsum johnny sit amet");
                DoDiffTest("lorem ipsum dolor sit", "aqua");
                DoDiffTest("a b c d a b c d", "x b a d d x b a d d");
                DoDiffTest("lorem ipsum dolor sit lorem ipsum dolor sit", "aqua ipsum lorem sit sit aqua ipsum lorem sit sit");
            }
        }
#endif
    }

    /// <summary>
    /// Preprocessor for revision texts. Splits the text into lines.
    /// </summary>
    public class TextPreprocessor
    {
        /// <summary>
        /// Wrapping limit (characters)
        /// </summary>
        private readonly int wrapLimit;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="wrapLimit">Wrapping limit (characters)</param>
        public TextPreprocessor(int wrapLimit)
        {
            this.wrapLimit = wrapLimit;
        }

        /// <summary>
        /// Splits the given text into lines
        /// </summary>
        /// <param name="text">Input text</param>
        /// <returns>List of lines from the text</returns>
        /// <remarks>
        /// The text is split on physical EOL characters (\n), or wrapped at <see cref="wrapLimit"/> (on the first
        /// following whitespace character).
        /// </remarks>
        public string[] Process(string text)
        {
            var result = new List<string>();
            StringBuilder currLine = new StringBuilder();
            foreach(char c in text)
            {
                if (c == '\n' || (Char.IsWhiteSpace(c) && currLine.Length >= wrapLimit))
                {
                    currLine.Append(c);
                    result.Add(currLine.ToString());
                    currLine.Length = 0;
                }
                else
                {
                    currLine.Append(c);
                }
            }
            if (currLine.Length > 0) result.Add(currLine.ToString());
            return result.ToArray();
        }
    }
}