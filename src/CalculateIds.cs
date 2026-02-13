using System;
using System.Security.Cryptography;

public class Program
{
    public static void Main()
    {
        var keys = new[]
        {
            "AwimzB+Lesq9GMNs27W2FGC2+aNK0O/ymZHPr9fRyiE=", // Bootstrap
            "SCwPpOukKLuCdL+D2ut99JPG6vr7MKBEJFYfhTQVxqo=", // Gateway
            "i1G2y3J4k5L6m7N8o9P0q1R2s3T4u5V6w7X8y9Z0a1b="  // Client
        };

        foreach (var key in keys)
        {
            try
            {
                var bytes = Convert.FromBase64String(key);
                using (var sha256 = SHA256.Create())
                {
                    var hash = sha256.ComputeHash(bytes);
                    Console.WriteLine($"Key: {key.Substring(0, 10)}... -> NodeID: {Convert.ToHexString(hash).ToLower()}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing key {key}: {ex.Message}");
            }
        }
        
        // Check if the "odd" ID is a GUID
        var oddId = "cbc20dca8e0a4f2c905bcdf86151ca63";
        Console.WriteLine($"Odd ID length: {oddId.Length}");
    }
}
