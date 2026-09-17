using FusionDedicated;

namespace FusionDedicated.Tests;

public class ServerConfigTests : IDisposable
{
    private readonly string _path = Path.Combine(
        Path.GetTempPath(), $"fusion-test-{Guid.NewGuid():N}.json");

    public void Dispose()
    {
        if (File.Exists(_path))
        {
            File.Delete(_path);
        }
    }

    [Fact]
    public void Load_creates_a_default_file_when_none_exists()
    {
        Assert.False(File.Exists(_path));

        var config = ServerConfig.Load(_path);

        Assert.Equal("Dedicated Fusion Server", config.ServerName);
        Assert.True(File.Exists(_path), "a fresh config is written back so the operator can edit it");
    }

    [Fact]
    public void Corrupt_config_falls_back_to_defaults_instead_of_crashing()
    {
        File.WriteAllText(_path, "{ this is not json");

        var config = ServerConfig.Load(_path);

        Assert.NotNull(config);
        Assert.Equal(10, config.MaxPlayers);
    }

    [Fact]
    public void Save_and_load_round_trips_settings()
    {
        var config = new ServerConfig
        {
            ServerName = "Test <color=#4ae08c>Server</color>",
            MaxPlayers = 24,
            Privacy = 2,
            VersionMajor = 1,
            VersionMinor = 14,
            DevTools = PermissionLevel.Operator,
        };
        config.Ban(12345, "spammer", "flooding");
        config.SetPermission(999, "admin", PermissionLevel.Owner);

        config.Save(_path);
        var loaded = ServerConfig.Load(_path);

        Assert.Equal(config.ServerName, loaded.ServerName);
        Assert.Equal(24, loaded.MaxPlayers);
        Assert.Equal(2, loaded.Privacy);
        Assert.Equal("1.14.2", loaded.Version);
        Assert.Equal(PermissionLevel.Operator, loaded.DevTools);
        Assert.Equal(PermissionLevel.Owner, loaded.GetPermission(999));
        Assert.True(loaded.IsBanned(12345));
        Assert.Equal("flooding", loaded.FindBan(12345)!.Reason);
    }

    [Fact]
    public void Permission_enums_serialize_as_names_not_numbers()
    {
        // Fusion's own files spell levels out; matching that keeps the file readable
        // and hand-editable.
        var config = new ServerConfig { Kicking = PermissionLevel.Operator };
        config.Save(_path);

        var text = File.ReadAllText(_path);

        Assert.Contains("\"Kicking\": \"Operator\"", text);
    }

    [Fact]
    public void Legacy_ban_id_lists_migrate_into_ban_entries()
    {
        File.WriteAllText(_path,
            """{ "BannedPlatformIds": [111, 222], "Bans": [] }""");

        var config = ServerConfig.Load(_path);

        Assert.Equal(2, config.Bans.Count);
        Assert.True(config.IsBanned(111));
        Assert.True(config.IsBanned(222));
        Assert.Empty(config.BannedPlatformIds);
    }

    [Fact]
    public void Setting_default_permission_removes_the_entry()
    {
        var config = new ServerConfig();
        config.SetPermission(42, "someone", PermissionLevel.Operator);
        Assert.Single(config.Permissions);

        // Default is implicit; keeping the entry would only grow the file.
        config.SetPermission(42, "someone", PermissionLevel.Default);

        Assert.Empty(config.Permissions);
        Assert.Equal(PermissionLevel.Default, config.GetPermission(42));
    }

    [Fact]
    public void SetPermission_updates_in_place_and_keeps_names_fresh()
    {
        var config = new ServerConfig();
        config.SetPermission(42, "old name", PermissionLevel.Operator);

        config.SetPermission(42, "new name", PermissionLevel.Owner);

        var entry = Assert.Single(config.Permissions);
        Assert.Equal(PermissionLevel.Owner, entry.Level);
        Assert.Equal("new name", entry.Username);
    }

    [Fact]
    public void Ban_is_idempotent_and_updates_the_reason()
    {
        var config = new ServerConfig();

        config.Ban(7, "griefer", "first");
        config.Ban(7, "griefer", "second");

        var ban = Assert.Single(config.Bans);
        Assert.Equal("second", ban.Reason);
    }

    [Fact]
    public void Unban_removes_both_modern_and_legacy_entries()
    {
        // A config written by an older build holds a bare id list alongside (or
        // instead of) the modern ban entries; Load migrates both into Bans.
        File.WriteAllText(_path,
            """
            {
              "BannedPlatformIds": [8],
              "Bans": [ { "PlatformId": 7, "Username": "x", "Reason": "reason" } ]
            }
            """);

        var config = ServerConfig.Load(_path);

        Assert.True(config.Unban(7));
        Assert.True(config.Unban(8));
        Assert.False(config.Unban(9));
    }

    [Fact]
    public void LearnMod_records_new_barcodes_and_reports_changes()
    {
        var config = new ServerConfig();

        Assert.True(config.LearnMod("barcode:a", 100, null));
        Assert.False(config.LearnMod("barcode:a", 100, null), "unchanged re-learn is not news");

        Assert.True(config.LearnMod("barcode:a", 100, 555), "a new file id is news");
        Assert.False(config.LearnMod("barcode:b", 0, null), "invalid mod ids are rejected");

        var found = config.FindMod("barcode:a");
        Assert.Equal(100, found!.ModId);
        Assert.Equal(555, found.ModFileId);
        Assert.Null(config.FindMod("barcode:unknown"));
    }

    [Fact]
    public void PermissionLevel_clamps_out_of_range_values()
    {
        Assert.Equal(PermissionLevel.Guest, PermissionLevels.Clamp(-100));
        Assert.Equal(PermissionLevel.Owner, PermissionLevels.Clamp(100));
        Assert.Equal(PermissionLevel.Default, PermissionLevels.Clamp(0));
    }

    [Fact]
    public void Permission_level_wire_strings_match_fusion()
    {
        Assert.Equal("GUEST", PermissionLevel.Guest.ToFusionString());
        Assert.Equal("DEFAULT", PermissionLevel.Default.ToFusionString());
        Assert.Equal("OPERATOR", PermissionLevel.Operator.ToFusionString());
        Assert.Equal("OWNER", PermissionLevel.Owner.ToFusionString());
    }
}
