using BonelabServerBrowser.Fusion;

namespace FusionDedicated.Tests;

/// <summary>
/// Round-trips every primitive through the writer and back. These two classes are
/// the wire format itself, so any asymmetry here silently corrupts clients.
/// </summary>
public class FusionNetWriterTests
{
    private static byte[] Write(Action<FusionNetWriter> write)
    {
        var writer = new FusionNetWriter();
        write(writer);
        return writer.ToArray();
    }

    [Fact]
    public void Byte_round_trips()
    {
        var bytes = Write(w =>
        {
            w.Write((byte)0);
            w.Write((byte)127);
            w.Write((byte)255);
        });

        Assert.Equal(3, bytes.Length);
        var r = new FusionNetReader(bytes);
        Assert.Equal((byte)0, r.ReadByte());
        Assert.Equal((byte)127, r.ReadByte());
        Assert.Equal((byte)255, r.ReadByte());
    }

    [Fact]
    public void Bool_round_trips()
    {
        var r = new FusionNetReader(Write(w =>
        {
            w.Write(true);
            w.Write(false);
        }));

        Assert.True(r.ReadBool());
        Assert.False(r.ReadBool());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    [InlineData(int.MaxValue)]
    [InlineData(0x01020304)]
    public void Int32_round_trips_big_endian(int value)
    {
        var writer = new FusionNetWriter();
        writer.Write(value);
        var bytes = writer.ToArray();

        // Big-endian on the wire: the most significant byte comes first.
        Assert.Equal((byte)(value >> 24), bytes[0]);

        var r = new FusionNetReader(bytes);
        Assert.Equal(value, r.ReadInt32());
    }

    [Theory]
    [InlineData(0ul)]
    [InlineData(ulong.MaxValue)]
    [InlineData(76561198000000000ul)]
    public void UInt64_round_trips(ulong value)
    {
        var writer = new FusionNetWriter();
        writer.Write(value);

        var r = new FusionNetReader(writer.ToArray());
        Assert.Equal(value, r.ReadUInt64());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(short.MinValue)]
    [InlineData(short.MaxValue)]
    public void Int16_round_trips(short value)
    {
        var writer = new FusionNetWriter();
        writer.WriteInt16(value);

        var r = new FusionNetReader(writer.ToArray());
        Assert.Equal(value, r.ReadInt16());
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(uint.MaxValue)]
    public void UInt32_round_trips(uint value)
    {
        var writer = new FusionNetWriter();
        writer.WriteUInt32(value);

        var r = new FusionNetReader(writer.ToArray());
        Assert.Equal(value, r.ReadUInt32());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(ushort.MaxValue)]
    public void UInt16_round_trips(ushort value)
    {
        var writer = new FusionNetWriter();
        writer.WriteUInt16(value);

        var r = new FusionNetReader(writer.ToArray());
        Assert.Equal(value, r.ReadUInt16());
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-1.5f)]
    [InlineData(3.14159f)]
    public void Single_round_trips(float value)
    {
        var writer = new FusionNetWriter();
        writer.Write(value);

        var r = new FusionNetReader(writer.ToArray());
        Assert.Equal(value, r.ReadSingle());
    }

    [Fact]
    public void SByte_uses_the_plus_128_bias_fusion_expects()
    {
        var writer = new FusionNetWriter();
        writer.WriteSByte(-1);
        writer.WriteSByte(0);
        writer.WriteSByte(127);
        writer.WriteSByte(-128);

        var bytes = writer.ToArray();

        // -1 is stored as 127, 0 as 128, 127 as 255, -128 as 0.
        Assert.Equal(new byte[] { 127, 128, 255, 0 }, bytes);

        var r = new FusionNetReader(bytes);
        Assert.Equal((sbyte)-1, r.ReadSByte());
        Assert.Equal((sbyte)0, r.ReadSByte());
        Assert.Equal((sbyte)127, r.ReadSByte());
        Assert.Equal((sbyte)-128, r.ReadSByte());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("hello")]
    [InlineData("привет — Unity rich text <color=#4ae08c>")]
    [InlineData("emoji \U0001f600 ok")]
    public void String_round_trips(string? value)
    {
        var writer = new FusionNetWriter();
        writer.Write(value);

        var r = new FusionNetReader(writer.ToArray());
        Assert.Equal(value, r.ReadString());
    }

    [Fact]
    public void Nullable_byte_round_trips_both_states()
    {
        var writer = new FusionNetWriter();
        writer.WriteNullable(null);
        writer.WriteNullable(42);

        var r = new FusionNetReader(writer.ToArray());
        Assert.Null(r.ReadNullableByte());
        Assert.Equal((byte)42, r.ReadNullableByte());
    }

    [Fact]
    public void Dictionary_and_list_round_trip()
    {
        var writer = new FusionNetWriter();
        writer.Write(new Dictionary<string, string>
        {
            ["barcode:x"] = "mod-1",
            ["version"] = "1.14.2",
        });
        writer.Write(new List<string> { "a", "b" });

        var r = new FusionNetReader(writer.ToArray());
        var map = new Dictionary<string, string>();

        for (int i = 0, count = r.ReadInt32(); i < count; i++)
        {
            var key = r.ReadString();
            var val = r.ReadString();

            map[key!] = val!;
        }

        Assert.Equal("mod-1", map["barcode:x"]);
        Assert.Equal("1.14.2", map["version"]);

        var list = new List<string>();
        for (int i = 0, count = r.ReadInt32(); i < count; i++)
        {
            list.Add(r.ReadString()!);
        }

        Assert.Equal(new[] { "a", "b" }, list);
    }

    [Fact]
    public void Block_round_trips_with_length_prefix_and_raw_does_not()
    {
        var writer = new FusionNetWriter();
        writer.WriteBlock(new byte[] { 1, 2, 3 });
        writer.WriteRaw(new byte[] { 9, 8 });

        var bytes = writer.ToArray();
        Assert.Equal(sizeof(int) + 3 + 2, bytes.Length);

        var r = new FusionNetReader(bytes);
        Assert.Equal(new byte[] { 1, 2, 3 }, r.ReadRaw(r.ReadInt32()).ToArray());
        Assert.Equal(new byte[] { 9, 8 }, r.ReadRaw(2).ToArray());
    }

    [Fact]
    public void Buffer_grows_beyond_initial_capacity()
    {
        var writer = new FusionNetWriter(capacity: 8);
        var payload = Enumerable.Range(0, 1000).Select(i => (byte)(i % 251)).ToArray();

        writer.WriteBlock(payload);
        Assert.Equal(payload.Length + sizeof(int), writer.Position);

        var r = new FusionNetReader(writer.ToArray());
        Assert.Equal(payload, r.ReadRaw(r.ReadInt32()).ToArray());
    }
}
