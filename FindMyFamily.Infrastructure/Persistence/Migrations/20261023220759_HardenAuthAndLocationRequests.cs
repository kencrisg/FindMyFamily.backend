using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FindMyFamily.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HardenAuthAndLocationRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_devices_user_id",
                table: "devices");

            migrationBuilder.DropColumn(
                name: "refresh_token",
                table: "devices");

            migrationBuilder.AddColumn<string>(
                name: "installation_id",
                table: "devices",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "refresh_token_hash",
                table: "devices",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "location_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    requester_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    family_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    fulfilled_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    location_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_location_requests", x => x.id);
                    table.ForeignKey(
                        name: "FK_location_requests_families_family_id",
                        column: x => x.family_id,
                        principalTable: "families",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_location_requests_users_requester_id",
                        column: x => x.requester_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_location_requests_users_target_user_id",
                        column: x => x.target_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_devices_refresh_token_hash",
                table: "devices",
                column: "refresh_token_hash");

            migrationBuilder.CreateIndex(
                name: "IX_devices_user_id_installation_id",
                table: "devices",
                columns: new[] { "user_id", "installation_id" },
                unique: true,
                filter: "installation_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_location_requests_family_id",
                table: "location_requests",
                column: "family_id");

            migrationBuilder.CreateIndex(
                name: "IX_location_requests_requester_id_target_user_id_created_at",
                table: "location_requests",
                columns: new[] { "requester_id", "target_user_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_location_requests_target_user_id_fulfilled_at_expires_at",
                table: "location_requests",
                columns: new[] { "target_user_id", "fulfilled_at", "expires_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "location_requests");

            migrationBuilder.DropIndex(
                name: "IX_devices_refresh_token_hash",
                table: "devices");

            migrationBuilder.DropIndex(
                name: "IX_devices_user_id_installation_id",
                table: "devices");

            migrationBuilder.DropColumn(
                name: "installation_id",
                table: "devices");

            migrationBuilder.DropColumn(
                name: "refresh_token_hash",
                table: "devices");

            migrationBuilder.AddColumn<string>(
                name: "refresh_token",
                table: "devices",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_devices_user_id",
                table: "devices",
                column: "user_id");
        }
    }
}
