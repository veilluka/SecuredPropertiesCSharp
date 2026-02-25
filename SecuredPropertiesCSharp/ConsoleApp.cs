using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SecuredPropertiesCSharp
{
    public class ConsoleApp
    {
        public static void Run(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("No input. Use -help for help");
                Console.WriteLine("Secured-Properties Console Tool");
                Console.WriteLine();
                return;
            }

            var parser = new ConsoleParser();
            parser.Parse(args);

            try
            {
                // Help command
                if (parser.HasOption("help"))
                {
                    ConsoleParser.PrintHelp();
                    return;
                }

                // Generate password command
                if (parser.HasOption("generatePassword"))
                {
                    var password = new Enc().GeneratePassword();
                    Console.WriteLine(password);
                    return;
                }

                // Print command
                if (parser.HasOption("print"))
                {
                    PrintStorage(parser);
                    return;
                }

                // Create command
                if (parser.HasOption("create"))
                {
                    CreateStorage(parser);
                    return;
                }

                // Add secured property
                if (parser.HasOption("addSecured"))
                {
                    AddProperty(parser, secured: true);
                    return;
                }

                // Add unsecured property
                if (parser.HasOption("addUnsecured"))
                {
                    AddProperty(parser, secured: false);
                    return;
                }

                // Get value
                if (parser.HasOption("getValue"))
                {
                    GetPropertyValue(parser);
                    return;
                }

                // Delete property
                if (parser.HasOption("delete"))
                {
                    DeleteProperty(parser);
                    return;
                }

                // Add property to all files
                if (parser.HasOption("add"))
                {
                    AddPropertyToAllFiles(parser);
                    return;
                }

                Console.WriteLine("Unknown command. Use -help for usage information.");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"ERROR: {ex.Message}");
                Environment.Exit(1);
            }
        }

        private static void PrintStorage(ConsoleParser parser)
        {
            var fileName = parser.GetOptionValue("print");
            if (fileName == null)
            {
                throw new Exception("File name not provided");
            }

            Console.WriteLine();
            Console.WriteLine($"Printing content of the file: {fileName}");
            Console.WriteLine();

            SecStorage? secStorage = null;

            try
            {
                secStorage = SecStorage.OpenSecuredStorage(fileName, openSecured: false);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"Cannot print: {e.Message}");
                return;
            }

            var allLabels = secStorage.GetAllLabels();

            if (allLabels.Count == 0)
            {
                Console.WriteLine("(No properties found)");
                return;
            }

            foreach (var label in allLabels.OrderBy(x => x))
            {
                var map = secStorage.GetAllPropertiesAsMap(label);
                
                if (map.Count == 0)
                    continue;

                Console.WriteLine($"───────────────── {label} ─────────────────");
                
                foreach (var key in map.Keys.OrderBy(x => x))
                {
                    var prop = secStorage.GetProperty(string.IsNullOrEmpty(label) ? key : $"{label}@@{key}");
                    var marker = prop?.IsEncrypted == true ? "[ENCRYPTED]" : "[PLAIN]    ";
                    Console.WriteLine($"  {marker} {key} = {map[key]}");
                }
                Console.WriteLine();
            }

            Console.WriteLine("──────────────────────────────────────────────");
        }

        private static void CreateStorage(ConsoleParser parser)
        {
            var fileName = parser.GetOptionValue("create");
            if (fileName == null)
            {
                throw new Exception("File name not provided");
            }

            SecureString? pass = null;
            var secured = true;

            if (parser.HasOption("pass"))
            {
                pass = new SecureString(parser.GetOptionValue("pass"));
                Console.WriteLine("INFO: Using provided password.");
                Console.WriteLine("      Avoid using special characters like &, $, ;, | in passwords");
                Console.WriteLine("      when scripting, or use longer passwords (20+ characters).");
            }

            if (parser.HasFlag("unsecured"))
            {
                secured = false;
            }

            if (secured && pass == null)
            {
                pass = new SecureString(new Enc().GeneratePassword());
                Console.WriteLine($"Using random password: {pass}");
            }

            SecStorage.CreateNewSecureStorage(fileName, pass, secured);
            Console.WriteLine($"Storage created: {fileName}");
            
            if (secured && pass != null)
            {
                Console.WriteLine("IMPORTANT: Save this password securely. You will need it to access encrypted properties.");
            }
        }

        private static void AddProperty(ConsoleParser parser, bool secured)
        {
            var key = parser.GetOptionValue("key");
            if (key == null)
            {
                throw new Exception("Property key not provided (use -key <value>)");
            }

            var val = parser.GetOptionValue("value");
            if (val == null)
            {
                Console.WriteLine("INFO: Value not provided, generating random value");
                val = new Enc().GeneratePassword();
                Console.WriteLine($"Generated value: {val}");
            }

            var secureString = new SecureString(val);
            var fileName = parser.GetOptionValue(secured ? "addSecured" : "addUnsecured");
            
            if (fileName == null)
            {
                throw new Exception("File name not provided");
            }

            SecureString? pass = null;
            if (parser.HasOption("pass"))
            {
                pass = new SecureString(parser.GetOptionValue("pass"));
            }

            SecStorage? secStorage = null;

            if (pass != null && pass.Value != null)
            {
                secStorage = SecStorage.OpenSecuredStorage(fileName, pass);
            }
            else
            {
                secStorage = SecStorage.OpenSecuredStorage(fileName, openSecured: secured);
            }

            if (secured)
            {
                secStorage.AddSecuredProperty(key, secureString);
                Console.WriteLine($"Added encrypted property: {key}");
            }
            else
            {
                secStorage.AddUnsecuredProperty(key, secureString.ToString()!);
                Console.WriteLine($"Added unencrypted property: {key}");
            }

            SecStorage.Destroy();
        }

        private static void GetPropertyValue(ConsoleParser parser)
        {
            var key = parser.GetOptionValue("key");
            if (key == null)
            {
                throw new Exception("Property key not provided (use -key <value>)");
            }

            var fileName = parser.GetOptionValue("getValue");
            if (fileName == null)
            {
                throw new Exception("File name not provided");
            }

            SecureString? pass = null;
            if (parser.HasOption("pass"))
            {
                pass = new SecureString(parser.GetOptionValue("pass"));
            }

            SecStorage? secStorage = null;
            var secured = true;

            if (parser.HasFlag("unsecured"))
            {
                secured = false;
            }

            if (pass != null && pass.Value != null)
            {
                secStorage = SecStorage.OpenSecuredStorage(fileName, pass);
            }
            else
            {
                secStorage = SecStorage.OpenSecuredStorage(fileName, openSecured: secured);
            }

            if (secStorage != null)
            {
                var value = secStorage.GetPropertyValue(key);
                if (value != null && value.Value != null)
                {
                    Console.WriteLine(value);
                    value.DestroyValue();
                }
                else
                {
                    Console.WriteLine($"Property '{key}' not found");
                }
            }

            SecStorage.Destroy();
        }

        private static void DeleteProperty(ConsoleParser parser)
        {
            var key = parser.GetOptionValue("key");
            if (key == null)
            {
                throw new Exception("Property key not provided (use -key <value>)");
            }

            var fileName = parser.GetOptionValue("delete");
            if (fileName == null)
            {
                throw new Exception("File name not provided");
            }

            SecureString? pass = null;
            if (parser.HasOption("pass"))
            {
                pass = new SecureString(parser.GetOptionValue("pass"));
            }

            SecStorage? secStorage = null;

            if (pass != null && pass.Value != null)
            {
                secStorage = SecStorage.OpenSecuredStorage(fileName, pass);
            }
            else
            {
                secStorage = SecStorage.OpenSecuredStorage(fileName, openSecured: false);
            }

            var props = secStorage.GetAllProperties(key);
            
            if (props.Count == 0)
            {
                Console.WriteLine($"No properties found matching: {key}");
            }
            else
            {
                foreach (var secureProperty in props)
                {
                    var propKey = SecureProperty.CreateKeyWithSeparator(secureProperty.Key);
                    Console.WriteLine($"Deleting property: {propKey}");
                    secStorage.DeleteProperty(propKey);
                }
                Console.WriteLine($"Deleted {props.Count} property(ies)");
            }

            SecStorage.Destroy();
        }

        private static void AddPropertyToAllFiles(ConsoleParser parser)
        {
            var key = parser.GetOptionValue("key");
            if (key == null)
            {
                throw new Exception("Property key not provided (use -key <value>)");
            }

            var val = parser.GetOptionValue("value");
            if (val == null)
            {
                throw new Exception("Property value not provided (use -value <value>)");
            }

            // Get password if provided
            SecureString? pass = null;
            if (parser.HasOption("pass"))
            {
                pass = new SecureString(parser.GetOptionValue("pass"));
            }

            // Find all .properties files in current directory
            var currentDir = Directory.GetCurrentDirectory();
            var propertyFiles = Directory.GetFiles(currentDir, "*.properties");

            if (propertyFiles.Length == 0)
            {
                Console.WriteLine("No .properties files found in current directory");
                return;
            }

            Console.WriteLine($"Found {propertyFiles.Length} .properties file(s)");
            Console.WriteLine();

            int successCount = 0;
            int skipCount = 0;
            int errorCount = 0;

            foreach (var fileName in propertyFiles)
            {
                var fileShortName = Path.GetFileName(fileName);
                
                try
                {
                    // Check if file has valid header (is a secured properties file)
                    if (!IsValidPropertiesFile(fileName))
                    {
                        Console.WriteLine($"[SKIP] {fileShortName} - Not a valid secured properties file");
                        skipCount++;
                        continue;
                    }

                    // Try to open the file
                    SecStorage? secStorage = null;
                    
                    if (pass != null && pass.Value != null)
                    {
                        // Try with password
                        try
                        {
                            secStorage = SecStorage.OpenSecuredStorage(fileName, pass);
                        }
                        catch
                        {
                            // If password fails, try Windows DPAPI
                            if (SecStorage.IsWindowsSecured(fileName))
                            {
                                try
                                {
                                    secStorage = SecStorage.OpenSecuredStorageWithWindows(fileName);
                                }
                                catch
                                {
                                    throw new Exception("Cannot open with password or Windows DPAPI");
                                }
                            }
                            else
                            {
                                throw;
                            }
                        }
                    }
                    else if (SecStorage.IsWindowsSecured(fileName))
                    {
                        // Try Windows DPAPI
                        secStorage = SecStorage.OpenSecuredStorageWithWindows(fileName);
                    }
                    else
                    {
                        // File needs password but none provided
                        Console.WriteLine($"[SKIP] {fileShortName} - Password required but not provided");
                        skipCount++;
                        SecStorage.Destroy();
                        continue;
                    }

                    // Add the property (secured by default)
                    var secureString = new SecureString(val);
                    secStorage.AddSecuredProperty(key, secureString);
                    Console.WriteLine($"[OK] {fileShortName} - Added encrypted property '{key}'");
                    successCount++;
                    
                    SecStorage.Destroy();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] {fileShortName} - {ex.Message}");
                    errorCount++;
                    SecStorage.Destroy();
                }
            }

            Console.WriteLine();
            Console.WriteLine($"Summary: {successCount} succeeded, {skipCount} skipped, {errorCount} failed");
        }

        private static bool IsValidPropertiesFile(string fileName)
        {
            try
            {
                if (!File.Exists(fileName))
                    return false;

                var lines = File.ReadAllLines(fileName);
                // Check if file has the header markers
                return lines.Any(line => line.Contains("@@HEADER_START@@")) &&
                       lines.Any(line => line.Contains("@@HEADER_END@@"));
            }
            catch
            {
                return false;
            }
        }
    }
}
