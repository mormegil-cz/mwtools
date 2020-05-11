using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using MWLib;

namespace CatBrowser.UI
{
    public class CatBrowserConsole
    {
        private BrowserEngine engine;
        private string basePath = "";
        private Regex sortingRegex;
        private Regex transformRegex;
        private string transformReplacement;
        private CategoryFixups fixups;

        [DebuggerNonUserCode]
        private static void Main(string[] args)
        {
            try
            {
                new CatBrowserConsole(args).Run();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Unhandled error: {0}", ex);
            }
        }

        public CatBrowserConsole(string[] commandLineArgs)
        {
            ProcessCommandLineArguments(commandLineArgs);
        }

        public void Run()
        {
            using (loadLog = new StreamWriter("catbrowser.log", false))
            {
                engine = new BrowserEngine(basePath);
                engine.LoadProgress += LoadProgress;
                engine.Error += Error;
                engine.Warning += Error;
                engine.Notice += Error;
                engine.Load();

                while (true)
                {
                    Console.Write("> ");
                    string command = Console.ReadLine();
                    if (command == null) break;
                    command = command.Trim();

                    if (!ProcessCommand(command)) break;
                }
            }
        }

        private void ProcessCommandLineArguments(string[] args)
        {
            int idx = 0;
            string fixupFile = null;

            while (idx < args.Length)
            {
                string arg = args[idx];
                if (!arg.StartsWith("-")) break;
                if (arg == "--")
                {
                    ++idx;
                    break;
                }

                if (arg.StartsWith("--fixups="))
                {
                    if (fixupFile != null) throw new ArgumentException("Duplicate --fixups argument");
                    fixupFile = arg.Substring(9);
                }
                else throw new ArgumentException("Unknown parameter: " + arg);

                ++idx;
            }

            if (idx < args.Length)
            {
                basePath = args[idx];
                ++idx;
            }

            if (idx != args.Length) throw new ArgumentException("Invalid arguments");

            if (fixupFile != null)
            {
                using (var reader = new StreamReader(fixupFile, Encoding.UTF8))
                {
                    fixups = new CategoryFixups(reader);
                }
            }
        }

        private bool ProcessCommand(string command)
        {
            string[] split = command.Split(' ');
            string cmd0 = split.Length > 0 ? split[0] : "";

            if (command.Length == 0)
            {
                // do nothing
            }
            else if (command == "quit" || command == "exit" || command == "bye")
            {
                return false;
            }
            else if (cmd0 == "page")
            {
                LoadCatLinks();
                string name = String.Join("_", split, 1, split.Length - 1);
                if (name.Length == 0)
                {
                    Console.Write("Name: ");
                    name = Console.ReadLine();
                }
                Page page;
                Namespace ns;
                string title;
                Page.ParseTitle(name, out ns, out title);
                if (!engine.PagesByName.TryGetValue(new Pair<Namespace, string>(ns, title), out page))
                {
                    Console.WriteLine("No such page");
                    return true;
                }
                Console.WriteLine("ID = {0}", page.Id);
                Console.Write("Categories: ");
                bool first = true;
                foreach (Category cat in page.Categories)
                {
                    if (!first) Console.Write(", ");
                    first = false;
                    Console.Write(cat.Title);
                }
                if (page.Categories.Count == 0) Console.Write("(uncategorized)");
                Console.WriteLine();
            }
            else if (cmd0 == "category" || cmd0 == "cat")
            {
                LoadCatLinks();
                string name = String.Join("_", split, 1, split.Length - 1);
                if (name.Length == 0)
                {
                    Console.Write("Name: ");
                    name = Console.ReadLine();
                }
                Page page;
                if (!engine.PagesByName.TryGetValue(new Pair<Namespace, string>(Namespace.Category, name), out page))
                {
                    Console.WriteLine("No such category");
                    return true;
                }
                var cat = (Category) page;
                Console.WriteLine("ID = {0}", page.Id);
                Console.Write("Categories: ");
                bool first = true;
                foreach (Category supercat in page.Categories)
                {
                    if (!first) Console.Write(", ");
                    first = false;
                    Console.Write(supercat.Title);
                }
                if (page.Categories.Count == 0) Console.Write("(uncategorized)");
                Console.WriteLine();

                if (cat.Subcategories.Count > 0)
                {
                    Console.Write("Subcategories: ");
                    first = true;
                    foreach (Category subcat in cat.Subcategories)
                    {
                        if (!first) Console.Write(", ");
                        first = false;
                        Console.Write(subcat.Title);
                    }
                    Console.WriteLine();
                }

                if (cat.Articles.Count > 0)
                {
                    Console.Write("Pages: ");
                    first = true;
                    foreach (Page subpage in cat.Articles)
                    {
                        if (!first) Console.Write(", ");
                        first = false;
                        Console.Write(subpage.Title);
                    }
                    Console.WriteLine();
                }
            }
            else if (cmd0 == "+" || cmd0 == "-" || cmd0 == "~" || cmd0 == "=" || cmd0 == "*" || cmd0 == "?")
            {
                char op = cmd0[0];
                bool restrictingOp = op == '*' || op == '-';
                if (restrictingOp && engine.Count == 0)
                {
                    Console.WriteLine("Current result set is empty, no point in doing that operation");
                    return true;
                }

                if (split.Length < 2)
                {
                    Console.WriteLine("Missing set specifier (category, categoryre template, namespace, pagelink, extlink, duplicates)");
                    return true;
                }
                string set = split[1];
                string name = String.Join("_", split, 2, split.Length - 2);
                if (name.Length == 0)
                {
                    Console.Write("Name: ");
                    name = Console.ReadLine();
                    if (name == null) return false;
                }

                Selector selector;
                if (set == "category" || set == "cat")
                {
                    LoadCatLinks();
                    selector = Selector.PagesInsideCategory(name);
                }
                else if (set == "categoryre" || set == "catre")
                {
                    LoadCatLinks();
                    selector = Selector.PagesInCategoryRE(name);
                }
                else if (set == "dupes" || set == "duplicates")
                {
                    LoadCatLinks();
                    selector = Selector.PagesDuplicateWithinCategory(name);
                }
                else if (set == "template")
                {
                    selector = Selector.LinkingToTemplate(name);
                }
                else if (set == "ns" || set == "namespace")
                {
                    Namespace ns;
                    try
                    {
                        int nsint;
                        if (Int32.TryParse(name, out nsint))
                        {
                            ns = (Namespace) nsint;
                        }
                        else
                        {
                            ns = (Namespace) Enum.Parse(typeof(Namespace), name.Replace(' ', '_'), true);
                        }
                    }
                    catch (ArgumentException)
                    {
                        Console.WriteLine("Unknown namespace");
                        return true;
                    }
                    selector = Selector.PagesInNamespace(ns);
                }
                else if (set == "pagelink")
                {
                    try
                    {
                        Pair<Namespace, string> title = BrowserEngine.ParseTitle(name);
                        selector = Selector.LinkingToPage(title.First, title.Second);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e.Message);
                        return true;
                    }
                }
                else if (set == "extlink")
                {
                    selector = Selector.WithExtLinkRE(name);
                }
                else
                {
                    Console.WriteLine("Unknown set specifier");
                    return true;
                }

                Operator operation;
                switch (op)
                {
                    case '=':
                        operation = Operator.Replace;
                        break;

                    case '+':
                        operation = Operator.Add;
                        break;

                    case '-':
                        operation = Operator.Subtract;
                        break;

                    case '~':
                        operation = Operator.ReverseSubtract;
                        break;

                    case '*':
                        operation = Operator.Intersect;
                        break;

                    case '?':
                        Console.WriteLine("Set {0} contains {1} pages", name, engine.ComputeCount(selector));
                        return true;

                    default:
                        Console.WriteLine("Unknown operator");
                        return true;
                }

                int result;
                try
                {
                    result = engine.Operate(operation, selector);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                    return true;
                }
                Console.WriteLine("Set {0} contains {1} pages; current result size: {2}", name, result, engine.Count);
            }
            else if (command == "count")
            {
                Console.WriteLine("Current result size: " + engine.Count);
            }
            else if (cmd0 == "list")
            {
                if (split.Length > 2)
                {
                    Console.WriteLine("Too many parameters");
                    return true;
                }
                int limit;
                if (split.Length > 1)
                    Int32.TryParse(split[1], out limit);
                else
                    limit = 0;

                int count = 0;
                if (sortingRegex != null)
                {
                    var outputList = new SortedDictionary<Pair<string, string>, string>();
                    foreach (Page page in engine)
                    {
                        string title = page.ToString();
                        Match sortKeyMatch = sortingRegex.Match(title);
                        string sortKey = (sortKeyMatch.Success && sortKeyMatch.Value.Length > 0) ? sortKeyMatch.Value : title;
                        outputList.Add(new Pair<string, string>(sortKey, title), title);
                    }
                    foreach (string title in outputList.Values)
                    {
                        if (limit > 0 && ++count > limit) break;
                        string t = title;
                        if (transformRegex != null)
                            t = transformRegex.Replace(t, transformReplacement);
                        Console.WriteLine(t);
                    }
                }
                else
                {
                    foreach (Page page in engine)
                    {
                        if (limit > 0 && ++count > limit) break;
                        string title = page.ToString();
                        if (transformRegex != null)
                            title = transformRegex.Replace(title, transformReplacement);
                        Console.WriteLine(title);
                    }
                }
            }
            else if (cmd0 == "set")
            {
                string name = String.Join(" ", split, 1, split.Length - 1);
                if (name.Length == 0)
                {
                    Console.Write("Name: ");
                    name = Console.ReadLine();
                }

                engine.StoreVariable(name);
            }
            else if (cmd0 == "get")
            {
                string name = String.Join(" ", split, 1, split.Length - 1);
                if (name.Length == 0)
                {
                    Console.Write("Name: ");
                    name = Console.ReadLine();
                }

                if (engine.LoadVariable(name))
                {
                    Console.WriteLine(String.Format("Current result size: {0}", engine.Count));
                }
                else
                {
                    Console.WriteLine("Variable not found");
                }
            }
            else if (command == "unset")
            {
                string name = String.Join(" ", split, 1, split.Length - 1);
                if (name.Length == 0)
                {
                    Console.Write("Name: ");
                    name = Console.ReadLine();
                }

                if (!engine.UnsetVariable(name))
                {
                    Console.WriteLine("Variable not found");
                }
            }
            else if (command == "transform")
            {
                Console.Write("Search: ");
                string search = Console.ReadLine();
                if (search == null) return false;
                if (search.Length == 0)
                {
                    transformRegex = null;
                    transformReplacement = null;
                    return true;
                }
                try
                {
                    transformRegex = new Regex(search);
                }
                catch (ArgumentException e)
                {
                    Console.WriteLine(e.Message);
                    transformRegex = null;
                    transformReplacement = null;
                    return true;
                }
                Console.Write("Replace: ");
                transformReplacement = Console.ReadLine();
            }
            else if (cmd0 == "sort")
            {
                string regex = String.Join(" ", split, 1, split.Length - 1);
                if (regex.Length == 0)
                {
                    sortingRegex = null;
                }
                else
                {
                    try
                    {
                        sortingRegex = new Regex(regex);
                    }
                    catch (ArgumentException e)
                    {
                        Console.WriteLine(e.Message);
                        sortingRegex = null;
                        return true;
                    }
                }
            }
            else if (command == "vars")
            {
                foreach (string var in engine.VariableNames)
                {
                    Console.WriteLine(var);
                }
            }
            else if (cmd0 == "cycles")
            {
                LoadCatLinks();
                string name = String.Join("_", split, 1, split.Length - 1);
                if (name.Length == 0)
                {
                    Console.Write("Name: ");
                    name = Console.ReadLine();
                }
                Page page;
                if (!engine.PagesByName.TryGetValue(new Pair<Namespace, string>(Namespace.Category, name), out page))
                {
                    Console.WriteLine("No such category");
                    return true;
                }

                List<List<Category>> cycles = engine.FindCategoryCycles((Category) page);
                int lastCount = -1;
                foreach (List<Category> cycle in cycles.OrderBy(c => c.Count))
                {
                    if (lastCount != cycle.Count)
                    {
                        lastCount = cycle.Count;
                        Console.WriteLine();
                        Console.WriteLine("{0}", lastCount);
                        Console.WriteLine();
                    }
                    bool first = true;
                    foreach (Category cat in cycle)
                    {
                        if (!first) Console.Write(" ← ");
                        first = false;
                        Console.Write(cat.Title);
                    }
                    Console.WriteLine();
                }
            }
            else if (cmd0 == "furthestfrom")
            {
                string name = String.Join("_", split, 1, split.Length - 1);
                if (name.Length == 0)
                {
                    Console.Write("Name: ");
                    name = Console.ReadLine();
                }
                Page page;
                if (!engine.PagesByName.TryGetValue(new Pair<Namespace, string>(Namespace.Main, name), out page))
                {
                    Console.WriteLine("No such page");
                    return true;
                }
                var longestPath = engine.LongestPathTo(page);
                Console.WriteLine("Longest path: {0}", longestPath.First);
                foreach (var furthestPage in longestPath.Second)
                {
                    Console.Write("* ");
                    foreach (var step in furthestPage)
                    {
                        Console.Write(" {0}", step.Title);
                    }
                    Console.WriteLine();
                }
            }
            else if (command == "longestpaths")
            {
                var longestPaths = engine.GloballyLongestPaths();
                Console.WriteLine("Longest path: {0}", longestPaths.Item1);
                foreach (var longestPath in longestPaths.Item2)
                {
                    Console.Write("* ");
                    foreach (var step in longestPath)
                    {
                        Console.Write(" {0}", step.Title);
                    }
                    Console.WriteLine();
                }
            }
            else if (command == "wantedarticles")
            {
                var redlinks = engine.FindRedlinks();
                using (var fs = new StreamWriter("wantedarticles.csv"))
                {
                    foreach (var wanted in redlinks.OrderByDescending(w => w.Value))
                    {
                        fs.WriteLine("{0};{1}", wanted.Value, wanted.Key);
                    }
                }
            }
            else if (cmd0 == "popular")
            {
                string limitStr, projectNames;
                if (split.Length < 2)
                {
                    Console.Write("Limit: ");
                    limitStr = Console.ReadLine();
                }
                else
                {
                    limitStr = split[1];
                }
                if (split.Length < 3)
                {
                    Console.Write("Project names: ");
                    projectNames = Console.ReadLine();
                }
                else
                {
                    projectNames = String.Join(" ", split.Skip(2));
                }
                int limit = Int32.Parse(limitStr);
                engine.LoadPageViews(new HashSet<string>(projectNames.Split(' ')));

                var outputList = new SortedDictionary<Pair<int, string>, string>();
                foreach (Page page in engine)
                {
                    int views;
                    var title = page.ToString();
                    engine.PageViews.TryGetValue(page, out views);
                    outputList[new Pair<int, string>(-views, title)] = title;
                }
                int count = 0;
                foreach (var key in outputList.Keys)
                {
                    if (limit > 0 && ++count > limit || key.First == 0) break;
                    var title = outputList[key];
                    string t = title;
                    if (transformRegex != null) t = transformRegex.Replace(t, transformReplacement);
                    Console.WriteLine("{0}: {1}", -key.First, t);
                }
            }
            else if (command == "help")
            {
                Console.WriteLine("Known commands: quit, page, category, +, -, ~, *, =, ?, list, count, set, get, vars, cycles, furthestfrom, longestpaths, wantedarticles, popular, transform, sort");
            }
            else
            {
                Console.WriteLine("Invalid command");
            }

            return true;
        }

        private static TextWriter loadLog;

        private static void Error(object sender, FileLoadNotificationEventArgs e)
        {
            loadLog.WriteLine("{0} [{1}:{2}] {3}", e.Filename, e.Line, e.Column, e.Message);
            loadLog.Flush();
        }

        private static void LoadProgress(object sender, FileLoadNotificationEventArgs e)
        {
            Console.WriteLine(e.Message);
        }

        private bool catLinksLoaded;

        private void LoadCatLinks()
        {
            if (catLinksLoaded) return;
            engine.LoadCatLinks();
            if (fixups != null)
            {
                fixups.ApplyTo(engine);
            }
            catLinksLoaded = true;
        }
    }
}