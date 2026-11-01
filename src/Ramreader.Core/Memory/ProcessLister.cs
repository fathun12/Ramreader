using System.Diagnostics;
using Ramreader.Core.Models;

namespace Ramreader.Core.Memory;

public static class ProcessLister
{
    /// <summary>
    /// Daftar proses untuk dipilih user. Default hanya proses yang punya jendela
    /// (game hampir selalu punya), supaya daftar tidak penuh proses sistem.
    /// </summary>
    public static IReadOnlyList<ProcessInfo> GetRunning(bool onlyWithWindow = true)
    {
        var result = new List<ProcessInfo>();

        foreach (var process in Process.GetProcesses())
        {
            try
            {
                string title = process.MainWindowTitle;
                if (onlyWithWindow && string.IsNullOrWhiteSpace(title)) continue;
                result.Add(new ProcessInfo(process.Id, process.ProcessName, title));
            }
            catch
            {
                // Proses sudah hilang atau tidak bisa diakses. Lewati.
            }
            finally
            {
                process.Dispose();
            }
        }

        return result.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }
}