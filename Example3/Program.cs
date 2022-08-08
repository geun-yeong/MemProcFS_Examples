using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Example3
{
    public class Program
    {
        //
        // import procedures from vmm.dll
        //
        [DllImport("vmm.dll", EntryPoint = "VMMDLL_Initialize")]
        public static extern bool VMMDLL_Initialize(int argc, string[] argv);

        [DllImport("vmm.dll", EntryPoint = "VMMDLL_PidList")]
        public static extern unsafe bool VMMDLL_PidList(uint[] pPids, ref ulong pPidNum);



        //
        // declare the process information structure that vmm uses
        //
        public static ulong VMMDLL_PROCESS_INFORMATION_MAGIC = 0xc0ffee663df9301e;
        public static ushort VMMDLL_PROCESS_INFORMATION_VERSION = 7;
        [System.Runtime.InteropServices.StructLayoutAttribute(System.Runtime.InteropServices.LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public struct VMMDLL_PROCESS_INFORMATION
        {
            public ulong magic;
            public ushort wVersion;
            public ushort wSize;
            public uint tpMemoryModel;
            public uint tpSystem;
            public bool fUserOnly;
            public uint dwPID;
            public uint dwPPID;
            public uint dwState;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 16)] public string szName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string szNameLong;
            public ulong paDTB;
            public ulong paDTB_UserOpt;
            public ulong vaEPROCESS;
            public ulong vaPEB;
            public ulong _Reserved1;
            public bool fWow64;
            public uint vaPEB32;
            public uint dwSessionId;
            public ulong qwLUID;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szSID;
            public uint IntegrityLevel;
        }

        [DllImport("vmm.dll", EntryPoint = "VMMDLL_ProcessGetInformation")]
        public static extern unsafe bool VMMDLL_ProcessGetInformation(
            uint dwPID,
            uint dwPPID,
            ref VMMDLL_PROCESS_INFORMATION pProcessInformation,
            ref ulong pcbProcessInformation;



        //
        // declare the vfs filelist structure that vmm uses
        //
        [DllImport("vmm.dll", EntryPoint = "VMMDLL_InitializePlugins")]
        public static extern bool VMMDLL_InitializePlugins();

        public static uint VMMDLL_VFS_FILELIST_EXINFO_VERSION = 1;
        public static uint VMMDLL_VFS_FILELIST_VERSION = 2;

        [System.Runtime.InteropServices.StructLayoutAttribute(System.Runtime.InteropServices.LayoutKind.Sequential)]
        public struct VMMDLL_VFS_FILELIST
        {
            public uint dwVersion;
            public uint _Reserved;
            public IntPtr pfnAddFile;
            public IntPtr pfnAddDirectory;
            public ulong h;
        }

        [DllImport("vmm.dll", EntryPoint = "VMMDLL_VfsListU")]
        public static extern unsafe bool VMMDLL_VfsList(
            [MarshalAs(UnmanagedType.LPUTF8Str)] string wcsPath,
            ref VMMDLL_VFS_FILELIST pFileList);


        [DllImport("vmm.dll", EntryPoint = "VMMDLL_VfsReadU")]
        public static extern unsafe uint VMMDLL_VfsRead(
            [MarshalAs(UnmanagedType.LPUTF8Str)] string wcsFileName,
            byte* pb,
            uint cb,
            out uint pcbRead,
            ulong cbOffset);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate bool VfsCallBack_AddFile(ulong h, [MarshalAs(UnmanagedType.LPUTF8Str)] string wszName, ulong cb, IntPtr pExInfo);
        
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate bool VfsCallBack_AddDirectory(ulong h, [MarshalAs(UnmanagedType.LPUTF8Str)] string wszName, IntPtr pExInfo);

        [System.Runtime.InteropServices.StructLayoutAttribute(System.Runtime.InteropServices.LayoutKind.Sequential)]
        public struct VMMDLL_VFS_FILELIST_EXINFO
        {
            public uint dwVersion;
            public bool fCompressed;
            public ulong ftCreationTime;
            public ulong ftLastAccessTime;
            public ulong ftLastWriteTime;
        }

        private static ulong last_minidump_file_size = 0;


        //
        // entry point
        //
        static void Main(string[] args)
        {
            //
            // initialize the vmm
            //
            string[] argv = {
                "", // argv[0] == the name of this program
                "-device",
                "D:\\mem.dmp" // write your memory dump path
            };
            Console.WriteLine("Options: " + argv[1] + " " + argv[2]);

            bool vmmdll_result = VMMDLL_Initialize(argv.Length, argv);
            if (!vmmdll_result)
            {
                Console.WriteLine("VMMDLL_Initialize failed");
                return;
            }
            Console.WriteLine("VMMDLL_Initialize success");



            //
            // get list of processes from memory dump
            //
            // first, get the number of proceses, then get process ids
            //
            ulong pidNum = 0;
            unsafe
            {
                vmmdll_result = VMMDLL_PidList(null, ref pidNum);
            }
            if (!vmmdll_result)
            {
                Console.WriteLine("VMMDLL_PidList failed");
                return;
            }
            else if (pidNum == 0)
            {
                Console.WriteLine("VMMDLL_PidList returned the number of pid as 0");
                return;
            }


            uint[] pids = new uint[pidNum];
            unsafe
            {
                vmmdll_result = VMMDLL_PidList(pids, ref pidNum);
            }
            if (!vmmdll_result)
            {
                Console.WriteLine("VMMDLL_PidList failed in 2nd phase");
                return;
            }



            //
            // get name of process with pid and print it
            //
            // magic and wVersion must be set 0xc0ffee663df9301e and 7
            // piSize must be set as size of process information structure
            //
            VMMDLL_PROCESS_INFORMATION pi = new VMMDLL_PROCESS_INFORMATION();
            pi.magic = VMMDLL_PROCESS_INFORMATION_MAGIC;
            pi.wVersion = VMMDLL_PROCESS_INFORMATION_VERSION;
            ulong piSize = (ulong)Marshal.SizeOf(typeof(VMMDLL_PROCESS_INFORMATION));

            foreach (uint pid in pids)
            {
                unsafe
                {
                    vmmdll_result = VMMDLL_ProcessGetInformation(
                        pid,
                        ref pi,
                        ref piSize);
                }
                Console.WriteLine(String.Format("[{0}] {1}", pid, pi.szName));
            }

            //
            // explore the vfs tree
            //
            VMMDLL_InitializePlugins(); // need for vfs

            pids = new uint[] { 7940, 6776, 6656 };
            foreach (uint pid in pids)
            {
                Console.WriteLine();
                Console.WriteLine(String.Format("PID {0} starts", pid));
                VMMDLL_VFS_FILELIST file_list;
                file_list.dwVersion = VMMDLL_VFS_FILELIST_VERSION;
                file_list.h = 1;
                file_list._Reserved = 0;
                file_list.pfnAddFile = Marshal.GetFunctionPointerForDelegate((VfsCallBack_AddFile)ExampleVfsCallBack_AddFile);
                file_list.pfnAddDirectory = Marshal.GetFunctionPointerForDelegate((VfsCallBack_AddDirectory)ExampleVfsCallBack_AddDirectory);

                vmmdll_result = VMMDLL_VfsList(String.Format("\\pid\\{0}\\minidump", pid), ref file_list);
                if (vmmdll_result)
                {
                    Console.WriteLine("VMMDLL_VfsList success");
                }
                else
                {
                    Console.WriteLine("VMMDLL_VfsList failed");
                    continue;
                }

                byte[] buffer = new byte[last_minidump_file_size];
                uint read_size = 0, nt_status = 0, offset = 0;
                while (nt_status == 0 && offset < buffer.Length)
                {
                    unsafe
                    {
                        fixed (byte* p = buffer)
                        {
                            nt_status = VMMDLL_VfsRead(String.Format("\\pid\\{0}\\minidump\\minidump.dmp", pid), p, (uint)buffer.Length, out read_size, offset);
                        }
                    }
                    if (nt_status == 0)
                    {
                        Console.WriteLine("VMMDLL_VfsRead successfully read data from " + offset.ToString() + " offset");
                        Console.WriteLine("VMMDLL_VfsRead return " + read_size.ToString() + " as read size");
                        offset += read_size;
                        //Console.WriteLine(HexDump(buffer));
                    }
                    else
                    {
                        Console.WriteLine("VMMDLL_VfsRead failed");
                        Console.WriteLine("VMMDLL_VfsRead returns " + nt_status.ToString("X") + " as NTSTATUS");
                        break;
                    }
                }
                Console.WriteLine("VMMDLL_VfsRead successfully read all data as " + offset.ToString() + " bytes");

                FileStream fs = File.Open(String.Format(".\\minidump_{0}.dmp", pid), FileMode.Create);
                using (BinaryWriter wr = new BinaryWriter(fs))
                {
                    wr.Write(buffer);
                }
                fs.Close();
            }
        }

        public static bool ExampleVfsCallBack_AddFile(ulong h, string wszName, ulong cb, IntPtr pExInfo)
        {
            Console.WriteLine(wszName + " was added");
            if (wszName.Equals("minidump.dmp"))
            {
                last_minidump_file_size = cb;
            }
            return true;
        }

        static bool ExampleVfsCallBack_AddDirectory(ulong h, [MarshalAs(UnmanagedType.LPUTF8Str)] string wszName, IntPtr pExInfo)
        {
            Console.WriteLine(wszName + " was added");
            return true;
        }

        public static string HexDump(byte[] bytes, int bytesPerLine = 16)
        {
            if (bytes == null) return "<null>";
            int bytesLength = bytes.Length;

            char[] HexChars = "0123456789ABCDEF".ToCharArray();

            int firstHexColumn =
                  8                   // 8 characters for the address
                + 1;                  // 3 spaces

            int firstCharColumn = firstHexColumn
                + bytesPerLine * 3       // - 2 digit for the hexadecimal value and 1 space
                + (bytesPerLine - 1) / 8 // - 1 extra space every 8 characters from the 9th
                + 2;                  // 2 spaces

            int lineLength = firstCharColumn
                + bytesPerLine           // - characters to show the ascii value
                + Environment.NewLine.Length; // Carriage return and line feed (should normally be 2)

            char[] line = (new String(' ', lineLength - Environment.NewLine.Length) + Environment.NewLine).ToCharArray();
            int expectedLines = (bytesLength + bytesPerLine - 1) / bytesPerLine;
            StringBuilder result = new StringBuilder(expectedLines * lineLength);

            for (int i = 0; i < bytesLength; i += bytesPerLine)
            {
                line[0] = HexChars[(i >> 28) & 0xF];
                line[1] = HexChars[(i >> 24) & 0xF];
                line[2] = HexChars[(i >> 20) & 0xF];
                line[3] = HexChars[(i >> 16) & 0xF];
                line[4] = HexChars[(i >> 12) & 0xF];
                line[5] = HexChars[(i >> 8) & 0xF];
                line[6] = HexChars[(i >> 4) & 0xF];
                line[7] = HexChars[(i >> 0) & 0xF];

                int hexColumn = firstHexColumn;
                int charColumn = firstCharColumn;

                for (int j = 0; j < bytesPerLine; j++)
                {
                    if (j > 0 && (j & 7) == 0) hexColumn++;
                    if (i + j >= bytesLength)
                    {
                        line[hexColumn] = ' ';
                        line[hexColumn + 1] = ' ';
                        line[charColumn] = ' ';
                    }
                    else
                    {
                        byte b = bytes[i + j];
                        line[hexColumn] = HexChars[(b >> 4) & 0xF];
                        line[hexColumn + 1] = HexChars[b & 0xF];
                        line[charColumn] = (b < 32 ? '·' : (char)b);
                    }

                    hexColumn += 3;
                    charColumn++;
                }

                result.Append(line);
            }

            return result.ToString();
        }
    }
}
