using System;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using AmfibiaStudio.Models;

namespace AmfibiaStudio.Services;

/// <summary>Сохранение изменений панели между запусками (JSON-файл).</summary>
public interface IStudioStore
{
    StudioState? Load();
    void Save(StudioState state);
    void Reset();
}

public class StudioStore : IStudioStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly string path;

    public StudioStore(string folder)
    {
        Directory.CreateDirectory(folder);
        path = Path.Combine(folder, "studio.json");
    }

    public static StudioStore CreateDefault() =>
        new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AMFIBIA", "Lab_MVVM"));

    public StudioState? Load()
    {
        if (!File.Exists(path)) return null;
        try
        {
            return JsonSerializer.Deserialize<StudioState>(File.ReadAllText(path), Options);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public void Save(StudioState state)
    {
        string temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(state, Options));
        File.Move(temp, path, overwrite: true);
    }

    public void Reset()
    {
        if (File.Exists(path)) File.Delete(path);
    }
}
