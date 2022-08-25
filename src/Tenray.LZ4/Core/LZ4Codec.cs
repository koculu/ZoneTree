#region license

/*
Copyright (c) 2013, Milosz Krajewski
All rights reserved.

Redistribution and use in source and binary forms, with or without modification, are permitted provided
that the following conditions are met:

* Redistributions of source code must retain the above copyright notice, this list of conditions
  and the following disclaimer.

* Redistributions in binary form must reproduce the above copyright notice, this list of conditions
  and the following disclaimer in the documentation and/or other materials provided with the distribution.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED
WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE
FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY,
OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN
IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*/

#endregion

#region Modification license
//
//  Copyright 2014  Matthew Ducker
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.
#endregion


// ReSharper disable InconsistentNaming

namespace Tenray.LZ4.Core;

public static partial class LZ4Codec
{
    #region configuration

    /// <summary>
    ///     Memory usage formula : N->2^N Bytes (examples : 10 -> 1KB; 12 -> 4KB ; 16 -> 64KB; 20 -> 1MB; etc.)
    ///     Increasing memory usage improves compression ratio
    ///     Reduced memory usage can improve speed, due to cache effect
    ///     Default value is 14, for 16KB, which nicely fits into Intel x86 L1 cache
    /// </summary>
    private const int MEMORY_USAGE = 14;

    /// <summary>
    ///     Decreasing this value will make the algorithm skip faster data segments considered "incompressible"
    ///     This may decrease compression ratio dramatically, but will be faster on incompressible data
    ///     Increasing this value will make the algorithm search more before declaring a segment "incompressible"
    ///     This could improve compression a bit, but will be slower on incompressible data
    ///     The default value (6) is recommended
    /// </summary>
    private const int NOTCOMPRESSIBLE_DETECTIONLEVEL = 6;

    /// <summary>
    ///     Buffer length when Buffer.BlockCopy becomes faster than straight loop.
    ///     Please note that safe implementation REQUIRES it to be greater (not even equal) than 8.
    /// </summary>
    private const int BLOCK_COPY_LIMIT = 16;

    #endregion

    #region consts

    private const int MINMATCH = 4;
    private const int SKIPSTRENGTH = NOTCOMPRESSIBLE_DETECTIONLEVEL > 2 ? NOTCOMPRESSIBLE_DETECTIONLEVEL : 2;
    private const int COPYLENGTH = 8;
    private const int LASTLITERALS = 5;
    private const int MFLIMIT = COPYLENGTH + MINMATCH;
    private const int MINLENGTH = MFLIMIT + 1;
    private const int MAXD_LOG = 16;
    private const int MAXD = 1 << MAXD_LOG;
    private const int MAXD_MASK = MAXD - 1;
    private const int MAX_DISTANCE = (1 << MAXD_LOG) - 1;
    private const int ML_BITS = 4;
    private const int ML_MASK = (1 << ML_BITS) - 1;
    private const int RUN_BITS = 8 - ML_BITS;
    private const int RUN_MASK = (1 << RUN_BITS) - 1;
    private const int STEPSIZE_64 = 8;
    private const int STEPSIZE_32 = 4;

    private const int LZ4_64KLIMIT = (1 << 16) + (MFLIMIT - 1);

    private const int HASH_LOG = MEMORY_USAGE - 2;
    private const int HASH_TABLESIZE = 1 << HASH_LOG;
    private const int HASH_ADJUST = MINMATCH * 8 - HASH_LOG;

    private const int HASH64K_LOG = HASH_LOG + 1;
    private const int HASH64K_TABLESIZE = 1 << HASH64K_LOG;
    private const int HASH64K_ADJUST = MINMATCH * 8 - HASH64K_LOG;

    private const int HASHHC_LOG = MAXD_LOG - 1;
    private const int HASHHC_TABLESIZE = 1 << HASHHC_LOG;
    private const int HASHHC_ADJUST = MINMATCH * 8 - HASHHC_LOG;
    //private const int HASHHC_MASK = HASHHC_TABLESIZE - 1;

    private static readonly int[] DECODER_TABLE_32 = { 0, 3, 2, 3, 0, 0, 0, 0 };
    private static readonly int[] DECODER_TABLE_64 = { 0, 0, 0, -1, 0, 1, 2, 3 };

    private static readonly int[] DEBRUIJN_TABLE_32 = {
        0, 0, 3, 0, 3, 1, 3, 0, 3, 2, 2, 1, 3, 2, 0, 1,
        3, 3, 1, 2, 2, 2, 2, 0, 3, 1, 2, 0, 1, 0, 1, 1
    };

    private static readonly int[] DEBRUIJN_TABLE_64 = {
        0, 0, 0, 0, 0, 1, 1, 2, 0, 3, 1, 3, 1, 4, 2, 7,
        0, 2, 3, 6, 1, 5, 3, 5, 1, 3, 4, 4, 2, 5, 6, 7,
        7, 0, 1, 2, 3, 3, 4, 6, 2, 6, 5, 5, 3, 4, 5, 6,
        7, 1, 2, 4, 6, 4, 4, 5, 7, 2, 6, 5, 7, 6, 7, 7
    };

    private const int MAX_NB_ATTEMPTS = 256;
    private const int OPTIMAL_ML = ML_MASK - 1 + MINMATCH;

    #endregion

    #region public interface (common)

    /// <summary>Gets maximum the length of the output.</summary>
    /// <param name="inputLength">Length of the input.</param>
    /// <returns>Maximum number of bytes needed for compressed buffer.</returns>
    public static int MaximumOutputLength(int inputLength)
    {
        return inputLength + inputLength / 255 + 16;
    }

    #endregion

    #region internal interface (common)

    internal static void CheckArguments(
        byte[] input, int inputOffset, ref int inputLength,
        byte[] output, int outputOffset, ref int outputLength)
    {
        if (inputLength < 0)
        {
            inputLength = input.Length - inputOffset;
        }
        if (inputLength == 0)
        {
            outputLength = 0;
            return;
        }

        if (input == null)
        {
            throw new ArgumentNullException(nameof(input));
        }
        if (inputOffset < 0 || inputOffset + inputLength > input.Length)
        {
            throw new ArgumentException("inputOffset and inputLength are invalid for given input");
        }

        if (outputLength < 0)
        {
            outputLength = output.Length - outputOffset;
        }
        if (output == null)
        {
            throw new ArgumentNullException(nameof(output));
        }
        if (outputOffset < 0 || outputOffset + outputLength > output.Length)
        {
            throw new ArgumentException("outputOffset and outputLength are invalid for given output");
        }
    }

    #endregion

    private static readonly int _platform = IntPtr.Size * 8;

    /// <summary>
    ///     Encode a block of input with the LZ4 compression algorithm, 
    ///     producing compressed output.
    /// </summary>
    /// <remarks>
    ///     Self-optimises to use 32 or 64 bit implementation depending on execution environment word size.
    ///     Will use 'unsafe' implementation also, if LZ4.Portable was compiled in ReleaseUnsafe configuration 
    ///     (uses an INCLUDE_UNSAFE directive; recommended if at all possible, as performance is much higher).
    /// </remarks>
    /// <param name="input">Buffer containing data to be compressed.</param>
    /// <param name="inputOffset">Offset in <paramref name="input"/> to read from.</param>
    /// <param name="inputLength">Number of bytes to compress.</param>
    /// <param name="output">Buffer to write compressed data to.</param>
    /// <param name="outputOffset">Offset in <paramref name="output"/> to write to.</param>
    /// <param name="outputLength">Maximum size of output to generate.</param>
    /// <param name="highCompression">If <c>true</c>, use a higher-compression but slower codec variant.</param>
    /// <returns>Number of bytes written to <paramref name="output"/> buffer.</returns>
    public static int Encode(byte[] input, int inputOffset, int inputLength, byte[] output, int outputOffset, int outputLength,
        bool highCompression = false)
    {
        switch (_platform)
        {
            case 32:
                // x86 or other 32-bit word size ISA
                return highCompression
                    ? Encode32HC(input, inputOffset, inputLength, output, outputOffset, outputLength)
                    : Encode32(input, inputOffset, inputLength, output, outputOffset, outputLength);
            case 64:
#if INCLUDE_UNSAFE
                return highCompression ?
                    Encode64HC(input, inputOffset, inputLength, output, outputOffset, outputLength)
                    : Encode64(input, inputOffset, inputLength, output, outputOffset, outputLength);
#else
                return highCompression ?
                    Encode32HC(input, inputOffset, inputLength, output, outputOffset, outputLength)
                    : Encode32(input, inputOffset, inputLength, output, outputOffset, outputLength);
#endif
            default:
                throw new InvalidOperationException();
        }
    }

    /// <summary>
    ///     Decode a block of input that was encoded with the LZ4 compression algorithm, 
    ///     producing decompressed output.
    /// </summary>
    /// <remarks>
    ///     Self-optimises to use 32 or 64 bit implementation depending on execution environment word size.
    ///     Will use 'unsafe' implementation also, if LZ4.Portable was compiled in ReleaseUnsafe configuration 
    ///     (uses an INCLUDE_UNSAFE directive; recommended if at all possible, as performance is much higher).
    /// </remarks>
    /// <param name="input">Buffer containing data to be decompressed.</param>
    /// <param name="inputOffset">Offset in <paramref name="input"/> to read from.</param>
    /// <param name="inputLength">Number of bytes to read for decompressing.</param>
    /// <param name="output">Buffer to write decompressed data to.</param>
    /// <param name="outputOffset">Offset in <paramref name="output"/> to write to.</param>
    /// <param name="outputLength">Maximum size of output allowable.</param>
    /// <param name="knownLength">If <c>true</c>, length of the input block is known.</param>
    /// <returns>Number of bytes written to <paramref name="output"/> buffer.</returns>
    public static int Decode(byte[] input, int inputOffset, int inputLength, byte[] output, int outputOffset, int outputLength,
        bool knownLength = false)
    {
        switch (_platform)
        {
            case 32:
                // x86 or other 32-bit word size ISA
                return Decode64(input, inputOffset, inputLength, output, outputOffset, outputLength, knownLength);
            case 64:
#if INCLUDE_UNSAFE
                return Decode32(input, inputOffset, inputLength, output, outputOffset, outputLength, knownLength);
#else
                return Decode64(input, inputOffset, inputLength, output, outputOffset, outputLength, knownLength);
#endif
            default:
                throw new InvalidOperationException();
        }
    }
}

// ReSharper restore InconsistentNaming
