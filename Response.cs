using System;
using System.Runtime.InteropServices;

namespace WinFspSharp
{
    [StructLayout(LayoutKind.Sequential)]
    struct IoStatus
    {
        internal UInt32 Information;
        internal UInt32 Status;
    }

    [StructLayout(LayoutKind.Sequential)]
    unsafe struct VolumeInfo
    {
        internal UInt64 TotalSize;
        internal UInt64 FreeSize;
        internal UInt16 VolumeLabelLength;
        internal fixed char VolumeLabel[32];
    }

    [StructLayout(LayoutKind.Explicit, Size = 72)]
    struct Response
    {
        [FieldOffset(0)]
        internal UInt16 Version;
        [FieldOffset(2)]
        internal UInt16 Size;
        [FieldOffset(4)]
        internal RequestKind Kind;
        [FieldOffset(6)]
        internal UInt64 Hint;
        [FieldOffset(14)]
        internal IoStatus IoStatus;

        [FieldOffset(22)]
        internal VolumeInfo QueryVolumeInformation;
    }


}
