/**
 * DatabaseInfo.java
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

namespace GeoIP
{
    public class DatabaseInfo
    {
        private readonly String _info;

        /**
          * Creates a new DatabaseInfo object given the database info String.
          * @param info
          */
        public DatabaseInfo(String info)
        {
            _info = info;
        }

        public int GetTypeCode()
        {
            if ((_info == null) | (_info == ""))
            {
                return DatabaseTypeCodes.COUNTRY_EDITION;
            }
            
            // Get the type code from the database info string and then
            // subtract 105 from the value to preserve compatability with
            // databases from April 2003 and earlier.
            return Convert.ToInt32(_info.Substring(4, 3)) - 105;
        }

        /**
         * Returns true if the database is the premium version.
         *
         * @return true if the premium version of the database.
         */
        public bool IsPremium()
        {
            return _info.IndexOf("FREE", StringComparison.Ordinal) < 0;
        }

        /**
         * Returns the date of the database.
         *
         * @return the date of the database.
         */
        public DateTime GetDate()
        {
            for (int i = 0; i < _info.Length - 9; i++)
            {
                if (Char.IsWhiteSpace(_info[i]))
                {
                    String dateString = _info.Substring(i + 1, 8);
                    try
                    {
                        return DateTime.ParseExact(dateString, "yyyyMMdd", null);
                    }
                    catch (Exception e)
                    {
                        Console.Write(e.Message);
                    }
                    break;
                }
            }
            return DateTime.Now;
        }

        public override string ToString()
        {
            return _info;
        }
    }
}

