//******************************************************************************************************
//  Crc16.cs - Gbtc
//
//  Copyright � 2012, Grid Protection Alliance.  All Rights Reserved.
//
//  Licensed to the Grid Protection Alliance (GPA) under one or more contributor license agreements. See
//  the NOTICE file distributed with this work for additional information regarding copyright ownership.
//  The GPA licenses this file to you under the MIT License (MIT), the "License"; you may
//  not use this file except in compliance with the License. You may obtain a copy of the License at:
//
//      http://www.opensource.org/licenses/MIT
//
//  Unless agreed to in writing, the subject software distributed under the License is distributed on an
//  "AS-IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. Refer to the
//  License for the specific language governing permissions and limitations.
//
//  Code Modification History:
//  ----------------------------------------------------------------------------------------------------
//  09/25/2008 - J. Ritchie Carroll
//       Generated original version of source code.
//  06/10/2009 - Mehulbhi Thakkar
//       Modified code to calculate either standard CRC-16 or ModBus CRC.
//  08/05/2009 - Josh L. Patterson
//       Edited Comments.
//  09/14/2009 - Stephen C. Wills
//       Added new header and license agreement.
//  12/14/2012 - Starlynn Danyelle Gilliam
//       Modified Header.
//
//******************************************************************************************************

using System;

namespace gemstone.io.checksums
{
    #region [ Enumerations ]

    /// <summary>
    /// Indicates type of CRC-16 calculation performed.
    /// </summary>
    public enum ChecksumType
    {
        /// <summary>
        /// Regular CRC-16 calculation.
        /// </summary>
        Crc16,

        /// <summary>
        /// ModBus CRC-16 calculation.
        /// </summary>
        ModBus
    }

    #endregion

    /// <summary>
    /// Generates a byte-wise 16-bit CRC calculation.
    /// </summary>
    /// <remarks>
    /// <para>2-byte (16-bit) CRC: The generating polynomial is</para>
    /// <para>        16   15   2    1</para>
    /// <para>G(X) = X  + X  + X  + X</para>
    /// </remarks>
    public sealed class Crc16
    {
        #region [ Members ]

        // Constants
        private const ushort Crc16Seed = 0x0000;
        private const ushort ModBusSeed = 0xFFFF;

        #endregion

        #region [ Constructors ]

        /// <summary>
        /// Creates a new instance of the Crc16 class.
        /// The checksum starts off with a value of 0x0000.
        /// </summary>
        public Crc16() => Reset();

        /// <summary>
        /// Creates a new instance of the Crc16 class.
        /// </summary>
        /// <param name="checksumType">
        /// Type of calculation to perform, CRC-16 or ModBus.
        /// </param>
        public Crc16(ChecksumType checksumType) => Reset(checksumType);

        #endregion

        #region [ Properties ]

        /// <summary>
        /// Returns the CRC-16 data checksum computed so far.
        /// </summary>
        public ushort Value { get; set; }

        #endregion

        #region [ Methods ]

        /// <summary>
        /// Resets the CRC-16 data checksum as if no update was ever called.
        /// </summary>
        public void Reset() => Reset(ChecksumType.Crc16);

        /// <summary>
        /// Resets the CRC-16 data checksum as if no update was ever called.
        /// </summary>
        /// <param name="checksumType">Type of CRC calculation. CRC-16 resets to 0x0000, ModBus resets to 0xFFFF</param>
        public void Reset(ChecksumType checksumType) => Value = checksumType == ChecksumType.ModBus ? ModBusSeed : Crc16Seed;

        /// <summary>
        /// Updates the checksum with the byte value.
        /// </summary>
        /// <param name="value">The <see cref="byte"/> value to use for the update.</param>
        public void Update(byte value)
        {
            ushort temp = (ushort)(value & 0x00FF);
            temp = (ushort)(Value ^ temp);
            Value = (ushort)((Value >> 8) ^ CrcTable[temp & 0xFF]);
        }

        /// <summary>
        /// Updates the checksum with the bytes taken from the array.
        /// </summary>
        /// <param name="buffer">buffer an array of bytes</param>
        public void Update(byte[] buffer)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));

            Update(buffer, 0, buffer.Length);
        }

        /// <summary>
        /// Adds the byte array to the data checksum.
        /// </summary>
        /// <param name="buffer">The buffer which contains the data</param>
        /// <param name="offset">The offset in the buffer where the data starts</param>
        /// <param name="count">The number of data bytes to update the CRC with.</param>
        public void Update(byte[] buffer, int offset, int count)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));

            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count), "Count cannot be less than zero");

            if (offset < 0 || offset + count > buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(offset));

            for (int i = 0; i < count; i++)
            {
                ushort temp = (ushort)(buffer[offset + i] & 0x00FF);
                temp = (ushort)(Value ^ temp);
                Value = (ushort)((Value >> 8) ^ CrcTable[temp & 0xFF]);
            }
        }

        #endregion

        #region [ Static ]

        // Static Fields
        private static readonly ushort[] CrcTable = {
           0X0000, 0XC0C1, 0XC181, 0X0140, 0XC301, 0X03C0, 0X0280, 0XC241,
           0XC601, 0X06C0, 0X0780, 0XC741, 0X0500, 0XC5C1, 0XC481, 0X0440,
           0XCC01, 0X0CC0, 0X0D80, 0XCD41, 0X0F00, 0XCFC1, 0XCE81, 0X0E40,
           0X0A00, 0XCAC1, 0XCB81, 0X0B40, 0XC901, 0X09C0, 0X0880, 0XC841,
           0XD801, 0X18C0, 0X1980, 0XD941, 0X1B00, 0XDBC1, 0XDA81, 0X1A40,
           0X1E00, 0XDEC1, 0XDF81, 0X1F40, 0XDD01, 0X1DC0, 0X1C80, 0XDC41,
           0X1400, 0XD4C1, 0XD581, 0X1540, 0XD701, 0X17C0, 0X1680, 0XD641,
           0XD201, 0X12C0, 0X1380, 0XD341, 0X1100, 0XD1C1, 0XD081, 0X1040,
           0XF001, 0X30C0, 0X3180, 0XF141, 0X3300, 0XF3C1, 0XF281, 0X3240,
           0X3600, 0XF6C1, 0XF781, 0X3740, 0XF501, 0X35C0, 0X3480, 0XF441,
           0X3C00, 0XFCC1, 0XFD81, 0X3D40, 0XFF01, 0X3FC0, 0X3E80, 0XFE41,
           0XFA01, 0X3AC0, 0X3B80, 0XFB41, 0X3900, 0XF9C1, 0XF881, 0X3840,
           0X2800, 0XE8C1, 0XE981, 0X2940, 0XEB01, 0X2BC0, 0X2A80, 0XEA41,
           0XEE01, 0X2EC0, 0X2F80, 0XEF41, 0X2D00, 0XEDC1, 0XEC81, 0X2C40,
           0XE401, 0X24C0, 0X2580, 0XE541, 0X2700, 0XE7C1, 0XE681, 0X2640,
           0X2200, 0XE2C1, 0XE381, 0X2340, 0XE101, 0X21C0, 0X2080, 0XE041,
           0XA001, 0X60C0, 0X6180, 0XA141, 0X6300, 0XA3C1, 0XA281, 0X6240,
           0X6600, 0XA6C1, 0XA781, 0X6740, 0XA501, 0X65C0, 0X6480, 0XA441,
           0X6C00, 0XACC1, 0XAD81, 0X6D40, 0XAF01, 0X6FC0, 0X6E80, 0XAE41,
           0XAA01, 0X6AC0, 0X6B80, 0XAB41, 0X6900, 0XA9C1, 0XA881, 0X6840,
           0X7800, 0XB8C1, 0XB981, 0X7940, 0XBB01, 0X7BC0, 0X7A80, 0XBA41,
           0XBE01, 0X7EC0, 0X7F80, 0XBF41, 0X7D00, 0XBDC1, 0XBC81, 0X7C40,
           0XB401, 0X74C0, 0X7580, 0XB541, 0X7700, 0XB7C1, 0XB681, 0X7640,
           0X7200, 0XB2C1, 0XB381, 0X7340, 0XB101, 0X71C0, 0X7080, 0XB041,
           0X5000, 0X90C1, 0X9181, 0X5140, 0X9301, 0X53C0, 0X5280, 0X9241,
           0X9601, 0X56C0, 0X5780, 0X9741, 0X5500, 0X95C1, 0X9481, 0X5440,
           0X9C01, 0X5CC0, 0X5D80, 0X9D41, 0X5F00, 0X9FC1, 0X9E81, 0X5E40,
           0X5A00, 0X9AC1, 0X9B81, 0X5B40, 0X9901, 0X59C0, 0X5880, 0X9841,
           0X8801, 0X48C0, 0X4980, 0X8941, 0X4B00, 0X8BC1, 0X8A81, 0X4A40,
           0X4E00, 0X8EC1, 0X8F81, 0X4F40, 0X8D01, 0X4DC0, 0X4C80, 0X8C41,
           0X4400, 0X84C1, 0X8581, 0X4540, 0X8701, 0X47C0, 0X4680, 0X8641,
           0X8201, 0X42C0, 0X4380, 0X8341, 0X4100, 0X81C1, 0X8081, 0X4040
        };

        #endregion
    }
}
