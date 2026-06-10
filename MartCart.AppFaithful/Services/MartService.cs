using System.Text.Json;

namespace MartCart.AppFaithful.Services;

public record Mart(string Id, string Name, int DefaultThreshold, int DefaultDiscount = 0);

public static class MartService
{
    private const string Key = "martcart.marts.v1";
    private const string SeededKey = "martcart.marts.seeded";

    public static List<Mart> GetAll()
    {
        EnsureSeeded();
        var json = Preferences.Default.Get(Key, "[]");
        try { return JsonSerializer.Deserialize<List<Mart>>(json) ?? new(); }
        catch { return new(); }
    }

    public static Mart? Get(string id) => GetAll().FirstOrDefault(m => m.Id == id);

    public static Mart? FindByName(string name)
        => GetAll().FirstOrDefault(m => string.Equals(m.Name, name, StringComparison.OrdinalIgnoreCase));

    public static void Add(string name, int threshold, int discount)
    {
        var list = GetAll();
        list.Add(new Mart(Guid.NewGuid().ToString("N"), name, threshold, discount));
        Save(list);
    }

    public static void Update(string id, string name, int threshold, int discount)
    {
        var list = GetAll();
        var idx = list.FindIndex(m => m.Id == id);
        if (idx < 0) return;
        list[idx] = list[idx] with { Name = name, DefaultThreshold = threshold, DefaultDiscount = discount };
        Save(list);
    }

    public static void Delete(string id)
    {
        var list = GetAll();
        list.RemoveAll(m => m.Id == id);
        Save(list);
    }

    private static void Save(List<Mart> list)
        => Preferences.Default.Set(Key, JsonSerializer.Serialize(list));

    private static void EnsureSeeded()
    {
        if (Preferences.Default.Get(SeededKey, false)) return;
        Preferences.Default.Set(SeededKey, true);
        var seed = new List<Mart>
        {
            new(Guid.NewGuid().ToString("N"), "이마트", 50000, 5000),
            new(Guid.NewGuid().ToString("N"), "코스트코", 100000, 10000),
            new(Guid.NewGuid().ToString("N"), "홈플러스", 50000, 5000),
        };
        Save(seed);
    }
}
