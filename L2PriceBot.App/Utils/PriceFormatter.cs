using System.Globalization;

namespace L2PriceBot.App.Utils;

public static class PriceFormatter
{
    public static string FormatDC(int price)
    {
        return $"{price.ToString("N0", new CultureInfo("es-AR"))} DC";
    }

    public static int ParseDC(string input)
    {
        input = input.ToLower().Replace("dc", "").Trim();
        
        // Handle k/K suffixes
        decimal multiplier = 1;
        if (input.EndsWith("k"))
        {
            multiplier = 1000;
            input = input.Substring(0, input.Length - 1);
        }

        // Remove possible thousands separators (dot or comma) if interpreted as decimal might fail depending on culture
        input = input.Replace(".", ",").Replace(",,", ","); // Just ensure consistent decimal point for parsing if 'k' is used like 1.5k
        if (input.Contains(",") && !input.EndsWith("k") && multiplier == 1) 
        {
             // If someone inputs 1.500 it might be 1,500. So strip dots/commas if they just want thousands
            var parts = input.Split(',');
            if (parts.Length == 2 && parts[1].Length == 3)
            {
               input = input.Replace(",", "");
            }
        }

        if (decimal.TryParse(input, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal value))
        {
            return (int)(value * multiplier);
        }
        
        // Try parsing with ES-AR culture
        if (decimal.TryParse(input, NumberStyles.Any, new CultureInfo("es-AR"), out decimal argValue))
        {
            return (int)(argValue * multiplier);
        }

        throw new System.ArgumentException("Formato de precio no válido.");
    }
}
