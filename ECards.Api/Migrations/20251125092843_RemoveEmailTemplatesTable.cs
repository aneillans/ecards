using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECards.Api.Migrations
{
    /// <inheritdoc />
    public partial class RemoveEmailTemplatesTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DROP TABLE IF EXISTS `EmailTemplates`;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Recreating EmailTemplates table not supported - templates are now file-based
            throw new NotSupportedException("EmailTemplates table cannot be restored. Email templates are now file-based (.hbs files).");
        }
    }
}
