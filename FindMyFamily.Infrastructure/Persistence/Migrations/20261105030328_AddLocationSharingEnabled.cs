using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FindMyFamily.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLocationSharingEnabled : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "location_sharing_enabled",
                table: "users",
                type: "boolean",
                nullable: false,
                // Editado a mano: EF ponía false, y todos los usuarios que ya existían habrían
                // dejado de compartir su ubicación sin haberlo elegido.
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "location_sharing_enabled",
                table: "users");
        }
    }
}
