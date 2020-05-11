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
using System.Collections.Generic;

namespace MWLib.Parsers
{
    /// <summary>
    /// Event arguments for an event regarding a page
    /// </summary>
    public class PageEventArgs : EventArgs
    {
        /// <summary>
        /// The page this event is about
        /// </summary>
        public Page Page;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="page">The page this event is about</param>
        public PageEventArgs(Page page)
        {
            Page = page;
        }
    }

    /// <summary>
    /// Event arguments for an event regarding one revision of a page
    /// </summary>
    public class RevisionEventArgs : EventArgs
    {
        /// <summary>
        /// The revision this event is about
        /// </summary>
        public Revision Revision;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="revision">The revision this event is about</param>
        public RevisionEventArgs(Revision revision)
        {
            Revision = revision;
        }
    }

    /// <summary>
    /// Event arguments for an event regarding one database row
    /// </summary>
    public class RowEventArgs : EventArgs
    {
        /// <summary>
        /// List of columns in the row
        /// </summary>
        public IList<string> Columns;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="columns">List of columns in the row</param>
        public RowEventArgs(IEnumerable<string> columns)
        {
            Columns = new List<string>(columns);
        }
    }

    /// <summary>
    /// Basic interface for a parser of MediaWiki data
    /// </summary>
    public interface IParser
    {
        /// <summary>
        /// Parse the specified data
        /// </summary>
        void Parse();
    }

    /// <summary>
    /// Abstract superclass for a parser of MediaWiki data
    /// </summary>
    public abstract class Parser : IParser
    {
        /// <summary>
        /// Parse the specified data
        /// </summary>
        public abstract void Parse();
    }

    /// <summary>
    /// Interface for classes capable of reading database table data row by row
    /// </summary>
    public interface IDatabaseTableReader : IParser
    {
        /// <summary>
        /// Event raised for each row from the table
        /// </summary>
        event EventHandler<RowEventArgs> RowComplete;
    }

    /// <summary>
    /// Abstract superclass for parser of a MediaWiki database table row by row
    /// </summary>
    public abstract class DatabaseParser : Parser, IDatabaseTableReader
    {
        /// <summary>
        /// Event raised for each row from the table
        /// </summary>
        public event EventHandler<RowEventArgs> RowComplete;

        /// <summary>
        /// A complete row has been read, raise the <see cref="RowComplete"/> event
        /// </summary>
        /// <param name="columns">Columns of the row</param>
        protected void OnRowComplete(IEnumerable<string> columns)
        {
            if (RowComplete != null)
            {
                RowComplete(this, new RowEventArgs(columns));
            }
        }
    }

    /// <summary>
    /// Interface for classes capable of reading page data (pages, revisions)
    /// </summary>
    public interface IPageDataReader : IParser
    {
        /// <summary>
        /// Event raised after a page header has been read, before processing its revisions
        /// </summary>
        event EventHandler<PageEventArgs> PageStart;

        /// <summary>
        /// Event raised after all revisions of a page have been read
        /// </summary>
        event EventHandler<PageEventArgs> PageComplete;

        /// <summary>
        /// Event raised for every revision of a page
        /// </summary>
        event EventHandler<RevisionEventArgs> RevisionComplete;

        /// <summary>
        /// Event raised after the file header has been processed, before reading the individual pages
        /// </summary>
        event EventHandler<EventArgs> SiteInfoProcessed;
    }

    /// <summary>
    /// Interface for classes capable of reading page data (pages, revisions)
    /// </summary>
    public abstract class PageDataReader : Parser, IPageDataReader
    {
        /// <summary>
        /// Event raised after a page header has been read, before processing its revisions
        /// </summary>
        public event EventHandler<PageEventArgs> PageStart;

        /// <summary>
        /// Event raised after all revisions of a page have been read
        /// </summary>
        public event EventHandler<PageEventArgs> PageComplete;

        /// <summary>
        /// Event raised for every revision of a page
        /// </summary>
        public event EventHandler<RevisionEventArgs> RevisionComplete;

        /// <summary>
        /// Event raised after the file header has been processed, before reading the individual pages
        /// </summary>
        public event EventHandler<EventArgs> SiteInfoProcessed;

        /// <summary>
        /// Beginning to process a page, raise the <see cref="PageStart"/> event
        /// </summary>
        /// <param name="page">Page that is going to be processed now</param>
        protected void OnPageStart(Page page)
        {
            if (PageStart != null)
            {
                PageStart(this, new PageEventArgs(page));
            }
        }

        /// <summary>
        /// All revisions of the current page has been processed, raise the <see cref="PageComplete"/> event
        /// </summary>
        /// <param name="page">Page that has been processed</param>
        protected void OnPageComplete(Page page)
        {
            if (PageComplete != null)
            {
                PageComplete(this, new PageEventArgs(page));
            }
        }

        /// <summary>
        /// A revision of the current page has been read, raise the <see cref="RevisionComplete"/> event
        /// </summary>
        /// <param name="revision">Revision that has been read</param>
        protected void OnRevisionComplete(Revision revision)
        {
            if (RevisionComplete != null)
            {
                RevisionComplete(this, new RevisionEventArgs(revision));
            }
        }

        /// <summary>
        /// Raise the <see cref="SiteInfoProcessed"/> event
        /// </summary>
        protected void OnSiteInfoProcessed()
        {
            if (SiteInfoProcessed != null)
            {
                SiteInfoProcessed(this, EventArgs.Empty);
            }
        }
    }
}