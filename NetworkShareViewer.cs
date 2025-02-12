using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Vega;

public class NetworkShareViewer
{
    private string _remoteComputerName;

    #region DLL AND EXTERNAL CLASS

    [StructLayout(LayoutKind.Sequential)]
    public class NETRESOURCE
    {
        public ResourceScope dwScope = 0;
        public ResourceType dwType = 0;
        public ResourceDisplaytype dwDisplayType = 0;
        public ResourceUsage dwUsage = 0;
        public string lpLocalName = null;
        public string lpRemoteName = null;
        public string lpComment = null;
        public string lpProvider = null;
    }

    public enum ResourceScope : int
    {
        Connected = 1,
        GlobalNetwork,
        Remembered,
        Recent,
        Context
    }

    public enum ResourceType : int
    {
        Any = 0,
        Disk = 1,
        Print = 2,
        Reserved = 8
    }

    public enum ResourceDisplaytype : int
    {
        Generic = 0x0,
        Domain = 0x01,
        Server = 0x02,
        Share = 0x03,
        File = 0x04,
        Group = 0x05,
        Network = 0x06,
        Root = 0x07,
        Shareadmin = 0x08,
        Directory = 0x09,
        Tree = 0x0a,
        Ndscontainer = 0x0b
    }

    public enum ResourceUsage : int
    {
        None = 0,
        Connect = 1,
        Disconnect = 2,
        Reconnect = 3,
        Delete = 4
    }

    [DllImport("Mpr.dll", CharSet = CharSet.Auto)]
    private static extern int WNetOpenEnum(ResourceScope dwScope, ResourceType dwType, ResourceUsage dwUsage,
        NETRESOURCE p, out IntPtr lphEnum);

    [DllImport("Mpr.dll", CharSet = CharSet.Auto)]
    private static extern int WNetEnumResource(IntPtr hEnum, ref uint lpcCount, IntPtr buffer, ref uint lpBufferSize);

    [DllImport("Mpr.dll", CharSet = CharSet.Auto)]
    private static extern int WNetCloseEnum(IntPtr hEnum);

    #endregion

    public static NetworkShareViewer Viewer(string remoteComputerName)
    {
        return new NetworkShareViewer(remoteComputerName);
    }

    private NetworkShareViewer(string remoteComputerName)
    {
        _remoteComputerName = remoteComputerName;
    }

    public List<string> ViewShare(string remoteComputer)
    {
        List<string> sharedDrives = new List<string>();
        uint cEntries = 1;
        uint cbBuffer = 16384;
        IntPtr buffer = Marshal.AllocHGlobal((int)cbBuffer);
        NETRESOURCE nr = new NETRESOURCE
        {
            dwScope = ResourceScope.GlobalNetwork,
            dwType = ResourceType.Disk,
            lpRemoteName = remoteComputer
        };
        int result = WNetOpenEnum(ResourceScope.GlobalNetwork, ResourceType.Disk, ResourceUsage.None, nr,
            out IntPtr handle);
        if (result != 0)
        {
            throw new Win32Exception(result);
        }

        try
        {
            while (WNetEnumResource(handle, ref cEntries, buffer, ref cbBuffer) == 0)
            {
                IntPtr current = buffer;
                for (int i = 0; i < cEntries; i++)
                {
                    NETRESOURCE resource = (NETRESOURCE)Marshal.PtrToStructure(current, typeof(NETRESOURCE));
                    sharedDrives.Add(resource.lpRemoteName);
                    current = (IntPtr)((long)current + Marshal.SizeOf(typeof(NETRESOURCE)));
                }
            }
        }
        finally
        {
            WNetCloseEnum(handle);
            Marshal.FreeHGlobal(buffer);
        }

        return sharedDrives;
    }
}