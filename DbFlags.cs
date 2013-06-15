namespace GeoIP
{
    public class DbFlags
    {
        public const int COUNTRY_BEGIN = 16776960;
        public const int STATE_BEGIN = 16700000;
        public const int STRUCTURE_INFO_MAX_SIZE = 20;
        public const int DATABASE_INFO_MAX_SIZE = 100;
        public const int FULL_RECORD_LENGTH = 100; //???
        public const int SEGMENT_RECORD_LENGTH = 3;
        public const int STANDARD_RECORD_LENGTH = 3;
        public const int ORG_RECORD_LENGTH = 4;
        public const int MAX_RECORD_LENGTH = 4;
        public const int MAX_ORG_RECORD_LENGTH = 1000; //???
        public const int FIPS_RANGE = 360;
        public const int STATE_BEGIN_REV0 = 16700000;
        public const int STATE_BEGIN_REV1 = 16000000;
        public const int US_OFFSET = 1;
        public const int CANADA_OFFSET = 677;
        public const int WORLD_OFFSET = 1353;
    }
}