using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hostr.Api.Migrations
{
    /// <inheritdoc />
    public partial class CreateTestAdminUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Create test admin user for panoramaview tenant
            // Password: "Admin123!" (hashed)
            migrationBuilder.Sql(@"
                INSERT INTO ""AspNetUsers"" (""Id"", ""UserName"", ""NormalizedUserName"", ""Email"", ""NormalizedEmail"", ""EmailConfirmed"", ""PasswordHash"", ""SecurityStamp"", ""ConcurrencyStamp"", ""PhoneNumber"", ""PhoneNumberConfirmed"", ""TwoFactorEnabled"", ""LockoutEnd"", ""LockoutEnabled"", ""AccessFailedCount"", ""IsActive"", ""CreatedAt"")
                VALUES (
                    999,
                    'admin@panoramaview.com',
                    'ADMIN@PANORAMAVIEW.COM',
                    'admin@panoramaview.com',
                    'ADMIN@PANORAMAVIEW.COM',
                    true,
                    'AQAAAAIAAYagAAAAEJYb8n3j8e7l4R3D5wQ9r3L8UGF6qX4fK3b7t2rN9mY8a4L3nM1pQ5W2j6kE9s0f7G==',
                    'ABCDEFGHIJKLMNOP',
                    '12345678-1234-1234-1234-123456789012',
                    null,
                    false,
                    false,
                    null,
                    true,
                    0,
                    true,
                    NOW()
                );");

            // Associate the test admin user with the panoramaview tenant as Admin
            migrationBuilder.Sql(@"
                INSERT INTO ""UserTenants"" (""UserId"", ""TenantId"", ""Role"")
                SELECT 999, ""Id"", 'Admin'
                FROM ""Tenants""
                WHERE ""Slug"" = 'panoramaview';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remove the test admin user and their tenant association
            migrationBuilder.Sql(@"DELETE FROM ""UserTenants"" WHERE ""UserId"" = 999;");
            migrationBuilder.Sql(@"DELETE FROM ""AspNetUsers"" WHERE ""Id"" = 999;");
        }
    }
}
