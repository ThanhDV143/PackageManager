using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PackageManager.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IgnoredFiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RelativePath = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IgnoredFiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Packages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PackageId = table.Column<string>(type: "TEXT", nullable: false, collation: "NOCASE"),
                    DisplayName = table.Column<string>(type: "TEXT", nullable: false),
                    Publisher = table.Column<string>(type: "TEXT", nullable: true),
                    Category = table.Column<string>(type: "TEXT", nullable: true),
                    UnityVersion = table.Column<string>(type: "TEXT", nullable: true),
                    StoreVersion = table.Column<string>(type: "TEXT", nullable: true),
                    ShortDescription = table.Column<string>(type: "TEXT", nullable: true),
                    DescriptionHtml = table.Column<string>(type: "TEXT", nullable: true),
                    AssetStoreUrl = table.Column<string>(type: "TEXT", nullable: true),
                    MainImageUrl = table.Column<string>(type: "TEXT", nullable: true),
                    MediaJson = table.Column<string>(type: "TEXT", nullable: true),
                    MetadataFetchedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Packages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PackageVersions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PackageDbId = table.Column<int>(type: "INTEGER", nullable: false),
                    Version = table.Column<string>(type: "TEXT", nullable: false),
                    FileName = table.Column<string>(type: "TEXT", nullable: false),
                    RelativePath = table.Column<string>(type: "TEXT", nullable: false),
                    Size = table.Column<long>(type: "INTEGER", nullable: false),
                    UnityVersion = table.Column<string>(type: "TEXT", nullable: true),
                    ReleaseNotes = table.Column<string>(type: "TEXT", nullable: true),
                    UploadedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PackageVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PackageVersions_Packages_PackageDbId",
                        column: x => x.PackageDbId,
                        principalTable: "Packages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IgnoredFiles_RelativePath",
                table: "IgnoredFiles",
                column: "RelativePath",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Packages_PackageId",
                table: "Packages",
                column: "PackageId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PackageVersions_PackageDbId",
                table: "PackageVersions",
                column: "PackageDbId");

            migrationBuilder.CreateIndex(
                name: "IX_PackageVersions_RelativePath",
                table: "PackageVersions",
                column: "RelativePath",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IgnoredFiles");

            migrationBuilder.DropTable(
                name: "PackageVersions");

            migrationBuilder.DropTable(
                name: "Packages");
        }
    }
}
