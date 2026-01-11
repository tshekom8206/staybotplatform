using Microsoft.AspNetCore.Identity;

class Program
{
    static void Main(string[] args)
    {
        if (args.Length < 1)
        {
            Console.WriteLine("Usage: dotnet run -- <password>");
            return;
        }
        var password = args[0];
        var hasher = new PasswordHasher<object>();
        var hash = hasher.HashPassword(null!, password);
        Console.WriteLine(hash);
    }
}
