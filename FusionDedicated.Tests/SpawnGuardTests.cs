using FusionDedicated;
using FusionDedicated.Server;
using Steamworks;

namespace FusionDedicated.Tests;

public class SpawnGuardTests
{
    private static ConnectedPlayer Player(byte smallId = 1, PermissionLevel level = PermissionLevel.Default)
        => new()
        {
            Connection = default,
            PlatformId = 1000ul + smallId,
            SmallId = smallId,
            Username = $"Player{smallId}",
            Permission = level,
        };

    private static ServerConfig Config(
        int burstLimit = 25,
        int windowSeconds = 5,
        int maxPerPlayer = 300,
        int strikes = 3,
        PermissionLevel exempt = PermissionLevel.Owner)
        => new()
        {
            AntiSpamEnabled = true,
            SpawnBurstLimit = burstLimit,
            SpawnWindowSeconds = windowSeconds,
            MaxEntitiesPerPlayer = maxPerPlayer,
            SpamStrikesBeforeKick = strikes,
            AntiSpamExemptLevel = exempt,
        };

    [Fact]
    public void Spawns_under_the_limit_pass()
    {
        var guard = new SpawnGuard(Config(burstLimit: 5, windowSeconds: 60));
        var player = Player();

        for (int i = 0; i < 5; i++)
        {
            var verdict = guard.Check(player, ownedEntities: i);

            Assert.True(verdict.Allowed);
            Assert.False(verdict.Purge);
        }
    }

    [Fact]
    public void Bursting_over_the_limit_is_refused_and_purged()
    {
        var guard = new SpawnGuard(Config(burstLimit: 3, windowSeconds: 60));
        var player = Player();

        for (int i = 0; i < 3; i++)
        {
            Assert.True(guard.Check(player, 0).Allowed);
        }

        var verdict = guard.Check(player, 3);

        Assert.False(verdict.Allowed);
        Assert.True(verdict.Purge);
        Assert.False(verdict.Kick, "the first offence warns, it does not eject");
        Assert.Contains("strike 1", verdict.Reason);
    }

    [Fact]
    public void Owning_too_many_entities_trips_even_without_a_burst()
    {
        var guard = new SpawnGuard(Config(maxPerPlayer: 10));

        var verdict = guard.Check(Player(), ownedEntities: 10);

        Assert.False(verdict.Allowed);
        Assert.True(verdict.Purge);
        Assert.Contains("10 entities", verdict.Reason);
    }

    [Fact]
    public void Kicks_after_repeated_strikes()
    {
        // A burst limit of zero trips on every spawn and one strike may be earned
        // per window, so a one-second window keeps this test quick.
        var guard = new SpawnGuard(Config(burstLimit: 0, windowSeconds: 1, strikes: 3));
        var player = Player();

        Assert.False(guard.Check(player, 0).Kick); // strike 1

        Thread.Sleep(1100);
        Assert.False(guard.Check(player, 0).Kick); // strike 2

        Thread.Sleep(1100);
        var final = guard.Check(player, 0);

        Assert.False(final.Allowed);
        Assert.True(final.Kick);
        Assert.Contains("strike 3", final.Reason);
    }

    /// <summary>
    /// The guard keeps one tracker per player in a private map. Tests reach in rather
    /// than sleeping through real time for strike decay, which is keyed to wall-clock
    /// minutes.
    /// </summary>
    private static object TrackerFor(SpawnGuard guard, byte smallId)
    {
        var map = (System.Collections.IDictionary)typeof(SpawnGuard)
            .GetField("_byPlayer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .GetValue(guard)!;

        return map[smallId]!;
    }

    [Fact]
    public void Strikes_decay_after_two_quiet_minutes()
    {
        var guard = new SpawnGuard(Config(burstLimit: 0, windowSeconds: 60, strikes: 3));
        var player = Player();

        guard.Check(player, 0);
        var tracker = TrackerFor(guard, player.SmallId);
        var strikes = tracker.GetType().GetField("Strikes")!;
        var lastStrike = tracker.GetType().GetField("LastStrike")!;

        Assert.Equal(1, strikes.GetValue(tracker));

        lastStrike.SetValue(tracker, DateTime.UtcNow.AddMinutes(-3));

        guard.Check(player, 0);
        Assert.Equal(1, strikes.GetValue(tracker));
    }

    [Fact]
    public void Only_one_strike_is_earned_per_window()
    {
        var guard = new SpawnGuard(Config(burstLimit: 1, windowSeconds: 60, strikes: 3));
        var player = Player();

        for (int i = 0; i < 20; i++)
        {
            guard.Check(player, 0);
        }

        var tracker = TrackerFor(guard, player.SmallId);

        Assert.Equal(1, tracker.GetType().GetField("Strikes")!.GetValue(tracker));
    }

    [Fact]
    public void Exempt_players_are_never_restricted_but_are_reported()
    {
        var guard = new SpawnGuard(Config(exempt: PermissionLevel.Owner));
        var owner = Player(level: PermissionLevel.Owner);

        for (int i = 0; i < 50; i++)
        {
            Assert.True(guard.Check(owner, ownedEntities: 10_000).Allowed);
        }

        Assert.NotNull(guard.ExemptOverrun);
        Assert.Contains("exempt", guard.ExemptOverrun);
    }

    [Fact]
    public void Non_exempt_players_do_not_set_the_exempt_report()
    {
        var guard = new SpawnGuard(Config(maxPerPlayer: 1));

        guard.Check(Player(), ownedEntities: 0);

        Assert.Null(guard.ExemptOverrun);
    }

    [Fact]
    public void Players_are_tracked_separately()
    {
        var guard = new SpawnGuard(Config(burstLimit: 2, windowSeconds: 60));
        var alice = Player(smallId: 1);
        var bob = Player(smallId: 2);

        Assert.True(guard.Check(alice, 0).Allowed);
        Assert.True(guard.Check(alice, 1).Allowed);
        Assert.False(guard.Check(alice, 2).Allowed);

        Assert.True(guard.Check(bob, 0).Allowed, "bob's allowance is his own");
    }

    [Fact]
    public void Forget_clears_a_players_history()
    {
        var guard = new SpawnGuard(Config(burstLimit: 1, windowSeconds: 60));
        var player = Player();

        Assert.True(guard.Check(player, 0).Allowed);
        Assert.False(guard.Check(player, 0).Allowed);

        guard.Forget(player.SmallId);

        Assert.True(guard.Check(player, 0).Allowed, "a returning player starts clean");
    }

    [Fact]
    public void Disabled_guard_allows_everything()
    {
        var config = Config(maxPerPlayer: 1);
        config.AntiSpamEnabled = false;
        var guard = new SpawnGuard(config);

        var verdict = guard.Check(Player(), ownedEntities: 9999);

        Assert.True(verdict.Allowed);
        Assert.Null(guard.ExemptOverrun);
    }
}
