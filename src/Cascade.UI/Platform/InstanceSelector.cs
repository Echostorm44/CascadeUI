using System.Globalization;

namespace Cascade.UI;

/// <summary>
/// Which running instance a <c>cascade mcp ... --app</c> (or the MCP bridge) talks to:
/// <c>Name</c> (that app's focused or most recently activated window), <c>Name#1234</c> (that
/// app's instance in process 1234) or <c>#1234</c> (whichever app process 1234 is). The
/// <c>Name#pid</c> form is what <c>cascade mcp info</c> prints for each instance.
/// </summary>
/// <remarks>
/// Two instances of one app register under the same name, so the name alone picked the focused
/// one — a second agent's fixture, a stale debug session — with no way to choose.
/// </remarks>
internal readonly record struct InstanceSelector(string? AppId, int? Pid)
{
    /// <summary>The selector for one registered instance, as <c>info</c> prints it.</summary>
    internal static string Format(InstanceEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return string.Create(CultureInfo.InvariantCulture, $"{entry.Title}#{entry.Pid}");
    }

    /// <summary>
    /// Parses an <c>--app</c> value. A trailing <c>#digits</c> is a process id; anything else is
    /// an app name as before (a name that itself contains '#' followed by non-digits stays whole).
    /// </summary>
    internal static InstanceSelector Parse(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return new InstanceSelector(null, null);
        }

        int hash = value.LastIndexOf('#');
        if (hash < 0 || hash == value.Length - 1)
        {
            return new InstanceSelector(value, null);
        }

        string digits = value[(hash + 1)..];
        if (!int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out int pid) || pid <= 0)
        {
            return new InstanceSelector(value, null);
        }

        string? app = hash == 0 ? null : value[..hash];
        return new InstanceSelector(app, pid);
    }

    /// <summary>
    /// Finds the instance this selector names: with a process id, the live entry for that process
    /// in the app's registry (or, without a name, the global one); otherwise the app's focused or
    /// most recently activated instance. Null when none matches.
    /// </summary>
    internal InstanceEntry? Resolve(string globalRegistryId)
    {
        string registryId = AppId ?? globalRegistryId;
        using var registry = new SharedInstanceRegistry(registryId);
        if (Pid is not int pid)
        {
            return registry.FindTarget();
        }

        return registry.FindAll().Find(e => e.Pid == pid);
    }
}
