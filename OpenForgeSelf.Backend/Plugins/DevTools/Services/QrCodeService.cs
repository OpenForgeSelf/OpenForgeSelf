using System.IO.Compression;
using System.Text;
using OpenForgeSelf.Backend.Plugins.DevTools.Models;
using NewLife.Log;

namespace OpenForgeSelf.Backend.Plugins.DevTools.Services;

public class QrCodeService : IQrCodeService
{
    private static readonly int[] _capacityTable = {
        26, 44, 70, 100, 134, 172, 196, 242, 292, 346,
        404, 466, 532, 581, 655, 733, 815, 901, 991, 1085,
        1156, 1258, 1364, 1474, 1588, 1706, 1828, 1921, 2051, 2185,
        2323, 2465, 2611, 2761, 2876, 3034, 3196, 3362, 3532, 3706
    };

    private static readonly int[] _ecCodewordsPerBlock = {
        7, 10, 13, 17, 7, 10, 13, 17, 15, 26, 39, 53,
        20, 36, 60, 78, 26, 48, 78, 106
    };

    private static readonly int[] _ecBlocksPerGroup1 = {
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 2, 2,
        2, 4, 4, 4, 2, 4, 6, 6
    };

    private static readonly int[] _ecDataCodewordsPerBlockG1 = {
        19, 34, 55, 80, 16, 28, 44, 64, 44, 64, 86, 108,
        70, 100, 134, 172, 98, 132, 154, 194
    };

    public Task<QrCodeGenerateResult> GenerateQrCodeAsync(string text, int size, string level, int margin)
    {
        try
        {
            XTrace.Log.Debug("[DevTools] 生成二维码，大小: {0}, 级别: {1}", size, level);

            if (string.IsNullOrWhiteSpace(text))
                throw new ArgumentException("二维码内容不能为空");
            if (size < 128 || size > 1024)
                throw new ArgumentException("图片大小必须在 128-1024 像素之间");
            if (margin < 0 || margin > 20)
                throw new ArgumentException("边距必须在 0-20 之间");

            var levelIndex = GetLevelIndex(level);
            var qrMatrix = GenerateQrMatrix(text, levelIndex);
            var imageBase64 = RenderQrCodeToPng(qrMatrix, size, margin, "#000000", "#FFFFFF");

            return Task.FromResult(new QrCodeGenerateResult
            {
                ImageBase64 = imageBase64,
                Size = size,
                Level = level
            });
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[DevTools] 二维码生成失败: {0}", ex.Message);
            throw new ArgumentException("二维码生成失败: " + ex.Message, nameof(text), ex);
        }
    }

    public Task<QrCodeGenerateResult> GenerateCustomQrCodeAsync(string text, int size, string level, int margin, string foregroundColor, string backgroundColor)
    {
        try
        {
            XTrace.Log.Debug("[DevTools] 生成自定义二维码，前景色: {0}, 背景色: {1}", foregroundColor, backgroundColor);

            if (string.IsNullOrWhiteSpace(text))
                throw new ArgumentException("二维码内容不能为空");
            if (size < 128 || size > 1024)
                throw new ArgumentException("图片大小必须在 128-1024 像素之间");
            if (margin < 0 || margin > 20)
                throw new ArgumentException("边距必须在 0-20 之间");

            var levelIndex = GetLevelIndex(level);
            var qrMatrix = GenerateQrMatrix(text, levelIndex);
            var imageBase64 = RenderQrCodeToPng(qrMatrix, size, margin, foregroundColor, backgroundColor);

            return Task.FromResult(new QrCodeGenerateResult
            {
                ImageBase64 = imageBase64,
                Size = size,
                Level = level
            });
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[DevTools] 自定义二维码生成失败: {0}", ex.Message);
            throw new ArgumentException("二维码生成失败: " + ex.Message, nameof(text), ex);
        }
    }

    public Task<QrCodeDecodeResult> DecodeQrCodeAsync(string imageBase64)
    {
        XTrace.Log.Debug("[DevTools] 解析二维码（暂未实现）");
        return Task.FromResult(new QrCodeDecodeResult
        {
            Success = false,
            ErrorMessage = "二维码解析功能暂未实现"
        });
    }

    private static int GetLevelIndex(string level)
    {
        return level.ToUpperInvariant() switch
        {
            "L" => 0,
            "M" => 1,
            "Q" => 2,
            "H" => 3,
            _ => throw new ArgumentException($"不支持的容错级别: {level}，支持 L, M, Q, H")
        };
    }

    private static bool[,] GenerateQrMatrix(string text, int levelIndex)
    {
        var dataBytes = Encoding.UTF8.GetBytes(text);
        var version = FindVersion(dataBytes.Length, levelIndex);
        var matrixSize = 17 + version * 4;

        var matrix = new bool[matrixSize, matrixSize];
        var isFunction = new bool[matrixSize, matrixSize];

        PlaceFinderPatterns(matrix, isFunction, matrixSize);
        PlaceAlignmentPatterns(matrix, isFunction, version, matrixSize);
        PlaceTimingPatterns(matrix, isFunction, matrixSize);
        PlaceFormatInfo(matrix, isFunction, matrixSize, levelIndex);
        PlaceVersionInfo(matrix, isFunction, version, matrixSize);

        var dataCodewords = GenerateDataCodewords(dataBytes, version, levelIndex);
        PlaceData(matrix, isFunction, dataCodewords, matrixSize);

        ApplyMask(matrix, isFunction, matrixSize, 0);

        return matrix;
    }

    private static int FindVersion(int dataLength, int levelIndex)
    {
        var baseIndex = levelIndex * 10;
        for (int i = 0; i < 40; i++)
        {
            var ecIndex = levelIndex * 10 + Math.Min(i / 4, 9);
            var blocks = _ecBlocksPerGroup1[Math.Min(levelIndex * 10 + i / 4, _ecBlocksPerGroup1.Length - 1)];
            var perBlock = _ecDataCodewordsPerBlockG1[Math.Min(levelIndex * 10 + i / 4, _ecDataCodewordsPerBlockG1.Length - 1)];
            var totalData = blocks * perBlock;

            var headerBits = 4 + 8;
            var dataBits = (dataLength + 2) * 8;
            var totalBits = headerBits + dataBits;
            var requiredBytes = (totalBits + 7) / 8;

            if (totalData >= requiredBytes)
                return Math.Min(i + 1, 40);
        }
        return 10;
    }

    private static void PlaceFinderPatterns(bool[,] matrix, bool[,] isFunction, int size)
    {
        int[,] positions = { { 0, 0 }, { 0, size - 7 }, { size - 7, 0 } };

        for (int p = 0; p < 3; p++)
        {
            int startX = positions[p, 0];
            int startY = positions[p, 1];

            for (int y = 0; y < 7; y++)
            {
                for (int x = 0; x < 7; x++)
                {
                    bool isDark = (x == 0 || x == 6 || y == 0 || y == 6) ||
                                  (x >= 2 && x <= 4 && y >= 2 && y <= 4);
                    matrix[startY + y, startX + x] = isDark;
                    isFunction[startY + y, startX + x] = true;
                }
            }
        }

        for (int i = 0; i < 8; i++)
        {
            if (i < 8)
            {
                matrix[7, i] = false;
                isFunction[7, i] = true;
                matrix[i, 7] = false;
                isFunction[i, 7] = true;

                matrix[7, size - 1 - i] = false;
                isFunction[7, size - 1 - i] = true;
                matrix[i, size - 8] = false;
                isFunction[i, size - 8] = true;

                matrix[size - 8, i] = false;
                isFunction[size - 8, i] = true;
                matrix[size - 1 - i, 7] = false;
                isFunction[size - 1 - i, 7] = true;
            }
        }
    }

    private static void PlaceAlignmentPatterns(bool[,] matrix, bool[,] isFunction, int version, int size)
    {
        if (version < 2) return;

        int[] alignmentPositions = GetAlignmentPositions(version);
        int count = alignmentPositions.Length;

        for (int i = 0; i < count; i++)
        {
            for (int j = 0; j < count; j++)
            {
                if ((i == 0 && j == 0) ||
                    (i == 0 && j == count - 1) ||
                    (i == count - 1 && j == 0))
                    continue;

                int centerX = alignmentPositions[j];
                int centerY = alignmentPositions[i];

                if (isFunction[centerY, centerX]) continue;

                for (int y = -2; y <= 2; y++)
                {
                    for (int x = -2; x <= 2; x++)
                    {
                        bool isDark = (x == -2 || x == 2 || y == -2 || y == 2) ||
                                      (x == 0 && y == 0);
                        matrix[centerY + y, centerX + x] = isDark;
                        isFunction[centerY + y, centerX + x] = true;
                    }
                }
            }
        }
    }

    private static int[] GetAlignmentPositions(int version)
    {
        if (version < 2) return Array.Empty<int>();

        int num = version / 7 + 2;
        int[] positions = new int[num];

        if (num > 0)
            positions[0] = 6;
        if (num > 1)
            positions[num - 1] = version * 4 + 10;

        if (num > 2)
        {
            int last = positions[num - 1];
            int step = (last - 6) / (num - 1);
            if (step % 2 == 1) step--;

            for (int i = num - 2; i >= 1; i--)
            {
                positions[i] = last - (num - 1 - i) * step;
                if (positions[i] % 2 == 0 && step % 2 == 0)
                {
                }
                else
                {
                    positions[i] = positions[i] - positions[i] % 2;
                }
            }
        }

        return positions;
    }

    private static void PlaceTimingPatterns(bool[,] matrix, bool[,] isFunction, int size)
    {
        for (int i = 8; i < size - 8; i++)
        {
            bool isDark = i % 2 == 0;
            matrix[6, i] = isDark;
            isFunction[6, i] = true;
            matrix[i, 6] = isDark;
            isFunction[i, 6] = true;
        }
    }

    private static void PlaceFormatInfo(bool[,] matrix, bool[,] isFunction, int size, int levelIndex)
    {
        var formatData = levelIndex switch
        {
            0 => 0x77c4,
            1 => 0x5412,
            2 => 0x355f,
            3 => 0x1689,
            _ => 0x5412
        };

        for (int i = 0; i < 15; i++)
        {
            bool bit = ((formatData >> i) & 1) == 1;

            if (i < 6)
            {
                matrix[i, 8] = bit;
                isFunction[i, 8] = true;
            }
            else if (i < 8)
            {
                matrix[i + 1, 8] = bit;
                isFunction[i + 1, 8] = true;
            }
            else
            {
                matrix[size - 15 + i, 8] = bit;
                isFunction[size - 15 + i, 8] = true;
            }

            if (i < 8)
            {
                matrix[8, size - 1 - i] = bit;
                isFunction[8, size - 1 - i] = true;
            }
            else if (i < 9)
            {
                matrix[8, 15 - i] = bit;
                isFunction[8, 15 - i] = true;
            }
            else
            {
                matrix[8, 14 - i] = bit;
                isFunction[8, 14 - i] = true;
            }
        }

        matrix[size - 8, 8] = true;
        isFunction[size - 8, 8] = true;
    }

    private static void PlaceVersionInfo(bool[,] matrix, bool[,] isFunction, int version, int size)
    {
        if (version < 7) return;

        long versionData = GetVersionInfo(version);

        for (int i = 0; i < 18; i++)
        {
            bool bit = ((versionData >> i) & 1) == 1;
            int x = i % 3;
            int y = i / 3;

            matrix[size - 11 + y, x] = bit;
            isFunction[size - 11 + y, x] = true;
            matrix[x, size - 11 + y] = bit;
            isFunction[x, size - 11 + y] = true;
        }
    }

    private static long GetVersionInfo(int version)
    {
        long[] versionCodes = {
            0x07c94, 0x085bc, 0x09a99, 0x0a4d3, 0x0bbf6, 0x0c762, 0x0d847, 0x0e60d,
            0x0f928, 0x10b78, 0x1145d, 0x12a17, 0x13532, 0x149a6, 0x15683, 0x168c9,
            0x177ec, 0x18ec4, 0x191e1, 0x1afab, 0x1b08e, 0x1cc1a, 0x1d33f, 0x1ed75,
            0x1f250, 0x209d5, 0x216f0, 0x228ba, 0x2379f, 0x24b0b, 0x2542e, 0x26a64,
            0x27541, 0x28c69
        };

        if (version >= 7 && version <= 40)
            return versionCodes[version - 7];
        return 0;
    }

    private static byte[] GenerateDataCodewords(byte[] dataBytes, int version, int levelIndex)
    {
        var ecIndex = Math.Min(levelIndex * 10 + version / 4, _ecDataCodewordsPerBlockG1.Length - 1);
        var blocks = _ecBlocksPerGroup1[Math.Min(levelIndex * 10 + version / 4, _ecBlocksPerGroup1.Length - 1)];
        var perBlock = _ecDataCodewordsPerBlockG1[ecIndex];
        var ecPerBlock = _ecCodewordsPerBlock[Math.Min(levelIndex * 10 + version / 4, _ecCodewordsPerBlock.Length - 1)];

        var totalData = blocks * perBlock;

        var dataStream = new List<byte>();

        dataStream.Add(0x40);

        var lengthBytes = BitConverter.GetBytes((short)dataBytes.Length);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(lengthBytes);
        dataStream.AddRange(lengthBytes);

        dataStream.AddRange(dataBytes);

        if (dataStream.Count < totalData)
            dataStream.Add(0xEC);
        if (dataStream.Count < totalData)
            dataStream.Add(0x11);

        while (dataStream.Count < totalData)
        {
            dataStream.Add(0xEC);
            if (dataStream.Count < totalData)
                dataStream.Add(0x11);
        }

        var result = new byte[totalData + blocks * ecPerBlock];
        var blockData = new byte[blocks][];

        for (int i = 0; i < blocks; i++)
        {
            blockData[i] = new byte[perBlock];
            Array.Copy(dataStream.ToArray(), i * perBlock, blockData[i], 0, perBlock);
        }

        for (int i = 0; i < blocks; i++)
        {
            var ec = ComputeReedSolomon(blockData[i], ecPerBlock);
            Array.Copy(blockData[i], 0, result, i * (perBlock + ecPerBlock), perBlock);
            Array.Copy(ec, 0, result, i * (perBlock + ecPerBlock) + perBlock, ecPerBlock);
        }

        var finalResult = new List<byte>();
        for (int i = 0; i < perBlock; i++)
        {
            for (int j = 0; j < blocks; j++)
            {
                finalResult.Add(result[j * (perBlock + ecPerBlock) + i]);
            }
        }

        for (int i = 0; i < ecPerBlock; i++)
        {
            for (int j = 0; j < blocks; j++)
            {
                finalResult.Add(result[j * (perBlock + ecPerBlock) + perBlock + i]);
            }
        }

        return finalResult.ToArray();
    }

    private static byte[] ComputeReedSolomon(byte[] data, int ecLength)
    {
        var result = new byte[ecLength];
        var logTable = new byte[256];
        var expTable = new byte[512];

        int x = 1;
        for (int i = 0; i < 255; i++)
        {
            expTable[i] = (byte)x;
            logTable[x] = (byte)i;
            x <<= 1;
            if ((x & 0x100) != 0)
                x ^= 0x11d;
        }
        for (int i = 255; i < 512; i++)
            expTable[i] = expTable[i - 255];

        var generator = new byte[ecLength + 1];
        generator[0] = 1;
        for (int i = 0; i < ecLength; i++)
        {
            for (int j = i; j >= 0; j--)
            {
                if (generator[j] != 0)
                {
                    generator[j + 1] ^= expTable[(logTable[generator[j]] + i) % 255];
                }
            }
        }

        Array.Copy(data, result, Math.Min(data.Length, ecLength));
        for (int i = 0; i < data.Length; i++)
        {
            byte coef = (byte)(data[i] ^ result[0]);
            for (int j = 0; j < ecLength - 1; j++)
            {
                if (coef != 0 && generator[j + 1] != 0)
                    result[j] = (byte)(result[j + 1] ^ expTable[(logTable[coef] + logTable[generator[j + 1]]) % 255]);
                else
                    result[j] = result[j + 1];
            }
            if (coef != 0 && generator[ecLength] != 0)
                result[ecLength - 1] = (byte)expTable[(logTable[coef] + logTable[generator[ecLength]]) % 255];
            else
                result[ecLength - 1] = 0;
        }

        return result;
    }

    private static void PlaceData(bool[,] matrix, bool[,] isFunction, byte[] data, int size)
    {
        bool upward = true;
        int dataIndex = 0;
        int bitIndex = 0;

        for (int x = size - 1; x > 0; x -= 2)
        {
            if (x == 6) x--;

            for (int y = 0; y < size; y++)
            {
                int actualY = upward ? size - 1 - y : y;

                for (int xOffset = 0; xOffset < 2; xOffset++)
                {
                    int actualX = x - xOffset;

                    if (!isFunction[actualY, actualX])
                    {
                        bool bit = false;
                        if (dataIndex < data.Length)
                        {
                            bit = ((data[dataIndex] >> (7 - bitIndex)) & 1) == 1;
                            bitIndex++;
                            if (bitIndex == 8)
                            {
                                bitIndex = 0;
                                dataIndex++;
                            }
                        }
                        matrix[actualY, actualX] = bit;
                    }
                }
            }
            upward = !upward;
        }
    }

    private static void ApplyMask(bool[,] matrix, bool[,] isFunction, int size, int maskPattern)
    {
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                if (isFunction[y, x]) continue;

                bool invert = maskPattern switch
                {
                    0 => (x + y) % 2 == 0,
                    1 => y % 2 == 0,
                    2 => x % 3 == 0,
                    3 => (x + y) % 3 == 0,
                    4 => (x / 3 + y / 2) % 2 == 0,
                    5 => (x * y) % 2 + (x * y) % 3 == 0,
                    6 => ((x * y) % 2 + (x * y) % 3) % 2 == 0,
                    7 => ((x + y) % 2 + (x * y) % 3) % 2 == 0,
                    _ => false
                };

                if (invert)
                    matrix[y, x] = !matrix[y, x];
            }
        }
    }

    private static string RenderQrCodeToPng(bool[,] matrix, int size, int margin, string foreground, string background)
    {
        var matrixSize = matrix.GetLength(0);
        var totalModules = matrixSize + 2 * margin;
        var moduleSize = Math.Max(1, size / totalModules);
        var actualSize = moduleSize * totalModules;

        var fg = ParseColor(foreground);
        var bg = ParseColor(background);

        var pixelData = new byte[actualSize * actualSize * 4];
        for (int i = 0; i < pixelData.Length; i += 4)
        {
            pixelData[i] = bg.r;
            pixelData[i + 1] = bg.g;
            pixelData[i + 2] = bg.b;
            pixelData[i + 3] = 255;
        }

        for (int y = 0; y < matrixSize; y++)
        {
            for (int x = 0; x < matrixSize; x++)
            {
                if (matrix[y, x])
                {
                    var startX = (margin + x) * moduleSize;
                    var startY = (margin + y) * moduleSize;
                    for (int py = 0; py < moduleSize; py++)
                    {
                        for (int px = 0; px < moduleSize; px++)
                        {
                            var idx = ((startY + py) * actualSize + (startX + px)) * 4;
                            pixelData[idx] = fg.r;
                            pixelData[idx + 1] = fg.g;
                            pixelData[idx + 2] = fg.b;
                            pixelData[idx + 3] = 255;
                        }
                    }
                }
            }
        }

        var pngBytes = GeneratePng(pixelData, actualSize, actualSize);
        var base64 = Convert.ToBase64String(pngBytes);
        return base64;
    }

    private static (byte r, byte g, byte b) ParseColor(string hex)
    {
        if (hex.StartsWith('#'))
            hex = hex[1..];
        if (hex.Length == 3)
            hex = $"{hex[0]}{hex[0]}{hex[1]}{hex[1]}{hex[2]}{hex[2]}";
        var r = Convert.ToByte(hex[..2], 16);
        var g = Convert.ToByte(hex.Substring(2, 2), 16);
        var b = Convert.ToByte(hex.Substring(4, 2), 16);
        return (r, g, b);
    }

    private static byte[] GeneratePng(byte[] pixels, int width, int height)
    {
        using var ms = new MemoryStream();

        byte[] signature = { 137, 80, 78, 71, 13, 10, 26, 10 };
        ms.Write(signature, 0, signature.Length);

        WriteChunk(ms, "IHDR", BuildIhdr(width, height));

        WriteChunk(ms, "IDAT", BuildIdat(pixels, width, height));

        WriteChunk(ms, "IEND", Array.Empty<byte>());

        return ms.ToArray();
    }

    private static byte[] BuildIhdr(int width, int height)
    {
        var data = new byte[13];
        WriteBigEndian(data, 0, width);
        WriteBigEndian(data, 4, height);
        data[8] = 8;
        data[9] = 6;
        data[10] = 0;
        data[11] = 0;
        data[12] = 0;
        return data;
    }

    private static byte[] BuildIdat(byte[] pixels, int width, int height)
    {
        var stride = width * 4 + 1;
        var rawData = new byte[stride * height];
        for (int y = 0; y < height; y++)
        {
            rawData[y * stride] = 0;
            Array.Copy(pixels, y * width * 4, rawData, y * stride + 1, width * 4);
        }

        using var compressedMs = new MemoryStream();
        compressedMs.WriteByte(120);
        compressedMs.WriteByte(156);

        using (var deflate = new DeflateStream(compressedMs, CompressionLevel.Optimal, leaveOpen: true))
        {
            deflate.Write(rawData, 0, rawData.Length);
        }

        uint adler = Adler32(rawData);
        var adlerBytes = new byte[4];
        WriteBigEndian(adlerBytes, 0, (int)adler);
        compressedMs.Write(adlerBytes, 0, 4);

        return compressedMs.ToArray();
    }

    private static void WriteChunk(MemoryStream ms, string type, byte[] data)
    {
        var lengthBytes = new byte[4];
        WriteBigEndian(lengthBytes, 0, data.Length);
        ms.Write(lengthBytes, 0, 4);

        var typeBytes = Encoding.ASCII.GetBytes(type);
        ms.Write(typeBytes, 0, 4);

        if (data.Length > 0)
            ms.Write(data, 0, data.Length);

        uint crc = Crc32(typeBytes.Concat(data).ToArray());
        var crcBytes = new byte[4];
        WriteBigEndian(crcBytes, 0, (int)crc);
        ms.Write(crcBytes, 0, 4);
    }

    private static void WriteBigEndian(byte[] buffer, int offset, int value)
    {
        buffer[offset] = (byte)((value >> 24) & 0xFF);
        buffer[offset + 1] = (byte)((value >> 16) & 0xFF);
        buffer[offset + 2] = (byte)((value >> 8) & 0xFF);
        buffer[offset + 3] = (byte)(value & 0xFF);
    }

    private static uint Crc32(byte[] data)
    {
        uint crc = 0xFFFFFFFF;
        for (int i = 0; i < data.Length; i++)
        {
            crc ^= data[i];
            for (int j = 0; j < 8; j++)
            {
                if ((crc & 1) != 0)
                    crc = (crc >> 1) ^ 0xEDB88320;
                else
                    crc >>= 1;
            }
        }
        return ~crc;
    }

    private static uint Adler32(byte[] data)
    {
        uint a = 1, b = 0;
        const uint MOD = 65521;
        for (int i = 0; i < data.Length; i++)
        {
            a = (a + data[i]) % MOD;
            b = (b + a) % MOD;
        }
        return (b << 16) | a;
    }
}
