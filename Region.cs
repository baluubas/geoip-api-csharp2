using System;

namespace GeoIP
{
    public class Region
    {
        public String CountryCode { get; set; }
        public String CountryName { get; set; }
        public String Name { get; set; }

        public Region()
        {
        }

        public Region(String countryCode, String countryName, String region)
        {
            CountryCode = countryCode;
            CountryName = countryName;
            Name = region;
        }
    }
}
