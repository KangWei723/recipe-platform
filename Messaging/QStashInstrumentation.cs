using System.Diagnostics;

namespace Messaging;

public static class QStashInstrumentation
{
    public const string ActivitySourceName = "RecipePlatform.QStash";
    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);
}
