using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace WikiHot
{
    public class HourImport
    {
        public HourImport()
        {
        }

        public void DoImport(int hour, int day, IEnumerable<Tuple<string, int>> data)
        {
            if (hour == 0)
            {
                // UPDATE Views SET [Day{0}]=0
            }

            foreach (var page in data)
            {
                // SELECT [ID] FROM [Pages] WHERE [Title]=@title
                // INSERT OR UPDATE [Views] SET [Day{0}] = [Day{0}] + @views WHERE [ID]=@id
            }

            /*
             * 12 3 9 4 9 3 9 3 2 7 20 21

             */
            // UPDATE [Statistics] SET [Total] = [Day1] + [Day2] + [Day3] + [Day4] + ...
            // UPDATE [Statistics] SET [WikitrendsScore] = ABS(
            /* WikiTrends ([Wikipedia-l] Top topics):
             *
             * score = abs(h2 - h1) * log((h2 + 1) / (h1 + 1));
             * h2 = hits from now to 1 week ago
             * h1 = (hits from 1 week ago to 3 weeks ago) / 2
             */
        }
    }
}
