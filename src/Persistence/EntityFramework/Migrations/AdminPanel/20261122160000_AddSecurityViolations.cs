// <copyright file="20261122160000_AddSecurityViolations.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MUnique.OpenMU.Persistence.EntityFramework.Migrations.AdminPanel;

/// <inheritdoc />
public partial class AddSecurityViolations : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "security_violations",
            schema: "admin",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                AccountLogin = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                CharacterName = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                IpAddress = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                HwidHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                Category = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                Severity = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                Detail = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                Sanction = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_security_violations", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_security_violations_CreatedAtUtc_Id",
            schema: "admin",
            table: "security_violations",
            columns: new[] { "CreatedAtUtc", "Id" });

        migrationBuilder.CreateIndex(
            name: "IX_security_violations_AccountLogin",
            schema: "admin",
            table: "security_violations",
            column: "AccountLogin");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "security_violations",
            schema: "admin");
    }
}
