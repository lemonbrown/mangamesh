using System;
using System.Security.Cryptography;

public class Program
{
    public static void Main()
    {
        var bootstrapPubKey = "AwimzB+Lesq9GMNs27W2FGC2+aNK0O/ymZHPr9fRyiE=";
        var gatewayPubKey = "SCwPpOukKLuCdL+D2ut99JPG6vr7MKBEJFYfhTQVxqo=";

        Console.WriteLine("Bootstrap Node ID: " + CalculateNodeId(bootstrapPubKey));
        Console.WriteLine("Gateway Node ID: " + CalculateNodeId(gatewayPubKey));
    }

    static string CalculateNodeId(string base64PubKey)
    {
        var bytes = Convert.FromBase64String(base64PubKey);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
