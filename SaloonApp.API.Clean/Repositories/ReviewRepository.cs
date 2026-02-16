using Npgsql;
using SaloonApp.API.Data;
using SaloonApp.API.Models;
using System.Data;

namespace SaloonApp.API.Repositories
{
    public class ReviewRepository
    {
        private readonly DatabaseContext _context;

        public ReviewRepository(DatabaseContext context)
        {
            _context = context;
        }

        public async Task AddReviewAsync(Review review)
        {
            using var connection = _context.CreateConnection();
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO reviews (shop_id, user_id, rating, comment) 
                VALUES (@shopId, @userId, @rating, @comment)
                RETURNING id";

            AddParam(command, "@shopId", review.ShopId);
            AddParam(command, "@userId", review.UserId);
            AddParam(command, "@rating", review.Rating);
            AddParam(command, "@comment", review.Comment ?? (object)DBNull.Value);

            var id = await (command as NpgsqlCommand).ExecuteScalarAsync();
            review.Id = Convert.ToInt32(id);

            // Update Shop Average Rating
            await UpdateShopRatingAsync(review.ShopId, connection, command);
        }

        public async Task<IEnumerable<Review>> GetReviewsByShopIdAsync(int shopId)
        {
            var reviews = new List<Review>();
            using var connection = _context.CreateConnection();
            connection.Open();
            using var command = connection.CreateCommand();
            
            // Join with users to get the reviewer's name
            command.CommandText = @"
                SELECT r.*, u.name as user_name 
                FROM reviews r 
                JOIN users u ON r.user_id = u.id 
                WHERE r.shop_id = @shopId 
                ORDER BY r.created_at DESC";
            
            AddParam(command, "@shopId", shopId);

            using var reader = await (command as NpgsqlCommand).ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                reviews.Add(new Review
                {
                    Id = reader.GetInt32(reader.GetOrdinal("id")),
                    ShopId = reader.GetInt32(reader.GetOrdinal("shop_id")),
                    UserId = reader.GetInt32(reader.GetOrdinal("user_id")),
                    Rating = reader.GetInt32(reader.GetOrdinal("rating")),
                    Comment = reader.IsDBNull(reader.GetOrdinal("comment")) ? "" : reader.GetString(reader.GetOrdinal("comment")),
                    CreatedAt = reader.GetDateTime(reader.GetOrdinal("created_at")),
                    UserName = reader.GetString(reader.GetOrdinal("user_name"))
                });
            }
            return reviews;
        }

        private async Task UpdateShopRatingAsync(int shopId, IDbConnection connection, IDbCommand command)
        {
            // Reset parameters for new query reuse or create new? Safer to create new inner command or clear params.
            // Using existing command object might be tricky with open reader if called elsewhere, but here it's fine.
            // Actually, let's just make a new command on the same connection.
            
            using var calcCommand = connection.CreateCommand();
            calcCommand.CommandText = @"
                UPDATE shops s
                SET rating = (SELECT AVG(rating) FROM reviews WHERE shop_id = @shopId)
                WHERE id = @shopId";
            
            // Re-use AddParam helper logic inline or ensure helper is accessible/static? 
            // Since AddParam is instance method check below.
            
            var param = calcCommand.CreateParameter();
            param.ParameterName = "@shopId";
            param.Value = shopId;
            calcCommand.Parameters.Add(param);

            await (calcCommand as NpgsqlCommand).ExecuteNonQueryAsync();
        }

        private void AddParam(IDbCommand command, string name, object value)
        {
            var param = command.CreateParameter();
            param.ParameterName = name;
            param.Value = value;
            command.Parameters.Add(param);
        }
    }
}
