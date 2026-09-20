using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable

namespace AuthService.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AuthDbContext))]
[Migration("20260918090000_AddBlockedUserStatus")]
public partial class AddBlockedUserStatus : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "ck_users_status",
            table: "users");

        migrationBuilder.AddCheckConstraint(
            name: "ck_users_status",
            table: "users",
            sql: "status IN ('Active', 'Blocked', 'Deleted')");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("UPDATE users SET status = 'Active' WHERE status = 'Blocked'");

        migrationBuilder.DropCheckConstraint(
            name: "ck_users_status",
            table: "users");

        migrationBuilder.AddCheckConstraint(
            name: "ck_users_status",
            table: "users",
            sql: "status IN ('Active', 'Deleted')");
    }
}
