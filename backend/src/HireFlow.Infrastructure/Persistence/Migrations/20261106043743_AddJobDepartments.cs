using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HireFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddJobDepartments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "job_departments",
                schema: "hireflow",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    JobId = table.Column<Guid>(type: "uuid", nullable: false),
                    DepartmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_job_departments", x => x.Id);
                    table.UniqueConstraint("AK_job_departments_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.ForeignKey(
                        name: "FK_job_departments_departments_OrganizationId_DepartmentId",
                        columns: x => new { x.OrganizationId, x.DepartmentId },
                        principalSchema: "hireflow",
                        principalTable: "departments",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_job_departments_jobs_OrganizationId_JobId",
                        columns: x => new { x.OrganizationId, x.JobId },
                        principalSchema: "hireflow",
                        principalTable: "jobs",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_job_departments_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "hireflow",
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_job_departments_OrganizationId",
                schema: "hireflow",
                table: "job_departments",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_job_departments_OrganizationId_DepartmentId",
                schema: "hireflow",
                table: "job_departments",
                columns: new[] { "OrganizationId", "DepartmentId" });

            migrationBuilder.CreateIndex(
                name: "IX_job_departments_OrganizationId_JobId_DepartmentId",
                schema: "hireflow",
                table: "job_departments",
                columns: new[] { "OrganizationId", "JobId", "DepartmentId" },
                unique: true);

            migrationBuilder.Sql("""
                INSERT INTO hireflow.job_departments ("Id", "JobId", "DepartmentId", "CreatedAt", "UpdatedAt", "CreatedBy", "OrganizationId")
                SELECT gen_random_uuid(), "Id", "DepartmentId", CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, "CreatedBy", "OrganizationId"
                FROM hireflow.jobs;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "job_departments",
                schema: "hireflow");
        }
    }
}
