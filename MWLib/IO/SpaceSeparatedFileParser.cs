using System.IO;
using MWLib.Parsers;

namespace MWLib.IO
{
    public class SpaceSeparatedFileParser : DatabaseParser
    {
        /// <summary>
        /// Reader of the raw dump
        /// </summary>
        private readonly TextReader reader;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="reader">Reader providing the raw dump text</param>
        public SpaceSeparatedFileParser(TextReader reader)
        {
            this.reader = reader;
        }

        public override void Parse()
        {
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                OnRowComplete(line.Split(' '));
            }
        }
    }
}