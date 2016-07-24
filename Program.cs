using System;
using System.Text;
using System.IO;
using System.Runtime.InteropServices;
using System.ComponentModel;

namespace WinFspSharp
{
    class Program
    {
        const int FSP_FSCTL_TRANSACT_REQ_SIZEMAX = (4096 - 64); /* 64: size for internal request header */
        const int FSP_FSCTL_TRANSACT_RSP_SIZEMAX = (4096 - 64); /* symmetry! */
        const int FSP_FSCTL_TRANSACT_BATCH_BUFFER_SIZEMIN = 16384;
        const int FSP_FSCTL_TRANSACT_BUFFER_SIZEMIN = FSP_FSCTL_TRANSACT_REQ_SIZEMAX;


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

        internal const uint DDD_RAW_TARGET_PATH = 0x00000001;
        internal const uint DDD_REMOVE_DEFINITION = 0x00000002;
        internal const uint DDD_EXACT_MATCH_ON_REMOVE = 0x00000004;
        internal const uint DDD_NO_BROADCAST_SYSTEM = 0x00000008;
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern bool DefineDosDeviceW(
            uint dwFlags,
            [MarshalAs(UnmanagedType.LPWStr)] string lpDeviceName,
            [MarshalAs(UnmanagedType.LPWStr)] string lpTargetPath);

        [DllImport("kernel32.dll")]
        static extern void RtlZeroMemory(IntPtr dst, int length);

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
                    @"\\?\GLOBALROOT\Device\WinFsp.Disk\VolumeParams=",
                    volumeParams);

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

                Console.WriteLine("Unknown error found. {0}", new Win32Exception(error).Message);
                return;
            }

            string volumeName = string.Empty;

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

                volumeName = Marshal.PtrToStringUni(volumeNameBuf, (int)(len / sizeof(char)));

                Console.WriteLine("VolumeName is: {0}", volumeName);
            }
            finally
            {
                Marshal.FreeHGlobal(volumeNameBuf);
            }

            if (DefineDosDeviceW(DDD_RAW_TARGET_PATH, "z:", volumeName))
            {
                Console.WriteLine("Correctly mounted!");
            }

            DispatcherThread(volumeHandle);

            DefineDosDeviceW(
                DDD_RAW_TARGET_PATH | DDD_REMOVE_DEFINITION | DDD_EXACT_MATCH_ON_REMOVE,
                "z:", volumeName);
        }

        /*FSP_API NTSTATUS FspFileSystemOpQueryVolumeInformation(
         * FSP_FILE_SYSTEM* FileSystem,
            FSP_FSCTL_TRANSACT_REQ* Request, FSP_FSCTL_TRANSACT_RSP* Response)
        {
            NTSTATUS Result;
            FSP_FSCTL_VOLUME_INFO VolumeInfo;

            if (0 == FileSystem->Interface->GetVolumeInfo)
                return STATUS_INVALID_DEVICE_REQUEST;

            memset(&VolumeInfo, 0, sizeof VolumeInfo);
            Result = FileSystem->Interface->GetVolumeInfo(FileSystem, Request, &VolumeInfo);
            if (!NT_SUCCESS(Result))
                return Result;

            memcpy(&Response->Rsp.QueryVolumeInformation.VolumeInfo, &VolumeInfo, sizeof VolumeInfo);
            return STATUS_SUCCESS;
        }*/

        unsafe static void DispatcherThread(IntPtr volumeHandle)
        {
            //FSP_FILE_SYSTEM* FileSystem = FileSystem0;
            long Result;
            //SIZE_T RequestSize, ResponseSize;
            //FSP_FSCTL_TRANSACT_REQ* Request = 0;
            //FSP_FSCTL_TRANSACT_RSP* Response = 0;
            //HANDLE DispatcherThread = 0;

            IntPtr Request = Marshal.AllocHGlobal(FSP_FSCTL_TRANSACT_BUFFER_SIZEMIN);
            IntPtr responsePtr = Marshal.AllocHGlobal(FSP_FSCTL_TRANSACT_RSP_SIZEMAX);

            RtlZeroMemory(responsePtr, FSP_FSCTL_TRANSACT_RSP_SIZEMAX);

            try
            {
                Response response = new Response();
                
                while (true)
                {
                    uint RequestSize;

                    if (!DeviceIoControl(volumeHandle,
                        FSP_FSCTL_TRANSACT(),
                        responsePtr, response.Size, Request, FSP_FSCTL_TRANSACT_BUFFER_SIZEMIN, out RequestSize, IntPtr.Zero))
                    {
                        Console.WriteLine("Error happened");
                        return;
                    }

                    RtlZeroMemory(responsePtr, FSP_FSCTL_TRANSACT_RSP_SIZEMAX);
                    if (0 == RequestSize)
                        continue;


                    RequestCommon requestCommon = (RequestCommon)Marshal.PtrToStructure(Request, typeof(RequestCommon));

                    response.Size = 112;
                    /*Response->Size = sizeof *Response;*/
                    response.Kind = requestCommon.Kind;
                    response.Hint = requestCommon.Hint;

                    if (requestCommon.Kind != RequestKind.QueryVolumeInformationKind)
                    {
                        response.IoStatus.Status = STATUS_INVALID_DEVICE_REQUEST;

                        Console.WriteLine("{0} received", requestCommon.Kind);
                    }
                    else
                    {
                        response.QueryVolumeInformation.FreeSize = 100 * 1024 * 1024;
                        response.QueryVolumeInformation.TotalSize = 512 * 1024 * 1024;

                        char[] nameArray = "PlasticWinFsp".ToCharArray();

                        int i = 0;
                        foreach (char c in nameArray)
                        {
                            response.QueryVolumeInformation.VolumeLabel[i] = c;
                        }

                        //Marshal.Copy(nameArray, 0, (IntPtr) response.SetVolumeInformation.VolumeLabel, nameArray.Length);

                        response.QueryVolumeInformation.VolumeLabelLength = (ushort) nameArray.Length;
                    }

                    /*if (FspFsctlTransactKindCount > Request->Kind && 0 != FileSystem->Operations[Request->Kind])
                    {
                        responsePtr->IoStatus.Status =
                            FspFileSystemEnterOperation(FileSystem, Request, responsePtr);
                        if (NT_SUCCESS(responsePtr->IoStatus.Status))
                        {
                            responsePtr->IoStatus.Status =
                                FileSystem->Operations[Request->Kind](FileSystem, Request, responsePtr);
                            FspFileSystemLeaveOperation(FileSystem, Request, responsePtr);
                        }
                    }*/

                    Marshal.StructureToPtr(response, responsePtr, false);

                    RtlZeroMemory(responsePtr + 112, FSP_FSCTL_TRANSACT_RSP_SIZEMAX - 112);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(responsePtr);
                Marshal.FreeHGlobal(Request);

                //FspFileSystemSetDispatcherResult(FileSystem, Result);

                //FspFsctlStop(FileSystem->VolumeHandle);
            }
        }

        static uint CTL_CODE(uint DeviceType, uint Function, uint Method, uint Access)
        {
            return ((DeviceType) << 16) | ((Access) << 14) | ((Function) << 2) | (Method);
        }

        static uint FSP_FSCTL_VOLUME_NAME()
        {
            return (FILE_DEVICE_FILE_SYSTEM << 16) | (FILE_ANY_ACCESS << 14) | ((0x800 + (int)'N') << 2) | METHOD_BUFFERED;
        }

        static uint FSP_FSCTL_TRANSACT()
        {
            return CTL_CODE(FILE_DEVICE_FILE_SYSTEM, 0x800 + (int) 'T', METHOD_BUFFERED, FILE_ANY_ACCESS);
        }

        const int FILE_DEVICE_FILE_SYSTEM = 0x00000009;
        const int METHOD_BUFFERED = 0;
        const int FILE_ANY_ACCESS = 0;

        // #define FILE_DEVICE_FILE_SYSTEM         0x00000009 // winioctl.h
        // #define METHOD_BUFFERED                 0
        // #define FILE_ANY_ACCESS                 0

        const uint STATUS_INVALID_DEVICE_REQUEST = 0xC0000010;
    }
}
