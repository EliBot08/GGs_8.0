using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GGs.Desktop.Services;

/// <summary>
/// Safe file operations used by installers/updaters. Each method is defensive and avoids partial writes.
/// </summary>
public static class SafeFileManager
{
    /// <summary>
    /// Writes the content to destination atomically. If destination exists, it is optionally backed up.
    /// </summary>
    public static bool WriteFileSafely(string destinationPath, byte[] content, bool createBackup, out string? backupPath, out string? error)
    {
        backupPath = null; error = null;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            var temp = destinationPath + ".tmp";
            File.WriteAllBytes(temp, content);

            if (File.Exists(destinationPath))
            {
                if (createBackup)
                {
                    backupPath = destinationPath + ".bak";
                    // Use File.Replace for atomic swap when available.
                    File.Replace(temp, destinationPath, backupPath, ignoreMetadataErrors: true);
                }
                else
                {
                    // Delete and move in a controlled way.
                    RemoveReadOnly(destinationPath);
                    File.Delete(destinationPath);
                    File.Move(temp, destinationPath);
                }
            }
            else
            {
                File.Move(temp, destinationPath);
            }
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            // Try to clean temp file
            try { var tmp = destinationPath + ".tmp"; if (File.Exists(tmp)) File.Delete(tmp); } catch { }
            return false;
        }
    }

    /// <summary>
    /// Replace destination with source atomically when possible.
    /// </summary>
    public static bool ReplaceFile(string sourcePath, string destinationPath, bool createBackup, out string? backupPath, out string? error)
    {
        backupPath = null; error = null;
        try
        {
            if (!File.Exists(sourcePath)) { error = "Source file not found."; return false; }
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            var temp = destinationPath + ".tmpcopy";
            File.Copy(sourcePath, temp, overwrite: true);

            if (File.Exists(destinationPath))
            {
                if (createBackup)
                {
                    backupPath = destinationPath + ".bak";
                    File.Replace(temp, destinationPath, backupPath, ignoreMetadataErrors: true);
                }
                else
                {
                    RemoveReadOnly(destinationPath);
                    File.Delete(destinationPath);
                    File.Move(temp, destinationPath);
                }
            }
            else
            {
                File.Move(temp, destinationPath);
            }
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message; return false;
        }
    }

    /// <summary>
    /// Deletes a file if it exists, removing read-only attribute and swallowing non-critical errors.
    /// </summary>
    public static bool DeleteFileSafe(string path, out string? error)
    {
        error = null;
        try
        {
            if (!File.Exists(path)) return true;
            RemoveReadOnly(path);
            File.Delete(path);
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message; return false;
        }
    }

    /// <summary>
    /// Deletes a set of files; returns count deleted.
    /// </summary>
    public static int CleanObsoleteFiles(IEnumerable<string> filePaths, out List<(string Path, string Error)> failures)
    {
        failures = new List<(string, string)>();
        int deleted = 0;
        foreach (var p in filePaths.Distinct())
        {
            if (DeleteFileSafe(p, out var err)) deleted++;
            else failures.Add((p, err ?? "Unknown error"));
        }
        return deleted;
    }

    private static void RemoveReadOnly(string path)
    {
        try
        {
            var attr = File.GetAttributes(path);
            if ((attr & FileAttributes.ReadOnly) != 0)
            {
                File.SetAttributes(path, attr & ~FileAttributes.ReadOnly);
            }
        }
        catch { }
    }
}
