using ImageLibrary.Utils;
using System;

namespace ImageLibrary.PlatformSwizzle.Algorithms.Switch
{
    public class TegraSwizzleLinearTile
    {
        public static byte[] Deswizzle(uint width, uint height, uint depth, uint arrayCount, uint mipCount,
            (uint Width, uint Height, uint Depth) blockSize, uint bpp, byte[] data, int mip_slice = -1)
        {
            var dims = new BlockDimX64
            {
                width = blockSize.Width,
                height = blockSize.Height,
                depth = blockSize.Depth
            };

            var blockHeightMip0 = TextureConverter.GetBlockHeight(
                TextureConverter.DIV_ROUND_UP(height, (uint)dims.height));

            var span = data.AsSpan();
            uint offset = 0;

            var mips = new List<byte[]>();

            for (int a = 0; a < arrayCount; a++)
            {
                for (int mip = 0; mip < mipCount; mip++)
                {
                    uint mipWidth = Math.Max(width >> mip, 1);
                    uint mipHeight = Math.Max(height >> mip, 1);
                    uint mipDepth = Math.Max(depth >> mip, 1);

                    int mipSize = CalculatePitchLinearSize(mipWidth, mipHeight, mipDepth,
                       (uint)dims.width, (uint)dims.height, (uint)dims.depth, bpp);

                    byte[] mipData;

                    if (offset + mipSize <= span.Length)
                    {
                        var slice = span.Slice((int)offset, mipSize);
                        mipData = SwizzlePitchLinear(mipWidth, mipHeight, mipDepth,
                            (uint)dims.width, (uint)dims.height, (uint)dims.depth, bpp, slice.ToArray(), deswizzle: true);
                    }
                    else
                    {
                        mipData = new byte[mipWidth * mipHeight * bpp]; // fallback zeroed data
                    }

                    mips.Add(mipData);
                    offset += (uint)mipSize;
                }

                // Align between array layers
                offset = TextureConverter.AlignLayerSize(offset, height, depth, blockHeightMip0, 1);
            }

            if (mip_slice != -1)
                return mips[mip_slice];

            return ByteUtil.CombineByteArray(mips.ToArray());
        }

        public static byte[] Swizzle(uint width, uint height, uint depth, uint arrayCount, uint mipCount,
            (uint Width, uint Height, uint Depth) blockSize, uint bpp, byte[] data)
        {
            var dims = new BlockDimX64
            {
                width = blockSize.Width,
                height = blockSize.Height,
                depth = blockSize.Depth
            };

            var blockHeightMip0 = TextureConverter.GetBlockHeight(
                TextureConverter.DIV_ROUND_UP(height, (uint)dims.height));

            uint totalSize = CalculateTotalPitchLinearSize(width, height, depth,
                (uint)dims.width, (uint)dims.height, (uint)dims.depth, arrayCount, mipCount, bpp);

            byte[] output = new byte[totalSize];
            var outputSpan = output.AsSpan();

            var srcSpan = data.AsSpan();
            uint srcOffset = 0;
            uint dstOffset = 0;

            for (int a = 0; a < arrayCount; a++)
            {
                for (int mip = 0; mip < mipCount; mip++)
                {
                    uint mipWidth = Math.Max(width >> mip, 1);
                    uint mipHeight = Math.Max(height >> mip, 1);
                    uint mipDepth = Math.Max(depth >> mip, 1);

                    int srcSize = (int)(mipWidth * mipHeight * bpp);
                    int dstSize = CalculatePitchLinearSize(mipWidth, mipHeight, mipDepth,
                      (uint)dims.width, (uint)dims.height, (uint)dims.depth, bpp);

                    if (srcOffset + srcSize <= srcSpan.Length)
                    {
                        var srcSlice = srcSpan.Slice((int)srcOffset, srcSize);

                        var swizzled = SwizzlePitchLinear(mipWidth, mipHeight, mipDepth,
                             (uint)dims.width, (uint)dims.height, (uint)dims.depth, bpp, srcSlice.ToArray(), deswizzle: false);

                        swizzled.CopyTo(outputSpan.Slice((int)dstOffset));
                    }

                    srcOffset += (uint)srcSize;
                    dstOffset += (uint)dstSize;
                }

                // Align between array layers
                dstOffset = TextureConverter.AlignLayerSize(dstOffset, height, depth, blockHeightMip0, 1);
            }

            return output;
        }

        private static uint CalculateTotalPitchLinearSize(uint width, uint height, uint depth,
            uint blkWidth, uint blkHeight, uint blkDepth, uint arrayCount, uint mipCount, uint bpp)
        {
            uint total = 0;

            for (int a = 0; a < arrayCount; a++)
            {
                for (int mip = 0; mip < mipCount; mip++)
                {
                    uint mipWidth = Math.Max(width >> mip, 1);
                    uint mipHeight = Math.Max(height >> mip, 1);
                    uint mipDepth = Math.Max(depth >> mip, 1);

                    total += (uint)CalculatePitchLinearSize(mipWidth, mipHeight, mipDepth,
                        blkWidth, blkHeight, blkDepth, bpp);
                }
            }

            return total;
        }

        public static int CalculatePitchLinearSize(int mip, uint width, uint height, uint depth,
    uint blkWidth, uint blkHeight, uint blkDepth, uint bpp)
        {
            uint mipWidth = Math.Max(width >> mip, 1);
            uint mipHeight = Math.Max(height >> mip, 1);
            uint mipDepth = Math.Max(depth >> mip, 1);
            return CalculatePitchLinearSize(mipWidth, mipHeight, mipDepth, blkWidth, blkHeight, blkDepth, bpp);
        }

        public static int CalculatePitchLinearSize(uint width, uint height, uint depth,
            uint blkWidth, uint blkHeight, uint blkDepth, uint bpp)
        {
            width = TextureConverter.DIV_ROUND_UP(width, blkWidth);
            height = TextureConverter.DIV_ROUND_UP(height, blkHeight);
            depth = TextureConverter.DIV_ROUND_UP(depth, blkDepth);

            uint pitch = width * bpp;
            return (int)(pitch * height);
        }

        private static byte[] SwizzlePitchLinear(uint width, uint height, uint depth,
            uint blkWidth, uint blkHeight, uint blkDepth, uint bpp,
            byte[] data, bool deswizzle)
        {
            width = TextureConverter.DIV_ROUND_UP(width, blkWidth);
            height = TextureConverter.DIV_ROUND_UP(height, blkHeight);
            depth = TextureConverter.DIV_ROUND_UP(depth, blkDepth);

            uint pitch = width * bpp;
            uint surfaceSize = pitch * height;

            byte[] result = new byte[surfaceSize];

            for (uint z = 0; z < depth; z++)
            {
                for (uint y = 0; y < height; y++)
                {
                    for (uint x = 0; x < width; x++)
                    {
                        uint linearPos = (y * width + x) * bpp;
                        uint pitchedPos = y * pitch + x * bpp;

                        if (pitchedPos + bpp > surfaceSize || linearPos + bpp > data.Length)
                            continue;

                        if (deswizzle)
                        {
                            Array.Copy(data, linearPos, result, pitchedPos, bpp);
                        }
                        else
                        {
                            Array.Copy(data, pitchedPos, result, linearPos, bpp);
                        }
                    }
                }
            }

            return result;
        }
    }
}