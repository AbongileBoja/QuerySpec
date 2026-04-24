using Microsoft.EntityFrameworkCore;

namespace QuerySpec.Samples.WebApi.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Employee> Employees => Set<Employee>();

    public static void Seed(AppDbContext db)
    {
        if (db.Employees.Any()) return;

        db.Employees.AddRange(
            new Employee { Name = "Alice Ndlovu",   Email = "alice@acme.io",   Department = "Engineering", Salary = 95_000,  HireDate = new(2021, 3, 14), IsActive = true,  Country = "ZA" },
            new Employee { Name = "Bongani Khumalo",Email = "bongani@acme.io", Department = "Engineering", Salary = 120_000, HireDate = new(2019, 7,  1), IsActive = true,  Country = "ZA" },
            new Employee { Name = "Carla Rossi",    Email = "carla@acme.io",   Department = "Sales",       Salary = 72_000,  HireDate = new(2022, 1, 10), IsActive = true,  Country = "IT" },
            new Employee { Name = "Dinesh Patel",   Email = "dinesh@acme.io",  Department = "Engineering", Salary = 104_000, HireDate = new(2020, 11, 5), IsActive = false, Country = "IN" },
            new Employee { Name = "Emma Fischer",   Email = "emma@acme.io",    Department = "Finance",     Salary = 88_000,  HireDate = new(2018, 6, 22), IsActive = true,  Country = "DE" },
            new Employee { Name = "Farai Moyo",     Email = "farai@acme.io",   Department = "Sales",       Salary = 65_000,  HireDate = new(2023, 2, 18), IsActive = true,  Country = "ZW" },
            new Employee { Name = "Grace Liu",      Email = "grace@acme.io",   Department = "Engineering", Salary = 135_000, HireDate = new(2017, 9, 30), IsActive = true,  Country = "CN" },
            new Employee { Name = "Hassan Ali",     Email = "hassan@acme.io",  Department = "Finance",     Salary = 78_000,  HireDate = new(2021, 8,  4), IsActive = true,  Country = "EG" }
        );
        db.SaveChanges();
    }
}
