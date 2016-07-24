using System;
using System.Text;
using System.IO;
using System.Runtime.InteropServices;
using System.ComponentModel;

namespace WinFspSharp
{
    class Program
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern IntPtr CreateFileW(
             [MarshalAs(UnmanagedType.LPWStr)] string filename,
             [MarshalAs(UnmanagedType.U4)] FileAccess access,
             [MarshalAs(UnmanagedType.U4)] FileShare share,
             IntPtr securityAttributes,
             [MarshalAs(UnmanagedType.U4)] FileMode creationDisposition,
             [MarshalAs(UnmanagedType.U4)] uint flagsAndAttributes,
             IntPtr templateFile);

        [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DeviceIoControl(
            IntPtr hDevice,
            UInt32 dwIoControlCode,
            IntPtr lpInBuffer,
            Int32 nInBufferSize,
            IntPtr lpOutBuffer,
            Int32 nOutBufferSize,
            out uint lpBytesReturned,
            IntPtr lpOverlapped);

        static string EncodeVolumeParams(
            string devicePath,
            VolumeParams volumeParams)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append(devicePath);

            byte[] serialized = volumeParams.Serialize();

            foreach (byte b in serialized)
            {
                // WCHAR Value = 0xF000 | *VolumeParamsPtr;
                char value = Convert.ToChar(0xF000 | b);
                builder.Append(value);
            }

            builder.Append('\0');

            return builder.ToString();
        }

        static long FspNtStatusFromWin32(int error)
        {
                    /* use FACILITY_NTWIN32 if able, else STATUS_ACCESS_DENIED */
          return 0xffff >= error ? (0x80070000 | error) : 0xC0000022L /*STATUS_ACCESS_DENIED*/;
        }

        static void Main(string[] args)
        {
            VolumeParams volumeParams = new VolumeParams();

            volumeParams.SectorSize = 512;
            volumeParams.SectorsPerAllocationUnit = 1;
            volumeParams.VolumeCreationTime = (ulong)DateTime.Now.ToFileTimeUtc();
            volumeParams.VolumeSerialNumber = (uint)(DateTime.Now.ToFileTimeUtc() / (10000 * 1000));
            volumeParams.FileInfoTimeout = 0xFFFFFFFF;  // Infinite timeout
            volumeParams.CaseSensitiveSearch = 1;
            volumeParams.CasePreservedNames = 1;
            volumeParams.UnicodeOnDisk = 1;
            volumeParams.PersistentAcls = 1;

            string devicePath =
                EncodeVolumeParams(
                    @"\\\\?\\GLOBALROOT\\Device\\WinFsp.Disk\\VolumeParams=",
                    volumeParams);

            // Sample debugging C code:
            //            0x000000465bfff490 L"\\\\?\\GLOBALROOT\\Device\\WinFsp.Disk\\VolumeParams="

            IntPtr volumeHandle = CreateFileW(
                devicePath, 0, FileShare.ReadWrite, IntPtr.Zero, FileMode.Open, 0x40000000 /*FILE_FLAG_OVERLAPPED*/, IntPtr.Zero);

            if (volumeHandle.ToInt32() == -1)
            {
                int error = Marshal.GetLastWin32Error();

                long fspError = FspNtStatusFromWin32(error);

                if (fspError == 0xC000003AL /*STATUS_OBJECT_PATH_NOT_FOUND*/)
                {
                    Console.WriteLine("Path not found");
                    return;
                }

                if (fspError == 0xC0000034L /*STATUS_OBJECT_NAME_NOT_FOUND*/)
                {
                    Console.WriteLine("Object name not found");
                    return;
                }

                Console.WriteLine("Unknown error found. {0}",new Win32Exception(error).Message);
                return;
            }


            /* if (INVALID_HANDLE_VALUE == volumeHandle)
            {
                Result = FspNtStatusFromWin32(GetLastError());
                if (STATUS_OBJECT_PATH_NOT_FOUND == Result ||
                    STATUS_OBJECT_NAME_NOT_FOUND == Result)
                    Result = STATUS_NO_SUCH_DEVICE;
                goto exit;
            }*/

            IntPtr volumeNameBuf = Marshal.AllocHGlobal(256);
            uint len;
            try
            {
                if (!DeviceIoControl(volumeHandle, FSP_FSCTL_VOLUME_NAME(),
                    IntPtr.Zero, 0,
                    volumeNameBuf, 256,
                    out len, IntPtr.Zero))
                {
                    //Result = FspNtStatusFromWin32(GetLastError());
                    //goto exit;

                    Console.WriteLine("Something went wront with the first DeviceIoControl");
                    return;
                }

                string volumeName = Marshal.PtrToStringUni(volumeNameBuf, (int)len);

                Console.WriteLine("VolumeName is: {0}", volumeName);
            }
            finally
            {
                Marshal.FreeHGlobal(volumeNameBuf);
            }

            // and this is the expected output for VolumeName
            // VolumeName: 0x000001a01eea3d70 L"\\Device\\Volume{bfdf588f-517e-11e6-b318-000c296fed13}"
            // VolumeNameSize = 256


            // if (DefineDosDeviceW(DDD_RAW_TARGET_PATH, MountPoint, FileSystem->VolumeName))
            // #define DDD_RAW_TARGET_PATH         0x00000001 // from WinBase.h
            // MountPoint = z:
            // FileSystem.VolumeName = 0x000001a01eea3d70 L"\\Device\\Volume{bfdf588f-517e-11e6-b318-000c296fed13}"
        }

        static uint FSP_FSCTL_VOLUME_NAME()
        {
            return (FILE_DEVICE_FILE_SYSTEM << 16) | (FILE_ANY_ACCESS << 14) | ((0x800 + (int)'N') << 2) | METHOD_BUFFERED;
        }

        const int FILE_DEVICE_FILE_SYSTEM = 0x00000009;
        const int METHOD_BUFFERED = 0;
        const int FILE_ANY_ACCESS = 0;

        // #define FILE_DEVICE_FILE_SYSTEM         0x00000009 // winioctl.h
        // #define METHOD_BUFFERED                 0
        // #define FILE_ANY_ACCESS                 0
    }
}
