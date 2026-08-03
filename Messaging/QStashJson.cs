using System.Text.Json;

namespace Messaging;

public static class QStashJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
}
