using System.Reflection;
using NUnit.Framework;

namespace GeoIP.Test
{
    [TestFixture]
    public class LookupServiceTests
    {
        private LookupService _lookupService;

        [SetUp]
        public void Setup()
        {
            var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("GeoIP.Test.GeoIP.dat");
            _lookupService = new LookupService(stream, LookupOptions.GEOIP_STANDARD);   
        }

        [Test]
        [TestCase("27.34.142.47", "JP", "Japan")]
        [TestCase("82.160.137.162", "PL", "Poland")]
        [TestCase("110.139.178.158", "ID", "Indonesia")]
        public void Should_lookup_country(string ip, string expectedCountryCode, string expectedCountry)
        {
            Country country = _lookupService.GetCountry(ip);

            Assert.That(country.Code, Is.EqualTo(expectedCountryCode));
            Assert.That(country.Name, Is.EqualTo(expectedCountry));
        }

        [Test]
        public void Should_return_empty_when_lookup_fails()
        {
            Country country = _lookupService.GetCountry("127.0.0.1");

            Assert.That(country.Code, Is.EqualTo("--"));
            Assert.That(country.Name, Is.EqualTo("N/A"));
        }
    }
}
