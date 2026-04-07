using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;

/// <summary>
/// Generates a strong-name key file (.snk) for assembly signing.
/// Equivalent to: sn.exe -k TradingSystem.snk
/// </summary>
class GenerateStrongNameKey
{
    static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Usage: GenerateStrongNameKey <output.snk>");
            return 1;
        }

        string outputPath = args[0];

        try
        {
            Console.WriteLine($"Generating strong-name key: {outputPath}");

            // Create RSA key pair (2048-bit)
            using (var rsa = RSA.Create(2048))
            {
                // Export the key pair in SNK format
                var keyPair = rsa.ExportCspBlob(true);

                // Write to file
                File.WriteAllBytes(outputPath, keyPair);
            }

            Console.WriteLine("✅ Strong-name key generated successfully");
            Console.WriteLine($"   File: {Path.GetFullPath(outputPath)}");
            Console.WriteLine($"   Size: {new FileInfo(outputPath).Length} bytes");

            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.Message}");
            return 1;
        }
    }
}
