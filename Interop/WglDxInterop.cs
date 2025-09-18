using System;
using System.Runtime.InteropServices;
using OpenTK.Graphics.OpenGL4;

namespace OpenGLOpt.Interop
{
    /// <summary>
    /// WGL_NV_DX_interop2 extension wrapper for OpenGL-DirectX texture sharing
    /// </summary>
    public static class WglDxInterop
    {
        private const string OpenGL32 = "opengl32.dll";

        // WGL_NV_DX_interop2 constants
        public const int WGL_ACCESS_READ_ONLY_NV = 0x00000000;
        public const int WGL_ACCESS_READ_WRITE_NV = 0x00000001;
        public const int WGL_ACCESS_WRITE_DISCARD_NV = 0x00000002;

        // Function pointers
        private static wglDXOpenDeviceNVDelegate _wglDXOpenDeviceNV;
        private static wglDXCloseDeviceNVDelegate _wglDXCloseDeviceNV;
        private static wglDXRegisterObjectNVDelegate _wglDXRegisterObjectNV;
        private static wglDXUnregisterObjectNVDelegate _wglDXUnregisterObjectNV;
        private static wglDXLockObjectsNVDelegate _wglDXLockObjectsNV;
        private static wglDXUnlockObjectsNVDelegate _wglDXUnlockObjectsNV;

        // Delegates
        private delegate IntPtr wglDXOpenDeviceNVDelegate(IntPtr dxDevice);
        private delegate bool wglDXCloseDeviceNVDelegate(IntPtr hDevice);
        private delegate IntPtr wglDXRegisterObjectNVDelegate(IntPtr hDevice, IntPtr dxObject, uint name, uint type, uint access);
        private delegate bool wglDXUnregisterObjectNVDelegate(IntPtr hDevice, IntPtr hObject);
        private delegate bool wglDXLockObjectsNVDelegate(IntPtr hDevice, int count, IntPtr[] hObjects);
        private delegate bool wglDXUnlockObjectsNVDelegate(IntPtr hDevice, int count, IntPtr[] hObjects);

        [DllImport(OpenGL32, EntryPoint = "wglGetProcAddress")]
        private static extern IntPtr wglGetProcAddress(string lpszProc);

        private static bool _initialized = false;

        public static void Initialize()
        {
            if (_initialized) return;

            _wglDXOpenDeviceNV = GetDelegate<wglDXOpenDeviceNVDelegate>("wglDXOpenDeviceNV");
            _wglDXCloseDeviceNV = GetDelegate<wglDXCloseDeviceNVDelegate>("wglDXCloseDeviceNV");
            _wglDXRegisterObjectNV = GetDelegate<wglDXRegisterObjectNVDelegate>("wglDXRegisterObjectNV");
            _wglDXUnregisterObjectNV = GetDelegate<wglDXUnregisterObjectNVDelegate>("wglDXUnregisterObjectNV");
            _wglDXLockObjectsNV = GetDelegate<wglDXLockObjectsNVDelegate>("wglDXLockObjectsNV");
            _wglDXUnlockObjectsNV = GetDelegate<wglDXUnlockObjectsNVDelegate>("wglDXUnlockObjectsNV");

            _initialized = true;
        }

        private static T GetDelegate<T>(string name) where T : class
        {
            IntPtr proc = wglGetProcAddress(name);
            if (proc == IntPtr.Zero)
                throw new NotSupportedException($"Extension function {name} not supported");

            return Marshal.GetDelegateForFunctionPointer(proc, typeof(T)) as T;
        }

        public static IntPtr DXOpenDevice(IntPtr dxDevice)
        {
            return _wglDXOpenDeviceNV(dxDevice);
        }

        public static bool DXCloseDevice(IntPtr hDevice)
        {
            return _wglDXCloseDeviceNV(hDevice);
        }

        public static IntPtr DXRegisterObject(IntPtr hDevice, IntPtr dxObject, uint glName, TextureTarget target, int access)
        {
            return _wglDXRegisterObjectNV(hDevice, dxObject, glName, (uint)target, (uint)access);
        }

        public static bool DXUnregisterObject(IntPtr hDevice, IntPtr hObject)
        {
            return _wglDXUnregisterObjectNV(hDevice, hObject);
        }

        public static bool DXLockObjects(IntPtr hDevice, IntPtr[] hObjects)
        {
            return _wglDXLockObjectsNV(hDevice, hObjects.Length, hObjects);
        }

        public static bool DXUnlockObjects(IntPtr hDevice, IntPtr[] hObjects)
        {
            return _wglDXUnlockObjectsNV(hDevice, hObjects.Length, hObjects);
        }

        public static bool IsSupported()
        {
            try
            {
                // Ensure OpenGL bindings are loaded by testing a basic call
                try
                {
                    string version = GL.GetString(StringName.Version);
                    if (string.IsNullOrEmpty(version))
                    {
                        Console.WriteLine("OpenGL context not available or bindings not loaded.");
                        return false;
                    }
                    Console.WriteLine($"OpenGL Version: {version}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"OpenGL bindings not loaded: {ex.Message}");
                    return false;
                }

                // Modern way to check OpenGL extensions (OpenGL 3.0+)
                GL.GetInteger(GetPName.NumExtensions, out int numExtensions);
                bool hasInterop = false;
                
                Console.WriteLine($"Number of OpenGL extensions: {numExtensions}");
                
                for (int i = 0; i < numExtensions; i++)
                {
                    string extension = GL.GetString(StringNameIndexed.Extensions, i);
                    if (extension != null && (extension.Contains("WGL_NV_DX_interop2") || extension.Contains("WGL_NV_DX_interop")))
                    {
                        hasInterop = true;
                        Console.WriteLine($"Found WGL DX Interop extension: {extension}");
                        break;
                    }
                }
                
                Console.WriteLine($"WGL DX Interop supported: {hasInterop}");
                return hasInterop;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error checking WGL DX Interop support: {ex.Message}");
                return false;
            }
        }
    }
}