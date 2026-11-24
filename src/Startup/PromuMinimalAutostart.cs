// <copyright file="PromuMinimalAutostart.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Startup;

using MUnique.OpenMU.Interfaces;

/// <summary>
/// Lab/ProMU helper: when enabled, <c>-autostart</c> only brings up the live X10 stack
/// (Game Server 0 + Open Source Connect on 44406 + Chat). Extra GS channels and GMO Connect stay stopped.
/// Enable with env <c>PROMU_MINIMAL_AUTOSTART=1</c> (or <c>true</c>).
/// </summary>
internal static class PromuMinimalAutostart
{
    public const int LiveGameServerId = 0;

    public const int OpenSourceConnectPort = 44406;

    public static bool IsEnabled
    {
        get
        {
            var value = Environment.GetEnvironmentVariable("PROMU_MINIMAL_AUTOSTART");
            return string.Equals(value, "1", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
        }
    }

    public static bool ShouldStartGameServer(IGameServer gameServer) =>
        !IsEnabled || gameServer.Id == LiveGameServerId;

    public static bool ShouldStartConnectServer(IConnectServer connectServer)
    {
        if (!IsEnabled)
        {
            return true;
        }

        return connectServer is global::MUnique.OpenMU.ConnectServer.ConnectServer concrete
               && concrete.Settings.ClientListenerPort == OpenSourceConnectPort;
    }
}
