using System;
using System.Runtime.InteropServices;

namespace Example2
{
    public class Program {
    
        //
        // import procedures from vmm.dll
        //
        [DllImport("vmm.dll", EntryPoint= "VMMDLL_Initialize")]
        public static extern bool VMMDLL_Initialize(int argc, string[] argv);

        [DllImport("vmm.dll", EntryPoint = "VMMDLL_InitializeEx")]
        public static extern bool VMMDLL_InitializeEx(int argc, string[] argv, out IntPtr ppLcErrorInfo);

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
            ref VMMDLL_PROCESS_INFORMATION pProcessInformation,
            ref ulong pcbProcessInformation);



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
            if(!vmmdll_result)
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

                Console.Write("[" + pid + "] => ");
                if (vmmdll_result)
                    Console.WriteLine(pi.szName);
                else
                    Console.WriteLine("<failed to get process information>");
            }
        }
    }
}
