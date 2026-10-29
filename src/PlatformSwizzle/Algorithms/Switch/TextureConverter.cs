using ImageLibrary.Utils;
using SixLabors.ImageSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace ImageLibrary.PlatformSwizzle.Algorithms.Switch
{
    public class TextureConverter
    {
        public static uint GetSwizzleSurfaceSizeMip(int mip, uint width, uint height, uint depth,
            (uint, uint, uint) blockSizes, uint bytes_per_pixel)
        {
            BlockDimX64 dims = new BlockDimX64() { width = blockSizes.Item1, height = blockSizes.Item2, depth = blockSizes.Item3, };
            var block_height_mip0 = GetBlockHeight(DIV_ROUND_UP(height, (uint)dims.height));

            var mip_width = Math.Max(DIV_ROUND_UP(width >> mip, (uint)dims.width), 1);
            var mip_height = Math.Max(DIV_ROUND_UP(height >> mip, (uint)dims.height), 1);
            var mip_depth = Math.Max(DIV_ROUND_UP(depth >> mip, (uint)dims.depth), 1);

            var mip_block_height = tegra_swizzle_native_x64.MipBlockHeight(mip_height, block_height_mip0);

            var swizzled_size = tegra_swizzle_native_x64.SwizzleSurfaceMipSize(mip_width, mip_height, depth, mip_block_height, bytes_per_pixel);
            return (uint)swizzled_size;
        }

        // Deswizzle a certain slice
        public static byte[] DeswizzleSurfaceSlice(uint width, uint height, uint depth, uint array_count, uint mip_count,
            (uint, uint, uint) blockSizes, uint bpp, uint array_level, uint mip_level, byte[] data, bool is_orin = false, int blockHeightOverride = -1)
        {
            BlockDimX64 dims = new BlockDimX64() { width = blockSizes.Item1, height = blockSizes.Item2, depth = blockSizes.Item3, };

            // Here we just deswizzle full surface and slice out what we need
            var output = Deswizzle(width, height, depth, array_count, mip_count, blockSizes, bpp, data, is_orin, blockHeightOverride).AsSpan();

            var blockHeightMip0 = GetBlockHeight(DIV_ROUND_UP(height, (uint)dims.height));
            if (blockHeightOverride >= 0)
                blockHeightMip0 = (uint)(1 << Math.Max(Math.Min(blockHeightOverride, 5), 0));

            uint offset = 0;
            for (int a = 0; a < array_count; a++)
            {
                for (int mip = 0; mip < mip_count; mip++)
                {
                    var mip_width = Math.Max(width >> mip, 1);
                    var mip_height = Math.Max(height >> mip, 1);
                    var mip_depth = Math.Max(depth >> mip, 1);
                    var dst_size = GetDeswizzleSurfaceSize(mip_width, mip_height, mip_depth, 1, 1, dims, bpp);

                    if (a == array_level && mip == mip_level)
                        return output.Slice((int)offset, (int)dst_size).ToArray();

                    offset += dst_size;
                }
            }
            return null;
        }

        // To swizzle a certain slice
        public static void SwizzleSliceSurfaces(List<GenericTextureBase.ImportSlice> surfaces,
            Memory<byte> data, uint width, uint height, uint depth, uint array_count, uint mip_count,
            (uint, uint, uint) blockSizes, uint bpp, bool is_orin = false)
        {
            BlockDimX64 dims = new BlockDimX64() { width = blockSizes.Item1, height = blockSizes.Item2, depth = blockSizes.Item3, };
            var blockHeightMip0 = GetBlockHeight(DIV_ROUND_UP(height, (uint)dims.height));
            var size2 = GetSwizzleSurfaceSize(width, height, depth, array_count, mip_count, dims, blockHeightMip0, bpp);

            uint offset = 0;
            for (int a = 0; a < array_count; a++)
            {
                for (int mip = 0; mip < mip_count; mip++)
                {
                    var mip_width = Math.Max(width >> mip, 1);
                    var mip_height = Math.Max(height >> mip, 1);
                    var mip_depth = Math.Max(depth >> mip, 1);
                    var mip_block_height = tegra_swizzle_native_x64.MipBlockHeight(mip_height, blockHeightMip0);
                    var size = GetSwizzleSurfaceSizeMip(mip, width, height, depth, ((uint)dims.width, (uint)dims.height, (uint)dims.depth), bpp);

                    var surface = surfaces.FirstOrDefault(x => x.Array == a && x.Mip == mip);

                    if (surface != null)
                    {
                        var output = new byte[size];
                        SwizzleSurface(mip_width, mip_height, 1, 1, 1, bpp, surface.Encoded,
                            dims, (uint)mip_block_height, output, is_orin);

                        var slice = data.Slice((int)offset, (int)size);
                        output.AsMemory().CopyTo(slice);
                    }

                    offset += (uint)size;
                }
                //align between layers
                offset = AlignLayerSize(offset, height, depth, blockHeightMip0, 1);
            }
        }

        // Deswizzle all slices
        public static byte[] Deswizzle(uint width, uint height, uint depth, uint array_count, uint mip_count,
            (uint, uint, uint) blockSizes, uint bpp, byte[] data, bool is_orin = false, int blockHeightOverride = -1)
        {
            BlockDimX64 dims = new BlockDimX64() { width = blockSizes.Item1, height = blockSizes.Item2, depth = blockSizes.Item3, };
            var blockHeightMip0 = GetBlockHeight(DIV_ROUND_UP(height, (uint)dims.height));
            if (blockHeightOverride >= 0)
                blockHeightMip0 = (uint)(1 << Math.Max(Math.Min(blockHeightOverride, 5), 0));

            var sw_size = GetSwizzleSurfaceSize(width, height, depth, array_count, mip_count, dims, blockHeightMip0, bpp);
            var size = GetDeswizzleSurfaceSize(width, height, depth, array_count, mip_count, dims, bpp);

            //Expand data to the expected swizzle data if necessary
            //A few cases like .txtg does this when no alignment is used
            if ((int)sw_size != data.Length)
                Debug.WriteLine($"[Switch deswizzle] expected {sw_size} swizzled bytes for block height {blockHeightMip0} but data is {data.Length} bytes. Wrong block height?");

            if ((int)sw_size > data.Length)
            {
                var expanded = new byte[sw_size];
                Array.Copy(data, 0, expanded, 0, data.Length);
                data = expanded;
            }
            var output = new byte[size];
            DeswizzleSurface(width, height, depth, array_count, mip_count, bpp, data, dims, blockHeightMip0, output, is_orin);

            return output;
        }

        // Swizzle all slices
        public static byte[] Swizzle(uint width, uint height, uint depth, uint array_count, uint mip_count,
            (uint, uint, uint) blockSizes, uint bpp, byte[] data, bool is_orin = false)
        {
            BlockDimX64 dims = new BlockDimX64() { width = blockSizes.Item1, height = blockSizes.Item2, depth = blockSizes.Item3, };
            var blockHeightMip0 = GetBlockHeight(DIV_ROUND_UP(height, (uint)dims.height));
            var size = GetSwizzleSurfaceSize(width, height, depth, array_count, mip_count, dims, blockHeightMip0, bpp);
            var desize = GetDeswizzleSurfaceSize(width, height, depth, array_count, mip_count, dims, bpp);

            if (desize > data.Length)
                throw new Exception($"Deswizzle size too small {data.Length}. Expected {desize}");

            var output = new byte[size];
            SwizzleSurface(width, height, depth, array_count, mip_count, bpp, data, dims, blockHeightMip0, output, is_orin);
            return output;
        }

        private static unsafe void DeswizzleSurface(uint width, uint height, uint depth, uint array_count, uint mip_count,
            uint bpp, byte[] data, BlockDimX64 blockDims, uint blockHeightMip0, byte[] output, bool is_orin)
        {
            fixed (byte* dataPtr = data)
            {
                fixed (byte* outputPtr = output)
                {
                    if (Environment.Is64BitProcess)
                        tegra_swizzle_native_x64.DeswizzleSurface(width, height, depth, dataPtr,
                            (ulong)data.Length, outputPtr, (ulong)output.Length, blockDims, blockHeightMip0, bpp, mip_count, array_count, is_orin);
                    else
                        tegra_swizzle_native_x86.DeswizzleSurface(width, height, depth, dataPtr,
                            (uint)data.Length, outputPtr, (uint)output.Length, blockDims, blockHeightMip0, bpp, mip_count, array_count, is_orin);
                }
            }
        }

        private static unsafe void SwizzleSurface(uint width, uint height, uint depth, uint array_count, uint mip_count,
            uint bpp, byte[] data, BlockDimX64 blockDims, uint blockHeightMip0, byte[] output, bool is_orin)
        {
            fixed (byte* dataPtr = data)
            {
                fixed (byte* outputPtr = output)
                {
                    if (Environment.Is64BitProcess)
                        tegra_swizzle_native_x64.SwizzleSurface(width, height, depth, dataPtr,
                            (ulong)data.Length, outputPtr, (ulong)output.Length, blockDims, blockHeightMip0, bpp, mip_count, array_count, is_orin);
                    else
                        tegra_swizzle_native_x86.SwizzleSurface(width, height, depth, dataPtr,
                            (uint)data.Length, outputPtr, (uint)output.Length, blockDims, blockHeightMip0, bpp, mip_count, array_count, is_orin);
                }
            }
        }

        public static uint GetBlockHeight(uint heightInBytes)
        {
            if (Environment.Is64BitProcess)
                return (uint)tegra_swizzle_native_x64.BlockHeightMip0(heightInBytes);
            else
                return tegra_swizzle_native_x86.BlockHeightMip0(heightInBytes);
        }

        public static uint GetBlockHeightLog(uint height, uint blkHeight)
        {
            var blockHeight = GetBlockHeight(DIV_ROUND_UP(height, (uint)blkHeight));
            return (uint)Convert.ToString(blockHeight, 2).Length - 1;
        }

        public static uint GetDeswizzleSurfaceSize(uint width, uint height, uint depth,
            uint array_count, uint mip_count, BlockDimX64 blockDims, uint bpp)
        {
            if (Environment.Is64BitProcess)
                return (uint)tegra_swizzle_native_x64.DeswizzleSurfaceSize(width, height, depth, blockDims, bpp, mip_count, array_count);
            else
                return (uint)tegra_swizzle_native_x86.DeswizzleSurfaceSize(width, height, depth, blockDims, bpp, mip_count, array_count);
        }

        public static uint GetSwizzleSurfaceSize(uint width, uint height, uint depth,
            uint array_count, uint mip_count, BlockDimX64 blockDims, uint blockHeightMip0, uint bpp)
        {
            if (Environment.Is64BitProcess)
                return (uint)tegra_swizzle_native_x64.SwizzleSurfaceSize(width, height, depth, blockDims, blockHeightMip0, bpp, mip_count, array_count);
            else
                return (uint)tegra_swizzle_native_x86.SwizzleSurfaceSize(width, height, depth, blockDims, blockHeightMip0, bpp, mip_count, array_count);
        }

        public static uint DIV_ROUND_UP(uint n, uint d)
        {
            return (n + d - 1) / d;
        }
        public static uint ROUND_UP(uint x, uint y)
        {
            return ((x - 1) | (y - 1)) + 1;
        }

        public static uint AlignLayerSize(
                uint size,
                uint height,
                uint depth,
                uint block_height_mip0,
                uint gobDepth)
        {
            var gobBlocksInTileX = 1;
            var gobHeight = block_height_mip0;

            const int GOB_SIZE_IN_PIXELS = 512;

            if (gobBlocksInTileX < 2)
            {
                while (height <= gobHeight / 2 * 8 && gobHeight > 1)
                {
                    gobHeight /= 2;
                }

                while (depth <= gobDepth / 2 && gobDepth > 1)
                {
                    gobDepth /= 2;
                }

                var blockOfGobsSize = gobHeight * gobDepth * GOB_SIZE_IN_PIXELS;
                var sizeInBlockOfGobs = size / blockOfGobsSize;

                if (size != sizeInBlockOfGobs * blockOfGobsSize)
                {
                    size = (sizeInBlockOfGobs + 1) * blockOfGobsSize;
                }
            }
            else
            {
                var alignment = gobBlocksInTileX * GOB_SIZE_IN_PIXELS * gobHeight * gobDepth;
                size = (uint)(size + (alignment - 1) & ~(alignment - 1));
            }
            return size;
        }
    }
}
