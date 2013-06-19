/**
 * LookupService.cs
 *
 * Copyright (C) 2008 MaxMind Inc.  All Rights Reserved.
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 2.1 of the License, or (at your option) any later version.
 *
 * This library is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public
 * License along with this library; if not, write to the Free Software
 * Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA  02111-1307  USA
 */
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Runtime.CompilerServices;

namespace GeoIP
{
    public class LookupService
    {
        private IDbReader _dbReader;
        private DatabaseInfo _databaseInfo;
        private readonly Object _ioLock = new Object();
        private byte _databaseType = Convert.ToByte(DatabaseTypeCodes.COUNTRY_EDITION);
        private int[] _databaseSegments;
        private int _recordLength;

        private static readonly Country UnknownCountry = new Country("--", "N/A");

        public LookupService(String databaseFile, LookupOptions options)
        {
            _dbReader = options == LookupOptions.GEOIP_MEMORY_CACHE
                ? new StreamDbReader(databaseFile)
                : new CachedDbReader(databaseFile);

            Init();
        }

        public LookupService(Stream stream, LookupOptions options)
        {
            _dbReader = options == LookupOptions.GEOIP_MEMORY_CACHE
                ? new StreamDbReader(stream)
                : new CachedDbReader(stream);

            Init();
        }

        public LookupService(String databaseFile)
            : this(databaseFile, LookupOptions.GEOIP_STANDARD)
        {
        }

        private void Init()
        {
            _databaseType = DatabaseTypeCodes.COUNTRY_EDITION;
            _recordLength = DbFlags.STANDARD_RECORD_LENGTH;

            var position = FromEnd(-3);
            int i;
            for (i = 0; i < DbFlags.STRUCTURE_INFO_MAX_SIZE; i++)
            {
                var delim = _dbReader.Read(position, 3);
                position += 3;

                if (delim[0] == 255 && delim[1] == 255 && delim[2] == 255)
                {
                    _databaseType = _dbReader.ReadByte(position);
                    position++;

                    if (_databaseType >= 106)
                    {
                        // Backward compatibility with databases from April 2003 and earlier
                        _databaseType -= 105;
                    }
                    // Determine the database type.
                    switch (_databaseType)
                    {
                        case DatabaseTypeCodes.REGION_EDITION_REV0:
                            _databaseSegments = new int[1];
                            _databaseSegments[0] = DbFlags.STATE_BEGIN_REV0;
                            _recordLength = DbFlags.STANDARD_RECORD_LENGTH;
                            break;
                        case DatabaseTypeCodes.REGION_EDITION_REV1:
                            _databaseSegments = new int[1];
                            _databaseSegments[0] = DbFlags.STATE_BEGIN_REV1;
                            _recordLength = DbFlags.STANDARD_RECORD_LENGTH;
                            break;
                        case DatabaseTypeCodes.CITY_EDITION_REV1_V6:
                        case DatabaseTypeCodes.CITY_EDITION_REV0_V6:
                        case DatabaseTypeCodes.NETSPEED_EDITION_REV1_V6:
                        case DatabaseTypeCodes.NETSPEED_EDITION_REV1:
                        case DatabaseTypeCodes.ASNUM_EDITION_V6:
                        case DatabaseTypeCodes.ASNUM_EDITION:
                        case DatabaseTypeCodes.ISP_EDITION_V6:
                        case DatabaseTypeCodes.ISP_EDITION:
                        case DatabaseTypeCodes.ORG_EDITION_V6:
                        case DatabaseTypeCodes.ORG_EDITION:
                        case DatabaseTypeCodes.CITY_EDITION_REV1:
                        case DatabaseTypeCodes.CITY_EDITION_REV0:
                            {
                                _databaseSegments = new int[1];
                                _databaseSegments[0] = 0;
                                _recordLength = IsStandardRecordLengthType() ? DbFlags.STANDARD_RECORD_LENGTH : DbFlags.ORG_RECORD_LENGTH;
                                var buf = _dbReader.Read(position, DbFlags.SEGMENT_RECORD_LENGTH);

                                int j;
                                for (j = 0; j < DbFlags.SEGMENT_RECORD_LENGTH; j++)
                                {
                                    _databaseSegments[0] += (UnsignedByteToInt(buf[j]) << (j * 8));
                                }
                            }
                            break;
                    }
                    break;
                }

                position += -4;
            }

            if ((_databaseType == DatabaseTypeCodes.COUNTRY_EDITION) ||
                (_databaseType == DatabaseTypeCodes.COUNTRY_EDITION_V6) ||
                (_databaseType == DatabaseTypeCodes.PROXY_EDITION) ||
                (_databaseType == DatabaseTypeCodes.NETSPEED_EDITION))
            {
                _databaseSegments = new int[1];
                _databaseSegments[0] = DbFlags.COUNTRY_BEGIN;
                _recordLength = DbFlags.STANDARD_RECORD_LENGTH;
            }
        }

        private bool IsStandardRecordLengthType()
        {
            return _databaseType == DatabaseTypeCodes.CITY_EDITION_REV0 ||
                   _databaseType == DatabaseTypeCodes.CITY_EDITION_REV1 ||
                   _databaseType == DatabaseTypeCodes.ASNUM_EDITION_V6 ||
                   _databaseType == DatabaseTypeCodes.NETSPEED_EDITION_REV1 ||
                   _databaseType == DatabaseTypeCodes.NETSPEED_EDITION_REV1_V6 ||
                   _databaseType == DatabaseTypeCodes.CITY_EDITION_REV0_V6 ||
                   _databaseType == DatabaseTypeCodes.CITY_EDITION_REV1_V6 ||
                   _databaseType == DatabaseTypeCodes.ASNUM_EDITION;
        }

        private int FromEnd(int relativePosition)
        {
            return _dbReader.Length - relativePosition;
        }

        public void Close()
        {
            lock (_ioLock)
            {
                _dbReader.Close();
            }
            _dbReader = null;
        }

        public Country GetCountry(IPAddress ipAddress)
        {
            return GetCountry(BytestoLong(ipAddress.GetAddressBytes()));
        }

        public Country GetCountryV6(String ipAddress)
        {
            IPAddress addr;
            try
            {
                addr = IPAddress.Parse(ipAddress);
            }

            catch (Exception e)
            {
                Trace.WriteLine(e.Message);
                return UnknownCountry;
            }
            return GetCountryV6(addr);
        }

        public Country GetCountry(String ipAddress)
        {
            IPAddress addr;
            try
            {
                addr = IPAddress.Parse(ipAddress);
            }
            catch (Exception e)
            {
                Trace.WriteLine(e.Message);
                return UnknownCountry;
            }
            return GetCountry(BytestoLong(addr.GetAddressBytes()));
        }

        public Country GetCountryV6(IPAddress ipAddress)
        {
            if (_dbReader == null)
            {
                throw new Exception("Database has been closed.");
            }

            if ((_databaseType == DatabaseTypeCodes.CITY_EDITION_REV1) |
                (_databaseType == DatabaseTypeCodes.CITY_EDITION_REV0))
            {
                Location l = GetLocation(ipAddress);
                if (l == null)
                {
                    return UnknownCountry;
                }

                return new Country(l.CountryCode, l.CountryName);
            }
            int ret = SeekCountryV6(ipAddress) - DbFlags.COUNTRY_BEGIN;
            if (ret == 0)
            {
                return UnknownCountry;
            }

            return new Country(CountryConstants.CountryCodes[ret], CountryConstants.CountryNames[ret]);
        }

        public Country GetCountry(long ipAddress)
        {
            if (_dbReader == null)
            {
                throw new Exception("Database has been closed.");
            }
            if ((_databaseType == DatabaseTypeCodes.CITY_EDITION_REV1) |
                (_databaseType == DatabaseTypeCodes.CITY_EDITION_REV0))
            {
                Location l = GetLocation(ipAddress);
                if (l == null)
                {
                    return UnknownCountry;
                }

                return new Country(l.CountryCode, l.CountryName);

            }

            int ret = SeekCountry(ipAddress) - DbFlags.COUNTRY_BEGIN;
            if (ret == 0)
            {
                return UnknownCountry;
            }

            return new Country(CountryConstants.CountryCodes[ret], CountryConstants.CountryNames[ret]);
        }

        public int GetId(String ipAddress)
        {
            IPAddress addr;
            try
            {
                addr = IPAddress.Parse(ipAddress);
            }
            catch (Exception e)
            {
                Trace.WriteLine(e.Message);
                return 0;
            }
            return GetId(BytestoLong(addr.GetAddressBytes()));
        }

        public int GetId(IPAddress ipAddress)
        {

            return GetId(BytestoLong(ipAddress.GetAddressBytes()));
        }

        public int GetId(long ipAddress)
        {
            if (_dbReader == null)
            {
                throw new Exception("Database has been closed.");
            }
            int ret = SeekCountry(ipAddress) - _databaseSegments[0];
            return ret;
        }

        public DatabaseInfo GetDatabaseInfo()
        {
            if (_databaseInfo != null)
            {
                return _databaseInfo;
            }
            try
            {
                // Synchronize since we're accessing the database file.
                lock (_ioLock)
                {
                    bool hasStructureInfo = false;

                    // Advance to part of file where database info is stored.
                    int position = FromEnd(-3);
                    for (int i = 0; i < DbFlags.STRUCTURE_INFO_MAX_SIZE; i++)
                    {
                        var delim = _dbReader.Read(position, 3);
                        position += 3;
                        if (delim[0] == 255 && delim[1] == 255 && delim[2] == 255)
                        {
                            hasStructureInfo = true;
                            break;
                        }
                        position += -4;
                    }
                    if (hasStructureInfo)
                    {
                        position += -6;
                    }
                    else
                    {
                        // No structure info, must be pre Sep 2002 database, go back to end.
                        position += -3;
                    }
                    // Find the database info string.
                    for (int i = 0; i < DbFlags.DATABASE_INFO_MAX_SIZE; i++)
                    {
                        var delim = _dbReader.Read(position, 3);
                        if (delim[0] == 0 && delim[1] == 0 && delim[2] == 0)
                        {
                            char[] dbInfo2 = new char[i];
                            var dbInfo = _dbReader.Read(position, i);
                            position += i;
                            for (int a0 = 0; a0 < i; a0++)
                            {
                                dbInfo2[a0] = Convert.ToChar(dbInfo[a0]);
                            }
                            // Create the database info object using the string.
                            _databaseInfo = new DatabaseInfo(new String(dbInfo2));
                            return _databaseInfo;
                        }
                        position += -1;
                    }
                }
            }
            catch (Exception e)
            {
                Trace.WriteLine(e.Message);
            }
            return new DatabaseInfo("");
        }

        public Region GetRegion(IPAddress ipAddress)
        {
            return GetRegion(BytestoLong(ipAddress.GetAddressBytes()));
        }

        public Region GetRegion(String str)
        {
            IPAddress addr;
            try
            {
                addr = IPAddress.Parse(str);
            }
            catch (Exception e)
            {
                Trace.WriteLine(e.Message);
                return null;
            }

            return GetRegion(BytestoLong(addr.GetAddressBytes()));
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public Region GetRegion(long ipnum)
        {
            Region record = new Region();
            int seekRegion;
            if (_databaseType == DatabaseTypeCodes.REGION_EDITION_REV0)
            {
                seekRegion = SeekCountry(ipnum) - DbFlags.STATE_BEGIN_REV0;
                char[] ch = new char[2];
                if (seekRegion >= 1000)
                {
                    record.CountryCode = "US";
                    record.CountryName = "United States";
                    ch[0] = (char)(((seekRegion - 1000) / 26) + 65);
                    ch[1] = (char)(((seekRegion - 1000) % 26) + 65);
                    record.Name = new String(ch);
                }
                else
                {
                    record.CountryCode = CountryConstants.CountryCodes[seekRegion];
                    record.CountryName = CountryConstants.CountryNames[seekRegion];
                    record.Name = "";
                }
            }
            else if (_databaseType == DatabaseTypeCodes.REGION_EDITION_REV1)
            {
                seekRegion = SeekCountry(ipnum) - DbFlags.STATE_BEGIN_REV1;
                char[] ch = new char[2];
                if (seekRegion < DbFlags.US_OFFSET)
                {
                    record.CountryCode = "";
                    record.CountryName = "";
                    record.Name = "";
                }
                else if (seekRegion < DbFlags.CANADA_OFFSET)
                {
                    record.CountryCode = "US";
                    record.CountryName = "United States";
                    ch[0] = (char)(((seekRegion - DbFlags.US_OFFSET) / 26) + 65);
                    ch[1] = (char)(((seekRegion - DbFlags.US_OFFSET) % 26) + 65);
                    record.Name = new String(ch);
                }
                else if (seekRegion < DbFlags.WORLD_OFFSET)
                {
                    record.CountryCode = "CA";
                    record.CountryName = "Canada";
                    ch[0] = (char)(((seekRegion - DbFlags.CANADA_OFFSET) / 26) + 65);
                    ch[1] = (char)(((seekRegion - DbFlags.CANADA_OFFSET) % 26) + 65);
                    record.Name = new String(ch);
                }
                else
                {
                    record.CountryCode = CountryConstants.CountryCodes[(seekRegion - DbFlags.WORLD_OFFSET) / DbFlags.FIPS_RANGE];
                    record.CountryName = CountryConstants.CountryNames[(seekRegion - DbFlags.WORLD_OFFSET) / DbFlags.FIPS_RANGE];
                    record.Name = "";
                }
            }
            return record;
        }

        public Location GetLocation(IPAddress addr)
        {
            return GetLocation(BytestoLong(addr.GetAddressBytes()));
        }

        public Location GetLocationV6(String str)
        {
            IPAddress addr;
            try
            {
                addr = IPAddress.Parse(str);
            }
            catch (Exception e)
            {
                Trace.WriteLine(e.Message);
                return null;
            }

            return GetLocationV6(addr);
        }

        public Location GetLocation(String str)
        {
            IPAddress addr;
            try
            {
                addr = IPAddress.Parse(str);
            }
            catch (Exception e)
            {
                Trace.WriteLine(e.Message);
                return null;
            }

            return GetLocation(BytestoLong(addr.GetAddressBytes()));
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public Location GetLocationV6(IPAddress addr)
        {
            byte[] recordBuf = new byte[DbFlags.FULL_RECORD_LENGTH];
            char[] recordBuf2 = new char[DbFlags.FULL_RECORD_LENGTH];
            int recordBufOffset = 0;
            Location record = new Location();
            int strLength = 0;
            double latitude = 0, longitude = 0;

            try
            {
                int seekCountry = SeekCountryV6(addr);
                if (seekCountry == _databaseSegments[0])
                {
                    return null;
                }

                int recordPointer = seekCountry + ((2 * _recordLength - 1) * _databaseSegments[0]);
                recordBuf = _dbReader.Read(recordPointer, DbFlags.FULL_RECORD_LENGTH);

                for (int a0 = 0; a0 < DbFlags.FULL_RECORD_LENGTH; a0++)
                {
                    recordBuf2[a0] = Convert.ToChar(recordBuf[a0]);
                }

                // get country
                record.CountryCode = CountryConstants.CountryCodes[UnsignedByteToInt(recordBuf[0])];
                record.CountryName = CountryConstants.CountryNames[UnsignedByteToInt(recordBuf[0])];
                recordBufOffset++;

                // get region
                while (recordBuf[recordBufOffset + strLength] != '\0')
                    strLength++;
                if (strLength > 0)
                {
                    record.Region = new String(recordBuf2, recordBufOffset, strLength);
                }
                recordBufOffset += strLength + 1;
                strLength = 0;

                // get region_name
                record.RegionName = RegionName.GetRegionName(record.CountryCode, record.Region);

                // get city
                while (recordBuf[recordBufOffset + strLength] != '\0')
                    strLength++;
                if (strLength > 0)
                {
                    record.City = new String(recordBuf2, recordBufOffset, strLength);
                }
                recordBufOffset += (strLength + 1);
                strLength = 0;

                // get postal code
                while (recordBuf[recordBufOffset + strLength] != '\0')
                    strLength++;
                if (strLength > 0)
                {
                    record.PostalCode = new String(recordBuf2, recordBufOffset, strLength);
                }
                recordBufOffset += (strLength + 1);

                // get latitude
                int j;
                for (j = 0; j < 3; j++)
                    latitude += (UnsignedByteToInt(recordBuf[recordBufOffset + j]) << (j * 8));
                record.Latitude = (float)latitude / 10000 - 180;
                recordBufOffset += 3;

                // get longitude
                for (j = 0; j < 3; j++)
                    longitude += (UnsignedByteToInt(recordBuf[recordBufOffset + j]) << (j * 8));
                record.Longitude = (float)longitude / 10000 - 180;

                record.MetroCode = record.DmaCode = 0;
                record.AreaCode = 0;
                if (_databaseType == DatabaseTypeCodes.CITY_EDITION_REV1
                    || _databaseType == DatabaseTypeCodes.CITY_EDITION_REV1_V6)
                {
                    // get metro_code
                    int metroareaCombo = 0;
                    if (record.CountryCode == "US")
                    {
                        recordBufOffset += 3;
                        for (j = 0; j < 3; j++)
                            metroareaCombo += (UnsignedByteToInt(recordBuf[recordBufOffset + j]) << (j * 8));
                        record.MetroCode = record.DmaCode = metroareaCombo / 1000;
                        record.AreaCode = metroareaCombo % 1000;
                    }
                }
            }
            catch (IOException)
            {
                Trace.WriteLine("IO Exception while seting up segments");
            }
            return record;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public Location GetLocation(long ipnum)
        {
            byte[] recordBuf = new byte[DbFlags.FULL_RECORD_LENGTH];
            char[] recordBuf2 = new char[DbFlags.FULL_RECORD_LENGTH];
            int recordBufOffset = 0;
            Location record = new Location();
            int strLength = 0;
            double latitude = 0, longitude = 0;

            try
            {
                int seekCountry = SeekCountry(ipnum);
                if (seekCountry == _databaseSegments[0])
                {
                    return null;
                }
                int recordPointer = seekCountry + ((2 * _recordLength - 1) * _databaseSegments[0]);
                recordBuf = _dbReader.Read(recordPointer, DbFlags.FULL_RECORD_LENGTH);

                for (int a0 = 0; a0 < DbFlags.FULL_RECORD_LENGTH; a0++)
                {
                    recordBuf2[a0] = Convert.ToChar(recordBuf[a0]);
                }

                // get country
                record.CountryCode = CountryConstants.CountryCodes[UnsignedByteToInt(recordBuf[0])];
                record.CountryName = CountryConstants.CountryNames[UnsignedByteToInt(recordBuf[0])];
                recordBufOffset++;

                // get region
                while (recordBuf[recordBufOffset + strLength] != '\0')
                    strLength++;
                if (strLength > 0)
                {
                    record.Region = new String(recordBuf2, recordBufOffset, strLength);
                }
                recordBufOffset += strLength + 1;
                strLength = 0;

                // get region_name
                record.RegionName = RegionName.GetRegionName(record.CountryCode, record.Region);

                // get city
                while (recordBuf[recordBufOffset + strLength] != '\0')
                    strLength++;
                if (strLength > 0)
                {
                    record.City = new String(recordBuf2, recordBufOffset, strLength);
                }
                recordBufOffset += (strLength + 1);
                strLength = 0;

                // get postal code
                while (recordBuf[recordBufOffset + strLength] != '\0')
                    strLength++;
                if (strLength > 0)
                {
                    record.PostalCode = new String(recordBuf2, recordBufOffset, strLength);
                }
                recordBufOffset += (strLength + 1);

                // get latitude
                int j;
                for (j = 0; j < 3; j++)
                    latitude += (UnsignedByteToInt(recordBuf[recordBufOffset + j]) << (j * 8));
                record.Latitude = (float)latitude / 10000 - 180;
                recordBufOffset += 3;

                // get longitude
                for (j = 0; j < 3; j++)
                    longitude += (UnsignedByteToInt(recordBuf[recordBufOffset + j]) << (j * 8));
                record.Longitude = (float)longitude / 10000 - 180;

                record.MetroCode = record.DmaCode = 0;
                record.AreaCode = 0;
                if (_databaseType == DatabaseTypeCodes.CITY_EDITION_REV1)
                {
                    // get metro_code
                    int metroareaCombo = 0;
                    if (record.CountryCode == "US")
                    {
                        recordBufOffset += 3;
                        for (j = 0; j < 3; j++)
                            metroareaCombo += (UnsignedByteToInt(recordBuf[recordBufOffset + j]) << (j * 8));
                        record.MetroCode = record.DmaCode = metroareaCombo / 1000;
                        record.AreaCode = metroareaCombo % 1000;
                    }
                }
            }
            catch (IOException)
            {
                Trace.WriteLine("IO Exception while seting up segments");
            }
            return record;
        }

        public String GetOrg(IPAddress addr)
        {
            return GetOrg(BytestoLong(addr.GetAddressBytes()));
        }

        public String GetOrgV6(String str)
        {
            IPAddress addr;
            try
            {
                addr = IPAddress.Parse(str);
            }
            catch (Exception e)
            {
                Trace.WriteLine(e.Message);
                return null;
            }
            return GetOrgV6(addr);
        }

        public String GetOrg(String str)
        {
            IPAddress addr;
            try
            {
                addr = IPAddress.Parse(str);
            }
            catch (Exception e)
            {
                Trace.WriteLine(e.Message);
                return null;
            }
            return GetOrg(BytestoLong(addr.GetAddressBytes()));
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public string GetOrgV6(IPAddress addr)
        {
            int strLength = 0;
            byte[] buf = new byte[DbFlags.MAX_ORG_RECORD_LENGTH];
            char[] buf2 = new char[DbFlags.MAX_ORG_RECORD_LENGTH];

            try
            {
                int seekOrg = SeekCountryV6(addr);
                if (seekOrg == _databaseSegments[0])
                {
                    return null;
                }

                int recordPointer = seekOrg + (2 * _recordLength - 1) * _databaseSegments[0];
                buf = _dbReader.Read(recordPointer, DbFlags.MAX_ORG_RECORD_LENGTH);

                while (buf[strLength] != 0)
                {
                    buf2[strLength] = Convert.ToChar(buf[strLength]);
                    strLength++;
                }

                buf2[strLength] = '\0';
                String orgBuf = new String(buf2, 0, strLength);
                return orgBuf;
            }
            catch (IOException)
            {
                Trace.WriteLine("IO Exception");
                return null;
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public String GetOrg(long ipnum)
        {
            int strLength = 0;
            byte[] buf = new byte[DbFlags.MAX_ORG_RECORD_LENGTH];
            char[] buf2 = new char[DbFlags.MAX_ORG_RECORD_LENGTH];

            try
            {
                int seekOrg = SeekCountry(ipnum);
                if (seekOrg == _databaseSegments[0])
                {
                    return null;
                }

                int recordPointer = seekOrg + (2 * _recordLength - 1) * _databaseSegments[0];
                buf = _dbReader.Read(recordPointer, DbFlags.MAX_ORG_RECORD_LENGTH);

                while (buf[strLength] != 0)
                {
                    buf2[strLength] = Convert.ToChar(buf[strLength]);
                    strLength++;
                }
                buf2[strLength] = '\0';
                String orgBuf = new String(buf2, 0, strLength);
                return orgBuf;
            }
            catch (IOException)
            {
                Trace.WriteLine("IO Exception");
                return null;
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        private int SeekCountryV6(IPAddress ipAddress)
        {
            byte[] v6Vec = ipAddress.GetAddressBytes();
            byte[] buf = new byte[2 * DbFlags.MAX_RECORD_LENGTH];
            int[] x = new int[2];
            int offset = 0;
            for (int depth = 127; depth >= 0; depth--)
            {
                try
                {
                    buf = _dbReader.Read(2 * _recordLength * offset, DbFlags.MAX_RECORD_LENGTH);
                }
                catch (IOException)
                {
                    Trace.WriteLine("IO Exception");
                }
                for (int i = 0; i < 2; i++)
                {
                    x[i] = 0;
                    for (int j = 0; j < _recordLength; j++)
                    {
                        int y = buf[(i * _recordLength) + j];
                        if (y < 0)
                        {
                            y += 256;
                        }
                        x[i] += (y << (j * 8));
                    }
                }


                int bnum = 127 - depth;
                int idx = bnum >> 3;
                int bMask = 1 << (bnum & 7 ^ 7);
                if ((v6Vec[idx] & bMask) > 0)
                {
                    if (x[1] >= _databaseSegments[0])
                    {
                        return x[1];
                    }
                    offset = x[1];
                }
                else
                {
                    if (x[0] >= _databaseSegments[0])
                    {
                        return x[0];
                    }
                    offset = x[0];
                }
            }

            // shouldn't reach here
            Trace.WriteLine("Error Seeking country while Seeking " + ipAddress);
            return 0;

        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        private int SeekCountry(long ipAddress)
        {
            byte[] buf = new byte[2 * DbFlags.MAX_RECORD_LENGTH];
            int[] x = new int[2];
            int offset = 0;
            for (int depth = 31; depth >= 0; depth--)
            {
                try
                {
                    buf = _dbReader.Read(2 * _recordLength * offset, 2 * DbFlags.MAX_RECORD_LENGTH);
                }
                catch (IOException)
                {
                    Trace.WriteLine("IO Exception");
                }
                for (int i = 0; i < 2; i++)
                {
                    x[i] = 0;
                    for (int j = 0; j < _recordLength; j++)
                    {
                        int y = buf[(i * _recordLength) + j];
                        if (y < 0)
                        {
                            y += 256;
                        }
                        x[i] += (y << (j * 8));
                    }
                }

                if ((ipAddress & (1 << depth)) > 0)
                {
                    if (x[1] >= _databaseSegments[0])
                    {
                        return x[1];
                    }
                    offset = x[1];
                }
                else
                {
                    if (x[0] >= _databaseSegments[0])
                    {
                        return x[0];
                    }
                    offset = x[0];
                }
            }

            // shouldn't reach here
            Trace.WriteLine("Error Seeking country while Seeking " + ipAddress);
            return 0;

        }

        private static long BytestoLong(byte[] address)
        {
            long ipnum = 0;
            for (int i = 0; i < 4; ++i)
            {
                long y = address[i];
                if (y < 0)
                {
                    y += 256;
                }
                ipnum += y << ((3 - i) * 8);
            }
            return ipnum;
        }

        private static int UnsignedByteToInt(byte b)
        {
            return b & 0xFF;
        }

    }
}
