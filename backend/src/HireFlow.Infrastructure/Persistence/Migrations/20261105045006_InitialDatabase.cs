using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HireFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialDatabase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "hireflow");

            migrationBuilder.CreateTable(
                name: "organizations",
                schema: "hireflow",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Slug = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_organizations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "permissions",
                schema: "hireflow",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_permissions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "skills",
                schema: "hireflow",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    NormalizedName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_skills", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ai_processing_jobs",
                schema: "hireflow",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    JobType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PayloadJson = table.Column<string>(type: "jsonb", nullable: true),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_processing_jobs", x => x.Id);
                    table.UniqueConstraint("AK_ai_processing_jobs_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.ForeignKey(
                        name: "FK_ai_processing_jobs_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "hireflow",
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ai_usage_logs",
                schema: "hireflow",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Provider = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Model = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Operation = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    InputTokens = table.Column<int>(type: "integer", nullable: false),
                    OutputTokens = table.Column<int>(type: "integer", nullable: false),
                    EstimatedCost = table.Column<decimal>(type: "numeric(12,6)", precision: 12, scale: 6, nullable: true),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_usage_logs", x => x.Id);
                    table.UniqueConstraint("AK_ai_usage_logs_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.ForeignKey(
                        name: "FK_ai_usage_logs_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "hireflow",
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "departments",
                schema: "hireflow",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    ParentDepartmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_departments", x => x.Id);
                    table.UniqueConstraint("AK_departments_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.ForeignKey(
                        name: "FK_departments_departments_OrganizationId_ParentDepartmentId",
                        columns: x => new { x.OrganizationId, x.ParentDepartmentId },
                        principalSchema: "hireflow",
                        principalTable: "departments",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_departments_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "hireflow",
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "recruitment_stages",
                schema: "hireflow",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsTerminal = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recruitment_stages", x => x.Id);
                    table.UniqueConstraint("AK_recruitment_stages_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.ForeignKey(
                        name: "FK_recruitment_stages_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "hireflow",
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "roles",
                schema: "hireflow",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsSystemRole = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_roles", x => x.Id);
                    table.UniqueConstraint("AK_roles_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.ForeignKey(
                        name: "FK_roles_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "hireflow",
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "users",
                schema: "hireflow",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    NormalizedEmail = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    PasswordHash = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    FirstName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.Id);
                    table.UniqueConstraint("AK_users_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.ForeignKey(
                        name: "FK_users_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "hireflow",
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "jobs",
                schema: "hireflow",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DepartmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Location = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    EmploymentType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    MinimumExperienceYears = table.Column<int>(type: "integer", nullable: false),
                    MaximumExperienceYears = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ClosedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_jobs", x => x.Id);
                    table.UniqueConstraint("AK_jobs_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.ForeignKey(
                        name: "FK_jobs_departments_OrganizationId_DepartmentId",
                        columns: x => new { x.OrganizationId, x.DepartmentId },
                        principalSchema: "hireflow",
                        principalTable: "departments",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_jobs_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "hireflow",
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "role_permissions",
                schema: "hireflow",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    PermissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_role_permissions", x => x.Id);
                    table.UniqueConstraint("AK_role_permissions_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.ForeignKey(
                        name: "FK_role_permissions_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "hireflow",
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_role_permissions_permissions_PermissionId",
                        column: x => x.PermissionId,
                        principalSchema: "hireflow",
                        principalTable: "permissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_role_permissions_roles_OrganizationId_RoleId",
                        columns: x => new { x.OrganizationId, x.RoleId },
                        principalSchema: "hireflow",
                        principalTable: "roles",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "audit_logs",
                schema: "hireflow",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Action = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    EntityType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: true),
                    DataJson = table.Column<string>(type: "jsonb", nullable: true),
                    IpAddress = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_logs", x => x.Id);
                    table.UniqueConstraint("AK_audit_logs_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.ForeignKey(
                        name: "FK_audit_logs_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "hireflow",
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_audit_logs_users_OrganizationId_ActorUserId",
                        columns: x => new { x.OrganizationId, x.ActorUserId },
                        principalSchema: "hireflow",
                        principalTable: "users",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "candidates",
                schema: "hireflow",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FirstName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    LastName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    PhoneNumber = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Location = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ProfessionalSummary = table.Column<string>(type: "text", nullable: true),
                    TotalExperienceYears = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_candidates", x => x.Id);
                    table.UniqueConstraint("AK_candidates_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.ForeignKey(
                        name: "FK_candidates_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "hireflow",
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_candidates_users_OrganizationId_UserId",
                        columns: x => new { x.OrganizationId, x.UserId },
                        principalSchema: "hireflow",
                        principalTable: "users",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "notifications",
                schema: "hireflow",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    IsRead = table.Column<bool>(type: "boolean", nullable: false),
                    ReadAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DataJson = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notifications", x => x.Id);
                    table.UniqueConstraint("AK_notifications_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.ForeignKey(
                        name: "FK_notifications_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "hireflow",
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_notifications_users_OrganizationId_UserId",
                        columns: x => new { x.OrganizationId, x.UserId },
                        principalSchema: "hireflow",
                        principalTable: "users",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "refresh_tokens",
                schema: "hireflow",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReplacedByTokenHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_refresh_tokens", x => x.Id);
                    table.UniqueConstraint("AK_refresh_tokens_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.ForeignKey(
                        name: "FK_refresh_tokens_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "hireflow",
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_refresh_tokens_users_OrganizationId_UserId",
                        columns: x => new { x.OrganizationId, x.UserId },
                        principalSchema: "hireflow",
                        principalTable: "users",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_roles",
                schema: "hireflow",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_roles", x => x.Id);
                    table.UniqueConstraint("AK_user_roles_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.ForeignKey(
                        name: "FK_user_roles_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "hireflow",
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_roles_roles_OrganizationId_RoleId",
                        columns: x => new { x.OrganizationId, x.RoleId },
                        principalSchema: "hireflow",
                        principalTable: "roles",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_user_roles_users_OrganizationId_UserId",
                        columns: x => new { x.OrganizationId, x.UserId },
                        principalSchema: "hireflow",
                        principalTable: "users",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "job_recruiters",
                schema: "hireflow",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    JobId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecruiterUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_job_recruiters", x => x.Id);
                    table.UniqueConstraint("AK_job_recruiters_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.ForeignKey(
                        name: "FK_job_recruiters_jobs_OrganizationId_JobId",
                        columns: x => new { x.OrganizationId, x.JobId },
                        principalSchema: "hireflow",
                        principalTable: "jobs",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_job_recruiters_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "hireflow",
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_job_recruiters_users_OrganizationId_RecruiterUserId",
                        columns: x => new { x.OrganizationId, x.RecruiterUserId },
                        principalSchema: "hireflow",
                        principalTable: "users",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "job_skills",
                schema: "hireflow",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    JobId = table.Column<Guid>(type: "uuid", nullable: false),
                    SkillId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsRequired = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_job_skills", x => x.Id);
                    table.UniqueConstraint("AK_job_skills_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.ForeignKey(
                        name: "FK_job_skills_jobs_OrganizationId_JobId",
                        columns: x => new { x.OrganizationId, x.JobId },
                        principalSchema: "hireflow",
                        principalTable: "jobs",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_job_skills_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "hireflow",
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_job_skills_skills_SkillId",
                        column: x => x.SkillId,
                        principalSchema: "hireflow",
                        principalTable: "skills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "applications",
                schema: "hireflow",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CandidateId = table.Column<Guid>(type: "uuid", nullable: false),
                    JobId = table.Column<Guid>(type: "uuid", nullable: false),
                    CurrentStageId = table.Column<Guid>(type: "uuid", nullable: false),
                    Source = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    AppliedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_applications", x => x.Id);
                    table.UniqueConstraint("AK_applications_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.ForeignKey(
                        name: "FK_applications_candidates_OrganizationId_CandidateId",
                        columns: x => new { x.OrganizationId, x.CandidateId },
                        principalSchema: "hireflow",
                        principalTable: "candidates",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_applications_jobs_OrganizationId_JobId",
                        columns: x => new { x.OrganizationId, x.JobId },
                        principalSchema: "hireflow",
                        principalTable: "jobs",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_applications_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "hireflow",
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_applications_recruitment_stages_OrganizationId_CurrentStage~",
                        columns: x => new { x.OrganizationId, x.CurrentStageId },
                        principalSchema: "hireflow",
                        principalTable: "recruitment_stages",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "candidate_education",
                schema: "hireflow",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CandidateId = table.Column<Guid>(type: "uuid", nullable: false),
                    Institution = table.Column<string>(type: "text", nullable: false),
                    Degree = table.Column<string>(type: "text", nullable: true),
                    FieldOfStudy = table.Column<string>(type: "text", nullable: true),
                    StartYear = table.Column<int>(type: "integer", nullable: true),
                    EndYear = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_candidate_education", x => x.Id);
                    table.UniqueConstraint("AK_candidate_education_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.ForeignKey(
                        name: "FK_candidate_education_candidates_OrganizationId_CandidateId",
                        columns: x => new { x.OrganizationId, x.CandidateId },
                        principalSchema: "hireflow",
                        principalTable: "candidates",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_candidate_education_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "hireflow",
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "candidate_experience",
                schema: "hireflow",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CandidateId = table.Column<Guid>(type: "uuid", nullable: false),
                    Company = table.Column<string>(type: "text", nullable: false),
                    JobTitle = table.Column<string>(type: "text", nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: true),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    IsCurrent = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_candidate_experience", x => x.Id);
                    table.UniqueConstraint("AK_candidate_experience_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.ForeignKey(
                        name: "FK_candidate_experience_candidates_OrganizationId_CandidateId",
                        columns: x => new { x.OrganizationId, x.CandidateId },
                        principalSchema: "hireflow",
                        principalTable: "candidates",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_candidate_experience_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "hireflow",
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "candidate_match_scores",
                schema: "hireflow",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CandidateId = table.Column<Guid>(type: "uuid", nullable: false),
                    JobId = table.Column<Guid>(type: "uuid", nullable: false),
                    Score = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    MatchedSkillsJson = table.Column<string>(type: "jsonb", nullable: false),
                    MissingSkillsJson = table.Column<string>(type: "jsonb", nullable: false),
                    ExplanationJson = table.Column<string>(type: "jsonb", nullable: false),
                    AlgorithmVersion = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_candidate_match_scores", x => x.Id);
                    table.UniqueConstraint("AK_candidate_match_scores_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.CheckConstraint("ck_candidate_match_score_range", "\"Score\" >= 0 AND \"Score\" <= 100");
                    table.ForeignKey(
                        name: "FK_candidate_match_scores_candidates_OrganizationId_CandidateId",
                        columns: x => new { x.OrganizationId, x.CandidateId },
                        principalSchema: "hireflow",
                        principalTable: "candidates",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_candidate_match_scores_jobs_OrganizationId_JobId",
                        columns: x => new { x.OrganizationId, x.JobId },
                        principalSchema: "hireflow",
                        principalTable: "jobs",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_candidate_match_scores_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "hireflow",
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "candidate_skills",
                schema: "hireflow",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CandidateId = table.Column<Guid>(type: "uuid", nullable: false),
                    SkillId = table.Column<Guid>(type: "uuid", nullable: false),
                    YearsExperience = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_candidate_skills", x => x.Id);
                    table.UniqueConstraint("AK_candidate_skills_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.ForeignKey(
                        name: "FK_candidate_skills_candidates_OrganizationId_CandidateId",
                        columns: x => new { x.OrganizationId, x.CandidateId },
                        principalSchema: "hireflow",
                        principalTable: "candidates",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_candidate_skills_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "hireflow",
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_candidate_skills_skills_SkillId",
                        column: x => x.SkillId,
                        principalSchema: "hireflow",
                        principalTable: "skills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "resumes",
                schema: "hireflow",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CandidateId = table.Column<Guid>(type: "uuid", nullable: false),
                    OriginalFileName = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Sha256Hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    ProcessingStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_resumes", x => x.Id);
                    table.UniqueConstraint("AK_resumes_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.ForeignKey(
                        name: "FK_resumes_candidates_OrganizationId_CandidateId",
                        columns: x => new { x.OrganizationId, x.CandidateId },
                        principalSchema: "hireflow",
                        principalTable: "candidates",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_resumes_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "hireflow",
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "application_stage_history",
                schema: "hireflow",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromStageId = table.Column<Guid>(type: "uuid", nullable: true),
                    ToStageId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChangedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ChangedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_application_stage_history", x => x.Id);
                    table.UniqueConstraint("AK_application_stage_history_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.ForeignKey(
                        name: "FK_application_stage_history_applications_OrganizationId_Appli~",
                        columns: x => new { x.OrganizationId, x.ApplicationId },
                        principalSchema: "hireflow",
                        principalTable: "applications",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_application_stage_history_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "hireflow",
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_application_stage_history_recruitment_stages_OrganizationId~",
                        columns: x => new { x.OrganizationId, x.FromStageId },
                        principalSchema: "hireflow",
                        principalTable: "recruitment_stages",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_application_stage_history_recruitment_stages_OrganizationI~1",
                        columns: x => new { x.OrganizationId, x.ToStageId },
                        principalSchema: "hireflow",
                        principalTable: "recruitment_stages",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_application_stage_history_users_OrganizationId_ChangedByUse~",
                        columns: x => new { x.OrganizationId, x.ChangedByUserId },
                        principalSchema: "hireflow",
                        principalTable: "users",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "interviews",
                schema: "hireflow",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    RoundNumber = table.Column<int>(type: "integer", nullable: false),
                    StartsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EndsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    MeetingUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_interviews", x => x.Id);
                    table.UniqueConstraint("AK_interviews_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.ForeignKey(
                        name: "FK_interviews_applications_OrganizationId_ApplicationId",
                        columns: x => new { x.OrganizationId, x.ApplicationId },
                        principalSchema: "hireflow",
                        principalTable: "applications",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_interviews_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "hireflow",
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "resume_parsing_results",
                schema: "hireflow",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ResumeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ParserVersion = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ParsedDataJson = table.Column<string>(type: "jsonb", nullable: false),
                    ErrorCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_resume_parsing_results", x => x.Id);
                    table.UniqueConstraint("AK_resume_parsing_results_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.ForeignKey(
                        name: "FK_resume_parsing_results_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "hireflow",
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_resume_parsing_results_resumes_OrganizationId_ResumeId",
                        columns: x => new { x.OrganizationId, x.ResumeId },
                        principalSchema: "hireflow",
                        principalTable: "resumes",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "interview_feedback",
                schema: "hireflow",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InterviewId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubmittedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Rating = table.Column<int>(type: "integer", nullable: false),
                    Recommendation = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Strengths = table.Column<string>(type: "text", nullable: true),
                    Concerns = table.Column<string>(type: "text", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_interview_feedback", x => x.Id);
                    table.UniqueConstraint("AK_interview_feedback_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.CheckConstraint("ck_interview_feedback_rating", "\"Rating\" BETWEEN 1 AND 5");
                    table.ForeignKey(
                        name: "FK_interview_feedback_interviews_OrganizationId_InterviewId",
                        columns: x => new { x.OrganizationId, x.InterviewId },
                        principalSchema: "hireflow",
                        principalTable: "interviews",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_interview_feedback_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "hireflow",
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_interview_feedback_users_OrganizationId_SubmittedByUserId",
                        columns: x => new { x.OrganizationId, x.SubmittedByUserId },
                        principalSchema: "hireflow",
                        principalTable: "users",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "interview_participants",
                schema: "hireflow",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InterviewId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_interview_participants", x => x.Id);
                    table.UniqueConstraint("AK_interview_participants_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.ForeignKey(
                        name: "FK_interview_participants_interviews_OrganizationId_InterviewId",
                        columns: x => new { x.OrganizationId, x.InterviewId },
                        principalSchema: "hireflow",
                        principalTable: "interviews",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_interview_participants_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "hireflow",
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_interview_participants_users_OrganizationId_UserId",
                        columns: x => new { x.OrganizationId, x.UserId },
                        principalSchema: "hireflow",
                        principalTable: "users",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ai_processing_jobs_OrganizationId",
                schema: "hireflow",
                table: "ai_processing_jobs",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_ai_processing_jobs_OrganizationId_IdempotencyKey",
                schema: "hireflow",
                table: "ai_processing_jobs",
                columns: new[] { "OrganizationId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ai_processing_jobs_OrganizationId_Status_CreatedAt",
                schema: "hireflow",
                table: "ai_processing_jobs",
                columns: new[] { "OrganizationId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_usage_logs_OrganizationId",
                schema: "hireflow",
                table: "ai_usage_logs",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_ai_usage_logs_OrganizationId_RecordedAt",
                schema: "hireflow",
                table: "ai_usage_logs",
                columns: new[] { "OrganizationId", "RecordedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_application_stage_history_OrganizationId",
                schema: "hireflow",
                table: "application_stage_history",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_application_stage_history_OrganizationId_ApplicationId_Chan~",
                schema: "hireflow",
                table: "application_stage_history",
                columns: new[] { "OrganizationId", "ApplicationId", "ChangedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_application_stage_history_OrganizationId_ChangedByUserId",
                schema: "hireflow",
                table: "application_stage_history",
                columns: new[] { "OrganizationId", "ChangedByUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_application_stage_history_OrganizationId_FromStageId",
                schema: "hireflow",
                table: "application_stage_history",
                columns: new[] { "OrganizationId", "FromStageId" });

            migrationBuilder.CreateIndex(
                name: "IX_application_stage_history_OrganizationId_ToStageId",
                schema: "hireflow",
                table: "application_stage_history",
                columns: new[] { "OrganizationId", "ToStageId" });

            migrationBuilder.CreateIndex(
                name: "IX_applications_OrganizationId",
                schema: "hireflow",
                table: "applications",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_applications_OrganizationId_CandidateId_JobId",
                schema: "hireflow",
                table: "applications",
                columns: new[] { "OrganizationId", "CandidateId", "JobId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_applications_OrganizationId_CurrentStageId",
                schema: "hireflow",
                table: "applications",
                columns: new[] { "OrganizationId", "CurrentStageId" });

            migrationBuilder.CreateIndex(
                name: "IX_applications_OrganizationId_JobId_CurrentStageId",
                schema: "hireflow",
                table: "applications",
                columns: new[] { "OrganizationId", "JobId", "CurrentStageId" });

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_OrganizationId",
                schema: "hireflow",
                table: "audit_logs",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_OrganizationId_ActorUserId",
                schema: "hireflow",
                table: "audit_logs",
                columns: new[] { "OrganizationId", "ActorUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_OrganizationId_EntityType_EntityId",
                schema: "hireflow",
                table: "audit_logs",
                columns: new[] { "OrganizationId", "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_OrganizationId_OccurredAt",
                schema: "hireflow",
                table: "audit_logs",
                columns: new[] { "OrganizationId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_candidate_education_OrganizationId",
                schema: "hireflow",
                table: "candidate_education",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_candidate_education_OrganizationId_CandidateId",
                schema: "hireflow",
                table: "candidate_education",
                columns: new[] { "OrganizationId", "CandidateId" });

            migrationBuilder.CreateIndex(
                name: "IX_candidate_experience_OrganizationId",
                schema: "hireflow",
                table: "candidate_experience",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_candidate_experience_OrganizationId_CandidateId",
                schema: "hireflow",
                table: "candidate_experience",
                columns: new[] { "OrganizationId", "CandidateId" });

            migrationBuilder.CreateIndex(
                name: "IX_candidate_match_scores_OrganizationId",
                schema: "hireflow",
                table: "candidate_match_scores",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_candidate_match_scores_OrganizationId_CandidateId",
                schema: "hireflow",
                table: "candidate_match_scores",
                columns: new[] { "OrganizationId", "CandidateId" });

            migrationBuilder.CreateIndex(
                name: "IX_candidate_match_scores_OrganizationId_JobId_Score",
                schema: "hireflow",
                table: "candidate_match_scores",
                columns: new[] { "OrganizationId", "JobId", "Score" });

            migrationBuilder.CreateIndex(
                name: "IX_candidate_skills_OrganizationId",
                schema: "hireflow",
                table: "candidate_skills",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_candidate_skills_OrganizationId_CandidateId_SkillId",
                schema: "hireflow",
                table: "candidate_skills",
                columns: new[] { "OrganizationId", "CandidateId", "SkillId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_candidate_skills_SkillId",
                schema: "hireflow",
                table: "candidate_skills",
                column: "SkillId");

            migrationBuilder.CreateIndex(
                name: "IX_candidates_OrganizationId",
                schema: "hireflow",
                table: "candidates",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_candidates_OrganizationId_NormalizedEmail",
                schema: "hireflow",
                table: "candidates",
                columns: new[] { "OrganizationId", "NormalizedEmail" },
                unique: true,
                filter: "\"NormalizedEmail\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_candidates_OrganizationId_UserId",
                schema: "hireflow",
                table: "candidates",
                columns: new[] { "OrganizationId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_departments_OrganizationId",
                schema: "hireflow",
                table: "departments",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_departments_OrganizationId_Name",
                schema: "hireflow",
                table: "departments",
                columns: new[] { "OrganizationId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_departments_OrganizationId_ParentDepartmentId",
                schema: "hireflow",
                table: "departments",
                columns: new[] { "OrganizationId", "ParentDepartmentId" });

            migrationBuilder.CreateIndex(
                name: "IX_interview_feedback_OrganizationId",
                schema: "hireflow",
                table: "interview_feedback",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_interview_feedback_OrganizationId_InterviewId",
                schema: "hireflow",
                table: "interview_feedback",
                columns: new[] { "OrganizationId", "InterviewId" });

            migrationBuilder.CreateIndex(
                name: "IX_interview_feedback_OrganizationId_SubmittedByUserId",
                schema: "hireflow",
                table: "interview_feedback",
                columns: new[] { "OrganizationId", "SubmittedByUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_interview_participants_OrganizationId",
                schema: "hireflow",
                table: "interview_participants",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_interview_participants_OrganizationId_InterviewId_UserId",
                schema: "hireflow",
                table: "interview_participants",
                columns: new[] { "OrganizationId", "InterviewId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_interview_participants_OrganizationId_UserId",
                schema: "hireflow",
                table: "interview_participants",
                columns: new[] { "OrganizationId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_interviews_OrganizationId",
                schema: "hireflow",
                table: "interviews",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_interviews_OrganizationId_ApplicationId",
                schema: "hireflow",
                table: "interviews",
                columns: new[] { "OrganizationId", "ApplicationId" });

            migrationBuilder.CreateIndex(
                name: "IX_interviews_OrganizationId_StartsAt",
                schema: "hireflow",
                table: "interviews",
                columns: new[] { "OrganizationId", "StartsAt" });

            migrationBuilder.CreateIndex(
                name: "IX_job_recruiters_OrganizationId",
                schema: "hireflow",
                table: "job_recruiters",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_job_recruiters_OrganizationId_JobId_RecruiterUserId",
                schema: "hireflow",
                table: "job_recruiters",
                columns: new[] { "OrganizationId", "JobId", "RecruiterUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_job_recruiters_OrganizationId_RecruiterUserId",
                schema: "hireflow",
                table: "job_recruiters",
                columns: new[] { "OrganizationId", "RecruiterUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_job_skills_OrganizationId",
                schema: "hireflow",
                table: "job_skills",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_job_skills_OrganizationId_JobId_SkillId",
                schema: "hireflow",
                table: "job_skills",
                columns: new[] { "OrganizationId", "JobId", "SkillId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_job_skills_SkillId",
                schema: "hireflow",
                table: "job_skills",
                column: "SkillId");

            migrationBuilder.CreateIndex(
                name: "IX_jobs_OrganizationId",
                schema: "hireflow",
                table: "jobs",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_jobs_OrganizationId_DepartmentId_Status",
                schema: "hireflow",
                table: "jobs",
                columns: new[] { "OrganizationId", "DepartmentId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_jobs_OrganizationId_Status_CreatedAt",
                schema: "hireflow",
                table: "jobs",
                columns: new[] { "OrganizationId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_notifications_OrganizationId",
                schema: "hireflow",
                table: "notifications",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_notifications_OrganizationId_UserId_IsRead_CreatedAt",
                schema: "hireflow",
                table: "notifications",
                columns: new[] { "OrganizationId", "UserId", "IsRead", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_organizations_Slug",
                schema: "hireflow",
                table: "organizations",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_permissions_Code",
                schema: "hireflow",
                table: "permissions",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_recruitment_stages_OrganizationId",
                schema: "hireflow",
                table: "recruitment_stages",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_recruitment_stages_OrganizationId_Name",
                schema: "hireflow",
                table: "recruitment_stages",
                columns: new[] { "OrganizationId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_recruitment_stages_OrganizationId_SortOrder",
                schema: "hireflow",
                table: "recruitment_stages",
                columns: new[] { "OrganizationId", "SortOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_OrganizationId",
                schema: "hireflow",
                table: "refresh_tokens",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_OrganizationId_UserId_ExpiresAt",
                schema: "hireflow",
                table: "refresh_tokens",
                columns: new[] { "OrganizationId", "UserId", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_TokenHash",
                schema: "hireflow",
                table: "refresh_tokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_resume_parsing_results_OrganizationId",
                schema: "hireflow",
                table: "resume_parsing_results",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_resume_parsing_results_OrganizationId_ResumeId_CreatedAt",
                schema: "hireflow",
                table: "resume_parsing_results",
                columns: new[] { "OrganizationId", "ResumeId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_resumes_OrganizationId",
                schema: "hireflow",
                table: "resumes",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_resumes_OrganizationId_CandidateId",
                schema: "hireflow",
                table: "resumes",
                columns: new[] { "OrganizationId", "CandidateId" });

            migrationBuilder.CreateIndex(
                name: "IX_resumes_OrganizationId_Sha256Hash",
                schema: "hireflow",
                table: "resumes",
                columns: new[] { "OrganizationId", "Sha256Hash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_role_permissions_OrganizationId",
                schema: "hireflow",
                table: "role_permissions",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_role_permissions_OrganizationId_RoleId_PermissionId",
                schema: "hireflow",
                table: "role_permissions",
                columns: new[] { "OrganizationId", "RoleId", "PermissionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_role_permissions_PermissionId",
                schema: "hireflow",
                table: "role_permissions",
                column: "PermissionId");

            migrationBuilder.CreateIndex(
                name: "IX_roles_OrganizationId",
                schema: "hireflow",
                table: "roles",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_roles_OrganizationId_Name",
                schema: "hireflow",
                table: "roles",
                columns: new[] { "OrganizationId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_skills_NormalizedName",
                schema: "hireflow",
                table: "skills",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_roles_OrganizationId",
                schema: "hireflow",
                table: "user_roles",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_user_roles_OrganizationId_RoleId",
                schema: "hireflow",
                table: "user_roles",
                columns: new[] { "OrganizationId", "RoleId" });

            migrationBuilder.CreateIndex(
                name: "IX_user_roles_OrganizationId_UserId_RoleId",
                schema: "hireflow",
                table: "user_roles",
                columns: new[] { "OrganizationId", "UserId", "RoleId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_OrganizationId",
                schema: "hireflow",
                table: "users",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "ux_users_org_email",
                schema: "hireflow",
                table: "users",
                columns: new[] { "OrganizationId", "NormalizedEmail" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ai_processing_jobs",
                schema: "hireflow");

            migrationBuilder.DropTable(
                name: "ai_usage_logs",
                schema: "hireflow");

            migrationBuilder.DropTable(
                name: "application_stage_history",
                schema: "hireflow");

            migrationBuilder.DropTable(
                name: "audit_logs",
                schema: "hireflow");

            migrationBuilder.DropTable(
                name: "candidate_education",
                schema: "hireflow");

            migrationBuilder.DropTable(
                name: "candidate_experience",
                schema: "hireflow");

            migrationBuilder.DropTable(
                name: "candidate_match_scores",
                schema: "hireflow");

            migrationBuilder.DropTable(
                name: "candidate_skills",
                schema: "hireflow");

            migrationBuilder.DropTable(
                name: "interview_feedback",
                schema: "hireflow");

            migrationBuilder.DropTable(
                name: "interview_participants",
                schema: "hireflow");

            migrationBuilder.DropTable(
                name: "job_recruiters",
                schema: "hireflow");

            migrationBuilder.DropTable(
                name: "job_skills",
                schema: "hireflow");

            migrationBuilder.DropTable(
                name: "notifications",
                schema: "hireflow");

            migrationBuilder.DropTable(
                name: "refresh_tokens",
                schema: "hireflow");

            migrationBuilder.DropTable(
                name: "resume_parsing_results",
                schema: "hireflow");

            migrationBuilder.DropTable(
                name: "role_permissions",
                schema: "hireflow");

            migrationBuilder.DropTable(
                name: "user_roles",
                schema: "hireflow");

            migrationBuilder.DropTable(
                name: "interviews",
                schema: "hireflow");

            migrationBuilder.DropTable(
                name: "skills",
                schema: "hireflow");

            migrationBuilder.DropTable(
                name: "resumes",
                schema: "hireflow");

            migrationBuilder.DropTable(
                name: "permissions",
                schema: "hireflow");

            migrationBuilder.DropTable(
                name: "roles",
                schema: "hireflow");

            migrationBuilder.DropTable(
                name: "applications",
                schema: "hireflow");

            migrationBuilder.DropTable(
                name: "candidates",
                schema: "hireflow");

            migrationBuilder.DropTable(
                name: "jobs",
                schema: "hireflow");

            migrationBuilder.DropTable(
                name: "recruitment_stages",
                schema: "hireflow");

            migrationBuilder.DropTable(
                name: "users",
                schema: "hireflow");

            migrationBuilder.DropTable(
                name: "departments",
                schema: "hireflow");

            migrationBuilder.DropTable(
                name: "organizations",
                schema: "hireflow");
        }
    }
}
