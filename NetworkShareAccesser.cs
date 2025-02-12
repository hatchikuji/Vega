using System.ComponentModel;
using System.Runtime.InteropServices;
namespace Vega;

/// <summary>
/// Provides access to a network share.
/// </summary>
public class NetworkShareAccesser : IDisposable
{
    private string _remoteUncName;
    private string _remoteComputerName;

    public string RemoteComputerName
    {
        get
        {
            return this._remoteComputerName;
        }
        set
        {
            this._remoteComputerName = value;
            this._remoteUncName = @"\\" + this._remoteComputerName;
        }
    }

    public string UserName
    {
        get;
        set;
    }
    public string Password
    {
        get;
        set;
    }

    #region PINVOKE Consts

    private const int CONNECT_UPDATE_PROFILE = 0x00000001;
    private const int CONNECT_UPDATE_RECENT = 0x00000002;
    private const int CONNECT_TEMPORARY = 0x00000004;
    private const int CONNECT_INTERACTIVE = 0x00000008;
    private const int CONNECT_PROMPT = 0x00000010;
    private const int CONNECT_REDIRECT = 0x00000080;
    private const int CONNECT_CURRENT_MEDIA = 0x00000200;
    private const int CONNECT_COMMANDLINE = 0x00000800;
    private const int CONNECT_CMD_SAVECRED = 0x00001000;
    private const int CONNECT_CRED_RESET = 0x00002000;

    #endregion

    #region DLL AND EXTERNAL CLASS

    [DllImport("Mpr.dll")]
    private static extern int WNetUseConnection(
        IntPtr hwndOwner,
        NETRESOURCE lpNetResource,
        string lpPassword,
        string lpUserID,
        int dwFlags,
        string lpAccessName,
        string lpBufferSize,
        string lpResult
        );

    [DllImport("Mpr.dll")]
    private static extern int WNetCancelConnection2(
        string lpName,
        int dwFlags,
        bool fForce
    );

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


    #endregion

    /// <summary>
    /// Creates a NetworkShareAccesser for the given computer name using the given username (format: domainOrComputername\Username) and password
    /// </summary>
    /// <param name="remoteComputerName"></param>
    /// <param name="userName"></param>
    /// <param name="password"></param>
    public static NetworkShareAccesser Access(string remoteComputerName, string userName, string password)
    {
        return new NetworkShareAccesser(remoteComputerName,
            userName,
            password);
    }

    private NetworkShareAccesser(string remoteComputerName, string userName, string password)
    {
        RemoteComputerName = remoteComputerName;
        UserName = userName;
        Password = password;

        this.ConnectToShare(this._remoteUncName, this.UserName, this.Password, false);
    }

    private void ConnectToShare(string remoteUnc, string username, string password, bool promptUser)
    {
        NETRESOURCE nr = new NETRESOURCE
        {
            dwType = ResourceType.Disk,
            lpRemoteName = remoteUnc
        };

        int result = WNetUseConnection(IntPtr.Zero, nr, password, username, 0, null, null, null);


        if (result != 0)
        {
            throw new Win32Exception(result);
        }
    }

    private void DisconnectFromShare(string remoteUnc)
    {
        int result = WNetCancelConnection2(remoteUnc, 0, false);
        if (result != 0)
        {
            throw new Win32Exception(result);
        }
    }

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
    /// </summary>
    /// <filterpriority>2</filterpriority>
    public void Dispose()
    {
        this.DisconnectFromShare(this._remoteUncName);
    }
}