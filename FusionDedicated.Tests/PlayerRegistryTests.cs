using FusionDedicated.Server;
using Steamworks;

namespace FusionDedicated.Tests;

public class PlayerRegistryTests
{
    private static ConnectedPlayer Player(byte smallId, ulong platformId)
        => new()
        {
            Connection = default,
            PlatformId = platformId,
            SmallId = smallId,
            Username = $"Player{smallId}",
        };

    [Fact]
    public void Ids_start_at_one_because_zero_belongs_to_the_server()
    {
        var registry = new PlayerRegistry();

        Assert.Equal((byte)1, registry.AllocateSmallId());
    }

    [Fact]
    public void Freed_ids_are_reused_lowest_first()
    {
        var registry = new PlayerRegistry();

        var a = Player(registry.AllocateSmallId()!.Value, 1);
        var b = Player(registry.AllocateSmallId()!.Value, 2);
        registry.Add(a);
        registry.Add(b);

        registry.RemoveBySmallId(a.SmallId);

        Assert.Equal(a.SmallId, registry.AllocateSmallId());
    }

    [Fact]
    public void Ids_are_exhausted_below_the_reserved_player_range()
    {
        // Player IDs 0-255 are reserved by clients for player rigs, so allocation
        // must stop at 254 rather than wrapping into the rig range.
        var registry = new PlayerRegistry();
        var ids = new HashSet<byte>();

        for (int i = 0; i < 254; i++)
        {
            var id = registry.AllocateSmallId();

            Assert.NotNull(id);
            Assert.True(ids.Add(id.Value), "no id may be handed out twice");

            registry.Add(Player(id.Value, (ulong)(i + 1)));
        }

        Assert.Null(registry.AllocateSmallId());
    }

    [Fact]
    public void Lookups_find_the_same_player_every_way()
    {
        var registry = new PlayerRegistry();
        var player = Player(registry.AllocateSmallId()!.Value, 4242);
        registry.Add(player);

        Assert.Same(player, registry.Get(player.SmallId));
        Assert.Same(player, registry.GetByPlatformId(4242));
        Assert.Same(player, registry.GetByConnection(default));
        Assert.True(registry.Contains(4242));
        Assert.Equal(1, registry.Count);
    }

    [Fact]
    public void Remove_by_connection_returns_the_removed_player()
    {
        var registry = new PlayerRegistry();
        var player = Player(1, 7);
        registry.Add(player);

        var removed = registry.Remove(default);

        Assert.Same(player, removed);
        Assert.Equal(0, registry.Count);
        Assert.Null(registry.Get(1));
        Assert.False(registry.Contains(7));
    }

    [Fact]
    public void Removing_an_unknown_player_is_a_clean_null()
    {
        var registry = new PlayerRegistry();

        Assert.Null(registry.Remove(default));
        Assert.Null(registry.RemoveBySmallId(200));
    }

    [Fact]
    public void Players_are_listed_in_id_order()
    {
        var registry = new PlayerRegistry();

        foreach (var platform in new ulong[] { 3, 1, 2 })
        {
            registry.Add(Player(registry.AllocateSmallId()!.Value, platform));
        }

        Assert.Equal(new byte[] { 1, 2, 3 }, registry.Players.Select(p => p.SmallId).ToArray());
    }

    [Fact]
    public void Contains_reflects_the_add_and_remove_lifecycle()
    {
        var registry = new PlayerRegistry();
        var player = Player(1, 500);
        registry.Add(player);

        Assert.True(registry.Contains(500));

        registry.RemoveBySmallId(1);

        Assert.False(registry.Contains(500));
        Assert.Null(registry.GetByPlatformId(500));
    }

    [Fact]
    public void DisplayName_prefers_nickname_then_username_then_id()
    {
        var bare = Player(1, 1);
        bare.Username = "";
        Assert.Equal("Player 1", bare.DisplayName);

        var named = Player(2, 2);
        named.Username = "alice";
        Assert.Equal("alice", named.DisplayName);

        var nicknamed = Player(3, 3);
        nicknamed.Username = "alice";
        nicknamed.Nickname = "ace";
        Assert.Equal("alice (ace)", nicknamed.DisplayName);
    }
}
