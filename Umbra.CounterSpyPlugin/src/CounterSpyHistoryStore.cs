using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Umbra.Common;

namespace Umbra.CounterSpyPlugin;

[Service]
internal sealed class CounterSpyHistoryStore
{
    private const int MaxEntries = 10;

    private readonly string _path = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "XIVLauncher", "pluginConfigs", "Umbra.CounterSpy", "history.json"
    );

    private readonly List<TargetHistoryEntry> _entries = [];
    private readonly object                   _lock    = new();

    public CounterSpyHistoryStore()
    {
        Load();
    }

    public List<TargetHistoryEntry> GetAll()
    {
        lock (_lock) {
            return _entries.OrderByDescending(e => e.LastSeenUtc).ToList();
        }
    }

    public void Record(string name, string world)
    {
        if (string.IsNullOrWhiteSpace(name)) return;

        lock (_lock) {
            var existing = _entries.FirstOrDefault(e => e.Name == name && e.World == world);
            if (existing is not null) {
                existing.LastSeenUtc = DateTime.UtcNow;
            } else {
                _entries.Add(new TargetHistoryEntry {
                    Name        = name,
                    World       = world,
                    LastSeenUtc = DateTime.UtcNow,
                });
                while (_entries.Count > MaxEntries) {
                    var oldest = _entries.OrderBy(e => e.LastSeenUtc).First();
                    _entries.Remove(oldest);
                }
            }

            Save();
        }
    }

    public void Clear()
    {
        lock (_lock) {
            _entries.Clear();
            Save();
        }
    }

    private void Load()
    {
        try {
            if (!File.Exists(_path)) return;
            var json = File.ReadAllText(_path);
            var list = JsonSerializer.Deserialize<List<TargetHistoryEntry>>(json);
            if (list is not null) {
                _entries.Clear();
                _entries.AddRange(list);
            }
        } catch {
            // Corrupt file — ignore, will be overwritten on next save
        }
    }

    private void Save()
    {
        try {
            var dir = Path.GetDirectoryName(_path);
            if (dir is not null && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(_path, JsonSerializer.Serialize(_entries));
        } catch {
            // Best effort — don't crash the game over persistence
        }
    }
}
