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
using System.IO;
using System.Net;
using System.Runtime.CompilerServices;

namespace GeoIP
{
    public class LookupService
    {
        private FileStream _file;
        private DatabaseInfo _databaseInfo;
        private readonly Object _ioLock = new Object();
        private byte _databaseType = Convert.ToByte(DatabaseTypeCodes.COUNTRY_EDITION);
        private int[] _databaseSegments;
        private int _recordLength;
        private readonly int _dboptions;
        private byte[] _dbbuffer;

        private static readonly Country UNKNOWN_COUNTRY = new Country("--", "N/A");

        public LookupService(String databaseFile, int options)
        {
            try
            {
                lock (_ioLock)
                {
                    _file = new FileStream(databaseFile, FileMode.Open, FileAccess.Read);
                }
                _dboptions = options;
                Init();
            }
            catch (System.SystemException)
            {
                Console.Write("cannot open file " + databaseFile + "\n");
            }
        }

        public LookupService(String databaseFile)
            : this(databaseFile, LookupOptions.GEOIP_STANDARD)
        {
        }

        private void Init()
        {
            int i, j;
            byte[] delim = new byte[3];
            byte[] buf = new byte[DbFlags.SEGMENT_RECORD_LENGTH];
            _databaseType = (byte) DatabaseTypeCodes.COUNTRY_EDITION;
            _recordLength = DbFlags.STANDARD_RECORD_LENGTH;
            //file.Seek(file.Length() - 3,SeekOrigin.Begin);
            lock (_ioLock)
            {
                _file.Seek(-3, SeekOrigin.End);
                for (i = 0; i < DbFlags.STRUCTURE_INFO_MAX_SIZE; i++)
                {
                    _file.Read(delim, 0, 3);
                    if (delim[0] == 255 && delim[1] == 255 && delim[2] == 255)
                    {
                        _databaseType = Convert.ToByte(_file.ReadByte());
                        if (_databaseType >= 106)
                        {
                            // Backward compatibility with databases from April 2003 and earlier
                            _databaseType -= 105;
                        }
                        // Determine the database type.
                        if (_databaseType == DatabaseTypeCodes.REGION_EDITION_REV0)
                        {
                            _databaseSegments = new int[1];
                            _databaseSegments[0] = DbFlags.STATE_BEGIN_REV0;
                            _recordLength = DbFlags.STANDARD_RECORD_LENGTH;
                        }
                        else if (_databaseType == DatabaseTypeCodes.REGION_EDITION_REV1)
                        {
                            _databaseSegments = new int[1];
                            _databaseSegments[0] = DbFlags.STATE_BEGIN_REV1;
                            _recordLength = DbFlags.STANDARD_RECORD_LENGTH;
                        }
                        else if (_databaseType == DatabaseTypeCodes.CITY_EDITION_REV0 ||
                                 _databaseType == DatabaseTypeCodes.CITY_EDITION_REV1 ||
                                 _databaseType == DatabaseTypeCodes.ORG_EDITION ||
                                 _databaseType == DatabaseTypeCodes.ORG_EDITION_V6 ||
                                 _databaseType == DatabaseTypeCodes.ISP_EDITION ||
                                 _databaseType == DatabaseTypeCodes.ISP_EDITION_V6 ||
                                 _databaseType == DatabaseTypeCodes.ASNUM_EDITION ||
                                 _databaseType == DatabaseTypeCodes.ASNUM_EDITION_V6 ||
                                 _databaseType == DatabaseTypeCodes.NETSPEED_EDITION_REV1 ||
                                 _databaseType == DatabaseTypeCodes.NETSPEED_EDITION_REV1_V6 ||
                                 _databaseType == DatabaseTypeCodes.CITY_EDITION_REV0_V6 ||
                                 _databaseType == DatabaseTypeCodes.CITY_EDITION_REV1_V6
                            )
                        {
                            _databaseSegments = new int[1];
                            _databaseSegments[0] = 0;
                            if (_databaseType == DatabaseTypeCodes.CITY_EDITION_REV0 ||
                                _databaseType == DatabaseTypeCodes.CITY_EDITION_REV1 ||
                                _databaseType == DatabaseTypeCodes.ASNUM_EDITION_V6 ||
                                _databaseType == DatabaseTypeCodes.NETSPEED_EDITION_REV1 ||
                                _databaseType == DatabaseTypeCodes.NETSPEED_EDITION_REV1_V6 ||
                                _databaseType == DatabaseTypeCodes.CITY_EDITION_REV0_V6 ||
                                _databaseType == DatabaseTypeCodes.CITY_EDITION_REV1_V6 ||
                                _databaseType == DatabaseTypeCodes.ASNUM_EDITION
                                )
                            {
                                _recordLength = DbFlags.STANDARD_RECORD_LENGTH;
                            }
                            else
                            {
                                _recordLength = DbFlags.ORG_RECORD_LENGTH;
                            }
                            _file.Read(buf, 0, DbFlags.SEGMENT_RECORD_LENGTH);
                            for (j = 0; j < DbFlags.SEGMENT_RECORD_LENGTH; j++)
                            {
                                _databaseSegments[0] += (UnsignedByteToInt(buf[j]) << (j*8));
                            }
                        }
                        break;
                    }
                    else
                    {
                        //file.Seek(file.getFilePointer() - 4);
                        _file.Seek(-4, SeekOrigin.Current);
                        //file.Seek(file.position-4,SeekOrigin.Begin);
                    }
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
                if ((_dboptions & LookupOptions.GEOIP_MEMORY_CACHE) == 1)
                {
                    int l = (int) _file.Length;
                    _dbbuffer = new byte[l];
                    _file.Seek(0, SeekOrigin.Begin);
                    _file.Read(_dbbuffer, 0, l);
                }
            }
        }

        public void Close()
        {
            try
            {
                lock (_ioLock)
                {
                    _file.Close();
                }
                _file = null;
            }
            catch (Exception)
            {
            }
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
                //catch (UnknownHostException e) {
            catch (Exception e)
            {
                Console.Write(e.Message);
                return UNKNOWN_COUNTRY;
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
                //catch (UnknownHostException e) {
            catch (Exception e)
            {
                Console.Write(e.Message);
                return UNKNOWN_COUNTRY;
            }
            //  return getCountry(bytestoLong(addr.GetAddressBytes()));
            return GetCountry(BytestoLong(addr.GetAddressBytes()));
        }

        public Country GetCountryV6(IPAddress ipAddress)
        {
            if (_file == null)
            {
                //throw new IllegalStateException("Database has been closed.");
                throw new Exception("Database has been closed.");
            }
            if ((_databaseType == DatabaseTypeCodes.CITY_EDITION_REV1) |
                (_databaseType == DatabaseTypeCodes.CITY_EDITION_REV0))
            {
                Location l = GetLocation(ipAddress);
                if (l == null)
                {
                    return UNKNOWN_COUNTRY;
                }
                else
                {
                    return new Country(l.CountryCode, l.CountryName);
                }
            }
            else
            {
                int ret = SeekCountryV6(ipAddress) - DbFlags.COUNTRY_BEGIN;
                if (ret == 0)
                {
                    return UNKNOWN_COUNTRY;
                }
                else
                {
                    return new Country(CountryConstants.CountryCodes[ret], CountryConstants.CountryNames[ret]);
                }
            }
        }

        public Country GetCountry(long ipAddress)
        {
            if (_file == null)
            {
                //throw new IllegalStateException("Database has been closed.");
                throw new Exception("Database has been closed.");
            }
            if ((_databaseType == DatabaseTypeCodes.CITY_EDITION_REV1) |
                (_databaseType == DatabaseTypeCodes.CITY_EDITION_REV0))
            {
                Location l = GetLocation(ipAddress);
                if (l == null)
                {
                    return UNKNOWN_COUNTRY;
                }
                else
                {
                    return new Country(l.CountryCode, l.CountryName);
                }
            }
            else
            {
                int ret = SeekCountry(ipAddress) - DbFlags.COUNTRY_BEGIN;
                if (ret == 0)
                {
                    return UNKNOWN_COUNTRY;
                }
                else
                {
                    return new Country(CountryConstants.CountryCodes[ret], CountryConstants.CountryNames[ret]);
                }
            }
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
                Console.Write(e.Message);
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
            if (_file == null)
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
                    byte[] delim = new byte[3];
                    // Advance to part of file where database info is stored.
                    _file.Seek(-3, SeekOrigin.End);
                    for (int i = 0; i < DbFlags.STRUCTURE_INFO_MAX_SIZE; i++)
                    {
                        _file.Read(delim, 0, 3);
                        if (delim[0] == 255 && delim[1] == 255 && delim[2] == 255)
                        {
                            hasStructureInfo = true;
                            break;
                        }
                        _file.Seek(-4, SeekOrigin.Current);
                    }
                    if (hasStructureInfo)
                    {
                        _file.Seek(-6, SeekOrigin.Current);
                    }
                    else
                    {
                        // No structure info, must be pre Sep 2002 database, go back to end.
                        _file.Seek(-3, SeekOrigin.End);
                    }
                    // Find the database info string.
                    for (int i = 0; i < DbFlags.DATABASE_INFO_MAX_SIZE; i++)
                    {
                        _file.Read(delim, 0, 3);
                        if (delim[0] == 0 && delim[1] == 0 && delim[2] == 0)
                        {
                            byte[] dbInfo = new byte[i];
                            char[] dbInfo2 = new char[i];
                            _file.Read(dbInfo, 0, i);
                            for (int a0 = 0; a0 < i; a0++)
                            {
                                dbInfo2[a0] = Convert.ToChar(dbInfo[a0]);
                            }
                            // Create the database info object using the string.
                            this._databaseInfo = new DatabaseInfo(new String(dbInfo2));
                            return _databaseInfo;
                        }
                        _file.Seek(-4, SeekOrigin.Current);
                    }
                }
            }
            catch (Exception e)
            {
                Console.Write(e.Message);
                //e.printStackTrace();
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
                Console.Write(e.Message);
                return null;
            }

            return GetRegion(BytestoLong(addr.GetAddressBytes()));
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public Region GetRegion(long ipnum)
        {
            Region record = new Region();
            int seek_region = 0;
            if (_databaseType == DatabaseTypeCodes.REGION_EDITION_REV0)
            {
                seek_region = SeekCountry(ipnum) - DbFlags.STATE_BEGIN_REV0;
                char[] ch = new char[2];
                if (seek_region >= 1000)
                {
                    record.CountryCode = "US";
                    record.CountryName = "United States";
                    ch[0] = (char) (((seek_region - 1000)/26) + 65);
                    ch[1] = (char) (((seek_region - 1000)%26) + 65);
                    record.Name = new String(ch);
                }
                else
                {
                    record.CountryCode = CountryConstants.CountryCodes[seek_region];
                    record.CountryName = CountryConstants.CountryNames[seek_region];
                    record.Name = "";
                }
            }
            else if (_databaseType == DatabaseTypeCodes.REGION_EDITION_REV1)
            {
                seek_region = SeekCountry(ipnum) - DbFlags.STATE_BEGIN_REV1;
                char[] ch = new char[2];
                if (seek_region < DbFlags.US_OFFSET)
                {
                    record.CountryCode = "";
                    record.CountryName = "";
                    record.Name = "";
                }
                else if (seek_region < DbFlags.CANADA_OFFSET)
                {
                    record.CountryCode = "US";
                    record.CountryName = "United States";
                    ch[0] = (char) (((seek_region - DbFlags.US_OFFSET)/26) + 65);
                    ch[1] = (char) (((seek_region - DbFlags.US_OFFSET)%26) + 65);
                    record.Name = new String(ch);
                }
                else if (seek_region < DbFlags.WORLD_OFFSET)
                {
                    record.CountryCode = "CA";
                    record.CountryName = "Canada";
                    ch[0] = (char) (((seek_region - DbFlags.CANADA_OFFSET)/26) + 65);
                    ch[1] = (char) (((seek_region - DbFlags.CANADA_OFFSET)%26) + 65);
                    record.Name = new String(ch);
                }
                else
                {
                    record.CountryCode = CountryConstants.CountryCodes[(seek_region - DbFlags.WORLD_OFFSET)/DbFlags.FIPS_RANGE];
                    record.CountryName = CountryConstants.CountryNames[(seek_region - DbFlags.WORLD_OFFSET)/DbFlags.FIPS_RANGE];
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
                Console.Write(e.Message);
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
                Console.Write(e.Message);
                return null;
            }

            return GetLocation(BytestoLong(addr.GetAddressBytes()));
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public Location GetLocationV6(IPAddress addr)
        {
            int record_pointer;
            byte[] record_buf = new byte[DbFlags.FULL_RECORD_LENGTH];
            char[] record_buf2 = new char[DbFlags.FULL_RECORD_LENGTH];
            int record_buf_offset = 0;
            Location record = new Location();
            int str_length = 0;
            int j, Seek_country;
            double latitude = 0, longitude = 0;

            try
            {
                Seek_country = SeekCountryV6(addr);
                if (Seek_country == _databaseSegments[0])
                {
                    return null;
                }
                record_pointer = Seek_country + ((2*_recordLength - 1)*_databaseSegments[0]);
                if ((_dboptions & LookupOptions.GEOIP_MEMORY_CACHE) == 1)
                {
                    Array.Copy(_dbbuffer, record_pointer, record_buf, 0,
                               Math.Min(_dbbuffer.Length - record_pointer, DbFlags.FULL_RECORD_LENGTH));
                }
                else
                {
                    lock (_ioLock)
                    {
                        _file.Seek(record_pointer, SeekOrigin.Begin);
                        _file.Read(record_buf, 0, DbFlags.FULL_RECORD_LENGTH);
                    }
                }
                for (int a0 = 0; a0 < DbFlags.FULL_RECORD_LENGTH; a0++)
                {
                    record_buf2[a0] = Convert.ToChar(record_buf[a0]);
                }
                // get country
                record.CountryCode = CountryConstants.CountryCodes[UnsignedByteToInt(record_buf[0])];
                record.CountryName = CountryConstants.CountryNames[UnsignedByteToInt(record_buf[0])];
                record_buf_offset++;

                // get region
                while (record_buf[record_buf_offset + str_length] != '\0')
                    str_length++;
                if (str_length > 0)
                {
                    record.Region = new String(record_buf2, record_buf_offset, str_length);
                }
                record_buf_offset += str_length + 1;
                str_length = 0;

                // get region_name
                record.RegionName = RegionName.GetRegionName(record.CountryCode, record.Region);

                // get city
                while (record_buf[record_buf_offset + str_length] != '\0')
                    str_length++;
                if (str_length > 0)
                {
                    record.City = new String(record_buf2, record_buf_offset, str_length);
                }
                record_buf_offset += (str_length + 1);
                str_length = 0;

                // get postal code
                while (record_buf[record_buf_offset + str_length] != '\0')
                    str_length++;
                if (str_length > 0)
                {
                    record.PostalCode = new String(record_buf2, record_buf_offset, str_length);
                }
                record_buf_offset += (str_length + 1);

                // get latitude
                for (j = 0; j < 3; j++)
                    latitude += (UnsignedByteToInt(record_buf[record_buf_offset + j]) << (j*8));
                record.Latitude = (float) latitude/10000 - 180;
                record_buf_offset += 3;

                // get longitude
                for (j = 0; j < 3; j++)
                    longitude += (UnsignedByteToInt(record_buf[record_buf_offset + j]) << (j*8));
                record.Longitude = (float) longitude/10000 - 180;

                record.MetroCode = record.DmaCode = 0;
                record.AreaCode = 0;
                if (_databaseType == DatabaseTypeCodes.CITY_EDITION_REV1
                    || _databaseType == DatabaseTypeCodes.CITY_EDITION_REV1_V6)
                {
                    // get metro_code
                    int metroarea_combo = 0;
                    if (record.CountryCode == "US")
                    {
                        record_buf_offset += 3;
                        for (j = 0; j < 3; j++)
                            metroarea_combo += (UnsignedByteToInt(record_buf[record_buf_offset + j]) << (j*8));
                        record.MetroCode = record.DmaCode = metroarea_combo/1000;
                        record.AreaCode = metroarea_combo%1000;
                    }
                }
            }
            catch (IOException)
            {
                Console.Write("IO Exception while seting up segments");
            }
            return record;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public Location GetLocation(long ipnum)
        {
            int record_pointer;
            byte[] record_buf = new byte[DbFlags.FULL_RECORD_LENGTH];
            char[] record_buf2 = new char[DbFlags.FULL_RECORD_LENGTH];
            int record_buf_offset = 0;
            Location record = new Location();
            int str_length = 0;
            int j, Seek_country;
            double latitude = 0, longitude = 0;

            try
            {
                Seek_country = SeekCountry(ipnum);
                if (Seek_country == _databaseSegments[0])
                {
                    return null;
                }
                record_pointer = Seek_country + ((2*_recordLength - 1)*_databaseSegments[0]);
                if ((_dboptions & LookupOptions.GEOIP_MEMORY_CACHE) == 1)
                {
                    Array.Copy(_dbbuffer, record_pointer, record_buf, 0,
                               Math.Min(_dbbuffer.Length - record_pointer, DbFlags.FULL_RECORD_LENGTH));
                }
                else
                {
                    lock (_ioLock)
                    {
                        _file.Seek(record_pointer, SeekOrigin.Begin);
                        _file.Read(record_buf, 0, DbFlags.FULL_RECORD_LENGTH);
                    }
                }
                for (int a0 = 0; a0 < DbFlags.FULL_RECORD_LENGTH; a0++)
                {
                    record_buf2[a0] = Convert.ToChar(record_buf[a0]);
                }
                // get country
                record.CountryCode = CountryConstants.CountryCodes[UnsignedByteToInt(record_buf[0])];
                record.CountryName = CountryConstants.CountryNames[UnsignedByteToInt(record_buf[0])];
                record_buf_offset++;

                // get region
                while (record_buf[record_buf_offset + str_length] != '\0')
                    str_length++;
                if (str_length > 0)
                {
                    record.Region = new String(record_buf2, record_buf_offset, str_length);
                }
                record_buf_offset += str_length + 1;
                str_length = 0;

                // get region_name
                record.RegionName = RegionName.GetRegionName(record.CountryCode, record.Region);

                // get city
                while (record_buf[record_buf_offset + str_length] != '\0')
                    str_length++;
                if (str_length > 0)
                {
                    record.City = new String(record_buf2, record_buf_offset, str_length);
                }
                record_buf_offset += (str_length + 1);
                str_length = 0;

                // get postal code
                while (record_buf[record_buf_offset + str_length] != '\0')
                    str_length++;
                if (str_length > 0)
                {
                    record.PostalCode = new String(record_buf2, record_buf_offset, str_length);
                }
                record_buf_offset += (str_length + 1);

                // get latitude
                for (j = 0; j < 3; j++)
                    latitude += (UnsignedByteToInt(record_buf[record_buf_offset + j]) << (j*8));
                record.Latitude = (float) latitude/10000 - 180;
                record_buf_offset += 3;

                // get longitude
                for (j = 0; j < 3; j++)
                    longitude += (UnsignedByteToInt(record_buf[record_buf_offset + j]) << (j*8));
                record.Longitude = (float) longitude/10000 - 180;

                record.MetroCode = record.DmaCode = 0;
                record.AreaCode = 0;
                if (_databaseType == DatabaseTypeCodes.CITY_EDITION_REV1)
                {
                    // get metro_code
                    int metroarea_combo = 0;
                    if (record.CountryCode == "US")
                    {
                        record_buf_offset += 3;
                        for (j = 0; j < 3; j++)
                            metroarea_combo += (UnsignedByteToInt(record_buf[record_buf_offset + j]) << (j*8));
                        record.MetroCode = record.DmaCode = metroarea_combo/1000;
                        record.AreaCode = metroarea_combo%1000;
                    }
                }
            }
            catch (IOException)
            {
                Console.Write("IO Exception while seting up segments");
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
                //catch (UnknownHostException e) {
            catch (Exception e)
            {
                Console.Write(e.Message);
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
                //catch (UnknownHostException e) {
            catch (Exception e)
            {
                Console.Write(e.Message);
                return null;
            }
            return GetOrg(BytestoLong(addr.GetAddressBytes()));
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public String GetOrgV6(IPAddress addr)
        {
            int Seek_org;
            int record_pointer;
            int str_length = 0;
            byte[] buf = new byte[DbFlags.MAX_ORG_RECORD_LENGTH];
            char[] buf2 = new char[DbFlags.MAX_ORG_RECORD_LENGTH];
            String org_buf;

            try
            {
                Seek_org = SeekCountryV6(addr);
                if (Seek_org == _databaseSegments[0])
                {
                    return null;
                }

                record_pointer = Seek_org + (2*_recordLength - 1)*_databaseSegments[0];
                if ((_dboptions & LookupOptions.GEOIP_MEMORY_CACHE) == 1)
                {
                    Array.Copy(_dbbuffer, record_pointer, buf, 0,
                               Math.Min(_dbbuffer.Length - record_pointer, DbFlags.MAX_ORG_RECORD_LENGTH));
                }
                else
                {
                    lock (_ioLock)
                    {
                        _file.Seek(record_pointer, SeekOrigin.Begin);
                        _file.Read(buf, 0, DbFlags.MAX_ORG_RECORD_LENGTH);
                    }
                }
                while (buf[str_length] != 0)
                {
                    buf2[str_length] = Convert.ToChar(buf[str_length]);
                    str_length++;
                }
                buf2[str_length] = '\0';
                org_buf = new String(buf2, 0, str_length);
                return org_buf;
            }
            catch (IOException)
            {
                Console.Write("IO Exception");
                return null;
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public String GetOrg(long ipnum)
        {
            int Seek_org;
            int record_pointer;
            int str_length = 0;
            byte[] buf = new byte[DbFlags.MAX_ORG_RECORD_LENGTH];
            char[] buf2 = new char[DbFlags.MAX_ORG_RECORD_LENGTH];
            String org_buf;

            try
            {
                Seek_org = SeekCountry(ipnum);
                if (Seek_org == _databaseSegments[0])
                {
                    return null;
                }

                record_pointer = Seek_org + (2*_recordLength - 1)*_databaseSegments[0];
                if ((_dboptions & LookupOptions.GEOIP_MEMORY_CACHE) == 1)
                {
                    Array.Copy(_dbbuffer, record_pointer, buf, 0,
                               Math.Min(_dbbuffer.Length - record_pointer, DbFlags.MAX_ORG_RECORD_LENGTH));
                }
                else
                {
                    lock (_ioLock)
                    {
                        _file.Seek(record_pointer, SeekOrigin.Begin);
                        _file.Read(buf, 0, DbFlags.MAX_ORG_RECORD_LENGTH);
                    }
                }
                while (buf[str_length] != 0)
                {
                    buf2[str_length] = Convert.ToChar(buf[str_length]);
                    str_length++;
                }
                buf2[str_length] = '\0';
                org_buf = new String(buf2, 0, str_length);
                return org_buf;
            }
            catch (IOException)
            {
                Console.Write("IO Exception");
                return null;
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        private int SeekCountryV6(IPAddress ipAddress)
        {
            byte[] v6vec = ipAddress.GetAddressBytes();
            byte[] buf = new byte[2*DbFlags.MAX_RECORD_LENGTH];
            int[] x = new int[2];
            int offset = 0;
            for (int depth = 127; depth >= 0; depth--)
            {
                try
                {
                    if ((_dboptions & LookupOptions.GEOIP_MEMORY_CACHE) == 1)
                    {
                        for (int i = 0; i < (2*DbFlags.MAX_RECORD_LENGTH); i++)
                        {
                            buf[i] = _dbbuffer[i + (2*_recordLength*offset)];
                        }
                    }
                    else
                    {
                        lock (_ioLock)
                        {
                            _file.Seek(2*_recordLength*offset, SeekOrigin.Begin);
                            _file.Read(buf, 0, 2*DbFlags.MAX_RECORD_LENGTH);
                        }
                    }
                }
                catch (IOException)
                {
                    Console.Write("IO Exception");
                }
                for (int i = 0; i < 2; i++)
                {
                    x[i] = 0;
                    for (int j = 0; j < _recordLength; j++)
                    {
                        int y = buf[(i*_recordLength) + j];
                        if (y < 0)
                        {
                            y += 256;
                        }
                        x[i] += (y << (j*8));
                    }
                }


                int bnum = 127 - depth;
                int idx = bnum >> 3;
                int b_mask = 1 << (bnum & 7 ^ 7);
                if ((v6vec[idx] & b_mask) > 0)
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
            Console.Write("Error Seeking country while Seeking " + ipAddress);
            return 0;

        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        private int SeekCountry(long ipAddress)
        {
            byte[] buf = new byte[2*DbFlags.MAX_RECORD_LENGTH];
            int[] x = new int[2];
            int offset = 0;
            for (int depth = 31; depth >= 0; depth--)
            {
                try
                {
                    if ((_dboptions & LookupOptions.GEOIP_MEMORY_CACHE) == 1)
                    {
                        for (int i = 0; i < (2*DbFlags.MAX_RECORD_LENGTH); i++)
                        {
                            buf[i] = _dbbuffer[i + (2*_recordLength*offset)];
                        }
                    }
                    else
                    {
                        lock (_ioLock)
                        {
                            _file.Seek(2*_recordLength*offset, SeekOrigin.Begin);
                            _file.Read(buf, 0, 2*DbFlags.MAX_RECORD_LENGTH);
                        }
                    }
                }
                catch (IOException)
                {
                    Console.Write("IO Exception");
                }
                for (int i = 0; i < 2; i++)
                {
                    x[i] = 0;
                    for (int j = 0; j < _recordLength; j++)
                    {
                        int y = buf[(i*_recordLength) + j];
                        if (y < 0)
                        {
                            y += 256;
                        }
                        x[i] += (y << (j*8));
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
            Console.Write("Error Seeking country while Seeking " + ipAddress);
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
                ipnum += y << ((3 - i)*8);
            }
            return ipnum;
        }

        private static int UnsignedByteToInt(byte b)
        {
            return (int) b & 0xFF;
        }

    }
}
