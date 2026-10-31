namespace Midnight.Shared.Enums;

/// <summary>Lifecycle of a server in the panel, independent of whether its container is running (see ServerState)</summary>
public enum ServerStatus
{
    /// <summary>The demon is running the install script</summary>
    Installing = 0,

    /// <summary>The install script failed, the server can be reinstalled</summary>
    InstallFailed = 1,

    /// <summary>Installed and usable</summary>
    Ready = 2
}
