namespace Majlis.Rooms.Application;

/// <summary>Detects ar/en for user text: Arabic if most letters are Arabic.</summary>
public static class TextLanguage
{
    public static string Detect(string text)
    {
        int arabic = 0, latin = 0;
        foreach (var c in text)
        {
            if (c is >= '؀' and <= 'ۿ' or >= 'ݐ' and <= 'ݿ' or >= 'ﭐ' and <= '﷿' or >= 'ﹰ' and <= '﻿')
            {
                arabic++;
            }
            else if (char.IsAsciiLetter(c))
            {
                latin++;
            }
        }

        return arabic >= latin ? "ar" : "en";
    }
}
