using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmallBusiness.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddQuoteCustomerContactSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CustomerEmailSnapshot",
                table: "Quotes",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomerNumberSnapshot",
                table: "Quotes",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CustomerPhoneSnapshot",
                table: "Quotes",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CustomerEmailSnapshot",
                table: "Quotes");

            migrationBuilder.DropColumn(
                name: "CustomerNumberSnapshot",
                table: "Quotes");

            migrationBuilder.DropColumn(
                name: "CustomerPhoneSnapshot",
                table: "Quotes");
        }
    }
}
