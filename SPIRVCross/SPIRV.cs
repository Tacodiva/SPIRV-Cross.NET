using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace SPIRVCross
{
    public static unsafe partial class SPIRV
    {



        internal static IntPtr LoadNativeLibrary()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return LibraryLoader.LoadLocalLibrary("spirv-cross-c-shared.dll");
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return LibraryLoader.LoadLocalLibrary("libspirv-cross-c-shared.so");
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return LibraryLoader.LoadLocalLibrary("libspirv-cross-c-shared.dylib");
            }
            else
            {
                throw new PlatformNotSupportedException();
            }
        }
    }



    internal static class LibraryLoader
    {
        static LibraryLoader()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                Extension = ".dll";
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                Extension = ".dylib";
            else
                Extension = ".so";
        }

        public static string Extension { get; }

        public static IntPtr LoadLocalLibrary(string libraryName)
        {
            if (!libraryName.EndsWith(Extension, StringComparison.OrdinalIgnoreCase))
                libraryName += Extension;


            var osPlatform = GetOSPlatform();
            var architecture = GetArchitecture();

            var libraryPath = GetNativeAssemblyPath(osPlatform, architecture, libraryName);

            static string GetOSPlatform()
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    return "win";
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                    return "linux";
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    return "osx";

                throw new ArgumentException("Unsupported OS platform.");
            }

            static string GetArchitecture()
            {
                switch (RuntimeInformation.ProcessArchitecture)
                {
                    case Architecture.X86: return "x86";
                    case Architecture.X64: return "x64";
                    case Architecture.Arm: return "arm";
                    case Architecture.Arm64: return "arm64";
                }

                throw new ArgumentException("Unsupported architecture.");
            }

            static string GetNativeAssemblyPath(string osPlatform, string architecture, string libraryName)
            {
                var assemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

                var paths = new[]
                {
                    Path.Combine(assemblyLocation, libraryName),
                    Path.Combine(assemblyLocation, "runtimes", osPlatform, "native", libraryName),
                    Path.Combine(assemblyLocation, "runtimes", $"{osPlatform}-{architecture}", "native", libraryName),
                };

                foreach (var path in paths)
                {
                    if (File.Exists(path))
                        return path;
                }

                return libraryName;
            }

            IntPtr handle;
            handle = NativeLibrary.Load(libraryPath);

            if (handle == IntPtr.Zero)
                throw new DllNotFoundException($"Unable to load library '{libraryName}'.");

            return handle;
        }

        public static T LoadFunction<T>(IntPtr library, string name)
        {
            IntPtr symbol = NativeLibrary.GetExport(library, name);

            if (symbol == IntPtr.Zero)
                throw new EntryPointNotFoundException($"Unable to load symbol '{name}'.");

            return Marshal.GetDelegateForFunctionPointer<T>(symbol);
        }
    }
}
