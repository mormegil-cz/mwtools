using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace MWLib.Parsers
{
    /// <summary>
    /// Parser for a MySQL dump of a MediaWiki database table
    /// </summary>
    public class MySqlDumpParser : DatabaseParser
    {
        /// <summary>
        /// Current parser state
        /// </summary>
        private enum State
        {
            /// <summary>
            /// At the beginning of the dump
            /// </summary>
            Start,

            /// <summary>
            /// Before a row
            /// </summary>
            BeforeRow,

            /// <summary>
            /// Inside the row, before a column
            /// </summary>
            BeforeColumn,

            /// <summary>
            /// Inside a column value containing a number
            /// </summary>
            InNumberColumn,

            /// <summary>
            /// Inside a NULL-valued column
            /// </summary>
            InNullColumn,

            /// <summary>
            /// Inside a column value containing a string
            /// </summary>
            InStringColumn,

            /// <summary>
            /// Inside a column value containing a string, just after an escape character
            /// </summary>
            StringEscape,

            /// <summary>
            /// After the terminating quote character of a string column
            /// </summary>
            AfterStringColumn,

            /// <summary>
            /// After a row
            /// </summary>
            AfterRow
        }

        /// <summary>
        /// Reader of the SQL dump
        /// </summary>
        private readonly TextReader reader;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="reader">Reader providing the MySQL dump text</param>
        public MySqlDumpParser(TextReader reader)
        {
            this.reader = reader;
        }

        /// <summary>
        /// Throw an exception about a format error
        /// </summary>
        /// <param name="expected">What kind of token has been expected here</param>
        private static void XExpected(string expected)
        {
            throw new FormatException(expected + " expected");
        }

        /// <summary>
        /// Throw an exception about a format error
        /// </summary>
        /// <param name="expected">What kind of token has been expected here</param>
        private static void XExpectedYFound(string expected, char found)
        {
            throw new FormatException(String.Format("{0} expected, '{1}' found", expected, found));
        }

        /// <summary>
        /// Parse the dump
        /// </summary>
        /// <remarks>
        /// Reads the dump, raising the <see cref="DatabaseParser.RowComplete"/> events for every row read.
        /// After the method call returns, the dump will have been read completely.
        /// </remarks>
        public override void Parse()
        {
            string line;
            do
            {
                line = reader.ReadLine();
            } while (!line.StartsWith("/*!40000 ALTER TABLE `"));

            var buf = new char[1];

            State state = State.Start;
            var column = new StringBuilder();
            var columns = new List<string>();
            while (reader.Read(buf, 0, 1) > 0)
            {
                char c = buf[0];
                if (c == '\r') continue;

                switch (state)
                {
                    case State.Start:
                        while (Char.IsWhiteSpace(c) && reader.Read(buf, 0, 1) > 0)
                        {
                            c = buf[0];
                            if (c == '\n')
                            {
                                // empty line – end of dump
                                return;
                            }
                            else if (c == '/')
                            {
                                // comment “/*” – end of dump
                                return;
                            }
                        }
                        if (Char.IsWhiteSpace(c)) break;

                        if (c != 'I') XExpectedYFound("INSERT command", c);

                        while (c != '(' && reader.Read(buf, 0, 1) > 0)
                        {
                            c = buf[0];
                        }
                        if (c != '(') break;

                        state = State.BeforeColumn;
                        break;

                    case State.BeforeRow:
                        if (c != '(') XExpected("'('");
                        state = State.BeforeColumn;
                        break;

                    case State.BeforeColumn:
                        if (c == '\'')
                        {
                            state = State.InStringColumn;
                        }
                        else if ((c >= '0' && c <= '9') || c == '.' || c == '-' || c == '+' || c == 'e')
                        {
                            column.Append(c);
                            state = State.InNumberColumn;
                        }
                        else if (c == ')')
                        {
                            OnRowComplete(columns);
                            columns.Clear();
                            state = State.AfterRow;
                        }
                        else if (c == 'N')
                        {
                            state = State.InNullColumn;
                        }
                        else
                        {
                            XExpectedYFound("Number or string", c);
                        }
                        break;

                    case State.InNumberColumn:
                        if ((c >= '0' && c <= '9') || c == '.' || c == '-' || c == '+' || c == 'e')
                        {
                            column.Append(c);
                        }
                        else if (c == ',')
                        {
                            columns.Add(column.ToString());
                            column.Length = 0;
                            state = State.BeforeColumn;
                        }
                        else if (c == ')')
                        {
                            columns.Add(column.ToString());
                            column.Length = 0;
                            OnRowComplete(columns);
                            columns.Clear();
                            state = State.AfterRow;
                        }
                        else
                        {
                            XExpectedYFound("Number", c);
                        }
                        break;

                    case State.InNullColumn:
                        if (c == 'U' || c == 'L')
                        {
                            // OK, skip
                        }
                        else if (c == ',')
                        {
                            columns.Add("<NULL>");
                            column.Length = 0;
                            state = State.BeforeColumn;
                        }
                        else if (c == ')')
                        {
                            columns.Add("<NULL>");
                            column.Length = 0;
                            OnRowComplete(columns);
                            columns.Clear();
                            state = State.AfterRow;
                        }
                        else
                        {
                            XExpectedYFound("NULL", c);
                        }
                        break;

                    case State.InStringColumn:
                        if (c == '\\')
                        {
                            state = State.StringEscape;
                        }
                        else if (c == '\'')
                        {
                            columns.Add(column.ToString());
                            column.Length = 0;
                            state = State.AfterStringColumn;
                        }
                        else
                        {
                            column.Append(c);
                        }
                        break;

                    case State.StringEscape:
                        column.Append(c);
                        state = State.InStringColumn;
                        break;

                    case State.AfterStringColumn:
                        if (c == ',')
                        {
                            state = State.BeforeColumn;
                        }
                        else if (c == ')')
                        {
                            OnRowComplete(columns);
                            columns.Clear();
                            state = State.AfterRow;
                        }
                        else
                        {
                            XExpected("',' or ')'");
                        }
                        break;

                    case State.AfterRow:
                        if (c == ',')
                        {
                            state = State.BeforeRow;
                        }
                        else if (c == ';')
                        {
                            state = State.Start;
                        }
                        else XExpected("',' or ';'");
                        break;
                }
            }
        }
    }
}
