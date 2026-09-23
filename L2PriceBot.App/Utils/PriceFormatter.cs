using System.Globalization;

namespace L2PriceBot.App.Utils;

public static class PriceFormatter
{
    public static string FormatDC(string price)
    {
        return $"{price} DC";
    }

    public static string ParseDC(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return "0";
        
        // Split by common range separators if present
        if (input.Contains("/"))
        {
            var parts = input.Split('/').Select(p => FormatSingle(p.Trim()));
            return string.Join(" / ", parts);
        }
        if (input.Contains("-"))
        {
            var parts = input.Split('-').Select(p => FormatSingle(p.Trim()));
            return string.Join(" - ", parts);
        }
        
        return FormatSingle(input);
    }
    
    public static int ParseDCToInteger(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return 0;
        
        // Handle fraction format like "7/8"
        if (input.Contains("/"))
        {
            var parts = input.Split('/');
            if (parts.Length == 2 && decimal.TryParse(parts[0].Trim(), out decimal numerator) && 
                decimal.TryParse(parts[1].Trim(), out decimal denominator))
            {
                // Calculate the average value
                double average = (numerator + denominator) / 2.0;
                return (int)Math.Round(average);
            }
            // If parsing fails, return as string format
            return int.Parse(FormatSingle(input));
        }
        
        // For regular numeric values
        var formatted = FormatSingle(input);
        if (int.TryParse(formatted, out int result))
        {
            return result;
        }
        
        // If we get here, try to parse as decimal and round to integer
        if (decimal.TryParse(formatted, out decimal decimalResult))
        {
            return (int)Math.Round(decimalResult);
        }
        
        return 0;
    }
    
    private static string FormatSingle(string input)
    {
        input = input.ToLower().Replace("dc", "").Trim();
        
        decimal multiplier = 1;
        if (input.EndsWith("k"))
        {
            multiplier = 1000;
            input = input.Substring(0, input.Length - 1);
        }
        else if (input.EndsWith("m"))
        {
            multiplier = 1000000;
            input = input.Substring(0, input.Length - 1);
        }

        input = input.Replace(".", ",").Replace(",,", ","); 
        if (input.Contains(",") && !input.EndsWith("k") && multiplier == 1) 
        {
            var parts = input.Split(',');
            if (parts.Length == 2 && parts[1].Length == 3)
            {
               input = input.Replace(",", "");
            }
        }

        if (decimal.TryParse(input, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal value))
        {
            return ((int)(value * multiplier)).ToString("N0", new CultureInfo("es-AR"));
        }
        
        if (decimal.TryParse(input, NumberStyles.Any, new CultureInfo("es-AR"), out decimal argValue))
        {
            return ((int)(argValue * multiplier)).ToString("N0", new CultureInfo("es-AR"));
        }

        // Si es texto libre, simplemente lo devolvemos
        return input;
    }
}
