using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Objects.Core.Math;
using CUE4Parse.UE4.Objects.Core.i18N;
using CUE4Parse.UE4.Objects.UObject;

namespace PalworldAtlas.Extractor;

internal sealed class RowReader(FStructFallback row)
{
    private readonly Dictionary<string, FPropertyTag> _properties = row.Properties
        .GroupBy(property => property.Name.Text, StringComparer.OrdinalIgnoreCase)
        .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<string> FieldNames => _properties.Keys.Order(StringComparer.OrdinalIgnoreCase).ToArray();

    private FPropertyTag? Find(params string[] names)
    {
        foreach (var name in names)
            if (_properties.TryGetValue(name, out var property)) return property;
        return null;
    }

    public string String(string fallback, params string[] names)
    {
        var property = Find(names);
        if (property is null) return fallback;
        try
        {
            return property.TagData.Type switch
            {
                "NameProperty" or "EnumProperty" => property.Tag.GetValue<FName>().Text,
                "TextProperty" => property.Tag.GetValue<FText>().Text,
                _ => property.Tag.GetValue<string>() ?? fallback,
            };
        }
        catch
        {
            try { return Convert.ToString(property.Tag.GetValue<object>()) ?? fallback; }
            catch { return fallback; }
        }
    }

    public int Int(int fallback, params string[] names)
    {
        var property = Find(names);
        if (property is null) return fallback;
        foreach (var type in new[] { typeof(int), typeof(short), typeof(byte), typeof(long), typeof(float), typeof(double) })
        {
            try { return Convert.ToInt32(property.Tag.GetValue(type)); }
            catch { }
        }
        return fallback;
    }

    public double Number(double fallback, params string[] names)
    {
        var property = Find(names);
        if (property is null) return fallback;
        foreach (var type in new[] { typeof(double), typeof(float), typeof(int), typeof(long) })
        {
            try { return Convert.ToDouble(property.Tag.GetValue(type)); }
            catch { }
        }
        return fallback;
    }

    public double NumberAt(int index, double fallback, params string[] names)
    {
        var property = Find(names);
        if (property is null || index < 0) return fallback;
        try
        {
            var array = property.Tag.GetValue<UScriptArray>();
            if (index >= array.Properties.Count) return fallback;
            var item = array.Properties[index];
            foreach (var type in new[] { typeof(double), typeof(float), typeof(int), typeof(long), typeof(short), typeof(byte) })
            {
                try { return Convert.ToDouble(item.GetValue(type)); }
                catch { }
            }
        }
        catch { }
        return fallback;
    }

    public bool Bool(bool fallback, params string[] names)
    {
        var property = Find(names);
        if (property is null) return fallback;
        try { return property.Tag.GetValue<bool>(); }
        catch { return fallback; }
    }

    public (double X, double Y)? Coordinates(params string[] names)
    {
        foreach (var name in names)
        {
            var property = Find(name);
            if (property is null) continue;
            try
            {
                var vector = property.Tag.GetValue<FVector>();
                return (vector.X, vector.Y);
            }
            catch { }
            try
            {
                var transform = property.Tag.GetValue<FTransform>();
                return (transform.Translation.X, transform.Translation.Y);
            }
            catch { }
            try
            {
                var nested = property.Tag.GetValue<FStructFallback>();
                var nestedReader = new RowReader(nested);
                var x = nestedReader.Number(double.NaN, "X", "x");
                var y = nestedReader.Number(double.NaN, "Y", "y");
                if (!double.IsNaN(x) && !double.IsNaN(y)) return (x, y);
            }
            catch { }
        }

        var directX = Number(double.NaN, "WorldX", "LocationX", "PosX", "X");
        var directY = Number(double.NaN, "WorldY", "LocationY", "PosY", "Y");
        return double.IsNaN(directX) || double.IsNaN(directY) ? null : (directX, directY);
    }
}
