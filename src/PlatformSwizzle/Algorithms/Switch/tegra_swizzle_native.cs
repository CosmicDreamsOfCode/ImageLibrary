using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace ImageLibrary.PlatformSwizzle.Algorithms.Switch
{
    // Must match the Rust BlockDim in tegra_swizzle (three u32). Older tegra_swizzle builds used
    // usize here, which is why this used to be ulong on 64 bit Windows.
    [StructLayout(LayoutKind.Sequential)]
    public struct BlockDimX64
    {
        public uint width;
        public uint height;
        public uint depth;
    }

    public class tegra_swizzle_native_x64
    {
#if WINDOWS
        private const string DllName_x64 = "tegra_swizzle_x64";
#else
        private const string DllName_x64 = "libtegra_swizzle";
#endif

        // Parameter types mirror the Rust FFI: u32 -> uint, usize -> ulong, bool -> 1 byte.
        [DllImport(DllName_x64, EntryPoint = "swizzle_surface")]
        public static unsafe extern void SwizzleSurface(uint width, uint height, uint depth,
            byte* source, ulong sourceLength,
            byte* destination, ulong destinationLength,
            BlockDimX64 blockDim, uint blockHeightMip0, uint bytesPerPixel,
            uint mipmapCount, uint arrayCount, [MarshalAs(UnmanagedType.U1)] bool is_orin);


        [DllImport(DllName_x64, EntryPoint = "deswizzle_surface")]
        public static unsafe extern void DeswizzleSurface(uint width, uint height, uint depth,
                byte* source, ulong sourceLength,
                byte* destination, ulong destinationLength,
                BlockDimX64 blockDim, uint blockHeightMip0, uint bytesPerPixel,
                uint mipmapCount, uint arrayCount, [MarshalAs(UnmanagedType.U1)] bool is_orin);

        [DllImport(DllName_x64, EntryPoint = "deswizzled_surface_size")]
        public static unsafe extern ulong DeswizzleSurfaceSize(uint width, uint height, uint depth,
            BlockDimX64 blockDim, uint bytesPerPixel, uint mipmapCount, uint arrayCount);

        [DllImport(DllName_x64, EntryPoint = "deswizzled_mip_size")]
        public static unsafe extern ulong DewizzleSurfaceMipSize(uint width, uint height, uint depth,
            uint bytesPerPixel);


        [DllImport(DllName_x64, EntryPoint = "swizzled_surface_size")]
        public static unsafe extern ulong SwizzleSurfaceSize(uint width, uint height, uint depth,
            BlockDimX64 blockDim, uint blockHeightMip0, uint bytesPerPixel, uint mipmapCount, uint arrayCount);


        [DllImport(DllName_x64, EntryPoint = "swizzled_mip_size")]
        public static unsafe extern ulong SwizzleSurfaceMipSize(uint width, uint height, uint depth,
                uint blockHeightMip0, uint bytesPerPixel);



        [DllImport(DllName_x64, EntryPoint = "deswizzle_block_linear")]
        public static unsafe extern void DeswizzleBlockLinear(uint width, uint height, uint depth, byte* source, ulong sourceLength,
             byte* destination, ulong destinationLength, uint blockHeight, uint bytesPerPixel, [MarshalAs(UnmanagedType.U1)] bool is_orin);


        [DllImport(DllName_x64, EntryPoint = "block_height_mip0")]
        public static extern uint BlockHeightMip0(uint height);

        [DllImport(DllName_x64, EntryPoint = "mip_block_height")]
        public static extern uint MipBlockHeight(uint mipHeight, uint blockHeightMip0);
    }

    public class tegra_swizzle_native_x86
    {
#if WINDOWS
        private const string DllName_x86 = "tegra_swizzle_x86";
#else
        private const string DllName_x86 = "libtegra_swizzle";
#endif

        [DllImport(DllName_x86, EntryPoint = "swizzle_surface")]
        public static unsafe extern void SwizzleSurface(uint width, uint height, uint depth,
            byte* source, uint sourceLength,
            byte* destination, uint destinationLength,
            BlockDimX64 blockDim, uint blockHeightMip0, uint bytesPerPixel,
            uint mipmapCount, uint arrayCount, [MarshalAs(UnmanagedType.U1)] bool is_orin);

        [DllImport(DllName_x86, EntryPoint = "deswizzle_surface")]
        public static unsafe extern void DeswizzleSurface(uint width, uint height, uint depth,
            byte* source, uint sourceLength,
            byte* destination, uint destinationLength,
            BlockDimX64 blockDim, uint blockHeightMip0, uint bytesPerPixel,
            uint mipmapCount, uint arrayCount, [MarshalAs(UnmanagedType.U1)] bool is_orin);

        [DllImport(DllName_x86, EntryPoint = "deswizzled_surface_size")]
        public static unsafe extern ulong DeswizzleSurfaceSize(uint width, uint height, uint depth,
            BlockDimX64 blockDim, uint bytesPerPixel, uint mipmapCount, uint arrayCount);

        [DllImport(DllName_x86, EntryPoint = "deswizzled_mip_size")]
        public static unsafe extern ulong DewizzleSurfaceMipSize(uint width, uint height, uint depth,
                uint bytesPerPixel);

        [DllImport(DllName_x86, EntryPoint = "swizzled_surface_size")]
        public static unsafe extern ulong SwizzleSurfaceSize(uint width, uint height, uint depth,
            BlockDimX64 blockDim, uint blockHeightMip0, uint bytesPerPixel, uint mipmapCount, uint arrayCount);

        [DllImport(DllName_x86, EntryPoint = "swizzled_mip_size")]
        public static unsafe extern ulong SwizzleSurfaceMipSize(uint width, uint height, uint depth,
                uint blockHeightMip0, ulong bytesPerPixel);

        [DllImport(DllName_x86, EntryPoint = "deswizzle_block_linear")]
        public static unsafe extern void DeswizzleBlockLinear(uint width, uint height, uint depth, byte* source, uint sourceLength,
             byte* destination, uint destinationLength, uint blockHeight, uint bytesPerPixel, [MarshalAs(UnmanagedType.U1)] bool is_orin);

        [DllImport(DllName_x86, EntryPoint = "mip_block_height")]
        public static extern uint MipBlockHeight(uint mipHeight, uint blockHeightMip0);

        [DllImport(DllName_x86, EntryPoint = "block_height_mip0")]
        public static extern uint BlockHeightMip0(uint height);
    }
}
