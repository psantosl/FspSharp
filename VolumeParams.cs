using System;
using System.IO;

namespace WinFspSharp
{
    struct VolumeParams
    {
        internal UInt16 Version;                     /* set to 0 */
        /* volume information */
        internal UInt16 SectorSize;
        internal UInt16 SectorsPerAllocationUnit;
        internal UInt16 MaxComponentLength;          /* maximum file name component length (bytes) */
        internal UInt64 VolumeCreationTime;
        internal UInt32 VolumeSerialNumber;
        /* I/O timeouts, capacity, etc. */
        internal UInt32 TransactTimeout;             /* FSP_FSCTL_TRANSACT timeout (millis; 1 sec - 10 sec) */
        internal UInt32 IrpTimeout;                  /* pending IRP timeout (millis; 1 min - 10 min) */
        internal UInt32 IrpCapacity;                 /* maximum number of pending IRP's (100 - 1000)*/
        internal UInt32 FileInfoTimeout;             /* FileInfo/Security/VolumeInfo timeout (millis) */
        /* FILE_FS_ATTRIBUTE_INFORMATION::FileSystemAttributes */
        internal byte CaseSensitiveSearch;       /* file system supports case-sensitive file names */
        internal byte CasePreservedNames;        /* file system preserves the case of file names */
        internal byte UnicodeOnDisk;             /* file system supports Unicode in file names */
        internal byte PersistentAcls;            /* file system preserves and enforces access control lists */
        internal byte ReparsePoints;             /* file system supports reparse points (!!!: unimplemented) */
        internal byte NamedStreams;              /* file system supports named streams (!!!: unimplemented) */
        internal byte HardLinks;                 /* unimplemented; set to 0 */
        internal byte ExtendedAttributes;        /* unimplemented; set to 0 */
        internal byte ReadOnlyVolume;
        internal Char[] Prefix;                      /* UNC prefix (\Server\Share) */

        public byte[] Serialize()
        {
            MemoryStream st = new MemoryStream();
            BinaryWriter writer = new BinaryWriter(st, System.Text.Encoding.Unicode);
            writer.Write(Version);
            writer.Write(SectorSize);
            writer.Write(SectorsPerAllocationUnit);
            writer.Write(MaxComponentLength);
            writer.Write(VolumeCreationTime);
            writer.Write(VolumeSerialNumber);
            writer.Write(TransactTimeout);
            writer.Write(IrpTimeout);
            writer.Write(IrpCapacity);
            writer.Write(FileInfoTimeout);
            writer.Write(GetFileSystemAttributes());

            if (Prefix != null)
                writer.Write(Prefix);
            else
                writer.Write(new Char[64]);

            return st.ToArray();
        }

        UInt32 GetFileSystemAttributes()
        {
            return (UInt32)(
                  (CaseSensitiveSearch  << 0)
                | (CasePreservedNames   << 1)
                | (UnicodeOnDisk        << 2)
                | (PersistentAcls       << 3)
                | (ReparsePoints        << 4)
                | (NamedStreams         << 5)
                | (HardLinks            << 6)
                | (ExtendedAttributes   << 7)
                | (ReadOnlyVolume       << 8));
        }
    }

}
