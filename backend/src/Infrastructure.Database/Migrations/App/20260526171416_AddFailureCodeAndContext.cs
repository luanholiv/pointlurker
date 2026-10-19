using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Database.Migrations.App
{
    /// <inheritdoc />
    public partial class AddFailureCodeAndContext : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FailureCode",
                table: "redemptions",
                type: "integer",
                nullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "FailureContext",
                table: "redemptions",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true
            );

            // Back-fill FailureCode from existing FailureReason strings.
            // SubmissionFailureCode enum values:
            //   Unknown=0, TokenDecryptionFailed=1, InputCoverageLost=2, InputDecryptionFailed=3,
            //   SeApiBadRequest=4, SeApiUnauthorized=5, SeApiForbidden=6, SeApiNotFound=7, SeApiServerError=8
            migrationBuilder.Sql(
                """
                UPDATE redemptions
                SET
                    "FailureCode" = CASE
                        WHEN "FailureReason" = 'Token decryption failed'             THEN 1
                        WHEN "FailureReason" ILIKE '%input coverage lost%'           THEN 2
                        WHEN "FailureReason" ILIKE '%input decryption failed%'       THEN 3
                        WHEN "FailureReason" LIKE '400%' OR "FailureReason" LIKE '%SeApiBadRequest%'   THEN 4
                        WHEN "FailureReason" LIKE '401%' OR "FailureReason" LIKE '%SeApiUnauthorized%' THEN 5
                        WHEN "FailureReason" LIKE '403%' OR "FailureReason" LIKE '%SeApiForbidden%'    THEN 6
                        WHEN "FailureReason" LIKE '404%' OR "FailureReason" LIKE '%SeApiNotFound%'     THEN 7
                        WHEN "FailureReason" IS NOT NULL                             THEN 0
                        ELSE NULL
                    END,
                    "FailureContext" = CASE
                        WHEN "FailureReason" IS NOT NULL
                         AND "FailureReason" NOT IN (
                             'Token decryption failed'
                         )
                         AND "FailureReason" NOT ILIKE '%input coverage lost%'
                         AND "FailureReason" NOT ILIKE '%input decryption failed%'
                         AND "FailureReason" NOT LIKE '400%'
                         AND "FailureReason" NOT LIKE '401%'
                         AND "FailureReason" NOT LIKE '403%'
                         AND "FailureReason" NOT LIKE '404%'
                         AND "FailureReason" NOT LIKE '%SeApi%'
                        THEN "FailureReason"
                        ELSE NULL
                    END
                WHERE "FailureReason" IS NOT NULL
                  AND "Status" = 'SubmissionFailed';
                """
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "FailureCode", table: "redemptions");

            migrationBuilder.DropColumn(name: "FailureContext", table: "redemptions");
        }
    }
}
