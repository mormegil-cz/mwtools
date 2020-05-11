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
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using MWLib;
using MWLib.IO;
using MWLib.Parsers;

namespace CatBrowser
{
    #region Event types

    /// <summary>
    /// Event regarding a page
    /// </summary>
    public class PageEventArgs : EventArgs
    {
        /// <summary>
        /// The page affected by the event
        /// </summary>
        public Page Page;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="page">The page affected by the event</param>
        public PageEventArgs(Page page)
        {
            this.Page = page;
        }
    }

    /// <summary>
    /// A notification about an event during file load
    /// </summary>
    public class FileLoadNotificationEventArgs : EventArgs
    {
        public string Message;
        public string Filename;
        public int Line;
        public int Column;

        public FileLoadNotificationEventArgs(string message, string filename, int line, int column)
        {
            this.Message = message;
            this.Filename = filename;
            this.Line = line;
            this.Column = column;
        }
    }

    #endregion

    #region Browser engine

    public enum Operator
    {
        None = 0,
        Replace,
        Add,
        Subtract,
        ReverseSubtract,
        Intersect
    }

    /// <summary>
    /// A kind of functional wrapper for a set of pages described by a simple rule, e.g. "all pages inside the category Xyz"
    /// </summary>
    public class Selector
    {
        /// <summary>
        /// Type of the selector
        /// </summary>
        internal enum SelectorType
        {
            None = 0,
            InsideCategory,
            InCategoryRegExp,
            DuplicateWithinCategory,
            InNamespace,
            LinkingToPage,
            LinkingToTemplate,
            WithExtLink,
        }

        /// <summary>
        /// Type of this selector
        /// </summary>
        private readonly SelectorType type;

        /// <summary>
        /// Base page namespace
        /// </summary>
        private readonly Namespace ns;

        /// <summary>
        /// Base page name
        /// </summary>
        private readonly string name;

        internal SelectorType Type
        {
            get { return type; }
        }

        internal Namespace Namespace
        {
            get { return ns; }
        }

        internal string Name
        {
            get { return name; }
        }

        /// <summary>
        /// Get the base page
        /// </summary>
        /// <param name="engine">Browser engine to be used for the page load</param>
        /// <returns>The page specified by <see cref="Namespace"/>.<see cref="Name"/> from <paramref name="engine"/></returns>
        internal Page GetPage(BrowserEngine engine)
        {
            return engine.PagesByName[new Pair<Namespace, string>(Namespace, Name)];
        }

        /// <summary>
        /// An internal constructor; see static methods for public interface
        /// </summary>
        /// <param name="type">Selector type</param>
        /// <param name="ns">Base page namespace</param>
        /// <param name="name">Base page name</param>
        private Selector(SelectorType type, Namespace ns, string name)
        {
            this.type = type;
            this.ns = ns;
            if (name.Length == 0)
            {
                this.name = "";
            }
            else
            {
                var builder = new StringBuilder(name);
                builder.Replace(' ', '_');
                builder[0] = Char.ToUpper(builder[0]);
                this.name = builder.ToString();
            }
        }

        /// <summary>
        /// An internal constructor; see static methods for public interface
        /// </summary>
        /// <param name="type">Selector type</param>
        /// <param name="name">Base page name</param>
        private Selector(SelectorType type, string name)
            : this(type, Namespace.Main, name)
        {
        }

        /// <summary>
        /// Create a selector for all pages inside the specified category (and all its subcategories)
        /// </summary>
        /// <param name="name">Category name</param>
        public static Selector PagesInsideCategory(string name)
        {
            return new Selector(SelectorType.InsideCategory, Namespace.Category, name);
        }

        /// <summary>
        /// Create a selector for all pages inside all categories with name matching the given regular expression (and all their subcategories)
        /// </summary>
        /// <param name="re">Category name regular expression</param>
        public static Selector PagesInCategoryRE(string re)
        {
            return new Selector(SelectorType.InCategoryRegExp, re);
        }

        /// <summary>
        /// Create a selector for all pages that are more times within the specified category or its subcategories
        /// </summary>
        /// <param name="name">Category name</param>
        public static Selector PagesDuplicateWithinCategory(string name)
        {
            return new Selector(SelectorType.DuplicateWithinCategory, Namespace.Category, name);
        }

        /// <summary>
        /// Create a selector for all pages in the specified namespace
        /// </summary>
        /// <param name="ns">Namespace</param>
        public static Selector PagesInNamespace(Namespace ns)
        {
            return new Selector(SelectorType.InNamespace, ns, "");
        }

        /// <summary>
        /// Create a selector for all pages linking to the specified template
        /// </summary>
        /// <param name="ns">Template namespace</param>
        /// <param name="name">Template name</param>
        public static Selector LinkingToTemplate(Namespace ns, string name)
        {
            return new Selector(SelectorType.LinkingToTemplate, ns, name);
        }

        /// <summary>
        /// Create a selector for all pages linking to the specified template
        /// </summary>
        /// <param name="name">Template name</param>
        public static Selector LinkingToTemplate(string name)
        {
            return LinkingToTemplate(Namespace.Template, name);
        }

        /// <summary>
        /// Create a selector for all pages linking to the specified page
        /// </summary>
        /// <param name="ns">Target page namespace</param>
        /// <param name="name">Target page name</param>
        public static Selector LinkingToPage(Namespace ns, string name)
        {
            return new Selector(SelectorType.LinkingToPage, ns, name);
        }

        /// <summary>
        /// Create a selector for all pages containing an external link matching the given regular expression
        /// </summary>
        /// <param name="re">Regular expression for the external link</param>
        public static Selector WithExtLinkRE(string re)
        {
            return new Selector(SelectorType.WithExtLink, re);
        }
    }

    public class BrowserEngine : IEnumerable<Page>
    {
        internal Dictionary<int, Page> PagesById = new Dictionary<int, Page>();
        internal Dictionary<Pair<Namespace, string>, Page> PagesByName = new Dictionary<Pair<Namespace, string>, Page>();
        internal List<Category> Categories = new List<Category>();

        internal Dictionary<Page, HashSet<Page>> ArticlesLinkingToArticle;

        internal Dictionary<Page, int> PageViews = new Dictionary<Page, int>();

        private readonly string filenameBase;

        public event EventHandler<FileLoadNotificationEventArgs> Error;
        public event EventHandler<FileLoadNotificationEventArgs> Warning;
        public event EventHandler<FileLoadNotificationEventArgs> Notice;
        public event EventHandler<FileLoadNotificationEventArgs> LoadProgress;

        protected void OnError(string message)
        {
            if (Error != null)
            {
                Error(this, new FileLoadNotificationEventArgs(message, fileReader.FileName, fileReader.Line, fileReader.Column));
            }
        }

        protected void OnWarning(string message)
        {
            if (Warning != null)
            {
                Warning(this, new FileLoadNotificationEventArgs(message, fileReader.FileName, fileReader.Line, fileReader.Column));
            }
        }

        protected void OnNotice(string message)
        {
            if (Notice != null)
            {
                Notice(this, new FileLoadNotificationEventArgs(message, fileReader.FileName, fileReader.Line, fileReader.Column));
            }
        }

        protected void OnLoadProgress(string message)
        {
            if (LoadProgress != null)
            {
                if (fileReader != null)
                    LoadProgress(this, new FileLoadNotificationEventArgs(message, fileReader.FileName, fileReader.Line, fileReader.Column));
                else
                    LoadProgress(this, new FileLoadNotificationEventArgs(message, "", 0, 0));
            }
        }

        private CountingStreamReader fileReader;
        private HashSet<Page> resultSet;
        private Dictionary<string, HashSet<Page>> variables = new Dictionary<string, HashSet<Page>>();

        private void LoadSqlFile(string filename, EventHandler<RowEventArgs> processEntryDelegate)
        {
            if (processEntryDelegate == null) throw new ArgumentNullException("processEntryDelegate");
            using (TableReaderAdapter reader = TableReaderAdapter.Create(filename))
            {
                fileReader = reader.UnderlyingStreamReader ?? new CountingStreamReader("-", Stream.Null, Encoding.ASCII);
                reader.RowComplete += processEntryDelegate;
                reader.Parse();
            }
            fileReader = null;
        }

        private void LoadSpaceSeparatedFile(string filename, EventHandler<RowEventArgs> processEntryDelegate)
        {
            if (processEntryDelegate == null) throw new ArgumentNullException("processEntryDelegate");
            using (var streamReader = new CountingStreamReader(filename, DataFileTools.OpenInputFile(filename), Encoding.UTF8))
            {
                fileReader = streamReader;
                var reader = new SpaceSeparatedFileParser(streamReader);
                reader.RowComplete += processEntryDelegate;
                reader.Parse();
            }
            fileReader = null;
        }

        private static HashSet<Category> processedCategories;

        private static void CategoryWalk(Category rootCat, EventHandler<PageEventArgs> processPage)
        {
            if (rootCat == null) throw new ArgumentNullException("rootCat");
            if (processPage == null) throw new ArgumentNullException("processPage");
            if (processedCategories == null) throw new InvalidOperationException("processedCategories need to be setup first");

            if (processedCategories.Contains(rootCat)) return;
            processedCategories.Add(rootCat);

            foreach (Page page in rootCat.Articles)
            {
                processPage(rootCat, new PageEventArgs(page));
            }

            foreach (Category subcat in rootCat.Subcategories)
            {
                CategoryWalk(subcat, processPage);
            }
        }

        private static void TemplateWalk(Page template, IEnumerable<Page> pageSet, EventHandler<PageEventArgs> processTemplateUse)
        {
            if (template == null) throw new ArgumentNullException("template");
            if (pageSet == null) throw new ArgumentNullException("pageSet");
            if (processTemplateUse == null) throw new ArgumentNullException("processTemplateUse");

            foreach (Page page in pageSet)
            {
                if (page.Templates.Contains(template))
                {
                    processTemplateUse(template, new PageEventArgs(page));
                }
            }
        }

        private static void NamespaceWalk(Namespace ns, IEnumerable<Page> pageSet, EventHandler<PageEventArgs> processPage)
        {
            if (pageSet == null) throw new ArgumentNullException("pageSet");
            if (processPage == null) throw new ArgumentNullException("processPage");

            foreach (Page page in pageSet)
            {
                if (page.Namespace == ns)
                {
                    processPage(ns, new PageEventArgs(page));
                }
            }
        }

        private static void IntLinkWalk(Page linkedPage, IEnumerable<Page> pageSet, EventHandler<PageEventArgs> processPage)
        {
            if (linkedPage == null) throw new ArgumentNullException("linkedPage");
            if (pageSet == null) throw new ArgumentNullException("pageSet");
            if (processPage == null) throw new ArgumentNullException("processPage");

            foreach (Page page in pageSet)
            {
                if (page.InternalLinks.Contains(linkedPage))
                {
                    processPage(linkedPage, new PageEventArgs(page));
                }
            }
        }

        private static void ExtLinkWalk(Regex re, IEnumerable<Page> pageSet, EventHandler<PageEventArgs> processPage)
        {
            if (re == null) throw new ArgumentNullException("re");
            if (pageSet == null) throw new ArgumentNullException("pageSet");
            if (processPage == null) throw new ArgumentNullException("processPage");

            foreach (Page page in pageSet)
            {
                bool linking = false;
                foreach (string link in page.ExternalLinks)
                {
                    if (re.IsMatch(link))
                    {
                        linking = true;
                        break;
                    }
                }
                if (linking) processPage(re, new PageEventArgs(page));
            }
        }

        private bool catLinksLoaded;

        public void LoadCatLinks()
        {
            if (catLinksLoaded) return;
            OnLoadProgress("Loading category links");
            LoadSqlFile(filenameBase + "categorylinks", AddCategoryLink);
            OnLoadProgress("Category links loaded");
            catLinksLoaded = true;
        }

        private bool templateLinksLoaded;

        public void LoadTemplateLinks()
        {
            if (templateLinksLoaded) return;
            OnLoadProgress("Loading template links");
            LoadSqlFile(filenameBase + "templatelinks", AddTemplateLink);
            OnLoadProgress("Template links loaded");
            templateLinksLoaded = true;
        }

        private bool pageLinksLoaded;

        public void LoadPageLinks()
        {
            if (pageLinksLoaded) return;
            OnLoadProgress("Loading page links");
            LoadSqlFile(filenameBase + "pagelinks", AddPageLink);
            OnLoadProgress("Page links loaded");
            pageLinksLoaded = true;
        }

        private bool extLinksLoaded;

        public void LoadExtLinks()
        {
            if (extLinksLoaded) return;
            OnLoadProgress("Loading external links");
            LoadSqlFile(filenameBase + "externallinks", AddExternalLink);
            OnLoadProgress("External links loaded");
            extLinksLoaded = true;
        }

        private bool pageViewsLoaded;

        public void LoadPageViews(HashSet<string> projectIds)
        {
            if (pageViewsLoaded) return;
            OnLoadProgress("Loading page views");

            foreach (var pageViewFile in Directory.GetFiles(filenameBase + @"pageviews"))
            {
                LoadSpaceSeparatedFile(pageViewFile, (_, args) =>
                {
                    if (projectIds.Contains(args.Columns[0]))
                    {
                        var title = ParseTitle(args.Columns[1]);
                        Page page;
                        if (!PagesByName.TryGetValue(title, out page))
                        {
                            OnWarning("Unknown page '" + title + "'");
                            return;
                        }
                        int currViews;
                        PageViews.TryGetValue(page, out currViews);
                        PageViews[page] = currViews + Int32.Parse(args.Columns[2], CultureInfo.InvariantCulture);
                    }
                });
            }
            OnLoadProgress("Page views loaded");
            pageViewsLoaded = true;
        }

        public BrowserEngine(string filenameBase)
        {
            if (String.IsNullOrEmpty(filenameBase)) throw new ArgumentException("Need base filename");

            if (filenameBase.Contains("://"))
            {
                if (!filenameBase.EndsWith("/")) filenameBase += '/';
            }
            else
            {
                if (!filenameBase.EndsWith("-")) filenameBase += '-';
            }
            this.filenameBase = filenameBase;
            resultSet = new HashSet<Page>();
        }

        private bool baseDataLoaded;

        public void Load()
        {
            if (baseDataLoaded) return;
            OnLoadProgress("Loading pages");
            LoadSqlFile(filenameBase + "page", AddPage);
            OnLoadProgress("Pages loaded");
            baseDataLoaded = true;
        }

        public static Pair<Namespace, string> ParseTitle(string title)
        {
            if (title == null) throw new ArgumentNullException("title");
            if (title.Length == 0) throw new FormatException("Invalid title");
            int idx = title.IndexOf(':');
            if (idx < 0) return new Pair<Namespace, string>(Namespace.Main, title);
            if (idx == 0) return new Pair<Namespace, string>(Namespace.Main, title.Substring(1));
            string nsstr = title.Substring(0, idx);
            title = title.Substring(idx + 1);
            Namespace ns;
            try
            {
                int nsNumber;
                if (Int32.TryParse(nsstr, NumberStyles.Number, CultureInfo.InvariantCulture, out nsNumber))
                {
                    ns = (Namespace) nsNumber;
                }
                else
                {
                    ns = (Namespace) Enum.Parse(typeof(Namespace), nsstr.Replace(' ', '_'), true);
                }
                if (title.Length == 0) throw new FormatException("Invalid title");
            }
            catch (ArgumentException)
            {
                ns = Namespace.Main;
                title = nsstr + ":" + title;
            }
            return new Pair<Namespace, string>(ns, title);
        }

        private HashSet<Page> PagesInNamespace(Namespace ns, IEnumerable<Page> universalSet)
        {
            Load();
            processedPages = new HashSet<Page>();
            NamespaceWalk(ns, universalSet, ProcessPageInResult);
            return processedPages;
        }

        private HashSet<Page> PagesInsideCategory(Category category)
        {
            LoadCatLinks();
            processedPages = new HashSet<Page>();
            processedCategories = new HashSet<Category>();
            CategoryWalk(category, ProcessPageInResult);
            return processedPages;
        }

        private HashSet<Page> PagesInCategoryRegExp(string categoryRe)
        {
            var re = new Regex(categoryRe);
            LoadCatLinks();
            processedPages = new HashSet<Page>();
            foreach (Category cat in Categories)
            {
                if (re.IsMatch(cat.Title))
                {
                    processedCategories = new HashSet<Category>();
                    CategoryWalk(cat, ProcessPageInResult);
                }
            }
            return processedPages;
        }

        private HashSet<Page> PagesDuplicateWithinCategory(Category category)
        {
            LoadCatLinks();
            processedPages = new HashSet<Page>();
            processedCategories = new HashSet<Category>();
            var duplicatePages = new HashSet<Page>();
            CategoryWalk(category, delegate(object sender, PageEventArgs eventArgs)
            {
                Page page = eventArgs.Page;
                if (processedPages.Contains(page))
                {
                    duplicatePages.Add(page);
                }
                else
                {
                    processedPages.Add(page);
                }
            });
            return duplicatePages;
        }

        private HashSet<Page> PagesLinkingToTemplate(Page template, IEnumerable<Page> universalSet)
        {
            LoadTemplateLinks();
            processedPages = new HashSet<Page>();
            TemplateWalk(template, universalSet, ProcessPageInResult);
            return processedPages;
        }

        private HashSet<Page> PagesLinkingToPage(Page targetPage, IEnumerable<Page> universalSet)
        {
            LoadPageLinks();
            processedPages = new HashSet<Page>();
            IntLinkWalk(targetPage, universalSet, ProcessPageInResult);
            return processedPages;
        }

        private HashSet<Page> PagesWithExtLink(string extlink, IEnumerable<Page> universalSet)
        {
            LoadExtLinks();
            processedPages = new HashSet<Page>();
            var re = new Regex(extlink);
            ExtLinkWalk(re, universalSet, ProcessPageInResult);
            return processedPages;
        }

        private HashSet<Page> Evaluate(Selector sel, IEnumerable<Page> universalSet)
        {
            switch (sel.Type)
            {
                case Selector.SelectorType.InNamespace:
                    return PagesInNamespace(sel.Namespace, universalSet);

                case Selector.SelectorType.InsideCategory:
                    return PagesInsideCategory((Category) sel.GetPage(this));

                case Selector.SelectorType.InCategoryRegExp:
                    return PagesInCategoryRegExp(sel.Name);

                case Selector.SelectorType.DuplicateWithinCategory:
                    return PagesDuplicateWithinCategory((Category) sel.GetPage(this));

                case Selector.SelectorType.LinkingToTemplate:
                    return PagesLinkingToTemplate(sel.GetPage(this), universalSet);

                case Selector.SelectorType.LinkingToPage:
                    return PagesLinkingToPage(sel.GetPage(this), universalSet);

                case Selector.SelectorType.WithExtLink:
                    return PagesWithExtLink(sel.Name, universalSet);

                default:
                    throw new ArgumentException("Unknown selector type", "sel");
            }
        }

        public int Operate(Operator op, Selector sel)
        {
            HashSet<Page> rh;
            switch (op)
            {
                case Operator.Replace:
                    rh = Evaluate(sel, PagesById.Values);
                    resultSet = rh;
                    break;

                case Operator.Add:
                    rh = Evaluate(sel, PagesById.Values);
                    resultSet.UnionWith(rh);
                    break;

                case Operator.Subtract:
                    rh = Evaluate(sel, resultSet);
                    resultSet.ExceptWith(rh);
                    break;

                case Operator.ReverseSubtract:
                    rh = Evaluate(sel, PagesById.Values);
                    rh.ExceptWith(resultSet);
                    resultSet = rh;
                    break;

                case Operator.Intersect:
                    rh = Evaluate(sel, resultSet);
                    resultSet.IntersectWith(rh);
                    break;

                default:
                    throw new ArgumentOutOfRangeException("op");
            }
            return rh.Count;
        }

        public int ComputeCount(Selector sel)
        {
            return Evaluate(sel, PagesById.Values).Count;
        }

        public IEnumerator<Page> GetPages(Selector sel)
        {
            return Evaluate(sel, PagesById.Values).GetEnumerator();
        }

        private class CategoryCycleDetector
        {
            private HashSet<Category> current = new HashSet<Category>();
            private Stack<Category> stack = new Stack<Category>();
            private HashSet<Category> cycledCategories = new HashSet<Category>();
            private HashSet<Pair<int, int>> usedEdges = new HashSet<Pair<int, int>>();

            private HashSet<Category> visitedCategories = new HashSet<Category>();

            public List<List<Category>> Result
            {
                get { return result; }
            }

            private List<List<Category>> result = new List<List<Category>>();

            public CategoryCycleDetector(Category rootCategory)
            {
                FindCyclesRecursive(rootCategory);
            }

            private void FindCyclesRecursive(Category root)
            {
                if (current.Contains(root))
                {
                    var cycle = new List<Category>();
                    cycle.Add(root);
                    foreach (Category cat in stack)
                    {
                        cycle.Add(cat);
                        cycledCategories.Add(cat);
                        if (cat == root) break;
                    }
                    result.Add(cycle);
                    return;
                }
                if (visitedCategories.Contains(root)) return;
                visitedCategories.Add(root);
                current.Add(root);
                stack.Push(root);
                foreach (Category subcat in root.Subcategories)
                {
                    if (cycledCategories.Contains(subcat)) continue;

                    var edge = new Pair<int, int>(root.Id, subcat.Id);
                    if (usedEdges.Contains(edge)) continue;
                    usedEdges.Add(edge);

                    FindCyclesRecursive(subcat);
                }
                stack.Pop();
                current.Remove(root);
            }
        }

        public List<List<Category>> FindCategoryCycles(Category root)
        {
            LoadCatLinks();
            var detector = new CategoryCycleDetector(root);
            return detector.Result;
        }

        private struct PathVertexInfo
        {
            public int Depth;
            public Page From;
        }

        private void ComputeArticlesLinkingToArticle()
        {
            if (ArticlesLinkingToArticle != null) return;

            LoadPageLinks();
            Console.WriteLine("Computing reverse links");
            var result = new Dictionary<Page, HashSet<Page>>(PagesById.Count);
            foreach (var page in PagesById.Values)
            {
                if (page.Namespace != Namespace.Main) continue;

                foreach (var link in page.InternalLinks)
                {
                    if (link.Namespace != Namespace.Main) continue;

                    HashSet<Page> links;
                    if (!result.TryGetValue(link, out links))
                    {
                        links = new HashSet<Page>();
                        result.Add(link, links);
                    }
                    links.Add(page);
                }
            }
            ArticlesLinkingToArticle = result;
            Console.WriteLine("Reverse links computed");
        }

        public Tuple<int, List<List<Page>>> GloballyLongestPaths()
        {
            LoadPageLinks();
            var pages = new Dictionary<int, Page>();
            var pagesRev = new Dictionary<Page, int>();
            var idx = 0;
            foreach (var page in PagesById.Values)
            {
                if (page.Namespace == Namespace.Main)
                {
                    pages[idx] = page;
                    pagesRev[page] = idx;
                    ++idx;
                }
            }
            int n = pages.Count;
            Debug.Assert(n > 0);
            var dist = new sbyte[n, n];
            var next = new int[n, n];
            for (var i = 0; i < n; ++i)
            {
                for (var j = 0; j < n; ++j)
                {
                    dist[i, j] = -1;
                }
            }
            for (var i = 0; i < n; ++i)
            {
                var page = pages[i];
                var w = (sbyte) (page.IsRedirect ? 1 : 0);
                foreach (var link in page.InternalLinks)
                {
                    int linkIdx;
                    if (pagesRev.TryGetValue(link, out linkIdx))
                    {
                        dist[i, linkIdx] = w;
                        next[i, linkIdx] = linkIdx;
                    }
                }
            }
            for (var k = 0; k < n; ++k)
            {
                for (var i = 0; i < n; ++i)
                {
                    for (var j = 0; j < n; ++j)
                    {
                        if (i == j) continue;

                        var dik = dist[i, k];
                        var dkj = dist[k, j];
                        if (dik >= 0 && dkj >= 0)
                        {
                            var dij = dist[i, j];
                            if (dij < 0 || dij > dik + dkj)
                            {
                                dist[i, j] = (sbyte) (dik + dkj);
                                next[i, j] = next[i, k];
                            }
                        }
                    }
                }
            }

            var max = -1;
            List<Tuple<int, int>> maxEndpoints = new List<Tuple<int, int>>();
            for (var i = 0; i < n; ++i)
            {
                for (var j = 0; j < n; ++j)
                {
                    var d = dist[i, j];
                    if (d > max)
                    {
                        max = d;
                        maxEndpoints.Clear();
                    }
                    if (d == max)
                    {
                        maxEndpoints.Add(Tuple.Create(i, j));
                    }
                }
            }

            var resultList = new List<List<Page>>(maxEndpoints.Count);
            foreach (var maxEndpoint in maxEndpoints)
            {
                var result = new List<Page>();
                var u = maxEndpoint.Item1;
                var v = maxEndpoint.Item2;
                result.Add(pages[u]);
                while (u != v)
                {
                    u = next[u, v];
                    result.Add(pages[u]);
                }
                resultList.Add(result);
            }
            return Tuple.Create(max, resultList);
        }

        public Pair<int, List<List<Page>>> LongestPathTo(Page destination)
        {
            ComputeArticlesLinkingToArticle();
            var pagesLinkingTo = ArticlesLinkingToArticle;

            var queue = new Queue<Page>();
            var processed = new Dictionary<Page, PathVertexInfo>(pagesLinkingTo.Count);
            var max = 0;
            var pages = new List<Page>();
            queue.Enqueue(destination);
            processed.Add(destination, new PathVertexInfo { Depth = 0, From = null });
            while (queue.Count > 0)
            {
                var page = queue.Dequeue();
                var pageInfo = processed[page];
                var depth = pageInfo.Depth;

                if (depth > max)
                {
                    pages.Clear();
                    max = depth;
                }
                pages.Add(page);

                HashSet<Page> links;
                if (pagesLinkingTo.TryGetValue(page, out links))
                {
                    foreach (var link in links)
                    {
                        if (!processed.ContainsKey(link))
                        {
                            queue.Enqueue(link);
                            processed.Add(link, new PathVertexInfo { Depth = link.IsRedirect ? depth : depth + 1, From = page });
                        }
                    }
                }
            }

            pages = pages.Where(p => processed[p].Depth == max).ToList();

            var resultList = new List<List<Page>>(pages.Count);
            foreach (var page in pages)
            {
                var p = page;
                var list = new List<Page>();
                while (p != null)
                {
                    list.Add(p);
                    var info = processed[p];
                    p = info.From;
                }
                resultList.Add(list);
            }

            return new Pair<int, List<List<Page>>>(max, resultList);
        }

        public Dictionary<string, int> FindRedlinks()
        {
            LoadPageLinks();

            var redLinks = new Dictionary<string, int>();
            foreach (var page in PagesById.Values)
            {
                if (page.Namespace != Namespace.Main) continue;

                foreach (var linked in page.InternalLinks)
                {
                    if (linked.Namespace != Namespace.Main) continue;
                    if (linked.Id < 0)
                    {
                        // red link (missing target)
                        int currCount;
                        redLinks.TryGetValue(linked.Title, out currCount);
                        redLinks[linked.Title] = currCount + 1;
                    }
                }
            }
            return redLinks;
        }

        public void StoreVariable(string name)
        {
            variables[name] = resultSet;
        }

        public bool LoadVariable(string name)
        {
            HashSet<Page> variable;
            if (!variables.TryGetValue(name, out variable)) return false;
            resultSet = variable;
            return true;
        }

        public bool UnsetVariable(string name)
        {
            return variables.Remove(name);
        }

        public int Count
        {
            get { return resultSet.Count; }
        }

        public IEnumerator<Page> GetEnumerator()
        {
            return resultSet.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public IEnumerable<string> VariableNames
        {
            get
            {
                foreach (string s in variables.Keys)
                {
                    yield return s;
                }
            }
        }

        private static HashSet<Page> processedPages;

        private static void ProcessPageInResult(object sender, PageEventArgs eventArgs)
        {
            Page page = eventArgs.Page;
            processedPages.Add(page);
        }

        private void AddPage(object sender, RowEventArgs eventArgs)
        {
            IList<string> columns = eventArgs.Columns;
            if (columns.Count != 13) throw new FormatException("Column count mismatch");

            int id = Convert.ToInt32(columns[0], CultureInfo.InvariantCulture);
            Namespace ns = (Namespace) Convert.ToInt32(columns[1], CultureInfo.InvariantCulture);
            string title = columns[2];
            /*
            string restrictions = columns[3];
            */
            bool isRedirect = Convert.ToInt32(columns[4], CultureInfo.InvariantCulture) != 0;
            /*
            bool isNew = Convert.ToInt32(columns[5], CultureInfo.InvariantCulture) != 0;
            double random = Convert.ToDouble(columns[6], CultureInfo.InvariantCulture);
            string touched = columns[7];
            string linksUpdated = columns[8];
            int latest = Convert.ToInt32(columns[9], CultureInfo.InvariantCulture);
            int len = Convert.ToInt32(columns[10], CultureInfo.InvariantCulture);
            string contentModel = columns[11];
            string lang = columns[12];
            */

            Page page;
            if (ns == Namespace.Category)
            {
                var cat = new Category(title, id);
                page = cat;
                Categories.Add(cat);
            }
            else page = new Page(ns, title, id, isRedirect);
            PagesById.Add(id, page);
            PagesByName.Add(new Pair<Namespace, string>(ns, title), page);
        }

        private void AddCategoryLink(object sender, RowEventArgs eventArgs)
        {
            IList<string> columns = eventArgs.Columns;
            if (columns.Count != 7) throw new FormatException("Column count mismatch");

            int from = Convert.ToInt32(columns[0], CultureInfo.InvariantCulture);
            string to = columns[1];
            //string sortKey = columns[2];
            //string sortKeyPrefix = columns[3];
            string timestamp = columns[4];
            //string collation = columns[5];
            //string clType = columns[6];

            Page fromPage, toPage;
            if (!PagesById.TryGetValue(from, out fromPage))
            {
                OnWarning(String.Format("Category link {0} → {1} broken (since {2})", from, to, timestamp));
                return;
            }
            if (!PagesByName.TryGetValue(new Pair<Namespace, string>(Namespace.Category, to), out toPage))
            {
                toPage = new Category(to, -1);
                PagesByName.Add(new Pair<Namespace, string>(Namespace.Category, to), toPage);
                OnNotice(String.Format("Missing category {1} (requested by {0})", fromPage, to));
            }
            Category category = (Category) toPage;
            fromPage.Categories.Add(category);

            Category fromCategory = fromPage as Category;
            if (fromCategory == null) category.Articles.Add(fromPage);
            else category.Subcategories.Add(fromCategory);
        }

        private void AddTemplateLink(object sender, RowEventArgs eventArgs)
        {
            IList<string> columns = eventArgs.Columns;
            if (columns.Count != 4) throw new FormatException("Column count mismatch");

            int from = Convert.ToInt32(columns[0], CultureInfo.InvariantCulture);
            Namespace ns = (Namespace) Convert.ToInt32(columns[1], CultureInfo.InvariantCulture);
            string title = columns[2]; 
            // var fromNs = (Namespace) Convert.ToInt32(columns[3], CultureInfo.InvariantCulture);
                
            Page fromPage, toPage;
            if (!PagesById.TryGetValue(from, out fromPage))
            {
                OnWarning(String.Format("Template link {0} → {1}:{2} broken", from, ns, title));
                return;
            }
            if (!PagesByName.TryGetValue(new Pair<Namespace, string>(ns, title), out toPage))
            {
                toPage = new Page(ns, title, -2, false);
                PagesByName.Add(new Pair<Namespace, string>(ns, title), toPage);
                OnNotice(String.Format("Missing template {1}:{2} (requested by {0})", fromPage, ns, title));
            }

            fromPage.Templates.Add(toPage);
        }

        private void AddPageLink(object sender, RowEventArgs eventArgs)
        {
            IList<string> columns = eventArgs.Columns;
            if (columns.Count != 4) throw new FormatException("Column count mismatch");

            int from = Convert.ToInt32(columns[0], CultureInfo.InvariantCulture);
            Namespace ns = (Namespace) Convert.ToInt32(columns[1], CultureInfo.InvariantCulture);
            string title = columns[2];
            //Namespace fromNs = (Namespace)Convert.ToInt32(columns[3], CultureInfo.InvariantCulture);

            Page fromPage, toPage;
            if (!PagesById.TryGetValue(from, out fromPage))
            {
                OnWarning(String.Format("Page link {0} → {1}:{2} broken", from, ns, title));
                return;
            }
            if (!PagesByName.TryGetValue(new Pair<Namespace, string>(ns, title), out toPage))
            {
                // wanted page
                toPage = new Page(ns, title, -3, false);
                PagesByName.Add(new Pair<Namespace, string>(ns, title), toPage);
            }

            fromPage.InternalLinks.Add(toPage);
            //toPage.PagesLinkingHere.Add(fromPage);
        }

        private void AddExternalLink(object sender, RowEventArgs eventArgs)
        {
            IList<string> columns = eventArgs.Columns;
            if (columns.Count != 3) throw new FormatException("Column count mismatch");

            int from = Convert.ToInt32(columns[0], CultureInfo.InvariantCulture);
            string to = columns[1];
            //string index = columns[2];

            Page fromPage;
            if (!PagesById.TryGetValue(from, out fromPage))
            {
                OnWarning(String.Format("External link {0} → {1} broken", from, to));
                return;
            }

            fromPage.ExternalLinks.Add(to);
        }
    }

    #endregion
}