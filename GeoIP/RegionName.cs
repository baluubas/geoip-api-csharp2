/* Copyright 2013 Maxmind LLC All Rights Reserved */

using System;
using System.Collections.Specialized;
using System.IO;
using System.Reflection;

namespace GeoIP
{
    public static class RegionName
    {
        private static readonly Lazy<StringDictionary> Names = new Lazy<StringDictionary>(ReadRegionNames); 

        public static String GetRegionName(String ccode, String region)
        {
            if (region == null || region == "00" | !Names.Value.ContainsKey(ccode))
            {
                return null;
            }

            return Names.Value[ccode + region];
        }

        private static StringDictionary ReadRegionNames()
        {
            StringDictionary nameLookup = new StringDictionary();
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("RegionNameSource.txt"))
            using (StreamReader reader = new StreamReader(stream))
            {
                while (!reader.EndOfStream)
                {
                    ReadCountry(nameLookup, reader);
                }
            }

            return nameLookup;
        }

        private static void ReadCountry(StringDictionary nameLookup, StreamReader reader)
        {
            string countryCode = reader.ReadLine().Trim();
            string line = reader.ReadLine();

            while (string.IsNullOrWhiteSpace(line) == false)
            {
                string[] parts = line.Trim().Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
                string id = parts[0];
                string name = parts[1];

                nameLookup.Add(countryCode + id, name);
                line = reader.ReadLine();
            }

        }
    }
}
