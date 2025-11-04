using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
  public partial class AddUniqueConstraintsForRaceConditions : Migration
    {
   /// <inheritdoc />
      protected override void Up(MigrationBuilder migrationBuilder)
  {
  // RACE CONDITION PROTECTION: Prevent duplicate match requests
            // If User A sends request to User B for Movie X, another concurrent request will fail
   migrationBuilder.CreateIndex(
                name: "IX_MatchRequests_UniqueRequest",
 table: "MatchRequests",
                columns: new[] { "RequestorId", "TargetUserId", "TmdbId" },
         unique: true);

   // RACE CONDITION PROTECTION: Prevent duplicate movie likes
  // If user double-clicks "Like", only one entry is created
          migrationBuilder.CreateIndex(
 name: "IX_UserMovieLikes_UniqueLike",
  table: "UserMovieLikes",
 columns: new[] { "UserId", "TmdbId" },
          unique: true);
        }

        /// <inheritdoc />
     protected override void Down(MigrationBuilder migrationBuilder)
        {
   migrationBuilder.DropIndex(
      name: "IX_MatchRequests_UniqueRequest",
      table: "MatchRequests");

            migrationBuilder.DropIndex(
         name: "IX_UserMovieLikes_UniqueLike",
           table: "UserMovieLikes");
        }
    }
}
