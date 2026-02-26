using System.Globalization;
using DynamicCrudSample.Models;

namespace DynamicCrudSample.Services;

public interface IValueConverter
{
    bool TryConvert(string? input, ColumnDefinition column, out object? value, out string? error);
}

public class ValueConverter : IValueConverter
{
    public bool TryConvert(string? input, ColumnDefinition column, out object? value, out string? error)
    {
        value = null;
        error = null;

        if (string.IsNullOrWhiteSpace(input))
        {
            if (column.Required && !column.Identity)
            {
                error = "Required";
                return false;
            }

            return true;
        }

        switch (column.Type.ToLowerInvariant())
        {
            case "int":
                if (int.TryParse(input, out var i))
                {
                    value = i;
                    return true;
                }

                error = "Invalid integer";
                return false;

            case "long":
                if (long.TryParse(input, out var l))
                {
                    value = l;
                    return true;
                }

                error = "Invalid long";
                return false;

            case "decimal":
                if (decimal.TryParse(input, NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
                {
                    value = d;
                    return true;
                }

                error = "Invalid decimal";
                return false;

            case "double":
                if (double.TryParse(input, NumberStyles.Any, CultureInfo.InvariantCulture, out var db))
                {
                    value = db;
                    return true;
                }

                error = "Invalid double";
                return false;

            case "datetime":
            case "date":
                if (DateTime.TryParse(input, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                {
                    value = dt;
                    return true;
                }

                error = "Invalid date";
                return false;

            case "bool":
                if (bool.TryParse(input, out var b))
                {
                    value = b;
                    return true;
                }

                if (input == "on")
                {
                    value = true;
                    return true;
                }

                error = "Invalid boolean";
                return false;

            default:
                value = input;
                return true;
        }
    }
}
