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

using System.Data;
using MySql.Data.MySqlClient;

namespace MWLib.Parsers
{
    /// <summary>
    /// A table reader reading data directly from a MySQL database via a live connection
    /// </summary>
    public class MySqlConnectionTableReader : DatabaseParser
    {
        private readonly string connectionString;
        private readonly string tableName;

        /// <summary>
        /// Table name prefix
        /// </summary>
        public string DbTablePrefix { get; set; }

        /// <summary>
        /// Initialization
        /// </summary>
        /// <param name="connectionString">Connection string to the MySQL database</param>
        /// <param name="tableName">Name of the table to be read</param>
        public MySqlConnectionTableReader(string connectionString, string tableName)
        {
            this.connectionString = connectionString;
            this.tableName = tableName;
        }

        /// <summary>
        /// Read the data from the table
        /// </summary>
        public override void Parse()
        {
            using (var connection = new MySqlConnection(connectionString))
            {
                connection.Open();

                using (var pageCommand = new MySqlCommand())
                {
                    pageCommand.Connection = connection;
                    pageCommand.CommandText = "SELECT * FROM " + DbTablePrefix + tableName;

                    using (MySqlDataReader pageReader = pageCommand.ExecuteReader(CommandBehavior.SequentialAccess))
                    {
                        while (pageReader.Read())
                        {
                            var columns = new string[pageReader.FieldCount];
                            for (int i = 0; i < pageReader.FieldCount; ++i)
                            {
                                columns[i] = pageReader.GetString(i);
                            }
                            OnRowComplete(columns);
                        }
                    }
                }
            }
        }
    }
}
