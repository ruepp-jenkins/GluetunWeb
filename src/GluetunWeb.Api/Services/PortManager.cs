using GluetunWeb.Api.Data;
using GluetunWeb.Api.Docker;
using Microsoft.EntityFrameworkCore;

namespace GluetunWeb.Api.Services;

public class PortAllocationException(string message) : Exception(message);

/// <summary>
/// Assigns each connection and each load balancer one contiguous, block-aligned range of host
/// ports. Individual ports are never allocated on their own — they are derived from the block at
/// the fixed offsets in <see cref="PortLayout"/>, so toggling a proxy off and on again always
/// yields the same port.
///
/// A block is taken if another connection/balancer already holds it, or if any port inside it is
/// currently published by a live Docker container (which may be something we do not manage).
/// </summary>
public class PortManager(AppDbContext db, IContainerOrchestrator orchestrator)
{
    /// <summary>
    /// Pure block picker: returns the first free block start in [start, end], stepping by
    /// <paramref name="blockSize"/> from <paramref name="start"/> so blocks stay aligned and
    /// predictable. Throws when the range holds no free block.
    /// </summary>
    public static int FindFreeBlock(int start, int end, int blockSize, IReadOnlySet<int> occupiedPorts)
    {
        if (blockSize < 1)
            throw new PortAllocationException($"Invalid block size {blockSize}.");
        if (start < 1 || end > 65535 || start > end)
            throw new PortAllocationException($"Invalid port range {start}-{end}.");
        if (end - start + 1 < blockSize)
            throw new PortAllocationException(
                $"Port range {start}-{end} is smaller than one {blockSize}-port block.");

        for (var blockStart = start; blockStart + blockSize - 1 <= end; blockStart += blockSize)
        {
            var free = true;
            for (var p = blockStart; p < blockStart + blockSize && free; p++)
                free = !occupiedPorts.Contains(p);
            if (free)
                return blockStart;
        }

        throw new PortAllocationException(
            $"No free {blockSize}-port block left in range {start}-{end}. Widen the range in Global Settings " +
            "or remove unused connections/load balancers.");
    }

    /// <summary>Returns the connection's block, assigning one on first use.</summary>
    public async Task<int> EnsureConnectionBlockAsync(Connection c, CancellationToken ct = default)
    {
        if (c.PortBlockStart > 0)
            return c.PortBlockStart;

        var settings = await db.GlobalSettings.AsNoTracking().FirstAsync(ct);
        var occupied = await OccupiedPortsAsync(ct);
        c.PortBlockStart = FindFreeBlock(
            settings.PortRangeStart, settings.PortRangeEnd, PortLayout.ConnectionBlockSize, occupied);
        await db.SaveChangesAsync(ct);
        return c.PortBlockStart;
    }

    /// <summary>Returns the load balancer's block, assigning one on first use.</summary>
    public async Task<int> EnsureBalancerBlockAsync(LoadBalancer l, CancellationToken ct = default)
    {
        if (l.PortBlockStart > 0)
            return l.PortBlockStart;

        var settings = await db.GlobalSettings.AsNoTracking().FirstAsync(ct);
        var occupied = await OccupiedPortsAsync(ct);
        l.PortBlockStart = FindFreeBlock(
            settings.BalancerPortRangeStart, settings.BalancerPortRangeEnd, PortLayout.BalancerBlockSize, occupied);
        await db.SaveChangesAsync(ct);
        return l.PortBlockStart;
    }

    /// <summary>
    /// Every host port that is off-limits: all ports of every assigned block, plus everything live
    /// Docker currently publishes. Blocks of both kinds are included even though the two ranges are
    /// normally disjoint, so a misconfigured overlap still cannot double-assign a port.
    /// </summary>
    private async Task<HashSet<int>> OccupiedPortsAsync(CancellationToken ct)
    {
        var occupied = new HashSet<int>();

        var connectionBlocks = await db.Connections
            .Where(x => x.PortBlockStart > 0)
            .Select(x => x.PortBlockStart)
            .ToListAsync(ct);
        foreach (var b in connectionBlocks)
            AddBlock(occupied, b, PortLayout.ConnectionBlockSize);

        var balancerBlocks = await db.LoadBalancers
            .Where(x => x.PortBlockStart > 0)
            .Select(x => x.PortBlockStart)
            .ToListAsync(ct);
        foreach (var b in balancerBlocks)
            AddBlock(occupied, b, PortLayout.BalancerBlockSize);

        foreach (var used in await orchestrator.GetUsedHostPortsAsync(ct))
            occupied.Add(used);

        return occupied;
    }

    private static void AddBlock(HashSet<int> set, int blockStart, int size)
    {
        for (var i = 0; i < size; i++)
            set.Add(blockStart + i);
    }
}
