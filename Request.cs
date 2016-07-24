using System;
using System.Runtime.InteropServices;

namespace WinFspSharp
{
    enum RequestKind : UInt32
    {
        ReservedKind = 0,
        CreateKind,
        OverwriteKind,
        CleanupKind,
        CloseKind,
        ReadKind,
        WriteKind,
        QueryInformationKind,
        SetInformationKind,
        QueryEaKind,
        SetEaKind,
        FlushBuffersKind,
        QueryVolumeInformationKind,
        SetVolumeInformationKind,
        QueryDirectoryKind,
        FileSystemControlKind,
        DeviceControlKind,
        ShutdownKind,
        LockControlKind,
        QuerySecurityKind,
        SetSecurityKind,
        KindCount,
    };


    [StructLayout(LayoutKind.Sequential)]
    struct RequestCommon
    {
        internal UInt16 Version;
        internal UInt16 Size;
        internal RequestKind Kind;
        internal UInt64 Hint;
    }
}
