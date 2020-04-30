using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace WikiHot
{
    public class PageCountsParser
    {
        private readonly TextReader reader;

        public PageCountsParser(TextReader reader)
        {
            if (reader == null) throw new ArgumentNullException("reader");
            this.reader = reader;
        }

        public IEnumerable<Tuple<string, int>> Parse()
        {
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                var fields = line.Split(' ');
                if (fields.Length != 3) throw new FormatException("Unexpected line format");
                if (fields[0] != "cs") continue;

                var title = Uri.UnescapeDataString(fields[1]);
                if (title.Length == 0) continue; // ?
                title = Char.ToUpper(title[0]) + title.Substring(1);
                var count = Int32.Parse(fields[2], CultureInfo.InvariantCulture);

                yield return new Tuple<string, int>(title, count);
            }
        }
    }
}
