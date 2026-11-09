using System;

[Serializable]
public struct RoomInfo
{
    public string RoomName;
    public string HostIP;
    public ushort Port;
    public int PlayerCount;
    public int MaxPlayers;
}
