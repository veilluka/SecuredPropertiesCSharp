using System;
using System.IO;

namespace SecuredPropertiesCSharp
{
    class Program
    {
        static void Main(string[] args)
        {
            // Check if running in console mode with arguments
            if (args.Length > 0)
            {
                ConsoleApp.Run(args);
                return;
            }

            // Otherwise run demo/tests
            Console.WriteLine("═══════════════════════════════════════════════════════════════════════════");
            Console.WriteLine("  SECURED PROPERTIES - Demo Mode");
            Console.WriteLine("  (Run with -help for command line usage)");
            Console.WriteLine("═══════════════════════════════════════════════════════════════════════════");
            Console.WriteLine();

            var enc = new Enc();

// Test salted hash
            Console.WriteLine("=== Encryption Tests ===");
            Console.WriteLine("Salted hash test:");
            Console.WriteLine(enc.GetSaltedHash("justMe".ToCharArray()));
            Console.WriteLine();

            // Test encryption/decryption
            var key = "NoneOfTheAbove+1";
            var input = "Why is noone here";

            Console.WriteLine("Encryption/Decryption test:");
            for (int i = 0; i < 3; i++)
            {
                var encrypted = enc.Encrypt(input, key);
                var decrypted = enc.Decrypt(encrypted, key);
                Console.WriteLine(encrypted);
                Console.WriteLine(decrypted);
            }

            Console.WriteLine();
            Console.WriteLine("Generated password:");
            Console.WriteLine(enc.GeneratePassword());
            Console.WriteLine();

            // Test SecureProperty
            Console.WriteLine("=== SecureProperty Tests ===");
            var prop1 = SecureProperty.CreateNewSecureProperty("app.database.host", "localhost", false);
            var prop2 = SecureProperty.CreateNewSecureProperty("app.database.password", "secret123", true);

            Console.WriteLine($"Property 1: {prop1.GetValueKey()} = {prop1.Value} (Encrypted: {prop1.IsEncrypted})");
            Console.WriteLine($"Property 2: {prop2.GetValueKey()} = {prop2.Value} (Encrypted: {prop2.IsEncrypted})");
            Console.WriteLine();

            // Test SecureProperties
            Console.WriteLine("=== SecureProperties Tests ===");
            var testFile = Path.Combine(Path.GetTempPath(), "test_secure.properties");

            try
            {
                // Clean up if exists
                if (File.Exists(testFile))
                    File.Delete(testFile);

                var props = SecureProperties.CreateSecuredProperties(testFile);
                props.AddStringProperty(new[] { "app", "name" }, "MyApplication", false);
                props.AddStringProperty(new[] { "app", "database", "host" }, "localhost", false);
                props.AddStringProperty(new[] { "app", "database", "port" }, "5432", false);
                props.AddStringProperty(new[] { "app", "database", "password" }, "encrypted_secret", true);
                props.AddBooleanProperty(new[] { "app", "database", "ssl" }, true);
                props.SaveProperties();

                Console.WriteLine($"Created properties file: {testFile}");
                Console.WriteLine();

                // Read it back
                var loadedProps = SecureProperties.OpenSecuredProperties(testFile);
                Console.WriteLine("Loaded properties:");
                
                var allKeys = loadedProps.GetAllKeys();
                foreach (var k in allKeys)
                {
                    var p = loadedProps.GetProperty(k);
                    if (p != null)
                    {
                        var encMarker = p.IsEncrypted ? "[ENC]" : "     ";
                        Console.WriteLine($"  {encMarker} {k} = {p.Value}");
                    }
                }
                
                Console.WriteLine();
                Console.WriteLine("All labels:");
                foreach (var label in loadedProps.GetAllLabels())
                {
                    Console.WriteLine($"  - {label}");
                }

                Console.WriteLine();
                Console.WriteLine("Database properties:");
                var dbProps = loadedProps.GetAllProperties("app.database");
                foreach (var p in dbProps)
                {
                    Console.WriteLine($"  {p.GetValueKey()} = {p.Value}");
                }

                // Clean up
                File.Delete(testFile);
                Console.WriteLine();
                Console.WriteLine("Test completed successfully!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                if (File.Exists(testFile))
                    File.Delete(testFile);
            }

            Console.WriteLine();
            Console.WriteLine("=== SecStorage Tests ===");
            var storageFile = Path.Combine(Path.GetTempPath(), "test_storage.properties");

            try
            {
                // Clean up if exists
                if (File.Exists(storageFile))
                    File.Delete(storageFile);

                // Create a new secured storage
                var masterPass = new SecuredPropertiesCSharp.SecureString("MySecurePassword123!");
                SecStorage.CreateNewSecureStorage(storageFile, masterPass, createSecured: true);
                Console.WriteLine($"Created secured storage: {storageFile}");

                // Open the storage
                var storage = SecStorage.OpenSecuredStorage(storageFile, masterPass);
                Console.WriteLine($"Storage opened. Secure mode: {storage.IsSecureMode}");

                // Add some properties
                storage.AddUnsecuredProperty("app.name", "MyApp");
                storage.AddUnsecuredProperty("app.version", "1.0.0");
                storage.AddSecuredProperty("app.database.password", new SecuredPropertiesCSharp.SecureString("super_secret_db_pass"));
                storage.AddSecuredProperty("app.api.key", new SecuredPropertiesCSharp.SecureString("api_key_12345"));
                Console.WriteLine("Added properties (2 unsecured, 2 secured)");

                // Retrieve properties
                Console.WriteLine();
                Console.WriteLine("Retrieved properties:");
                Console.WriteLine($"  app.name = {storage.GetPropertyStringValue("app.name")}");
                Console.WriteLine($"  app.version = {storage.GetPropertyStringValue("app.version")}");
                Console.WriteLine($"  app.database.password = {storage.GetPropertyValue("app.database.password")}");
                Console.WriteLine($"  app.api.key = {storage.GetPropertyValue("app.api.key")}");

                // List all keys
                Console.WriteLine();
                Console.WriteLine("All keys in storage:");
                foreach (var k in storage.GetAllKeys())
                {
                    var prop = storage.GetProperty(k);
                    var marker = prop?.IsEncrypted == true ? "[SECURED]" : "[PLAIN]  ";
                    Console.WriteLine($"  {marker} {k}");
                }

                // Test password verification
                Console.WriteLine();
                Console.WriteLine($"Password correct check: {SecStorage.IsPasswordCorrect(storageFile, masterPass)}");
                Console.WriteLine($"Is secured: {SecStorage.IsSecured(storageFile)}");

                // Destroy storage
                SecStorage.Destroy();
                Console.WriteLine();
                Console.WriteLine("Storage destroyed");

                // Reopen and verify
                var storage2 = SecStorage.OpenSecuredStorage(storageFile, new SecuredPropertiesCSharp.SecureString("MySecurePassword123!"));
                Console.WriteLine($"Reopened storage successfully");
                Console.WriteLine($"  Retrieved encrypted value: {storage2.GetPropertyValue("app.database.password")}");

                // Clean up
                SecStorage.Destroy();
                File.Delete(storageFile);
                Console.WriteLine();
                Console.WriteLine("SecStorage tests completed successfully!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                if (File.Exists(storageFile))
                    File.Delete(storageFile);
            }
        }
    }
}
