using GluetunWeb.Api.Data;

namespace GluetunWeb.Api.Services;

/// <summary>
/// Fixed host-port layout. Every connection and every load balancer owns one contiguous,
/// block-aligned range of host ports, and within that block each purpose sits at a **fixed offset**.
///
/// The point is stability: toggling SOCKS5/HTTP/Shadowsocks off leaves its slot unused rather than
/// compacting the others, so a port a client is already pointed at never moves. Blocks are sized
/// with spare slots so a future proxy can be added without relocating anything.
/// </summary>
public static class PortLayout
{
    /// <summary>Ports reserved per connection: 4 in use + 4 spare.</summary>
    public const int ConnectionBlockSize = 8;

    /// <summary>Ports reserved per load balancer: 3 in use + 1 spare.</summary>
    public const int BalancerBlockSize = 4;

    /// <summary>
    /// Offset of each purpose inside its owner's block. Changing these values relocates ports on
    /// every existing deployment, so treat them as a stable contract.
    /// </summary>
    public static int OffsetOf(PortPurpose purpose) => purpose switch
    {
        // Connection block
        PortPurpose.Control => 0,
        PortPurpose.Socks5 => 1,
        PortPurpose.HttpProxy => 2,
        PortPurpose.Shadowsocks => 3,

        // Load-balancer block
        PortPurpose.BalancerListen => 0,
        PortPurpose.BalancerWeb => 1,
        PortPurpose.BalancerState => 2,

        _ => throw new ArgumentOutOfRangeException(nameof(purpose), purpose, "No port offset defined."),
    };

    /// <summary>The host port for a purpose within a block, or 0 when the block is unassigned.</summary>
    public static int PortFor(int blockStart, PortPurpose purpose) =>
        blockStart <= 0 ? 0 : blockStart + OffsetOf(purpose);

    /// <summary>Inclusive last port of a block (for display and overlap checks).</summary>
    public static int BlockEnd(int blockStart, int blockSize) => blockStart + blockSize - 1;
}
