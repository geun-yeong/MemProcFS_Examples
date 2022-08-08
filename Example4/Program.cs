using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Example4
{
    public class Program
    {
        //
        // import procedures from vmm.dll
        //
        [DllImport("vmm.dll", EntryPoint = "VMMDLL_Initialize")]
        public static extern bool VMMDLL_Initialize(int argc, string[] argv);
        [DllImport("vmm.dll", EntryPoint = "VMMDLL_Close")]
        public static extern bool VMMDLL_Close();



        //
        // vmmdll map pfn type
        //
        public enum VMMDLL_MAP_PFN_TYPE : uint
        {
            Zero = 0,
            Free = 1,
            Standby = 2,
            Modified = 3,
            ModifiedNoWrite = 4,
            Bad = 5,
            Active = 6,
            Transition = 7,

            PfnTypeMax = 8
        }

        public enum VMMDLL_MAP_PFN_TYPEEXTENDED : uint
        {
            Unknown = 0,
            Unused = 1,
            ProcessPrivate = 2,
            PageTable = 3,
            LargePage = 4,
            DriverLocked = 5,
            Shareable = 6,
            File = 7,

            PfnTypeExtendedMax = 8
        }

        internal static uint VMMDLL_MAP_PFN_VERSION = 1;

        [System.Runtime.InteropServices.StructLayoutAttribute(System.Runtime.InteropServices.LayoutKind.Sequential)]
        public struct VMMDLL_ADDRESS_INFO
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 5)] public uint[] dwPfnPte;
            public ulong va;
        }

        [System.Runtime.InteropServices.StructLayoutAttribute(System.Runtime.InteropServices.LayoutKind.Sequential)]
        internal struct VMMDLL_MAP_PFNENTRY
        {
            internal uint dwPfn;
            internal VMMDLL_MAP_PFN_TYPEEXTENDED tpExtended;
            internal VMMDLL_ADDRESS_INFO AddressInfo;
            internal ulong vaPte;
            internal ulong OriginalPte;
            internal uint _u3;
            internal ulong _u4;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)] internal uint[] _FutureUse;
        }

        [System.Runtime.InteropServices.StructLayoutAttribute(System.Runtime.InteropServices.LayoutKind.Sequential)]
        internal struct VMMDLL_MAP_PFN
        {
            internal uint dwVersion;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 5)] internal uint[] _Reserved1;
            internal uint cMap;
            //internal uint _Reserved2; // 메모리 주소 오프셋 맞춰주기 위한 패딩
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1)] internal VMMDLL_MAP_PFNENTRY[] Maps;
        }

        [DllImport("vmm.dll", EntryPoint = "VMMDLL_Map_GetPfn")]
        internal static extern unsafe bool VMMDLL_Map_GetPfn(
            uint[] pPfns,
            uint cPfns,
            ref VMMDLL_MAP_PFN pPfnMap,
            ref uint pcbPfnMap);




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
                "-waitinitialize",
                "-device",
                "D:\\mem.dmp" // write your memory dump path
            };

            if (!VMMDLL_Initialize(argv.Length, argv))
            {
                Console.WriteLine("VMMDLL_Initialize failed");
                return;
            }
            Console.WriteLine("VMMDLL_Initialize success");




            //
            // get pfn information
            //
            uint maxPfns = 0x100000;
            uint[] pfnTypeCnts = new uint[(uint)VMMDLL_MAP_PFN_TYPE.PfnTypeMax];
            uint errCnts = 0;

            for (uint i = 0; i < maxPfns; i++)
            {
                uint[] pfns = new uint[1] { i };
                uint mapSize = (uint)Marshal.SizeOf(typeof(VMMDLL_MAP_PFN));
                VMMDLL_MAP_PFN map = new VMMDLL_MAP_PFN();

                if(VMMDLL_Map_GetPfn(pfns, (uint)pfns.Length, ref map, ref mapSize)
                    && map.dwVersion == VMMDLL_MAP_PFN_VERSION)
                {
                    // vmmsharp에서는 _u3 & 0x7로 tp를 획득.
                    // 하지만 실제 코드상으로는 DWORD _u3로 취급하여 값을 넣기 때문에 Byte Order의 영향을 받음
                    // |  2  | 1 | 1 | 형태로 있어야 하는데 | 1 | 1 |  2  | 형태가 되버리는 셈
                    // 바이트 오더에 따라 AND 연산하는 비트 위치를 조정
                    VMMDLL_MAP_PFN_TYPE tp = (VMMDLL_MAP_PFN_TYPE)((map.Maps[0]._u3 & 0x00070000) >> 16);
                    pfnTypeCnts[(uint)tp]++;
                }
                else
                {
                    errCnts++;
                }
            }

            for (uint type = 0; type < pfnTypeCnts.Length; type++)
            {
                Console.WriteLine(String.Format("{0} = {1}", (VMMDLL_MAP_PFN_TYPE)type, pfnTypeCnts[type]));
            }
            Console.WriteLine(String.Format("{0} = {1}", "Error", errCnts));
        }
    }
}