using System.Text.Json;
using System.Text.Json.Serialization;

public class RawJsonBytesConverter : JsonConverter<byte[]>
{
    public override byte[] Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) 
        => throw new NotImplementedException();

    public override void Write(Utf8JsonWriter writer, byte[] value, JsonSerializerOptions options)
    {
        // This writes the UTF-8 bytes directly to the output stream without transcoding to UTF-16 strings
        writer.WriteRawValue(value);
    }
}
