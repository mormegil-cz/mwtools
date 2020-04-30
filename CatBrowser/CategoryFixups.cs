using System;
using System.Collections.Generic;
using System.IO;
using MWLib;

namespace CatBrowser
{
    public class CategoryFixups
    {
        private enum FixupOperation
        {
            None = 0,
            RemoveCategory
        }

        private struct Fixup
        {
            public FixupOperation Operation;
            public string TargetPage;
            public string OperationArgument;

            public Namespace TargetNamespace;
            public string TargetTitle;

            public Namespace ArgumentNamespace;
            public string ArgumentTitle;

            public Fixup(FixupOperation operation, string targetPage, string operationArgument)
            {
                Operation = operation;
                TargetPage = targetPage;
                OperationArgument = operationArgument;

                Page.ParseTitle(targetPage, out TargetNamespace, out TargetTitle);
                Page.ParseTitle(operationArgument, out ArgumentNamespace, out ArgumentTitle);
            }
        }

        private List<Fixup> Fixups;

        public CategoryFixups(StreamReader fixupData)
        {
            Fixups = new List<Fixup>();

            string lineStr;
            while ((lineStr = fixupData.ReadLine()) != null)
            {
                string line = lineStr;
                int comment = line.IndexOf('#');
                if (comment >= 0) line = line.Substring(comment);
                line = line.Trim();
                if (line.Length == 0) continue;

                string[] command = line.Split('|');
                if (command.Length != 3) throw new InvalidDataException("Bad syntax: " + lineStr);

                FixupOperation op;
                switch (command[0])
                {
                    case "-cat":
                        op = FixupOperation.RemoveCategory;
                        break;
                    default:
                        throw new InvalidDataException("Unknown command: " + command[0]);
                }

                Fixups.Add(new Fixup(op, command[1], command[2]));
            }
        }

        public void ApplyTo(BrowserEngine engine)
        {
            foreach (Fixup fixup in Fixups)
            {
                Page targetPage, argument;
                engine.PagesByName.TryGetValue(new Pair<Namespace, string>(fixup.TargetNamespace, fixup.TargetTitle), out targetPage);
                engine.PagesByName.TryGetValue(new Pair<Namespace, string>(fixup.ArgumentNamespace, fixup.ArgumentTitle), out argument);

                switch (fixup.Operation)
                {
                    case FixupOperation.RemoveCategory:
                        if (targetPage == null) throw new InvalidOperationException("Unknown title: " + fixup.TargetPage);
                        if (argument == null) throw new InvalidOperationException("Unknown title: " + fixup.OperationArgument);
                        if (argument.Namespace != Namespace.Category) throw new InvalidOperationException("Argument must be category: " + fixup.OperationArgument);

                        Category cat = (Category) argument;
                        if (!targetPage.Categories.Contains(cat)) throw new InvalidOperationException(String.Format("Page {0} is not a member of {1}", fixup.TargetPage, fixup.OperationArgument));

                        targetPage.Categories.Remove(cat);

                        Category targetCat = targetPage as Category;
                        if (targetCat != null)
                        {
                            cat.Subcategories.Remove(targetCat);
                        }
                        else
                        {
                            cat.Articles.Remove(targetPage);
                        }
                        break;
                }
            }
        }
    }
}