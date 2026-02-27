using System;
using System.Collections.Generic;
using System.Linq;

namespace SecuredPropertiesCSharp
{
    public class ConsoleParser
    {
        public Dictionary<string, string> Options { get; private set; }
        public HashSet<string> Flags { get; private set; }

        public ConsoleParser()
        {
            Options = new Dictionary<string, string>();
            Flags = new HashSet<string>();
        }

        public void Parse(string[] args)
        {
            for (int i = 0; i < args.Length; i++)
            {
                var arg = args[i];

                if (arg.StartsWith("-") || arg.StartsWith("--"))
                {
                    var key = arg.TrimStart('-');

                    // Check if next argument exists and is not a flag
                    if (i + 1 < args.Length && !args[i + 1].StartsWith("-"))
                    {
                        Options[key] = args[i + 1];
                        i++; // Skip next argument as it's the value
                    }
                    else
                    {
                        Flags.Add(key);
                    }
                }
            }
        }

        public bool HasOption(string name)
        {
            return Options.ContainsKey(name) || Flags.Contains(name);
        }

        public string? GetOptionValue(string name)
        {
            return Options.TryGetValue(name, out var value) ? value : null;
        }

        public bool HasFlag(string name)
        {
            return Flags.Contains(name);
        }

        public static void PrintHelp()
        {
            Console.WriteLine();
            Console.WriteLine("╔═══════════════════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║              SECURED PROPERTIES - Command Line Tool                      ║");
            Console.WriteLine("╚═══════════════════════════════════════════════════════════════════════════╝");
            Console.WriteLine();
            Console.WriteLine("USAGE:");
            Console.WriteLine("  SecuredPropertiesCSharp [COMMAND] [OPTIONS]");
            Console.WriteLine();
            Console.WriteLine("COMMANDS:");
            Console.WriteLine();
            Console.WriteLine("  Storage Management:");
            Console.WriteLine("    -init <file>             Initialize secured properties (create or open)");
            Console.WriteLine("                             Creates file with auto-generated password if new.");
            Console.WriteLine("                             Opens existing file, handles user re-encryption");
            Console.WriteLine("                             and {ENC} property processing automatically.");
            Console.WriteLine("                             Password saved to master_password_plain_text_store_and_delete.txt");
            Console.WriteLine();
            Console.WriteLine("    -create <file>           Create a new secure storage file");
            Console.WriteLine("                             Use with: -pass <password> (optional, auto-generated if omitted)");
            Console.WriteLine("                                      -unsecured (create without encryption)");
            Console.WriteLine();
            Console.WriteLine("    -print <file>            Display all properties in the storage");
            Console.WriteLine("                             Use with: -pass <password> (if secured)");
            Console.WriteLine();
            Console.WriteLine("  Property Operations:");
            Console.WriteLine("    -addSecured <file>       Add an encrypted property");
            Console.WriteLine("                             Requires: -key <key> -value <value> -pass <password>");
            Console.WriteLine();
            Console.WriteLine("    -addUnsecured <file>     Add an unencrypted property");
            Console.WriteLine("                             Requires: -key <key> -value <value>");
            Console.WriteLine();
            Console.WriteLine("    -getValue <file>         Retrieve a property value");
            Console.WriteLine("                             Requires: -key <key>");
            Console.WriteLine("                             Use with: -pass <password> (if secured)");
            Console.WriteLine();
            Console.WriteLine("    -delete <file>           Delete property(ies) matching key");
            Console.WriteLine("                             Requires: -key <key>");
            Console.WriteLine("                             Use with: -pass <password> (if secured)");
            Console.WriteLine();
            Console.WriteLine("  Utilities:");
            Console.WriteLine("    -generatePassword        Generate a random secure password");
            Console.WriteLine("    -version                 Display version information");
            Console.WriteLine("    -help                    Display this help message");
            Console.WriteLine();
            Console.WriteLine("OPTIONS:");
            Console.WriteLine("    -key <value>             Property key (hierarchical: use . as separator)");
            Console.WriteLine("    -value <value>           Property value");
            Console.WriteLine("    -pass <password>         Master password (min 12 characters for new storage)");
            Console.WriteLine("    -unsecured               Create/open storage without encryption");
            Console.WriteLine("    -verbose                 Enable verbose/debug logging to stderr");
            Console.WriteLine();
            Console.WriteLine("═══════════════════════════════════════════════════════════════════════════");
            Console.WriteLine();
            Console.WriteLine("EXAMPLES:");
            Console.WriteLine();
            Console.WriteLine("  1. Create a secured storage:");
            Console.WriteLine("     SecuredPropertiesCSharp -create myconfig.properties -pass MySecurePass123!");
            Console.WriteLine();
            Console.WriteLine("  2. Create an unsecured storage:");
            Console.WriteLine("     SecuredPropertiesCSharp -create myconfig.properties -unsecured");
            Console.WriteLine();
            Console.WriteLine("  3. Add an encrypted property:");
            Console.WriteLine("     SecuredPropertiesCSharp -addSecured myconfig.properties \\");
            Console.WriteLine("       -key app.database.password -value secret123 -pass MySecurePass123!");
            Console.WriteLine();
            Console.WriteLine("  4. Add an unencrypted property:");
            Console.WriteLine("     SecuredPropertiesCSharp -addUnsecured myconfig.properties \\");
            Console.WriteLine("       -key app.name -value MyApplication");
            Console.WriteLine();
            Console.WriteLine("  5. Get a property value:");
            Console.WriteLine("     SecuredPropertiesCSharp -getValue myconfig.properties \\");
            Console.WriteLine("       -key app.database.password -pass MySecurePass123!");
            Console.WriteLine();
            Console.WriteLine("  6. Print all properties:");
            Console.WriteLine("     SecuredPropertiesCSharp -print myconfig.properties -pass MySecurePass123!");
            Console.WriteLine();
            Console.WriteLine("  7. Delete a property:");
            Console.WriteLine("     SecuredPropertiesCSharp -delete myconfig.properties \\");
            Console.WriteLine("       -key app.database.password -pass MySecurePass123!");
            Console.WriteLine();
            Console.WriteLine("  8. Generate a random password:");
            Console.WriteLine("     SecuredPropertiesCSharp -generatePassword");
            Console.WriteLine();
            Console.WriteLine("  9. Initialize secured properties (create or open):");
            Console.WriteLine("     SecuredPropertiesCSharp -init myconfig.properties");
            Console.WriteLine();
            Console.WriteLine("═══════════════════════════════════════════════════════════════════════════");
            Console.WriteLine();
            Console.WriteLine("NOTES:");
            Console.WriteLine("  • Hierarchical keys use . as separator (e.g., app.database.host)");
            Console.WriteLine("  • Secured storage requires a master password of at least 12 characters");
            Console.WriteLine("  • Encrypted properties are marked with {ENC} in the properties file");
            Console.WriteLine("  • If no value is provided for addSecured, a random password is generated");
            Console.WriteLine("  • -init auto-generates password and saves to companion .txt file");
            Console.WriteLine("  • If file was encrypted by another user, add MASTER_PASSWORD=XXX to the file");
            Console.WriteLine();
            Console.WriteLine("═══════════════════════════════════════════════════════════════════════════");
            Console.WriteLine();
        }

        public static void PrintExamples()
        {
            Console.WriteLine();
            Console.WriteLine("QUICK START EXAMPLES:");
            Console.WriteLine();
            Console.WriteLine("1. Create secured storage:");
            Console.WriteLine("   SecuredPropertiesCSharp -create testStorage.properties -pass mySECRET123!");
            Console.WriteLine();
            Console.WriteLine("2. Add unencrypted property:");
            Console.WriteLine("   SecuredPropertiesCSharp -addUnsecured testStorage.properties -key user -value admin");
            Console.WriteLine();
            Console.WriteLine("3. Add encrypted property:");
            Console.WriteLine("   SecuredPropertiesCSharp -addSecured testStorage.properties -key password \\");
            Console.WriteLine("     -value sEcRETString -pass mySECRET123!");
            Console.WriteLine();
            Console.WriteLine("4. View all properties:");
            Console.WriteLine("   SecuredPropertiesCSharp -print testStorage.properties");
            Console.WriteLine();
            Console.WriteLine("5. Get specific value:");
            Console.WriteLine("   SecuredPropertiesCSharp -getValue testStorage.properties -key password \\");
            Console.WriteLine("     -pass mySECRET123!");
            Console.WriteLine();
        }
    }
}
