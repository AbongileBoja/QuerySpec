using Microsoft.EntityFrameworkCore;
using QuerySpec.Core.Advanced;
using QuerySpec.Core.Security;
using QuerySpec.EFCore;

using var db = new CustomerDb();
db.Database.EnsureCreated();

// ---------- 1. Field encryption ----------
var key = AesGcmEncryptionProvider.GenerateKey();
var crypto = new AesGcmEncryptionProvider(key);

var encryptedSsn = crypto.Encrypt("123-45-6789");
Console.WriteLine($"[encryption] plaintext  = 123-45-6789");
Console.WriteLine($"[encryption] ciphertext = {encryptedSsn}");
Console.WriteLine($"[encryption] roundtrip  = {crypto.Decrypt(encryptedSsn)}\n");

Seed(db, crypto);

// ---------- 2. Data masking ----------
var masker = new DataMaskingEngine();
masker.RegisterFieldMask("Email", DataMaskingEngine.MaskingStrategy.EmailMask);
masker.RegisterFieldMask("CreditCard", DataMaskingEngine.MaskingStrategy.LastFourOnly);
masker.RegisterFieldMask("PhoneNumber", DataMaskingEngine.MaskingStrategy.PartialMask);
masker.RegisterFieldMask("Ssn", DataMaskingEngine.MaskingStrategy.HashMask);

var sample = db.Customers.First();
Console.WriteLine("[masking] unprivileged view of a customer:");
Console.WriteLine($"  Email       : {masker.Mask("Email", sample.Email)}");
Console.WriteLine($"  CreditCard  : {masker.Mask("CreditCard", sample.CreditCard)}");
Console.WriteLine($"  PhoneNumber : {masker.Mask("PhoneNumber", sample.PhoneNumber)}");
Console.WriteLine($"  Ssn (hash)  : {masker.Mask("Ssn", crypto.Decrypt(sample.SsnEncrypted))}\n");

// ---------- 3. Row-level security ----------
var rls = new RowLevelSecurityEngine();

Func<RowLevelSecurityEngine.RLSContext, System.Linq.Expressions.Expression<Func<Customer, bool>>>
    tenantPredicate = ctx => c => ctx.AllowedTenants.Contains(c.TenantId);

rls.RegisterPolicy(new RowLevelSecurityEngine.RLSPolicy
{
    ResourceType = nameof(Customer),
    PredicateFactory = tenantPredicate
});

var rlsContext = new RowLevelSecurityEngine.RLSContext
{
    UserId = "u-42",
    AllowedTenants = new List<string> { "tenant-a" }
};

var predicate = rls.GetPredicate<Customer>(nameof(Customer), rlsContext)!;

var userFilter = new AdvancedFilterExpression
{
    Field = "Country",
    Operator = FilterOperator.Equal,
    Value = "ZA"
};

var visible = QuerySpecExpressionTranslator
    .ApplyFilter(db.Customers.Where(predicate), userFilter)
    .ToList();

Console.WriteLine($"[rls] user u-42 (tenant-a only) + filter Country=ZA → {visible.Count} row(s):");
foreach (var c in visible)
    Console.WriteLine($"  {c.Id}  {c.Name,-18}  tenant={c.TenantId}  country={c.Country}");
Console.WriteLine();

// ---------- 4. Dynamic permissions ----------
var perms = new DynamicPermissionEvaluator();
perms.RegisterPermission("analyst", DynamicPermissionEvaluator.PermissionType.Read, _ => true);
perms.RegisterPermission("analyst", DynamicPermissionEvaluator.PermissionType.ViewSensitive,
    ctx => ctx.FieldName != "Ssn");
perms.RegisterPermission("admin", DynamicPermissionEvaluator.PermissionType.ViewSensitive, _ => true);

foreach (var role in new[] { "analyst", "admin" })
    foreach (var field in new[] { "Email", "Ssn" })
    {
        var ctx = new DynamicPermissionEvaluator.DynamicContext
        {
            UserId = $"{role}-1",
            Roles = new List<string> { role },
            ResourceType = nameof(Customer),
            FieldName = field,
            AccessTime = DateTime.UtcNow
        };
        var allowed = perms.HasPermission(ctx, DynamicPermissionEvaluator.PermissionType.ViewSensitive);
        Console.WriteLine($"[permissions] role={role,-7} field={field,-5} → {(allowed ? "allow" : "deny")}");
    }

static void Seed(CustomerDb db, IEncryptionProvider crypto)
{
    if (db.Customers.Any()) return;
    db.Customers.AddRange(
        new Customer { Name = "Alice Ndlovu", Email = "alice@acme.io", CreditCard = "4111111111111234", PhoneNumber = "+27821234567", SsnEncrypted = crypto.Encrypt("123-45-6789"), TenantId = "tenant-a", Country = "ZA" },
        new Customer { Name = "Bongani K.", Email = "bongani@acme.io", CreditCard = "4111222233334567", PhoneNumber = "+27829876543", SsnEncrypted = crypto.Encrypt("987-65-4321"), TenantId = "tenant-a", Country = "ZA" },
        new Customer { Name = "Carla Rossi", Email = "carla@acme.io", CreditCard = "5500000000005555", PhoneNumber = "+39055123456", SsnEncrypted = crypto.Encrypt("555-12-3456"), TenantId = "tenant-b", Country = "IT" },
        new Customer { Name = "Dinesh Patel", Email = "dinesh@acme.io", CreditCard = "340000000000009", PhoneNumber = "+911122334455", SsnEncrypted = crypto.Encrypt("111-22-3333"), TenantId = "tenant-b", Country = "IN" }
    );
    db.SaveChanges();
}

public class Customer
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string CreditCard { get; set; } = "";
    public string PhoneNumber { get; set; } = "";
    public string SsnEncrypted { get; set; } = "";
    public string TenantId { get; set; } = "";
    public string Country { get; set; } = "";
}

public class CustomerDb : DbContext
{
    public DbSet<Customer> Customers => Set<Customer>();
    protected override void OnConfiguring(DbContextOptionsBuilder options)
        => options.UseInMemoryDatabase("security-sample");
}
