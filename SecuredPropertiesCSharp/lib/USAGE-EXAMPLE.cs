using SecuredPropertiesCSharp;

// Example 1: Basic encryption/decryption
var enc = new Enc();
var encrypted = enc.Encrypt("Hello, World!", "MyPassword123!");
var decrypted = enc.Decrypt(encrypted, "MyPassword123!");
Console.WriteLine($"Encrypted: {encrypted}");
Console.WriteLine($"Decrypted: {decrypted}");

// Example 2: Password generation
var password = enc.GeneratePassword(lowerCase: 6, upperCase: 8, numbers: 10, symbols: 6);
Console.WriteLine($"Generated password: {password}");

// Example 3: SecureStorage
var masterPassword = new SecureString("YourSecurePassword123!");
SecStorage.CreateNewSecureStorage("myconfig.properties", masterPassword, createSecured: true);

var storage = SecStorage.OpenSecuredStorage("myconfig.properties", masterPassword);
storage.AddUnsecuredProperty("app.name", "MyApp");
storage.AddSecuredProperty("app.api.key", new SecureString("secret-key-123"));

var apiKey = storage.GetPropertyValue("app.api.key");
Console.WriteLine($"API Key: {apiKey}");

SecStorage.Destroy();

// Example 4: SecureProperties (lower level)
var props = SecureProperties.CreateSecuredProperties("test.properties");
props.AddStringProperty(new[] { "database", "host" }, "localhost", encrypted: false);
props.AddStringProperty(new[] { "database", "password" }, "dbpass123", encrypted: true);
props.SaveProperties();

// Example 5: Password hashing
var hashedPassword = enc.GetSaltedHash("mypassword".ToCharArray());
Console.WriteLine($"Hashed: {hashedPassword}");

bool isValid = enc.Check("mypassword", hashedPassword);
Console.WriteLine($"Password valid: {isValid}");
