using NUnit.Framework;

namespace GeoIP.Test
{
    [TestFixture]
    public class RegionNameTests
    {
        [Test]
        [TestCase("SE", "27", "Skane Lan")]
        [TestCase("ZW", "02", "Midlands")]
        [TestCase("MA", "59", "La,youne-Boujdour-Sakia El Hamra")]
        public void Should_get_correct_region_name(string country, string region, string expected)
        {
            string actual = RegionName.GetRegionName(country, region);
            
            Assert.That(actual, Is.EqualTo(expected));
        }
        
        [Test]
        public void Should_return_null_when_not_providing_a_region_code()
        {
            string actual = RegionName.GetRegionName("SE", null);
            
            Assert.That(actual, Is.Null);
        }

        [Test]
        public void Should_return_null_when_not_providing_a_country()
        {
            string actual = RegionName.GetRegionName(null, "20");

            Assert.That(actual, Is.Null);
        }
        
        [Test]
        public void Should_return_null_when_not_region_id_is_zero()
        {
            string actual = RegionName.GetRegionName("SE", "00");

            Assert.That(actual, Is.Null);
        }
    }
}
